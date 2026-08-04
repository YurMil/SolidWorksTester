using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorksTester.UI;
using SolidWorksTester.UI.Controls;
using SolidWorksTester.UI.Theme;
using SolidWorksTester.UI.Views;

namespace SolidWorksTester.UI.Layout
{
    /// <summary>Builds the modern minimalist main form layout.</summary>
    internal static class MainFormLayoutBuilder
    {
        public static MainFormView Build(Form host)
        {
            host.Text = AppMetadata.ApplicationTitle;
            host.AutoScaleMode = AutoScaleMode.Dpi;
            host.ClientSize = new Size(UiTheme.WindowWidth, UiTheme.WindowHeight);
            host.StartPosition = FormStartPosition.CenterScreen;
            host.Font = UiTheme.AppFont;
            host.BackColor = UiTheme.AppBackground;
            UiControlHelper.EnableDoubleBuffer(host);

            var root = CreateRootPanel();
            host.Controls.Add(root);

            var toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 400, ReshowDelay = 200 };

            CreateHeader(root);
            CreateDisclaimer(root);
            ThemedTextField field = CreateTemplateSection(root, toolTip);
            TasksSection tasks = CreateTasksSection(root);
            var (runButton, cancelButton, statusLabel, progressBar) = CreateActionSection(root);
            var (eventLogHeader, logView) = CreateLogSection(root);
            var (footerAuthor, footerVersionLink) = CreateFooterSection(root);

            return new MainFormView
            {
                TemplateTextBox = field.Inner,
                TaskManager = tasks.Manager,
                TasksCountLabel = tasks.CountLabel,
                AddFilesButton = tasks.AddFiles,
                AddFolderButton = tasks.AddFolder,
                RemoveButton = tasks.Remove,
                ClearButton = tasks.Clear,
                SkipButton = tasks.Skip,
                RetryButton = tasks.Retry,
                RunButton = runButton,
                CancelButton = cancelButton,
                StatusLabel = statusLabel,
                ProgressBar = progressBar,
                EventLogHeader = eventLogHeader,
                LogTextBox = logView.Inner,
                FooterAuthorLabel = footerAuthor,
                FooterVersionLink = footerVersionLink
            };
        }

        private sealed class TasksSection
        {
            public required TaskManagerView Manager { get; init; }
            public required Label CountLabel { get; init; }
            public required ThemedButton AddFiles { get; init; }
            public required ThemedButton AddFolder { get; init; }
            public required ThemedButton Remove { get; init; }
            public required ThemedButton Clear { get; init; }
            public required ThemedButton Skip { get; init; }
            public required ThemedButton Retry { get; init; }
        }

