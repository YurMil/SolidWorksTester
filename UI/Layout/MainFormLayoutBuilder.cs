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

            // Title / banner / template / action / footer size themselves from their content, so
            // they stay correct at any DPI or font scale — a pixel constant here silently clips
            // the section it is too small for. Only the parts list and the log compete for the
            // leftover space, split by percent; their cards carry MinimumSize, and the form's
            // minimum size is derived from this panel's preferred size (see FormWindowConstraints).
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

            var header = UiControlHelper.CreateSectionLabel("Part files");

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0),
                MinimumSize = new Size(
                    UiTheme.PartsBodyMinWidth,
                    UiTheme.ControlHeight * 2 + UiTheme.ButtonGap + 8)
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

            // Host fills the right column; the grid inside sits at its top.
            var sideButtonHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = UiTheme.SideButtonWidth,
                BackColor = Color.Transparent
            };

            var sideButtons = CreatePartButtonsPanel(partsListBox, partsCountLabel);
            sideButtons.Dock = DockStyle.Top;
            sideButtons.Height = UiTheme.ControlHeight * 2 + UiTheme.ButtonGap;
            sideButtonHost.Controls.Add(sideButtons);

            body.Controls.Add(listHost);
            body.Controls.Add(sideButtonHost);

            outer.Controls.Add(header, 0, 0);
            outer.Controls.Add(body, 0, 1);
            outer.Controls.Add(partsCountLabel, 0, 2);
            card.Controls.Add(outer);
            root.Controls.Add(card, 0, 3);

            return (partsListBox, partsCountLabel);
        }

        /// <summary>
        /// 2x2 grid (rather than a 4-tall single column) so the parts card's minimum height
        /// stays small enough to fit comfortably on compact displays. Rows are fixed-height and
        /// the grid is top-aligned, so the buttons stay together instead of drifting apart as
        /// the window grows — the spare vertical space belongs to the list beside them.
        /// </summary>
        private static Control CreatePartButtonsPanel(ListBox partsListBox, Label partsCountLabel)
        {
            var panel = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                MinimumSize = new Size(
                    UiTheme.SideButtonColumnMinWidth * 2 + UiTheme.ButtonGap,
                    UiTheme.ControlHeight * 2 + UiTheme.ButtonGap)
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + UiTheme.ButtonGap));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));

            var addFiles = CreateSideButton("Add files", right: true, bottom: true);
            addFiles.Click += (_, _) => AddPartFiles(partsListBox, partsCountLabel);

            var addFolder = CreateSideButton("Add folder", right: false, bottom: true);
            addFolder.Click += (_, _) => AddPartFolder(partsListBox, partsCountLabel);

            var remove = CreateSideButton("Remove", right: true, bottom: false);
            remove.Click += (_, _) => RemoveSelectedParts(partsListBox, partsCountLabel);

            var clear = CreateSideButton("Clear all", right: false, bottom: false);
            clear.Click += (_, _) =>
            {
                partsListBox.Items.Clear();
                UpdatePartsCountLabel(partsListBox, partsCountLabel);
            };

            panel.Controls.Add(addFiles, 0, 0);
            panel.Controls.Add(addFolder, 1, 0);
            panel.Controls.Add(remove, 0, 1);
            panel.Controls.Add(clear, 1, 1);
            return panel;
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

        private static ThemedLogView CreateLogSection(TableLayoutPanel root)
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
            // Anchored columns rather than hand-placed Locations, so the version link tracks the
            // right edge on its own and the row height follows the caption font at any DPI.
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
