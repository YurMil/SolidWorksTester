namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Custom / configuration property names used to override geometry-based part classification.
    /// Aliases are checked in order (first match wins).
    /// </summary>
    internal static class PartClassificationPropertyNames
    {
        public static readonly string[] PartKindKeys =
        {
            "CADAS_PartKind",
            "DrawingPartType",
            "PartType",
            "CADAS_DrawingPartType"
        };

        public static readonly string[] FlatPlateSubKindKeys =
        {
            "CADAS_FlatPlateSubKind",
            "FlatPlateSubKind",
            "DrawingFlatPlateSubKind"
        };

        public static readonly string[] DrawingProfileKeys =
        {
            "CADAS_DrawingProfile",
            "DrawingProfile"
        };

        /// <summary>EST PDM configuration property — primary part family (PIPE, PLATE, FLANGE, …).</summary>
        public static readonly string[] EstNameKeys = { "Name" };

        public static readonly string[] EstDescriptionKeys = { "Description" };

        public static readonly string[] EstDimensionKeys =
        {
            "DIM1", "DIM2", "DIM3", "Length"
        };
    }
}
