using System;
using System.Collections.Generic;
using System.Linq;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>One cylindrical face sample from a single COM face scan.</summary>
    public readonly struct CylinderFaceSample
    {
        public CylinderFaceSample(double radius, double originX, double originY, double originZ)
        {
            Radius = radius;
            OriginX = originX;
            OriginY = originY;
            OriginZ = originZ;
        }

        public double Radius { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double OriginZ { get; }
    }

    /// <summary>
    /// Immutable geometry/feature snapshot from one feature-tree walk + one face scan + one bbox read.
    /// Derived hole/hollow stats are precomputed in the scanner — no repeated LINQ over cylinders.
    /// </summary>
    internal sealed class PartGeometrySnapshot
    {
        public const double MinHoleRadiusMeters = 0.001;
        public const double MaxSmallHoleRadiusMeters = 0.080;
        public const double LargeCylinderRadiusMeters = 0.015;
        /// <summary>
        /// Min cylinder radius counted for hollow-pipe detection.
        /// Must be below small-pipe ID (e.g. D21×3 → ID Ø15 → R 7.5 mm).
        /// </summary>
        public const double MinPipeFaceRadiusMeters = 0.0025;
        public const int MinDenseHoleCount = 40;

        public required IReadOnlyList<CylinderFaceSample> Cylinders { get; init; }
        public int PlanarFaces { get; init; }
        public int SolidBodyCount { get; init; }

        /// <summary>Sorted ascending: short, mid, long (meters).</summary>
        public required double[] BboxSortedDims { get; init; }
        public double BboxCenterX { get; init; }
        public double BboxCenterY { get; init; }
        public double BboxCenterZ { get; init; }

        public bool HasSheetMetal { get; init; }
        public int BendCount { get; init; }
        public required IReadOnlyList<string> BendNames { get; init; }
        public bool HasCylindricalFeature { get; init; }
        public bool HasHoleFeature { get; init; }
        public bool HasChamferFeature { get; init; }
        /// <summary>True when a LoftedBend / Lofted Bends feature exists in the tree.</summary>
        public bool HasLoftedBendFeature { get; init; }
        public bool HasLinearOrFillPattern { get; init; }
        public bool HasCircularPattern { get; init; }
        public bool HasDisqualifyingSketchImportFeatures { get; init; }
        public required ImportedGeometryDetector.ImportDetectionResult ImportDetection { get; init; }

        public int CylindricalFaces { get; init; }
        public bool HasHoles { get; init; }
        public bool IsHollow { get; init; }
        public int SmallCylinderFaces { get; init; }
        public int LargeCylinderFaces { get; init; }
        public bool HasLargeOuterCylinderFace { get; init; }

        /// <summary>
        /// Dominant similar small-hole count (radii 1.5–80 mm), computed once in the scanner.
        /// Used by baffle detection and flange anti-baffle guard.
        /// </summary>
        public int DominantSimilarSmallHoleCount { get; init; }

        public bool IsThinFlatPlate(double minThickness, double maxThickness, double minFlatRatio)
        {
            if (BboxSortedDims.Length < 3)
                return false;
            double s = BboxSortedDims[0];
            double m = BboxSortedDims[1];
            return s >= minThickness && s <= maxThickness && m / Math.Max(s, 1e-12) >= minFlatRatio;
        }

        public bool IsDiscLikeBbox(double minThickness, double minFlatRatio, double roundTolerance)
        {
            if (BboxSortedDims.Length < 3)
                return false;
            double s = BboxSortedDims[0];
            double m = BboxSortedDims[1];
            double l = BboxSortedDims[2];
            if (s < minThickness || m / Math.Max(s, 1e-12) < minFlatRatio)
                return false;
            return Math.Abs(l - m) / Math.Max(m, 1e-12) <= roundTolerance;
        }

        public bool IsRoundFlatDisc(double minThickness, double minFlatRatio, double roundTolerance) =>
            IsDiscLikeBbox(minThickness, minFlatRatio, roundTolerance);

        /// <summary>Bolt-circle hole count from cylinder origins vs bbox center (pure).</summary>
        public int CountBoltCircleHoles(double minHoleRadius, double maxHoleRadius, int minCount)
        {
            if (BboxSortedDims.Length < 3)
                return 0;

            double outerRadius = BboxSortedDims[2] / 2.0;
            var polarRadii = new List<double>();

            foreach (CylinderFaceSample c in Cylinders)
            {
                if (c.Radius < minHoleRadius || c.Radius > maxHoleRadius)
                    continue;

                double dx = c.OriginX - BboxCenterX;
                double dy = c.OriginY - BboxCenterY;
                double dz = c.OriginZ - BboxCenterZ;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist < outerRadius * 0.08 || dist > outerRadius * 0.98)
                    continue;

                polarRadii.Add(Math.Round(dist, 3));
            }

            if (polarRadii.Count < minCount)
                return 0;

            return polarRadii
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .First()
                .Count();
        }

        /// <summary>Precompute derived cylinder stats once after the face scan.</summary>
        internal static void ComputeCylinderDerived(
            IReadOnlyList<CylinderFaceSample> cylinders,
            bool hasHoleFeature,
            out int cylindricalFaces,
            out bool hasHoles,
            out bool isHollow,
            out int smallCylinderFaces,
            out int largeCylinderFaces,
            out bool hasLargeOuterCylinderFace,
            out int dominantSimilarSmallHoleCount)
        {
            cylindricalFaces = cylinders.Count;
            smallCylinderFaces = 0;
            largeCylinderFaces = 0;
            hasLargeOuterCylinderFace = false;
            hasHoles = hasHoleFeature;

            double minPipe = double.MaxValue;
            double maxPipe = 0;
            int pipeCount = 0;

            var smallHoleCounts = new Dictionary<double, int>();
            int dominantSmall = 0;

            foreach (CylinderFaceSample c in cylinders)
            {
                if (c.Radius >= MinHoleRadiusMeters && c.Radius < LargeCylinderRadiusMeters)
                    smallCylinderFaces++;
                if (c.Radius >= LargeCylinderRadiusMeters)
                    largeCylinderFaces++;
                if (c.Radius >= 0.025)
                    hasLargeOuterCylinderFace = true;

                if (!hasHoles && c.Radius >= MinHoleRadiusMeters && c.Radius < 0.05)
                    hasHoles = true;

                if (c.Radius >= MinPipeFaceRadiusMeters)
                {
                    pipeCount++;
                    if (c.Radius < minPipe) minPipe = c.Radius;
                    if (c.Radius > maxPipe) maxPipe = c.Radius;
                }

                if (c.Radius >= 0.0015 && c.Radius <= MaxSmallHoleRadiusMeters)
                {
                    double key = Math.Round(c.Radius, 4);
                    smallHoleCounts.TryGetValue(key, out int n);
                    n++;
                    smallHoleCounts[key] = n;
                    if (n > dominantSmall)
                        dominantSmall = n;
                }
            }

            isHollow = pipeCount >= 2 && maxPipe - minPipe > 0.0005;
            dominantSimilarSmallHoleCount = dominantSmall;
        }
    }
}
