namespace SolidWorksTester.Services.Analysis
{
    public enum PartModelKind
    {
        BentSheetMetal,
        FlatPlate,
        Cylindrical,
        ImportedGeometry,
        /// <summary>Sheet-metal lofted bend shell (rolled cylinder) — needs flat pattern + OD/height dims.</summary>
        LoftedBends
    }
}
