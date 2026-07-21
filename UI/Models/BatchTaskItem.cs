using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.UI.Models
{
    /// <summary>One completed pipeline stage with its measured duration.</summary>
    public sealed class BatchTaskStage
    {
        public required string Name { get; init; }
        public TimeSpan Duration { get; init; }
    }

    /// <summary>One .SLDPRT in the batch queue with status, timing, and per-file event log.</summary>
    public sealed class BatchTaskItem
    {
        private static readonly Regex TimeStart = new(
            @"^\[time\]\s*▶\s*(.+)$",
            RegexOptions.Compiled);

        private static readonly Regex TimeStepEnd = new(
            @"^\[time\]\s*■\s*(.+?):\s*.+?(?:\(Σ\s*(.+?)\))?$",
            RegexOptions.Compiled);

        private static readonly Regex TimeScopeEnd = new(
            @"^\[time\]\s*★\s*(.+?):\s*(.+)$",
            RegexOptions.Compiled);

        private readonly object _gate = new();
        private readonly List<string> _events = new();
        private readonly List<BatchTaskStage> _stages = new();
        private readonly Stopwatch _runClock = new();

        public BatchTaskItem(string partPath)
        {
            Id = Guid.NewGuid();
            PartPath = partPath;
            FileName = Path.GetFileName(partPath);
        }

        public Guid Id { get; }
        public string PartPath { get; }
        public string FileName { get; }

        public BatchTaskStatus Status { get; private set; } = BatchTaskStatus.Pending;
        public int Attempt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string CurrentStage { get; private set; } = string.Empty;
        public string ErrorSummary { get; private set; } = string.Empty;
        public double Progress01 { get; private set; }

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_gate)
                    return _events.ToArray();
            }
        }

        public IReadOnlyList<BatchTaskStage> Stages
        {
            get
            {
                lock (_gate)
                    return _stages.ToArray();
            }
        }

        public string DisplayTask =>
            string.IsNullOrEmpty(PartPath)
                ? FileName
                : $"{FileName}  ({TruncatePath(PartPath, 42)})";

        public string StatusText => Status switch
        {
            BatchTaskStatus.Pending => "Pending",
            BatchTaskStatus.Running => "Running",
            BatchTaskStatus.Succeeded => "Completed",
            BatchTaskStatus.Failed => "Failed",
            BatchTaskStatus.Skipped => "Skipped",
            BatchTaskStatus.Cancelled => "Cancelled",
            _ => Status.ToString()
        };

        public string AttemptText => Attempt > 0 ? Attempt.ToString() : string.Empty;

        public string StartText => StartedAt?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;

        public string ElapsedText
        {
            get
            {
                TimeSpan t = GetLiveElapsed();
                if (t <= TimeSpan.Zero && Status is BatchTaskStatus.Pending or BatchTaskStatus.Skipped)
                    return string.Empty;
                return FormatElapsedClock(t);
            }
        }

        public string StageText => CurrentStage;

        public void BeginAttempt()
        {
            lock (_gate)
            {
                Attempt++;
                Status = BatchTaskStatus.Running;
                StartedAt = DateTime.Now;
                Elapsed = TimeSpan.Zero;
                CurrentStage = string.Empty;
                ErrorSummary = string.Empty;
                Progress01 = 0.02;
                _events.Clear();
                _stages.Clear();
                _runClock.Restart();
            }
        }

        public void MarkSucceeded()
        {
            lock (_gate)
            {
                _runClock.Stop();
                Elapsed = _runClock.Elapsed;
                Status = BatchTaskStatus.Succeeded;
                CurrentStage = string.Empty;
                Progress01 = 1.0;
            }
        }

        public void MarkFailed(string errorSummary)
        {
            lock (_gate)
            {
                _runClock.Stop();
                Elapsed = _runClock.Elapsed;
                Status = BatchTaskStatus.Failed;
                ErrorSummary = errorSummary ?? string.Empty;
                Progress01 = Math.Max(Progress01, 0.15);
            }
        }

        public void MarkSkipped()
        {
            lock (_gate)
            {
                if (Status is BatchTaskStatus.Succeeded or BatchTaskStatus.Running or BatchTaskStatus.Failed)
                    return;

                Status = BatchTaskStatus.Skipped;
                CurrentStage = string.Empty;
                Progress01 = 0;
            }
        }

        /// <summary>Policy skip after the task already started (e.g. fastener detected during analysis).</summary>
        public void MarkSkippedByPolicy(string? note = null)
        {
            lock (_gate)
            {
                _runClock.Stop();
                Elapsed = _runClock.Elapsed;
                Status = BatchTaskStatus.Skipped;
                CurrentStage = string.Empty;
                Progress01 = 1.0;
                if (!string.IsNullOrWhiteSpace(note))
                    _events.Add(note);
            }
        }

        public void MarkCancelled()
        {
            lock (_gate)
            {
                if (Status is not BatchTaskStatus.Pending)
                    return;

                Status = BatchTaskStatus.Cancelled;
                CurrentStage = string.Empty;
                Progress01 = 0;
            }
        }

        public void ResetForRetry()
        {
            lock (_gate)
            {
                if (Status is not (BatchTaskStatus.Failed or BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled))
                    return;

                Status = BatchTaskStatus.Pending;
                CurrentStage = string.Empty;
                ErrorSummary = string.Empty;
                Progress01 = 0;
                Elapsed = TimeSpan.Zero;
                StartedAt = null;
                // Keep Attempt and prior Events history? Plan: clear for new attempt on BeginAttempt.
            }
        }

        public void TickElapsed()
        {
            lock (_gate)
            {
                if (Status != BatchTaskStatus.Running || !_runClock.IsRunning)
                    return;

                Elapsed = _runClock.Elapsed;
            }
        }

        public TimeSpan GetLiveElapsed()
        {
            lock (_gate)
            {
                if (Status == BatchTaskStatus.Running && _runClock.IsRunning)
                    return _runClock.Elapsed;
                return Elapsed;
            }
        }

        public void AppendEvent(string message)
        {
            if (message == null)
                return;

            lock (_gate)
            {
                _events.Add(message);
                ApplyTimeLine(message);
            }
        }

        public void AppendEventUnlockedBootstrap(string message)
        {
            AppendEvent(message);
        }

        private void ApplyTimeLine(string message)
        {
            string trimmed = message.Trim();
            if (!trimmed.StartsWith("[time]", StringComparison.Ordinal))
                return;

            Match start = TimeStart.Match(trimmed);
            if (start.Success)
            {
                CurrentStage = start.Groups[1].Value.Trim();
                Progress01 = Math.Min(0.95, Progress01 + 0.08);
                return;
            }

            Match stepEnd = TimeStepEnd.Match(trimmed);
            if (stepEnd.Success)
            {
                string name = stepEnd.Groups[1].Value.Trim();
                if (TryParseDurationFromStepLine(trimmed, out TimeSpan stepDuration))
                {
                    _stages.Add(new BatchTaskStage { Name = name, Duration = stepDuration });
                }

                if (stepEnd.Groups.Count > 2 &&
                    stepEnd.Groups[2].Success &&
                    TryParseLooseDuration(stepEnd.Groups[2].Value.Trim(), out TimeSpan sigma))
                {
                    Elapsed = sigma;
                }

                Progress01 = Math.Min(0.95, Progress01 + 0.1);
                return;
            }

            Match scopeEnd = TimeScopeEnd.Match(trimmed);
            if (scopeEnd.Success &&
                TryParseLooseDuration(scopeEnd.Groups[2].Value.Trim(), out TimeSpan total))
            {
                Elapsed = total;
                Progress01 = 1.0;
                CurrentStage = string.Empty;
            }
        }

        private static bool TryParseDurationFromStepLine(string line, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            // "[time] ■ name: 2.05s  (Σ 2.05s)" or "682ms"
            int colon = line.IndexOf(':');
            if (colon < 0)
                return false;

            string after = line[(colon + 1)..].Trim();
            int paren = after.IndexOf("(Σ", StringComparison.Ordinal);
            string token = (paren >= 0 ? after[..paren] : after).Trim();
            return TryParseLooseDuration(token, out duration);
        }

        private static bool TryParseLooseDuration(string text, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Prefer PipelineStopwatch-like tokens: "2.05s", "682ms", "1m 02.003s"
            Match m = Regex.Match(text, @"^(?:(\d+)m\s*)?(\d+(?:\.\d+)?)(ms|s)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                return false;

            double minutes = m.Groups[1].Success ? double.Parse(m.Groups[1].Value) : 0;
            double value = double.Parse(m.Groups[2].Value);
            string unit = m.Groups[3].Value.ToLowerInvariant();

            duration = unit == "ms"
                ? TimeSpan.FromMilliseconds(value) + TimeSpan.FromMinutes(minutes)
                : TimeSpan.FromSeconds(value) + TimeSpan.FromMinutes(minutes);
            return true;
        }

        public static string FormatElapsedClock(TimeSpan t)
        {
            if (t < TimeSpan.Zero)
                t = TimeSpan.Zero;

            int hours = (int)t.TotalHours;
            return $"{hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }

        public static string FormatStageDuration(TimeSpan t) => PipelineStopwatch.Format(t);

        private static string TruncatePath(string path, int max)
        {
            if (path.Length <= max)
                return path;
            if (max <= 3)
                return path[..max];
            return path[..(max - 3)] + "...";
        }
    }
}
