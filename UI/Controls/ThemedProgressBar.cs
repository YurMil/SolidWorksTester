using System.ComponentModel;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>Thin accent progress indicator.</summary>
    internal sealed class ThemedProgressBar : Control
    {
        private int _maximum = 100;
        private int _value;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(1, value);
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, 0, _maximum);
                Invalidate();
            }
        }

        public ThemedProgressBar()
        {
            Height = 4;
            UiControlHelper.EnableDoubleBuffer(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int w = Width;
            int h = Height;
            int radius = h / 2;

            using var track = new System.Drawing.SolidBrush(UiTheme.Border);
            g.FillRectangle(track, 0, 0, w, h);

            if (_value <= 0)
                return;

            float ratio = (float)_value / _maximum;
            int fillW = Math.Max(radius * 2, (int)(w * ratio));

            using var fill = new System.Drawing.SolidBrush(UiTheme.Accent);
            g.FillRectangle(fill, 0, 0, fillW, h);
        }
    }
}
