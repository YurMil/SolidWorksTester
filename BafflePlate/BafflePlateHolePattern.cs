using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Hole-pattern metrics for Detail A / Section B-B sketch geometry.
    /// Pure math over <see cref="CylinderFaceSample"/> collected during part analysis —
    /// no face walk at drawing stage. Pcs prefers a pattern-feature instance count.
    /// </summary>
    internal sealed class BaffleHolePatternInfo
    {
        public double RadiusMeters { get; init; }
        public int HoleCount { get; init; }
        /// <summary>False when the count comes from an early-exited sample (lower bound only).</summary>
        public bool HoleCountExact { get; init; }
        public double PitchMeters { get; init; }
        public double AngleDegrees { get; init; }
        public double SeedX { get; init; }
        public double SeedY { get; init; }
        public double SeedZ { get; init; }
        public double NeighborX { get; init; }
        public double NeighborY { get; init; }
        public double NeighborZ { get; init; }
        public double AngleNeighborX { get; init; }
        public double AngleNeighborY { get; init; }
        public double AngleNeighborZ { get; init; }
        public string CountSource { get; init; } = "sample";
    }

    internal static class BafflePlateHolePattern
    {
        private const double PitchMin = 0.010;
        private const double PitchMax = 0.200;
        private const double AngleTolDeg = 12.0;
        /// <summary>Origin clustering grid — merges split faces of the same hole (seams, halves).</summary>
        private const double OriginDedupeGridMeters = 0.001;

        /// <summary>
        /// Pattern metrics from analysis-stage samples. <paramref name="part"/> is optional and only
        /// used for a cheap feature-tree read of pattern instance totals (exact pcs).
        /// </summary>
        public static bool TryFromAnalysisSamples(
            IReadOnlyList<CylinderFaceSample> samples,
            PartDoc? part,
            Action<string> log,
            out BaffleHolePatternInfo info)
        {
            info = null!;

            if (samples == null || samples.Count < 3)
            {
                log($"  Hole pattern: analysis samples missing ({samples?.Count ?? 0}) — skip Detail/Section.");
                return false;
            }

            if (!TryComputeFromSamples(samples, log, out info))
                return false;

            if (part != null)
            {
                int patternCount = TryReadPatternInstanceCount(part, out string patternSource);
                if (patternCount >= info.HoleCount)
                {
                    info = CloneWithCount(info, patternCount, patternSource, exact: true);
                    log($"  Hole pattern: pcs override → {patternCount} ({patternSource}).");
                }
            }

            return true;
        }

        internal static bool TryComputeFromSamples(
            IReadOnlyList<CylinderFaceSample> samples,
            Action<string> log,
            out BaffleHolePatternInfo info)
        {
            info = null!;

            // 1) Merge faces of the same physical hole: cluster origins on a 1 mm grid per radius.
            var uniqueByCell = new Dictionary<(double R, double X, double Y, double Z), CylinderFaceSample>();
            foreach (CylinderFaceSample s in samples)
            {
                var key = (
                    Math.Round(s.Radius, 4),
                    Math.Round(s.OriginX / OriginDedupeGridMeters),
                    Math.Round(s.OriginY / OriginDedupeGridMeters),
                    Math.Round(s.OriginZ / OriginDedupeGridMeters));
                uniqueByCell.TryAdd(key, s);
            }

            // 2) Dominant radius group among deduped holes.
            var byRadius = new Dictionary<double, List<CylinderFaceSample>>();
            foreach (var kv in uniqueByCell)
            {
                if (!byRadius.TryGetValue(kv.Key.R, out var list))
                {
                    list = new List<CylinderFaceSample>();
                    byRadius[kv.Key.R] = list;
                }

                list.Add(kv.Value);
            }

            List<CylinderFaceSample>? dominant = null;
            double dominantRadius = 0;
            foreach (var kv in byRadius)
            {
                if (dominant == null || kv.Value.Count > dominant.Count)
                {
                    dominant = kv.Value;
                    dominantRadius = kv.Key;
                }
            }

            if (dominant == null || dominant.Count < 3)
            {
                log($"  Hole pattern: dominant radius group too small " +
                    $"({dominant?.Count ?? 0} of {uniqueByCell.Count} unique holes).");
                return false;
            }

            // 3) Seed = hole closest to the group centroid (stable interior pick).
            double cx = 0, cy = 0, cz = 0;
            foreach (CylinderFaceSample s in dominant)
            {
                cx += s.OriginX;
                cy += s.OriginY;
                cz += s.OriginZ;
            }

            cx /= dominant.Count;
            cy /= dominant.Count;
            cz /= dominant.Count;

            int seedIdx = 0;
            double bestDist2 = double.MaxValue;
            for (int i = 0; i < dominant.Count; i++)
            {
                CylinderFaceSample s = dominant[i];
                double d2 = Dist2(s.OriginX - cx, s.OriginY - cy, s.OriginZ - cz);
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    seedIdx = i;
                }
            }

            CylinderFaceSample seed = dominant[seedIdx];
            int neighborIdx = FindNearest(dominant, seedIdx, PitchMin, PitchMax);
            if (neighborIdx < 0)
            {
                LogNeighborDiagnostics(dominant, seedIdx, log);
                return false;
            }

            CylinderFaceSample neighbor = dominant[neighborIdx];
            double pitch = Math.Sqrt(Dist2(
                neighbor.OriginX - seed.OriginX,
                neighbor.OriginY - seed.OriginY,
                neighbor.OriginZ - seed.OriginZ));

            double vx = neighbor.OriginX - seed.OriginX;
            double vy = neighbor.OriginY - seed.OriginY;
            double vz = neighbor.OriginZ - seed.OriginZ;

            int angleIdx = FindAngleNeighbor(dominant, seedIdx, neighborIdx, vx, vy, vz, pitch);
            double angleDeg = 60.0;
            CylinderFaceSample angleNeighbor = neighbor;
            if (angleIdx >= 0)
            {
                angleNeighbor = dominant[angleIdx];
                angleDeg = AngleDegreesBetween(
                    vx, vy, vz,
                    angleNeighbor.OriginX - seed.OriginX,
                    angleNeighbor.OriginY - seed.OriginY,
                    angleNeighbor.OriginZ - seed.OriginZ);
            }

            info = new BaffleHolePatternInfo
            {
                RadiusMeters = dominantRadius,
                HoleCount = dominant.Count,
                HoleCountExact = false,
                PitchMeters = pitch,
                AngleDegrees = angleDeg,
                SeedX = seed.OriginX,
                SeedY = seed.OriginY,
                SeedZ = seed.OriginZ,
                NeighborX = neighbor.OriginX,
                NeighborY = neighbor.OriginY,
                NeighborZ = neighbor.OriginZ,
                AngleNeighborX = angleNeighbor.OriginX,
                AngleNeighborY = angleNeighbor.OriginY,
                AngleNeighborZ = angleNeighbor.OriginZ,
                CountSource = "sample"
            };

            log($"  Hole pattern: Ø{dominantRadius * 2000:F1}, pitch {pitch * 1000:F1} mm, " +
                $"angle {angleDeg:F0}°, sample holes {dominant.Count} (of {uniqueByCell.Count} unique).");
            return true;
        }

        private static void LogNeighborDiagnostics(
            IReadOnlyList<CylinderFaceSample> dominant,
            int seedIdx,
            Action<string> log)
        {
            CylinderFaceSample seed = dominant[seedIdx];
            double minD = double.MaxValue;
            for (int i = 0; i < dominant.Count; i++)
            {
                if (i == seedIdx)
                    continue;

                CylinderFaceSample s = dominant[i];
                double d = Math.Sqrt(Dist2(
                    s.OriginX - seed.OriginX,
                    s.OriginY - seed.OriginY,
                    s.OriginZ - seed.OriginZ));
                if (d < minD)
                    minD = d;
            }

            log($"  Hole pattern: no neighbor in pitch band {PitchMin * 1000:F0}–{PitchMax * 1000:F0} mm " +
                $"(closest {minD * 1000:F1} mm over {dominant.Count} holes).");
        }

        /// <summary>
        /// LPattern / LocalLPattern / FillPattern / CirPattern instance totals (one feature-tree walk).
        /// </summary>
        private static int TryReadPatternInstanceCount(PartDoc part, out string source)
        {
            source = string.Empty;
            int best = 0;
            string bestSource = string.Empty;

            try
            {
                if (part is not ModelDoc2 model)
                    return 0;

                Feature? feat = model.FirstFeature() as Feature;
                while (feat != null)
                {
                    string type = feat.GetTypeName2() ?? string.Empty;
                    int n = 0;
                    string label = feat.Name ?? type;

                    try
                    {
                        object? def = feat.GetDefinition();
                        if (def is ILinearPatternFeatureData lp)
                            n = Math.Max(1, lp.D1TotalInstances) * Math.Max(1, lp.D2TotalInstances);
                        else if (def is ILocalLinearPatternFeatureData llp)
                            n = Math.Max(1, llp.D1TotalInstances) * Math.Max(1, llp.D2TotalInstances);
                        else if (def is IFillPatternFeatureData fill)
                            n = fill.NoOfInstances;
                        else if (def is ICircularPatternFeatureData cir)
                            n = Math.Max(cir.TotalInstances, cir.TotalInstances2);
                    }
                    catch
                    {
                        n = 0;
                    }

                    if (n > best)
                    {
                        best = n;
                        bestSource = $"{type}/{label}";
                    }

                    feat = feat.GetNextFeature() as Feature;
                }
            }
            catch
            {
                return 0;
            }

            source = bestSource;
            return best;
        }

        private static BaffleHolePatternInfo CloneWithCount(
            BaffleHolePatternInfo src,
            int count,
            string source,
            bool exact) =>
            new()
            {
                RadiusMeters = src.RadiusMeters,
                HoleCount = count,
                HoleCountExact = exact,
                PitchMeters = src.PitchMeters,
                AngleDegrees = src.AngleDegrees,
                SeedX = src.SeedX,
                SeedY = src.SeedY,
                SeedZ = src.SeedZ,
                NeighborX = src.NeighborX,
                NeighborY = src.NeighborY,
                NeighborZ = src.NeighborZ,
                AngleNeighborX = src.AngleNeighborX,
                AngleNeighborY = src.AngleNeighborY,
                AngleNeighborZ = src.AngleNeighborZ,
                CountSource = source
            };

        private static int FindNearest(
            IReadOnlyList<CylinderFaceSample> holes,
            int seedIdx,
            double minPitch,
            double maxPitch)
        {
            CylinderFaceSample seed = holes[seedIdx];
            int best = -1;
            double bestD2 = double.MaxValue;
            double min2 = minPitch * minPitch;
            double max2 = maxPitch * maxPitch;

            for (int i = 0; i < holes.Count; i++)
            {
                if (i == seedIdx)
                    continue;

                CylinderFaceSample s = holes[i];
                double d2 = Dist2(
                    s.OriginX - seed.OriginX,
                    s.OriginY - seed.OriginY,
                    s.OriginZ - seed.OriginZ);
                if (d2 < min2 || d2 > max2)
                    continue;

                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = i;
                }
            }

            return best;
        }

        private static int FindAngleNeighbor(
            IReadOnlyList<CylinderFaceSample> holes,
            int seedIdx,
            int neighborIdx,
            double vx,
            double vy,
            double vz,
            double pitch)
        {
            CylinderFaceSample seed = holes[seedIdx];
            int best = -1;
            double bestAngleErr = double.MaxValue;
            double pitchLo2 = pitch * 0.85 * pitch * 0.85;
            double pitchHi2 = pitch * 1.15 * pitch * 1.15;

            for (int i = 0; i < holes.Count; i++)
            {
                if (i == seedIdx || i == neighborIdx)
                    continue;

                CylinderFaceSample s = holes[i];
                double wx = s.OriginX - seed.OriginX;
                double wy = s.OriginY - seed.OriginY;
                double wz = s.OriginZ - seed.OriginZ;
                double d2 = Dist2(wx, wy, wz);
                if (d2 < pitchLo2 || d2 > pitchHi2)
                    continue;

                double ang = AngleDegreesBetween(vx, vy, vz, wx, wy, wz);
                double err = Math.Abs(ang - 60.0);
                if (err > AngleTolDeg)
                    continue;

                if (err < bestAngleErr)
                {
                    bestAngleErr = err;
                    best = i;
                }
            }

            return best;
        }

        private static double AngleDegreesBetween(
            double ax, double ay, double az,
            double bx, double by, double bz)
        {
            double la = Math.Sqrt(Dist2(ax, ay, az));
            double lb = Math.Sqrt(Dist2(bx, by, bz));
            if (la < 1e-12 || lb < 1e-12)
                return 0;

            double cos = (ax * bx + ay * by + az * bz) / (la * lb);
            cos = Math.Clamp(cos, -1.0, 1.0);
            return Math.Acos(cos) * (180.0 / Math.PI);
        }

        private static double Dist2(double dx, double dy, double dz) =>
            dx * dx + dy * dy + dz * dz;
    }
}
