namespace SolidWorksTester.SmartDim
{
    /// <summary>
    /// Shared tolerances and drawing labels used by smart-dimension helpers.
    /// </summary>
    internal static class SmartDimConstants
    {
        /// <summary>Default in-view orientation comparison tolerance (meters on sheet).</summary>
        public const double SheetOrientationToleranceMeters = 0.0005;

        /// <summary>Default numeric comparison tolerance for dimension values (meters).</summary>
        public const double DimensionValueToleranceMeters = 0.00005;

        /// <summary>Isometric view name created by cylindrical pipeline (excluded from value scans).</summary>
        public const string IsometricViewName = "Drawing View4";

        /// <summary>SOLIDWORKS SelectByID2 entity type token for drawing views.</summary>
        public const string DrawingViewSelectType = "DRAWINGVIEW";

        /// <summary>SOLIDWORKS SelectByID2 entity type token for components.</summary>
        public const string ComponentSelectType = "COMPONENT";

        /// <summary>SOLIDWORKS SelectByID2 entity type token for faces.</summary>
        public const string FaceSelectType = "FACE";

        /// <summary>SOLIDWORKS SelectByID2 entity type token for edges.</summary>
        public const string EdgeSelectType = "EDGE";
    }
}
