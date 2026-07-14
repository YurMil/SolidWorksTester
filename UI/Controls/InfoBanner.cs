using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Compact info / warning strip with left accent bar.</summary>
    internal sealed class InfoBanner : Panel
    {
        public InfoBanner(string message)
        {
            BackColor = UiTheme.BannerBackground;
            Padding = new Padding(14, 10, 12, 10);
            UiControlHelper.EnableDoubleBuffer(this);

            var label = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.BannerText,
                Font = UiTheme.AppFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                AutoSize = false
            };
            Controls.Add(label);
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
