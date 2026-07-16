namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Default absolute positions (meters from sheet origin) and sheet-relative helpers.
    /// Fixed constants suit A3; <see cref="ForSheet"/> scales for A1/A2 EST borders.
    /// </summary>
    internal static class DrawingViewLayout
    {
        public const double FrontX = 0.10;
        public const double FrontY = 0.18;
        public const double TopX = 0.10;
        public const double TopY = 0.08;
        public const double RightX = 0.20;
        public const double RightY = 0.18;
        public const double FlatPatternX = 0.30;
        public const double FlatPatternY = 0.13;
        public const double IsometricX = 0.30;
        public const double IsometricY = 0.13;

        public readonly struct SheetLayout
        {
            public SheetLayout(double frontX, double frontY, double topX, double topY, double rightX, double rightY)
            {
                FrontX = frontX;
                FrontY = frontY;
                TopX = topX;
                TopY = topY;
                RightX = rightX;
                RightY = rightY;
            }

            public double FrontX { get; }
            public double FrontY { get; }
            public double TopX { get; }
            public double TopY { get; }
            public double RightX { get; }
            public double RightY { get; }
        }

        /// <summary>
        /// Orthographic anchors inside the drawing area (above title block, left of right stamp).
        /// Fractions tuned for EST landscape borders.
        /// </summary>
        public static SheetLayout ForSheet(double sheetWidthM, double sheetHeightM)
        {
            // Fallback to classic A3 constants on tiny/unknown sheets.
            if (sheetWidthM < 0.20 || sheetHeightM < 0.15)
            {
                return new SheetLayout(FrontX, FrontY, TopX, TopY, RightX, RightY);
            }

            double frontX = sheetWidthM * 0.28;
            double frontY = sheetHeightM * 0.52;
            double topX = sheetWidthM * 0.28;
            double topY = sheetHeightM * 0.22;
            double rightX = sheetWidthM * 0.62;
            double rightY = sheetHeightM * 0.52;

            return new SheetLayout(frontX, frontY, topX, topY, rightX, rightY);
        }
    }
}
