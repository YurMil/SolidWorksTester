using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Read-only log output with monospace text inside a card.</summary>
    internal sealed class ThemedLogView : Panel
    {
        public TextBox Inner { get; }

        public ThemedLogView()
        {
            BackColor = UiTheme.LogBackground;
            Padding = new Padding(12, 10, 12, 10);
            UiControlHelper.EnableDoubleBuffer(this);

            Inner = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.LogBackground,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.LogFont
            };
            Controls.Add(Inner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = ThemedCard.CreateRoundedRect(rect, UiTheme.CornerRadius);
            using var fill = new SolidBrush(UiTheme.LogBackground);
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawPath(pen, path);
        }
    }
}
