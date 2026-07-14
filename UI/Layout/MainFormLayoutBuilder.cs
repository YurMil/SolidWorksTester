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
            host.MinimumSize = new Size(UiTheme.MinWindowWidth, UiTheme.MinWindowHeight);
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
            var (partsListBox, partsCountLabel) = CreatePartsSection(root);
            var (runButton, cancelButton, statusLabel, progressBar) = CreateActionSection(root);
            var logView = CreateLogSection(root);
            var (footerAuthor, footerVersionLink) = CreateFooterSection(root);

            return new MainFormView
            {
                TemplateTextBox = field.Inner,
                PartsListBox = partsListBox,
                PartsCountLabel = partsCountLabel,
                RunButton = runButton,
                CancelButton = cancelButton,
                StatusLabel = statusLabel,
                ProgressBar = progressBar,
                LogTextBox = logView.Inner,
                FooterAuthorLabel = footerAuthor,
                FooterVersionLink = footerVersionLink
            };
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

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.BannerHeight + 4));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.FooterHeight));
            return root;
        }

        private static void CreateHeader(TableLayoutPanel root)
        {
            var title = new Label
            {
                Text = AppMetadata.ApplicationTitle,
                Dock = DockStyle.Fill,
                Font = UiTheme.TitleFont,
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.BottomLeft,
                AutoSize = false,
                Padding = new Padding(0, 0, 0, 4)
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
            var card = new ThemedCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, UiTheme.SectionGap) };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + 4));

            var header = UiControlHelper.CreateSectionLabel("Drawing template");
            var caption = UiControlHelper.CreateCaptionLabel("SOLIDWORKS template (.DRWDOT / .SLDDRW)");

            var fieldRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                MinimumSize = new Size(UiTheme.TemplateRowMinWidth, UiTheme.ControlHeight + 4),
                Padding = new Padding(0, 4, 0, 0)
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

            // Fill first, fixed-width browse on the right — browse never clips or overlaps.
            fieldRow.Controls.Add(field);
            fieldRow.Controls.Add(browse);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(caption, 0, 1);
            layout.Controls.Add(fieldRow, 0, 2);

            card.Controls.Add(layout);
            root.Controls.Add(card, 0, 2);
            return field;
        }

        private static (ListBox list, Label countLabel) CreatePartsSection(TableLayoutPanel root)
        {
            var card = new ThemedCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, UiTheme.SectionGap) };

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

            var header = UiControlHelper.CreateSectionLabel("Part files");
            var caption = UiControlHelper.CreateCaptionLabel(".SLDPRT — add files or an entire folder");

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0),
                MinimumSize = new Size(
                    UiTheme.PartsBodyMinWidth,
                    UiTheme.ControlHeight * 4 + UiTheme.ButtonGap * 3 + 40)
            };

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 12, 0),
                MinimumSize = new Size(180, 80)
            };
            UiControlHelper.EnableDoubleBuffer(listHost);
            listHost.Paint += (_, e) =>
            {
                using var pen = new Pen(UiTheme.BorderStrong);
                e.Graphics.DrawRectangle(pen, 0, 0, listHost.Width - 1, listHost.Height - 1);
            };

            var partsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiExtended,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.AppFont
            };

            var partsCountLabel = new Label
            {
                Text = "0 parts selected",
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.CaptionFont,
                Margin = new Padding(0, 8, 0, 0)
            };

            partsListBox.SelectedIndexChanged += (_, _) =>
                UpdatePartsCountLabel(partsListBox, partsCountLabel);

            listHost.Controls.Add(partsListBox);

            var sideButtons = CreatePartButtonsPanel(partsListBox, partsCountLabel);
            sideButtons.Dock = DockStyle.Right;
            sideButtons.Width = UiTheme.SideButtonWidth;
            sideButtons.MinimumSize = new Size(UiTheme.SideButtonMinColumnWidth, 0);
            sideButtons.MaximumSize = new Size(UiTheme.SideButtonWidth, 0);

            body.Controls.Add(listHost);
            body.Controls.Add(sideButtons);

            outer.Controls.Add(header, 0, 0);
            outer.Controls.Add(body, 0, 1);
            outer.Controls.Add(partsCountLabel, 0, 2);
            card.Controls.Add(outer);
            root.Controls.Add(card, 0, 3);

            return (partsListBox, partsCountLabel);
        }

        private static Control CreatePartButtonsPanel(ListBox partsListBox, Label partsCountLabel)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                MinimumSize = new Size(
                    UiTheme.SideButtonMinColumnWidth,
                    UiTheme.ControlHeight * 4 + UiTheme.ButtonGap * 3)
            };

            for (int i = 0; i < 4; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            var addFiles = CreateSideButton("Add files");
            addFiles.Click += (_, _) => AddPartFiles(partsListBox, partsCountLabel);

            var addFolder = CreateSideButton("Add folder");
            addFolder.Click += (_, _) => AddPartFolder(partsListBox, partsCountLabel);

            var remove = CreateSideButton("Remove");
            remove.Click += (_, _) => RemoveSelectedParts(partsListBox, partsCountLabel);

            var clear = CreateSideButton("Clear all");
            clear.Click += (_, _) =>
            {
                partsListBox.Items.Clear();
                UpdatePartsCountLabel(partsListBox, partsCountLabel);
            };

            panel.Controls.Add(addFiles, 0, 0);
            panel.Controls.Add(addFolder, 0, 1);
            panel.Controls.Add(remove, 0, 2);
            panel.Controls.Add(clear, 0, 3);
            return panel;
        }

        private static ThemedButton CreateSideButton(string text) =>
            new()
            {
                Text = text,
                Variant = ButtonVariant.Subtle,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, UiTheme.ButtonGap),
                MinimumSize = new Size(UiTheme.SideButtonMinColumnWidth, 28)
            };

        private static (ThemedButton run, ThemedButton cancel, Label status, ThemedProgressBar progress)
            CreateActionSection(TableLayoutPanel root)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + 10));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 6));

            var topRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
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
                Maximum = 100,
                Value = 0
            };

            panel.Controls.Add(topRow, 0, 0);
            panel.Controls.Add(progressBar, 0, 1);
            root.Controls.Add(panel, 0, 4);

            return (runButton, cancelButton, statusLabel, progressBar);
        }

        private static ThemedLogView CreateLogSection(TableLayoutPanel root)
        {
            var card = new ThemedCard
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.LogBackground,
                Margin = new Padding(0, 0, 0, UiTheme.SectionGap)
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

            var header = UiControlHelper.CreateSectionLabel("Log");
            header.Margin = new Padding(0, 0, 0, 8);

            var logView = new ThemedLogView { Dock = DockStyle.Fill };

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(logView, 0, 1);
            card.Controls.Add(layout);
            root.Controls.Add(card, 0, 5);

            return logView;
        }

        private static (Label author, LinkLabel versionLink) CreateFooterSection(TableLayoutPanel root)
        {
            var footer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };
            footer.Paint += (_, e) =>
            {
                using var pen = new Pen(UiTheme.Border);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            var authorLabel = new Label
            {
                Text = AppMetadata.FooterAuthorText,
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.CaptionFont,
                Location = new Point(0, 10)
            };

            var versionLink = new LinkLabel
            {
                Text = AppMetadata.VersionDisplay,
                AutoSize = true,
                ForeColor = UiTheme.Accent,
                LinkColor = UiTheme.Accent,
                ActiveLinkColor = UiTheme.AccentHover,
                VisitedLinkColor = UiTheme.Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = UiTheme.CaptionFont,
                Cursor = Cursors.Hand
            };
            versionLink.Location = new Point(footer.Width - 60, 10);
            footer.Resize += (_, _) =>
                versionLink.Location = new Point(footer.Width - versionLink.Width, 10);

            footer.Controls.Add(authorLabel);
            footer.Controls.Add(versionLink);
            root.Controls.Add(footer, 0, 6);

            return (authorLabel, versionLink);
        }

        private static void UpdatePartsCountLabel(ListBox listBox, Label countLabel)
        {
            int count = listBox.Items.Count;
            countLabel.Text = count == 1 ? "1 part selected" : $"{count} parts selected";
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

        private static void AddPartFiles(ListBox partsListBox, Label partsCountLabel)
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

            AddUniquePaths(partsListBox, dialog.FileNames);
            UpdatePartsCountLabel(partsListBox, partsCountLabel);
        }

        private static void AddPartFolder(ListBox partsListBox, Label partsCountLabel)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder containing part files",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string[] files = Directory.GetFiles(dialog.SelectedPath, "*.SLDPRT", SearchOption.AllDirectories);
            AddUniquePaths(partsListBox, files);
            UpdatePartsCountLabel(partsListBox, partsCountLabel);
        }

        private static void RemoveSelectedParts(ListBox partsListBox, Label partsCountLabel)
        {
            foreach (object item in partsListBox.SelectedItems.Cast<object>().ToList())
                partsListBox.Items.Remove(item);

            UpdatePartsCountLabel(partsListBox, partsCountLabel);
        }

        private static void AddUniquePaths(ListBox partsListBox, IEnumerable<string> paths)
        {
            var existing = partsListBox.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.Add(path))
                    partsListBox.Items.Add(path);
            }
        }
    }
}
