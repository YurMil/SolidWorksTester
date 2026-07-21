using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorksTester.Services;
using SolidWorksTester.Services.SolidWorks;
using SolidWorksTester.UI.Forms;
using SolidWorksTester.UI.Layout;
using SolidWorksTester.UI.Models;
using SolidWorksTester.UI.Theme;
using SolidWorksTester.UI.Views;

namespace SolidWorksTester
{
    /// <summary>
    /// Main application window. Layout is built by <see cref="MainFormLayoutBuilder"/>;
    /// batch processing and SOLIDWORKS orchestration stay here.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly MainFormView _view;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly List<string> _sessionLog = new();
        private bool _isRunning;
        private BatchTaskItem? _activeTask;

        public MainForm()
        {
            _view = MainFormLayoutBuilder.Build(this);
            _view.RunButton.Click += async (_, _) => await RunBatchAsync(retrySelectedOnly: false);
            _view.CancelButton.Click += (_, _) => _cancellationTokenSource?.Cancel();
            _view.SkipButton.Click += (_, _) => SkipSelectedTasks();
            _view.RetryButton.Click += async (_, _) => await RunBatchAsync(retrySelectedOnly: true);
            _view.TaskManager.SkipRequested += (_, _) => SkipSelectedTasks();
            _view.TaskManager.RetryRequested += async (_, _) => await RunBatchAsync(retrySelectedOnly: true);
            _view.TaskManager.SelectionChanged += (_, _) => ShowSelectedTaskLog();
            _view.FooterVersionLink.LinkClicked += (_, _) =>
            {
                _view.FooterVersionLink.LinkVisited = false;
                ReleaseNotesForm.ShowReleaseNotes(this);
            };

            UpdateActionButtons();
            ShowSelectedTaskLog();
        }

