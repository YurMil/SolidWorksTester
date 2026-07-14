namespace SolidWorksTester.UI
{
    /// <summary>Application identity shown in the window title and footer.</summary>
    internal static class AppMetadata
    {
        public const string ApplicationTitle = "Sheet Metal Drawing Generator";
        public const string Author = "MY";
        public const string Version = "0.0.01";
        public const string VersionDisplay = "v" + Version;
        public const string FooterAuthorText = "Author: " + Author;

        public const string DefaultTemplatePath = @"C:\EST\91_SW Setup\Templates\DRAWING.DRWDOT";

        /// <summary>Release notes file name for a given application version (e.g. v0.0.01.md).</summary>
        public static string GetReleaseNotesFileName(string version) => $"v{version}.md";
    }
}
