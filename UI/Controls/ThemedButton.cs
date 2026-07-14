using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    internal enum ButtonVariant
    {
        Primary,
        Secondary,
        Subtle
    }

    /// <summary>Rounded button with subtle elevation (Material-style).</summary>
    internal sealed class ThemedButton : Button
    {
        private bool _hover;
        private bool _pressed;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;

        public ThemedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = UiTheme.ButtonFont;
            Height = UiTheme.ControlHeight;
            MinimumSize = new Size(72, UiTheme.ControlHeight);
            Cursor = Cursors.Hand;
            TabStop = true;
            UiControlHelper.EnableDoubleBuffer(this);

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; _pressed = false; Invalidate(); };
            MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _pressed = true;
                    Invalidate();
                }
            };
            MouseUp += (_, _) => { _pressed = false; Invalidate(); };
            EnabledChanged += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? UiTheme.AppBackground);

            int yOffset = _pressed && Enabled ? 1 : 0;
            var rect = new Rectangle(0, yOffset, Width - 1, Height - 1 - yOffset);
            GetColors(out Color back, out Color top, out Color bottom, out Color fore, out Color border);

            if (Enabled && !_pressed && Variant != ButtonVariant.Primary)
                DrawSoftShadow(g, rect);

            using var path = ThemedCard.CreateRoundedRect(rect, UiTheme.CornerRadius);
            using (var brush = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
                g.FillPath(brush, path);

            if (border.A > 0)
            {
                using var pen = new Pen(border);
                g.DrawPath(pen, path);
            }

            if (Enabled && !_pressed && Variant == ButtonVariant.Primary)
            {
                using var shine = new Pen(Color.FromArgb(48, 255, 255, 255));
                g.DrawLine(shine, rect.Left + UiTheme.CornerRadius, rect.Top + 1,
                    rect.Right - UiTheme.CornerRadius, rect.Top + 1);
            }

            var textRect = new Rectangle(rect.X, rect.Y + yOffset, rect.Width, rect.Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawSoftShadow(Graphics g, Rectangle rect)
        {
            var shadowRect = new Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height);
            using var path = ThemedCard.CreateRoundedRect(shadowRect, UiTheme.CornerRadius);
            using var shadow = new SolidBrush(Color.FromArgb(22, 0, 0, 0));
            g.FillPath(shadow, path);
        }

        private void GetColors(
            out Color back,
            out Color top,
            out Color bottom,
            out Color fore,
            out Color border)
        {
            border = Color.Transparent;
            back = UiTheme.Surface;

            if (!Enabled)
            {
                top = bottom = UiTheme.SurfaceMuted;
                fore = UiTheme.TextMuted;
                return;
            }

            switch (Variant)
            {
                case ButtonVariant.Primary:
                    back = _pressed ? UiTheme.AccentPressed : _hover ? UiTheme.AccentHover : UiTheme.Accent;
                    top = Lighten(back, 18);
                    bottom = Darken(back, 12);
                    fore = UiTheme.TextOnAccent;
                    break;

                case ButtonVariant.Subtle:
                    back = _pressed
                        ? Color.FromArgb(225, 224, 222)
                        : _hover ? Color.FromArgb(245, 244, 243) : Color.FromArgb(250, 249, 248);
                    top = Lighten(back, 10);
                    bottom = Darken(back, 8);
                    fore = UiTheme.TextPrimary;
                    border = UiTheme.BorderStrong;
                    break;

                default:
                    back = _pressed
                        ? Color.FromArgb(232, 231, 229)
                        : _hover ? Color.FromArgb(248, 247, 246) : UiTheme.Surface;
                    top = Color.White;
                    bottom = Color.FromArgb(240, 239, 237);
                    fore = UiTheme.TextPrimary;
                    border = UiTheme.BorderStrong;
                    break;
            }
        }

        private static Color Lighten(Color c, int amount) =>
            Color.FromArgb(c.A,
                Math.Min(255, c.R + amount),
                Math.Min(255, c.G + amount),
                Math.Min(255, c.B + amount));

        private static Color Darken(Color c, int amount) =>
            Color.FromArgb(c.A,
                Math.Max(0, c.R - amount),
                Math.Max(0, c.G - amount),
                Math.Max(0, c.B - amount));
    }
}