        private static TableLayoutPanel CreateRootPanel()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(UiTheme.RootPadding),
                BackColor = UiTheme.AppBackground
            };

            const float partsRowMin = UiTheme.PartsCardMinHeight + UiTheme.SectionGap;
            const float logRowMin = UiTheme.LogCardMinHeight + UiTheme.SectionGap;
            const float partsPercent = partsRowMin / (partsRowMin + logRowMin) * 100F;

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, partsPercent));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F - partsPercent));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return root;
        }

        private static void CreateHeader(TableLayoutPanel root)
        {
            var title = new Label
            {
                Text = AppMetadata.ApplicationTitle,
                Font = UiTheme.TitleFont,
                ForeColor = UiTheme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(title, 0, 0);
        }

        private static void CreateDisclaimer(TableLayoutPanel root)
        {
            var banner = new InfoBanner(AppReleaseNotes.DisclaimerBanner)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap)
            };
            root.Controls.Add(banner, 0, 1);
        }

        private static ThemedTextField CreateTemplateSection(TableLayoutPanel root, ToolTip toolTip)
        {
            var card = new ThemedCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = UiControlHelper.CreateSectionLabel("Drawing template");
            var caption = UiControlHelper.CreateCaptionLabel("SOLIDWORKS template (.DRWDOT / .SLDDRW)");

            var fieldRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Height = UiTheme.ControlHeight,
                MinimumSize = new Size(UiTheme.TemplateRowMinWidth, UiTheme.ControlHeight),
                Margin = new Padding(0, 8, 0, 0)
            };

            var field = new ThemedTextField(AppMetadata.DefaultTemplatePath)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0)
            };
            field.Inner.ShortcutsEnabled = false;
            toolTip.SetToolTip(field, field.Text);

            var browse = new ThemedButton
            {
                Text = "Browse",
                Dock = DockStyle.Right,
                Variant = ButtonVariant.Secondary,
                Width = UiTheme.BrowseButtonWidth,
                MinimumSize = new Size(UiTheme.BrowseButtonWidth, UiTheme.ControlHeight),
                MaximumSize = new Size(UiTheme.BrowseButtonWidth, UiTheme.ControlHeight)
            };
            browse.Click += (_, _) => BrowseTemplate(field.Inner, toolTip, field);

            fieldRow.Controls.Add(field);
            fieldRow.Controls.Add(browse);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(caption, 0, 1);
            layout.Controls.Add(fieldRow, 0, 2);

            card.Controls.Add(layout);
            root.Controls.Add(card, 0, 2);
            return field;
        }

        private static TasksSection CreateTasksSection(TableLayoutPanel root)
        {
            var card = new ThemedCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap),
                MinimumSize = new Size(UiTheme.PartsBodyMinWidth, UiTheme.PartsCardMinHeight)
            };

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = UiControlHelper.CreateSectionLabel("Tasks");
            var caption = UiControlHelper.CreateCaptionLabel(
                "Queue, status, elapsed time, and stage per part file");

            var headerBlock = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            headerBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerBlock.Controls.Add(header, 0, 0);
            headerBlock.Controls.Add(caption, 0, 1);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0),
                MinimumSize = new Size(
                    UiTheme.PartsBodyMinWidth,
                    UiTheme.ControlHeight * 3 + UiTheme.ButtonGap * 2 + 8)
            };

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 12, 0),
                MinimumSize = new Size(220, 100)
            };
            UiControlHelper.EnableDoubleBuffer(listHost);
            listHost.Paint += (_, e) =>
            {
                using var pen = new Pen(UiTheme.BorderStrong);
                e.Graphics.DrawRectangle(pen, 0, 0, listHost.Width - 1, listHost.Height - 1);
            };

            var taskManager = new TaskManagerView { Dock = DockStyle.Fill };

            var tasksCountLabel = new Label
            {
                Text = "0 tasks",
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.CaptionFont,
                Margin = new Padding(0, 8, 0, 0)
            };

            taskManager.TasksChanged += (_, _) => UpdateTasksCountLabel(taskManager, tasksCountLabel);
            taskManager.SelectionChanged += (_, _) => UpdateTasksCountLabel(taskManager, tasksCountLabel);

            listHost.Controls.Add(taskManager);

            var sideButtonHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = UiTheme.SideButtonWidth,
                BackColor = Color.Transparent
            };

            var side = CreateTaskButtonsPanel(taskManager, tasksCountLabel);
            side.Panel.Dock = DockStyle.Top;
            side.Panel.Height = UiTheme.ControlHeight * 3 + UiTheme.ButtonGap * 2;
            sideButtonHost.Controls.Add(side.Panel);

            body.Controls.Add(listHost);
            body.Controls.Add(sideButtonHost);

            outer.Controls.Add(headerBlock, 0, 0);
            outer.Controls.Add(body, 0, 1);
            outer.Controls.Add(tasksCountLabel, 0, 2);
            card.Controls.Add(outer);
            root.Controls.Add(card, 0, 3);

            return new TasksSection
            {
                Manager = taskManager,
                CountLabel = tasksCountLabel,
                AddFiles = side.AddFiles,
                AddFolder = side.AddFolder,
                Remove = side.Remove,
                Clear = side.Clear,
                Skip = side.Skip,
                Retry = side.Retry
            };
        }

        private sealed class TaskButtons
        {
            public required Control Panel { get; init; }
            public required ThemedButton AddFiles { get; init; }
            public required ThemedButton AddFolder { get; init; }
            public required ThemedButton Remove { get; init; }
            public required ThemedButton Clear { get; init; }
            public required ThemedButton Skip { get; init; }
            public required ThemedButton Retry { get; init; }
        }

        private static TaskButtons CreateTaskButtonsPanel(TaskManagerView taskManager, Label countLabel)
        {
            var panel = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                MinimumSize = new Size(
                    UiTheme.SideButtonColumnMinWidth * 2 + UiTheme.ButtonGap,
                    UiTheme.ControlHeight * 3 + UiTheme.ButtonGap * 2)
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + UiTheme.ButtonGap));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + UiTheme.ButtonGap));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));

            var addFiles = CreateSideButton("Add files", right: true, bottom: true);
            addFiles.Click += (_, _) =>
            {
                AddPartFiles(taskManager);
                UpdateTasksCountLabel(taskManager, countLabel);
            };

            var addFolder = CreateSideButton("Add folder", right: false, bottom: true);
            addFolder.Click += (_, _) =>
            {
                AddPartFolder(taskManager);
                UpdateTasksCountLabel(taskManager, countLabel);
            };

            var remove = CreateSideButton("Remove", right: true, bottom: true);
            remove.Click += (_, _) =>
            {
                taskManager.RemoveSelected();
                UpdateTasksCountLabel(taskManager, countLabel);
            };

            var clear = CreateSideButton("Clear all", right: false, bottom: true);
            clear.Click += (_, _) =>
            {
                taskManager.ClearTasks();
                UpdateTasksCountLabel(taskManager, countLabel);
            };

            var skip = CreateSideButton("Skip", right: true, bottom: false);
            var retry = CreateSideButton("Retry", right: false, bottom: false);

            panel.Controls.Add(addFiles, 0, 0);
            panel.Controls.Add(addFolder, 1, 0);
            panel.Controls.Add(remove, 0, 1);
            panel.Controls.Add(clear, 1, 1);
            panel.Controls.Add(skip, 0, 2);
            panel.Controls.Add(retry, 1, 2);

            return new TaskButtons
            {
                Panel = panel,
                AddFiles = addFiles,
                AddFolder = addFolder,
                Remove = remove,
                Clear = clear,
                Skip = skip,
                Retry = retry
            };
        }

        private static ThemedButton CreateSideButton(string text, bool right, bool bottom) =>
            new()
            {
                Text = text,
                Variant = ButtonVariant.Subtle,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, right ? UiTheme.ButtonGap : 0, bottom ? UiTheme.ButtonGap : 0),
                MinimumSize = new Size(UiTheme.SideButtonColumnMinWidth, UiTheme.ControlHeight)
            };

        private static (ThemedButton run, ThemedButton cancel, Label status, ThemedProgressBar progress)
            CreateActionSection(TableLayoutPanel root)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var topRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Height = UiTheme.ControlHeight,
                MinimumSize = new Size(340, UiTheme.ControlHeight)
            };

            var runButton = new ThemedButton
            {
                Text = "Generate",
                Variant = ButtonVariant.Primary,
                Dock = DockStyle.Left,
                Width = 140,
                Margin = new Padding(0, 0, 10, 0),
                MinimumSize = new Size(120, UiTheme.ControlHeight),
                MaximumSize = new Size(140, UiTheme.ControlHeight)
            };

            var cancelButton = new ThemedButton
            {
                Text = "Cancel",
                Variant = ButtonVariant.Secondary,
                Dock = DockStyle.Left,
                Width = 104,
                Enabled = false,
                Margin = new Padding(0, 0, 12, 0),
                MinimumSize = new Size(88, UiTheme.ControlHeight),
                MaximumSize = new Size(104, UiTheme.ControlHeight)
            };

            var statusLabel = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.AppFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                AutoSize = false,
                Padding = new Padding(4, 0, 0, 0)
            };

            topRow.Controls.Add(statusLabel);
            topRow.Controls.Add(cancelButton);
            topRow.Controls.Add(runButton);

            var progressBar = new ThemedProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 4,
                Margin = new Padding(0, 8, 0, 0),
                Maximum = 100,
                Value = 0
            };

            panel.Controls.Add(topRow, 0, 0);
            panel.Controls.Add(progressBar, 0, 1);
            root.Controls.Add(panel, 0, 4);

            return (runButton, cancelButton, statusLabel, progressBar);
        }

        private static (Label header, ThemedLogView log) CreateLogSection(TableLayoutPanel root)
        {
            var card = new ThemedCard
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.LogBackground,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap),
                MinimumSize = new Size(UiTheme.PartsBodyMinWidth, UiTheme.LogCardMinHeight)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = UiControlHelper.CreateSectionLabel("Event log");
            header.Margin = new Padding(0, 0, 0, 8);

            var logView = new ThemedLogView { Dock = DockStyle.Fill };

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(logView, 0, 1);
            card.Controls.Add(layout);
            root.Controls.Add(card, 0, 5);

            return (header, logView);
        }

        private static (Label author, LinkLabel versionLink) CreateFooterSection(TableLayoutPanel root)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            footer.Paint += (_, e) =>
            {
                using var pen = new Pen(UiTheme.Border);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            var authorLabel = new Label
            {
                Text = AppMetadata.FooterAuthorText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.CaptionFont,
                Margin = new Padding(0, 2, 0, 0)
            };

            var versionLink = new LinkLabel
            {
                Text = AppMetadata.VersionDisplay,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.Accent,
                LinkColor = UiTheme.Accent,
                ActiveLinkColor = UiTheme.AccentHover,
                VisitedLinkColor = UiTheme.Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = UiTheme.CaptionFont,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 0)
            };

            footer.Controls.Add(authorLabel, 0, 0);
            footer.Controls.Add(versionLink, 1, 0);
            root.Controls.Add(footer, 0, 6);

            return (authorLabel, versionLink);
        }

        private static void UpdateTasksCountLabel(TaskManagerView manager, Label countLabel)
        {
            int count = manager.TaskCount;
            int selected = manager.SelectedTasks.Count;
            string baseText = count == 1 ? "1 task" : $"{count} tasks";
            countLabel.Text = selected > 0 ? $"{baseText} · {selected} selected" : baseText;
        }

        private static void BrowseTemplate(TextBox templateTextBox, ToolTip toolTip, ThemedTextField field)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select Drawing Template",
                Filter = "SOLIDWORKS Templates|*.drwdot;*.slddrw|All Files|*.*",
                CheckFileExists = true,
                FileName = templateTextBox.Text,
                InitialDirectory = GetTemplateInitialDirectory(templateTextBox.Text)
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            templateTextBox.Text = dialog.FileName;
            toolTip.SetToolTip(field, dialog.FileName);
            toolTip.SetToolTip(templateTextBox, dialog.FileName);
        }

        private static string? GetTemplateInitialDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string? dir = Path.GetDirectoryName(path);
            return Directory.Exists(dir) ? dir : null;
        }

        private static void AddPartFiles(TaskManagerView taskManager)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select Part Files",
                Filter = "SOLIDWORKS Parts|*.sldprt|All Files|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            taskManager.AddPaths(dialog.FileNames);
        }

        private static void AddPartFolder(TaskManagerView taskManager)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder containing part files",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string[] files = Directory.GetFiles(dialog.SelectedPath, "*.SLDPRT", SearchOption.AllDirectories);
            taskManager.AddPaths(files);
        }
    }
}
