using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Flat card container (Teams-style surface on canvas background).</summary>
    internal sealed class ThemedCard : Panel
    {
        public ThemedCard()
        {
            BackColor = UiTheme.Surface;
            Padding = new Padding(UiTheme.CardPadding);
            UiControlHelper.EnableDoubleBuffer(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var pen = new Pen(UiTheme.Border);
            using var path = CreateRoundedRect(rect, UiTheme.CornerRadius);
            e.Graphics.DrawPath(pen, path);
        }

        internal static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