        private const int WmGetMinMaxInfo = 0x0024;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmGetMinMaxInfo)
            {
                base.WndProc(ref m);
                FormWindowConstraints.ApplyMinMaxInfo(this, ref m);
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FormWindowConstraints.Apply(this);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            FormWindowConstraints.Apply(this);
        }

        private async Task RunBatchAsync(bool retrySelectedOnly)
        {
            if (_isRunning)
                return;

            string templatePath = _view.TemplateTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                MessageBox.Show(this,
                    "Please select an existing drawing template (.DRWDOT or .SLDDRW).",
                    "Template Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_view.TaskManager.TaskCount == 0)
            {
                MessageBox.Show(this,
                    "Add at least one part file (.SLDPRT).",
                    "No Tasks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!SolidWorksBootstrap.TryValidate(out string bootstrapError, out string bootstrapLog))
            {
                MessageBox.Show(this, bootstrapError, "SOLIDWORKS Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<BatchTaskItem> queue;
            if (retrySelectedOnly)
            {
                var selected = _view.TaskManager.SelectedTasks
                    .Where(t => t.Status is BatchTaskStatus.Failed
                        or BatchTaskStatus.Skipped
                        or BatchTaskStatus.Cancelled)
                    .ToList();

                if (selected.Count == 0)
                {
                    MessageBox.Show(this,
                        "Select one or more Failed / Skipped / Cancelled tasks to retry.",
                        "Retry",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                foreach (BatchTaskItem task in selected)
                {
                    task.ResetForRetry();
                    _view.TaskManager.RefreshTask(task);
                }

                queue = selected;
            }
            else
            {
                // Full Generate: run Pending only (Succeeded stay; Failed need Retry).
                queue = _view.TaskManager.GetRunnableTasks().ToList();
                if (queue.Count == 0)
                {
                    // If everything is terminal, offer to reset Failed/Skipped via message.
                    bool anyRetryable = _view.TaskManager.SnapshotAll().Any(t =>
                        t.Status is BatchTaskStatus.Failed or BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled);

                    MessageBox.Show(this,
                        anyRetryable
                            ? "No Pending tasks. Select Failed/Skipped tasks and use Retry, or add new files."
                            : "No Pending tasks to run.",
                        "Nothing to Run",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            SetRunningState(true);
            _sessionLog.Clear();
            AppendSessionLog(bootstrapLog.TrimEnd());
            _view.ProgressBar.Maximum = Math.Max(1, queue.Count);
            _view.ProgressBar.Value = 0;
            ShowSelectedTaskLog();

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            int successCount = 0;
            int failCount = 0;
            int skipCount = 0;

            try
            {
                _view.TaskManager.StartElapsedTicker();

                await StaTaskRunner.Run(() =>
                {
                    AppendSessionLog("Starting batch on SOLIDWORKS STA worker...");
                    SolidWorks.Interop.sldworks.ISldWorks? swApp = null;
                    var service = new SheetMetalDrawingService();

                    try
                    {
                        for (int i = 0; i < queue.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();

                            BatchTaskItem task = queue[i];

                            // Skip may have been requested while waiting.
                            if (task.Status == BatchTaskStatus.Skipped)
                            {
                                skipCount++;
                                UpdateProgress(i + 1);
                                continue;
                            }

                            if (task.Status != BatchTaskStatus.Pending)
                            {
                                UpdateProgress(i + 1);
                                continue;
                            }

                            UpdateStatus($"Processing {i + 1}/{queue.Count}: {task.FileName}");
                            task.BeginAttempt();
                            UiBegin(() =>
                            {
                                _activeTask = task;
                                _view.TaskManager.SelectTask(task);
                                _view.TaskManager.RefreshTask(task);
                                _view.TaskManager.StartElapsedTicker();
                                ShowSelectedTaskLog();
                            });

                            Action<string> taskLog = message => AppendTaskLog(task, message);

                            try
                            {
                                if (!File.Exists(task.PartPath))
                                    throw new FileNotFoundException("Part file not found or not accessible.", task.PartPath);

                                swApp = SolidWorksConnection.EnsureConnected(swApp, msg =>
                                {
                                    AppendSessionLog(msg);
                                    AppendTaskLog(task, msg);
                                });

                                taskLog("");
                                taskLog($"=== [{i + 1}/{queue.Count}] {task.PartPath} ===");
                                ProcessPartOutcome outcome = service.ProcessPart(
                                    swApp, task.PartPath, templatePath, taskLog);

                                if (outcome == ProcessPartOutcome.SkippedFastener)
                                {
                                    taskLog("Done (skipped fastener).");
                                    task.MarkSkippedByPolicy("[task] Skipped — fastener (DocumentType/IsFastener).");
                                    skipCount++;
                                }
                                else
                                {
                                    taskLog("Done.");
                                    task.MarkSucceeded();
                                    successCount++;
                                }

                                UiBegin(() =>
                                {
                                    _view.TaskManager.RefreshTask(task);
                                    if (ReferenceEquals(_view.TaskManager.SelectedTask, task))
                                        ShowSelectedTaskLog();
                                });
                            }
                            catch (Exception ex)
                            {
                                failCount++;
                                string summary = ExceptionDisplay.GetLogSummary(ex);
                                taskLog($"ERROR: {summary}");

                                task.MarkFailed(summary);
                                UiBegin(() =>
                                {
                                    _view.TaskManager.RefreshTask(task);
                                    if (ReferenceEquals(_view.TaskManager.SelectedTask, task))
                                        ShowSelectedTaskLog();
                                });

                                if (SolidWorksComException.IsConnectionFailure(ex))
                                {
                                    SolidWorksConnection.ReleaseApp(ref swApp, msg =>
                                    {
                                        AppendSessionLog(msg);
                                        AppendTaskLog(task, msg);
                                    });
                                    AppendSessionLog("SOLIDWORKS connection reset — will reconnect before the next part.");
                                }
                            }
                            finally
                            {
                                try
                                {
                                    UpdateProgress(i + 1);
                                }
                                catch (Exception ex)
                                {
                                    AppendSessionLog($"Warning: progress update failed: {ExceptionDisplay.GetLogSummary(ex)}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        SolidWorksConnection.ReleaseApp(ref swApp, AppendSessionLog);
                        _activeTask = null;
                    }
                }, token);

                UpdateStatus($"Finished. Success: {successCount}, failed: {failCount}, skipped: {skipCount}");
                MessageBox.Show(this,
                    $"Batch complete.\n\nSuccess: {successCount}\nFailed: {failCount}\nSkipped: {skipCount}",
                    "Results",
                    MessageBoxButtons.OK,
                    failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                foreach (BatchTaskItem task in _view.TaskManager.SnapshotAll())
                {
                    if (task.Status == BatchTaskStatus.Pending)
                    {
                        task.MarkCancelled();
                        _view.TaskManager.RefreshTask(task);
                    }
                }

                UpdateStatus("Cancelled.");
                AppendSessionLog("Operation cancelled by user.");
                ShowSelectedTaskLog();
            }
            catch (Exception ex)
            {
                string userMessage = ExceptionDisplay.GetUserMessage(ex);
                UpdateStatus("Critical error.");
                AppendSessionLog($"Critical error: {ExceptionDisplay.GetLogSummary(ex)}");
                AppendSessionLog(ExceptionDisplay.GetFullDetails(ex));
                MessageBox.Show(this, userMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _view.TaskManager.StopElapsedTicker();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _activeTask = null;
                SetRunningState(false);
                ShowSelectedTaskLog();
            }
        }

        private void SkipSelectedTasks()
        {
            var selected = _view.TaskManager.SelectedTasks;
            if (selected.Count == 0)
                return;

            int skipped = 0;
            foreach (BatchTaskItem task in selected)
            {
                if (task.Status != BatchTaskStatus.Pending)
                    continue;

                task.MarkSkipped();
                task.AppendEvent("[task] Skipped by user.");
                _view.TaskManager.RefreshTask(task);
                skipped++;
            }

            if (skipped > 0)
            {
                AppendSessionLog($"Skipped {skipped} pending task(s).");
                ShowSelectedTaskLog();
                UpdateActionButtons();
            }
        }

        private void SetRunningState(bool isRunning)
        {
            _isRunning = isRunning;
            _view.RunButton.Enabled = !isRunning;
            _view.CancelButton.Enabled = isRunning;
            _view.TemplateTextBox.Enabled = !isRunning;
            _view.AddFilesButton.Enabled = !isRunning;
            _view.AddFolderButton.Enabled = !isRunning;
            _view.RemoveButton.Enabled = !isRunning;
            _view.ClearButton.Enabled = !isRunning;
            _view.TaskManager.SetInteractionEnabled(!isRunning);

            _view.StatusLabel.ForeColor = isRunning
                ? UiTheme.Accent
                : UiTheme.TextSecondary;

            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            var selected = _view.TaskManager.SelectedTasks;
            _view.SkipButton.Enabled = selected.Any(t => t.Status == BatchTaskStatus.Pending);
            _view.RetryButton.Enabled = !_isRunning && selected.Any(t =>
                t.Status is BatchTaskStatus.Failed or BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled);
        }

        private void ShowSelectedTaskLog()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(ShowSelectedTaskLog));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            UpdateActionButtons();

            BatchTaskItem? selected = _view.TaskManager.SelectedTask;
            var sb = new StringBuilder();

            if (selected == null)
            {
                _view.EventLogHeader.Text = "Event log · session";
                foreach (string line in _sessionLog)
                    sb.AppendLine(line);
            }
            else
            {
                _view.EventLogHeader.Text = $"Event log · {selected.FileName}";
                foreach (string line in selected.Events)
                    sb.AppendLine(line);

                if (selected.Stages.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- Stages ---");
                    foreach (BatchTaskStage stage in selected.Stages)
                        sb.AppendLine($"  {stage.Name}: {BatchTaskItem.FormatStageDuration(stage.Duration)}");
                }

                if (selected.Status == BatchTaskStatus.Failed && !string.IsNullOrEmpty(selected.ErrorSummary))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Error: {selected.ErrorSummary}");
                }
            }

            _view.LogTextBox.Text = sb.ToString().TrimEnd();
            _view.LogTextBox.SelectionStart = _view.LogTextBox.TextLength;
            _view.LogTextBox.ScrollToCaret();
        }

        private void AppendTaskLog(BatchTaskItem task, string message)
        {
            task.AppendEvent(message);

            if (IsDisposed)
                return;

            void Apply()
            {
                _view.TaskManager.RefreshTask(task);
                if (ReferenceEquals(_view.TaskManager.SelectedTask, task))
                    ShowSelectedTaskLog();
            }

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(new Action(Apply));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            Apply();
        }

        private void AppendSessionLog(string message)
        {
            lock (_sessionLog)
                _sessionLog.Add(message);

            if (IsDisposed)
                return;

            void Apply()
            {
                if (_view.TaskManager.SelectedTask == null)
                    AppendLogLineToView(message);
            }

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(new Action(Apply));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            Apply();
        }

        private void AppendLogLineToView(string message)
        {
            if (_view.LogTextBox.TextLength > 0)
                _view.LogTextBox.AppendText(Environment.NewLine);
            _view.LogTextBox.AppendText(message);
        }

        private void UiBegin(Action action)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(action);
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            action();
        }

        private void UpdateStatus(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;

                try
                {
                    BeginInvoke(new Action<string>(UpdateStatus), message);
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            _view.StatusLabel.Text = message;
        }

        private void UpdateProgress(int value)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;

                try
                {
                    BeginInvoke(new Action<int>(UpdateProgress), value);
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            if (_view.ProgressBar.Maximum <= 0)
                return;

            _view.ProgressBar.Value = Math.Min(Math.Max(value, 0), _view.ProgressBar.Maximum);
        }
    }
}
