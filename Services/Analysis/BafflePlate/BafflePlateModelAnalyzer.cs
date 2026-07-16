namespace SolidWorksTester.Services.Analysis.BafflePlate
{
    /// <summary>
    /// Detects sheet-metal baffle / tube-sheet plates with dense hole arrays.
    /// Pure over <see cref="PartGeometrySnapshot"/> — no COM.
    /// </summary>
    internal static class BafflePlateModelAnalyzer
    {
        public const int MinDenseHoleCount = PartGeometrySnapshot.MinDenseHoleCount;

        private const double MinFlatRatio = 8.0;
        private const double MinThicknessMeters = 0.0004;
        private const double MaxThicknessMeters = 0.080;

        public static bool IsBafflePlate(PartGeometrySnapshot snap)
        {
            if (!snap.HasHoles ||
                !snap.IsThinFlatPlate(MinThicknessMeters, MaxThicknessMeters, MinFlatRatio))
                return false;

            if (snap.HasLinearOrFillPattern)
                return true;

            return snap.DominantSimilarSmallHoleCount >= MinDenseHoleCount;
        }
    }
}
