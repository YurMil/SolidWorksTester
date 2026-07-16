namespace SolidWorksTester.Services.Drawing.Routing
{
    /// <summary>Stable drawing pipeline identifiers (P-01 … P-04 + fallback).</summary>
    public enum DrawingPipelineId
    {
        Unknown = 0,
        FlatPlate = 1,
        BentSheetMetal = 2,
        Cylindrical = 3,
        ImportedGeometry = 4,
        /// <summary>Views + minimal shared dims when kind/pipeline is ambiguous.</summary>
        GenericFallback = 5,
        /// <summary>Lofted-bend sheet-metal shell: ortho + flat pattern (inner up).</summary>
        LoftedBends = 6
    }
}
