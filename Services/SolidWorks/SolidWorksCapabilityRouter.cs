namespace SolidWorksTester.Services.SolidWorks
{
    /// <summary>Version-specific automation strategy flags (2022–2026).</summary>
    public enum CylindricalCenterlineStrategy
    {
        /// <summary>End-face center marks + side-view centerline via outer-edge / face APIs.</summary>
        FullLegacy
    }

    /// <summary>Selects algorithms based on <see cref="SolidWorksVersionContext"/>.</summary>
    public static class SolidWorksCapabilityRouter
    {
        public static SolidWorksVersionContext Context => SolidWorksVersionContext.Current;

        public static CylindricalCenterlineStrategy GetCylindricalCenterlineStrategy() =>
            CylindricalCenterlineStrategy.FullLegacy;

        public static bool SupportsSideViewCenterlineApi() => true;

        public static bool SupportsAutoInsertCenterMarks2() =>
            Context.ProductYear <= 2024;

        public static bool PreferSilentDocumentOpen() => true;

        public static bool UseViewPaletteFlatPatternSearch() => Context.ProductYear >= 2022;

        public static string GetStrategyNotes()
        {
            return Context.ProductYear switch
            {
                >= 2026 => $"2026 mode: HLV display + outer-edge side centerlines, interop baseline {Context.InteropReferenceVersion}.",
                2025 => "2025 mode: HLV display + outer-edge side centerlines (silhouette API avoided).",
                2024 => "2024 mode: HLV display + outer-edge side centerlines.",
                2023 => "2023 mode: HLV display + outer-edge side centerlines.",
                _ => "2022 mode: HLV display + outer-edge side centerlines."
            };
        }
    }
}
