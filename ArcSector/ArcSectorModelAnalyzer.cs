using System;
using System.Linq;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.ArcSector
{
    /// <summary>
    /// Model-space detect: thin flat plate with concentric large cylindrical rim faces.
    /// </summary>
    internal static class ArcSectorModelAnalyzer
    {
        private const double MinRimRadiusMeters = 0.050;
        private const double MinRadiusSpreadMeters = 0.008;
        private const double OriginBucketMeters = 0.003;

        public static bool IsArcSectorPlate(PartGeometrySnapshot snap)
        {
            if (snap.HasSheetMetal)
                return false;

            if (!snap.IsThinFlatPlate(0.0005, 0.080, 8.0))
                return false;

            var large = snap.Cylinders
                .Where(c => c.Radius >= MinRimRadiusMeters)
                .Select(c => (
                    c.Radius,
                    Ox: Math.Round(c.OriginX / OriginBucketMeters),
                    Oy: Math.Round(c.OriginY / OriginBucketMeters),
                    Oz: Math.Round(c.OriginZ / OriginBucketMeters)))
                .ToList();

            if (large.Count < 2)
                return false;

            foreach (var group in large.GroupBy(c => (c.Ox, c.Oy, c.Oz)))
            {
                var radii = group.Select(g => g.Radius).Distinct().OrderBy(r => r).ToList();
                if (radii.Count < 2)
                    continue;

                if (radii[^1] - radii[0] >= MinRadiusSpreadMeters)
                    return true;
            }

            return false;
        }
    }
}
