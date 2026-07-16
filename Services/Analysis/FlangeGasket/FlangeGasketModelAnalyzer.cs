namespace SolidWorksTester.Services.Analysis.FlangeGasket
{
    /// <summary>
    /// Detects disc-like flanges/gaskets with circular hole patterns.
    /// Pure over <see cref="PartGeometrySnapshot"/> — no COM.
    /// </summary>
    internal static class FlangeGasketModelAnalyzer
    {
        private const double MinHoleRadiusMeters = 0.001;
        private const double MaxHoleRadiusMeters = 0.25;
        private const int MinBoltCircleHoleCount = 3;
        private const int MaxBoltCircleHoleCount = 48;

        public static bool IsFlangeOrGasket(PartGeometrySnapshot snap)
        {
            if (!snap.HasHoles ||
                !snap.IsDiscLikeBbox(minThickness: 0.0005, minFlatRatio: 5.0, roundTolerance: 0.08))
                return false;

            if (snap.DominantSimilarSmallHoleCount >= PartGeometrySnapshot.MinDenseHoleCount)
                return false;

            if (snap.HasCircularPattern)
                return true;

            int boltCount = snap.CountBoltCircleHoles(
                MinHoleRadiusMeters, MaxHoleRadiusMeters, MinBoltCircleHoleCount);

            return boltCount >= MinBoltCircleHoleCount && boltCount <= MaxBoltCircleHoleCount;
        }
    }
}
