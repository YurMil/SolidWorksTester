using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Single-line text field with subtle border and focus ring.</summary>
    internal sealed class ThemedTextField : Panel
    {
        private readonly TextBox _inner;
        private bool _focused;

        public TextBox Inner => _inner;

        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value ?? string.Empty;
        }

        public ThemedTextField(string? initialText = null)
        {
            Height = UiTheme.ControlHeight;
            MinimumSize = new Size(120, UiTheme.ControlHeight);
            BackColor = UiTheme.Surface;
            Padding = new Padding(10, 0, 10, 0);
            UiControlHelper.EnableDoubleBuffer(this);

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.AppFont,
                Text = initialText ?? string.Empty,
                ShortcutsEnabled = false
            };

            _inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
            _inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };

            Controls.Add(_inner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using var path = ThemedCard.CreateRoundedRect(rect, 4);
            using var fill = new SolidBrush(Enabled ? UiTheme.Surface : UiTheme.SurfaceMuted);
            e.Graphics.FillPath(fill, path);

            Color borderColor = !Enabled
                ? UiTheme.Border
                : _focused ? UiTheme.Accent : UiTheme.BorderStrong;

            using var pen = new Pen(borderColor, _focused ? 1.5f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _inner.Enabled = Enabled;
            Invalidate();
        }
    }
}
