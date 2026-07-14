using System.Drawing;
using System.Windows.Forms;
using SolidWorksTester.UI.Controls;
using SolidWorksTester.UI.Documentation;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Forms
{
    /// <summary>Modal release-notes viewer backed by versioned Markdown files.</summary>
    internal sealed class ReleaseNotesForm : Form
    {
        private readonly MarkdownViewerControl _viewer;

        private ReleaseNotesForm()
        {
            Text = $"{AppMetadata.ApplicationTitle} — {AppMetadata.VersionDisplay}";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = UiTheme.AppFont;
            ClientSize = new Size(720, 560);
            MinimumSize = new Size(560, 420);
            BackColor = UiTheme.AppBackground;
            UiControlHelper.EnableDoubleBuffer(this);

            var card = new ThemedCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(UiTheme.RootPadding)
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            var titleLabel = new Label
            {
                Text = "Release notes & supported drawing types",
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionFont,
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };

            _viewer = new MarkdownViewerControl { Dock = DockStyle.Fill };

            var closeButton = new ThemedButton
            {
                Text = "Close",
                Variant = ButtonVariant.Secondary,
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                Width = 96,
                Height = UiTheme.ControlHeight
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.Transparent
            };
            buttonPanel.Controls.Add(closeButton);

            mainLayout.Controls.Add(titleLabel, 0, 0);
            mainLayout.Controls.Add(_viewer, 0, 1);
            mainLayout.Controls.Add(buttonPanel, 0, 2);
            card.Controls.Add(mainLayout);

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(UiTheme.RootPadding),
                BackColor = UiTheme.AppBackground
            };
            host.Controls.Add(card);
            Controls.Add(host);

            AcceptButton = closeButton;
            CancelButton = closeButton;

            Shown += async (_, _) =>
            {
                string markdown = ReleaseNotesLoader.GetMarkdownForCurrentVersion();
                await _viewer.DisplayMarkdownAsync(
                    markdown,
                    $"{AppMetadata.ApplicationTitle} {AppMetadata.VersionDisplay}");
            };
        }

        public static void ShowReleaseNotes(IWin32Window owner)
        {
            using var form = new ReleaseNotesForm();
            form.ShowDialog(owner);
        }
    }
}
