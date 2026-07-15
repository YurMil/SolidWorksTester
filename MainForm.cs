using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorksTester.Services;
using SolidWorksTester.Services.SolidWorks;
using SolidWorksTester.UI.Forms;
using SolidWorksTester.UI.Layout;
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

        public MainForm()
        {
            _view = MainFormLayoutBuilder.Build(this);
            _view.RunButton.Click += async (_, _) => await RunBatchAsync();
            _view.CancelButton.Click += (_, _) => _cancellationTokenSource?.Cancel();
            _view.FooterVersionLink.LinkClicked += (_, _) =>
            {
                _view.FooterVersionLink.LinkVisited = false;
                ReleaseNotesForm.ShowReleaseNotes(this);
            };
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

        private async Task RunBatchAsync()
        {
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

            var partPaths = _view.PartsListBox.Items.Cast<string>().ToList();
            if (partPaths.Count == 0)
            {
                MessageBox.Show(this,
                    "Add at least one part file (.SLDPRT).",
                    "No Parts",
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

            SetRunningState(true);
            _view.LogTextBox.Clear();
            _view.LogTextBox.AppendText(bootstrapLog.TrimEnd());
            _view.ProgressBar.Maximum = partPaths.Count;
            _view.ProgressBar.Value = 0;

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            int successCount = 0;
            int failCount = 0;

            try
            {
                await StaTaskRunner.Run(() =>
                {
                    AppendLog("Starting batch on SOLIDWORKS STA worker...");
                    SolidWorks.Interop.sldworks.ISldWorks? swApp = null;
                    var service = new SheetMetalDrawingService();

                    for (int i = 0; i < partPaths.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        string partPath = partPaths[i];
                        UpdateStatus($"Processing {i + 1}/{partPaths.Count}: {Path.GetFileName(partPath)}");

                        try
                        {
                            if (!File.Exists(partPath))
                                throw new FileNotFoundException("Part file not found or not accessible.", partPath);

                            swApp = SolidWorksConnection.EnsureConnected(swApp, AppendLog);

                            AppendLog("");
                            AppendLog($"=== [{i + 1}/{partPaths.Count}] {partPath} ===");
                            service.ProcessPart(swApp, partPath, templatePath, AppendLog);
                            successCount++;
                            AppendLog("Done.");
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            AppendLog($"ERROR: {ExceptionDisplay.GetLogSummary(ex)}");

                            if (SolidWorksComException.IsConnectionFailure(ex))
                            {
                                swApp = null;
                                AppendLog("SOLIDWORKS connection reset — will reconnect before the next part.");
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
                                AppendLog($"Warning: progress update failed: {ExceptionDisplay.GetLogSummary(ex)}");
                            }
                        }
                    }
                }, token);

                UpdateStatus($"Finished. Success: {successCount}, failed: {failCount}");
                MessageBox.Show(this,
                    $"Batch complete.\n\nSuccess: {successCount}\nFailed: {failCount}",
                    "Results",
                    MessageBoxButtons.OK,
                    failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Cancelled.");
                AppendLog("Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                string userMessage = ExceptionDisplay.GetUserMessage(ex);
                UpdateStatus("Critical error.");
                AppendLog($"Critical error: {ExceptionDisplay.GetLogSummary(ex)}");
                AppendLog(ExceptionDisplay.GetFullDetails(ex));
                MessageBox.Show(this, userMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                SetRunningState(false);
            }
        }

        private void SetRunningState(bool isRunning)
        {
            _view.RunButton.Enabled = !isRunning;
            _view.CancelButton.Enabled = isRunning;
            _view.TemplateTextBox.Enabled = !isRunning;
            _view.PartsListBox.Enabled = !isRunning;

            _view.StatusLabel.ForeColor = isRunning
                ? UI.Theme.UiTheme.Accent
                : UI.Theme.UiTheme.TextSecondary;
        }

        private void AppendLog(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;

                try
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                }
                catch (ObjectDisposedException)
                {
                    // Form is closing — ignore late log lines from the worker.
                }

                return;
            }

            if (_view.LogTextBox.TextLength > 0)
                _view.LogTextBox.AppendText(System.Environment.NewLine);
            _view.LogTextBox.AppendText(message);
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
