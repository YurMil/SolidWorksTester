using System.Reflection;
using System.Windows.Forms;

namespace SolidWorksTester.UI.Theme
{
    internal static class UiControlHelper
    {
        public static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                control,
                new object[] { true });
        }

        public static Label CreateSectionLabel(string text) =>
            new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.SectionFont,
                Margin = new Padding(0, 0, 0, 6)
            };

        public static Label CreateCaptionLabel(string text) =>
            new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.CaptionFont,
                Margin = new Padding(0, 4, 0, 0)
            };
    }
}
