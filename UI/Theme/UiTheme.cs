using System.Drawing;

namespace SolidWorksTester.UI.Theme
{
    /// <summary>Minimal Teams / Material-inspired palette — no external UI packages.</summary>
    internal static class UiTheme
    {
        // Surfaces
        public static readonly Color AppBackground = Color.FromArgb(243, 242, 241);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceMuted = Color.FromArgb(250, 250, 250);
        public static readonly Color LogBackground = Color.FromArgb(248, 248, 248);

        // Borders & accents
        public static readonly Color Border = Color.FromArgb(237, 235, 233);
        public static readonly Color BorderStrong = Color.FromArgb(210, 208, 206);
        public static readonly Color Accent = Color.FromArgb(98, 100, 167);
        public static readonly Color AccentHover = Color.FromArgb(70, 71, 117);
        public static readonly Color AccentPressed = Color.FromArgb(51, 52, 87);
        public static readonly Color FocusRing = Color.FromArgb(180, 182, 220);

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(36, 36, 36);
        public static readonly Color TextSecondary = Color.FromArgb(96, 94, 92);
        public static readonly Color TextMuted = Color.FromArgb(161, 159, 157);
        public static readonly Color TextOnAccent = Color.White;

        // Banner
        public static readonly Color BannerBackground = Color.FromArgb(255, 249, 230);
        public static readonly Color BannerBorder = Color.FromArgb(255, 185, 0);
        public static readonly Color BannerText = Color.FromArgb(50, 49, 48);

        // Typography
        public static readonly Font AppFont = new("Segoe UI", 9.25F);
        public static readonly Font TitleFont = new("Segoe UI Semibold", 14F, FontStyle.Regular);
        public static readonly Font SectionFont = new("Segoe UI Semibold", 9.5F, FontStyle.Regular);
        public static readonly Font CaptionFont = new("Segoe UI", 8.75F);
        public static readonly Font ButtonFont = new("Segoe UI Semibold", 9.25F);
        public static readonly Font LogFont = new("Cascadia Mono", 9F, FontStyle.Regular);

        // Layout
        //
        // These are logical (96-DPI) values; WinForms scales them with the form. Section heights
        // are deliberately absent — every content-sized row measures itself, and the window's
        // minimum size is measured from the live layout (see FormWindowConstraints), so nothing
        // here has to be kept in sync with what the controls actually need.
        public const int WindowWidth = 900;
        public const int WindowHeight = 700;

        public const int RootPadding = 12;
        public const int CardPadding = 16;
        public const int SectionGap = 12;
        public const int ControlHeight = 34;
        public const int CornerRadius = 6;
        public const int FooterHeight = 32;

        /// <summary>Minimum width for template path row (field + browse + margins).</summary>
        public const int TemplateRowMinWidth = 420;
        /// <summary>Minimum width for parts list + side button column.</summary>
        public const int PartsBodyMinWidth = 500;
        /// <summary>Minimum height of the parts card (header + 2x2 button grid + count label + padding).</summary>
        public const int PartsCardMinHeight = 174;
        /// <summary>Minimum height of the log card (header + a couple of visible lines + padding).</summary>
        public const int LogCardMinHeight = 92;
        /// <summary>Narrowest the content can get before the parts card starts to squeeze.</summary>
        public const int MinContentWidth = 700;

        /// <summary>Total width of the 2x2 part-action button grid.</summary>
        public const int SideButtonWidth = 220;
        /// <summary>Minimum width of a single button in the 2x2 grid.</summary>
        public const int SideButtonColumnMinWidth = 96;
        public const int BrowseButtonWidth = 96;
        public const int ButtonGap = 6;
    }
}
