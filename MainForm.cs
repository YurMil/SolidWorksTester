using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorksTester.Services;
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

            SetRunningState(true);
            _view.LogTextBox.Clear();
            _view.ProgressBar.Maximum = partPaths.Count;
            _view.ProgressBar.Value = 0;

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            int successCount = 0;
            int failCount = 0;

            try
            {
                await Task.Run(() =>
                {
                    AppendLog("Connecting to SOLIDWORKS...");
                    var connection = SolidWorksConnection.Connect(AppendLog);
                    var service = new SheetMetalDrawingService();

                    for (int i = 0; i < partPaths.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        string partPath = partPaths[i];
                        UpdateStatus($"Processing {i + 1}/{partPaths.Count}: {Path.GetFileName(partPath)}");

                        try
                        {
                            AppendLog("");
                            AppendLog($"=== [{i + 1}/{partPaths.Count}] {partPath} ===");
                            service.ProcessPart(connection.App, partPath, templatePath, AppendLog);
                            successCount++;
                            AppendLog("Done.");
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            AppendLog($"ERROR: {ex.Message}");
                        }

                        UpdateProgress(i + 1);
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
                UpdateStatus("Critical error.");
                AppendLog($"Critical error: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            if (_view.LogTextBox.TextLength > 0)
                _view.LogTextBox.AppendText(Environment.NewLine);
            _view.LogTextBox.AppendText(message);
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(UpdateStatus), message);
                return;
            }

            _view.StatusLabel.Text = message;
        }

        private void UpdateProgress(int value)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int>(UpdateProgress), value);
                return;
            }

            _view.ProgressBar.Value = Math.Min(value, _view.ProgressBar.Maximum);
        }
    }
}
