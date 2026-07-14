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
        public const int WindowWidth = 960;
        public const int WindowHeight = 780;
        /// <summary>Client area — template field + browse + part side buttons must fit without overlap.</summary>
        public const int MinWindowWidth = 880;
        public const int MinWindowHeight = 680;
        /// <summary>Minimum width for template path row (field + browse + margins).</summary>
        public const int TemplateRowMinWidth = 420;
        /// <summary>Minimum width for parts list + side button column.</summary>
        public const int PartsBodyMinWidth = 480;
        public const int RootPadding = 20;
        public const int CardPadding = 16;
        public const int SectionGap = 12;
        public const int ControlHeight = 34;
        public const int CornerRadius = 6;
        public const int BannerHeight = 52;
        public const int FooterHeight = 36;
        public const int SideButtonWidth = 132;
        public const int SideButtonMinColumnWidth = 120;
        public const int BrowseButtonWidth = 96;
        public const int ButtonGap = 6;
    }
}
