using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Compact info / warning strip with left accent bar.</summary>
    internal sealed class InfoBanner : Panel
    {
        private readonly string _message;

        public InfoBanner(string message)
        {
            _message = message;
            BackColor = UiTheme.BannerBackground;
            Padding = new Padding(14, 10, 12, 10);
            // Height follows the wrapped text; the disclaimer must never be truncated.
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            UiControlHelper.EnableDoubleBuffer(this);

            var label = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.BannerText,
                Font = UiTheme.AppFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            Controls.Add(label);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int available = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue
                ? proposedSize.Width
                : Width;

            int textWidth = Math.Max(1, available - Padding.Horizontal);
            Size text = TextRenderer.MeasureText(
                _message,
                Font,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak);

            return new Size(available, text.Height + Padding.Vertical);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var accent = new SolidBrush(UiTheme.BannerBorder);
            e.Graphics.FillRectangle(accent, 0, 0, 4, Height);

            using var border = new Pen(Color.FromArgb(255, 220, 163));
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(border, rect);
        }
    }
}
