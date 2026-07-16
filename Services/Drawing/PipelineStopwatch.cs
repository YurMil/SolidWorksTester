using System;
using System.Diagnostics;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Stopwatch helper for pipeline logs. Emits <c>[time]</c> lines so slow steps are obvious.
    /// </summary>
    internal sealed class PipelineStopwatch : IDisposable
    {
        private readonly Action<string> _log;
        private readonly string _scope;
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly Stopwatch _step = new();
        private string? _active;
        private bool _completed;

        public PipelineStopwatch(Action<string> log, string scope)
        {
            _log = log;
            _scope = scope;
            _log($"[time] ▶ {scope}");
        }

        /// <summary>Starts a named step (ends the previous step if any).</summary>
        public void Step(string name)
        {
            FinishStep();
            _active = name;
            _step.Restart();
            _log($"[time] ▶ {name}");
        }

        /// <summary>Ends the current step and logs elapsed + cumulative.</summary>
        public void FinishStep()
        {
            if (_active == null)
                return;

            _step.Stop();
            _log($"[time] ■ {_active}: {Format(_step.Elapsed)}  (Σ {Format(_total.Elapsed)})");
            _active = null;
        }

        public void Measure(string name, Action action)
        {
            Step(name);
            try
            {
                action();
            }
            finally
            {
                FinishStep();
            }
        }

        public T Measure<T>(string name, Func<T> action)
        {
            Step(name);
            try
            {
                return action();
            }
            finally
            {
                FinishStep();
            }
        }

        public void Complete()
        {
            if (_completed)
                return;

            FinishStep();
            _total.Stop();
            _completed = true;
            _log($"[time] ★ {_scope}: {Format(_total.Elapsed)}");
        }

        public void Dispose() => Complete();

        public static string Format(TimeSpan t)
        {
            if (t.TotalMinutes >= 1)
                return $"{(int)t.TotalMinutes}m {t.Seconds:D2}.{t.Milliseconds:D3}s";
            if (t.TotalSeconds >= 10)
                return $"{t.TotalSeconds:F1}s";
            if (t.TotalSeconds >= 1)
                return $"{t.TotalSeconds:F2}s";
            return $"{t.TotalMilliseconds:F0}ms";
        }

        /// <summary>One-shot timed block (no cumulative scope).</summary>
        public static void Run(Action<string> log, string name, Action action)
        {
            var sw = Stopwatch.StartNew();
            log($"[time] ▶ {name}");
            try
            {
                action();
            }
            finally
            {
                sw.Stop();
                log($"[time] ■ {name}: {Format(sw.Elapsed)}");
            }
        }

        public static T Run<T>(Action<string> log, string name, Func<T> action)
        {
            var sw = Stopwatch.StartNew();
            log($"[time] ▶ {name}");
            try
            {
                return action();
            }
            finally
            {
                sw.Stop();
                log($"[time] ■ {name}: {Format(sw.Elapsed)}");
            }
        }
    }
}
