using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>Groups circular edges into disc profile, bore, and bolt-circle rings.</summary>
    internal static class FlangeGasketPatternGeometry
    {
        private const double MinHoleDiameterMeters = 0.002;
        private const double MinBoltCircleHoleCount = 3;
        private const double CenterConcentricFactor = 0.015;
        private const double MinCenterConcentricMeters = 0.002;

        public static FlangeDiscGeometry? Analyze(SmartDimHelper h, IView view, Edge[] edges)
        {
            Edge? outer = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .OrderByDescending(h.GetCircleRadius)
                .FirstOrDefault();

            if (outer == null)
                return null;

            double[] center = h.GetCircleCenterOnSheet(outer, view);
            double outerRadius = h.GetCircleRadius(outer);
            double outerDiameter = outerRadius * 2.0;
            double maxHoleDiameter = outerDiameter * 0.92;
            double polarTolerance = Math.Max(0.003, outerRadius * 0.004);
            double maxCenterOffset = Math.Max(MinCenterConcentricMeters, outerRadius * CenterConcentricFactor);

            var fullCircles = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .Where(e => h.GetCircleRadius(e) < outerRadius * 0.98)
                .ToArray();

            Edge? innerBore = FindInnerBore(h, view, fullCircles, center, maxCenterOffset);
            double? innerDiameter = innerBore != null ? h.GetCircleRadius(innerBore) * 2.0 : null;

            var holeCandidates = fullCircles
                .Where(e => !ReferenceEquals(e, innerBore))
                .Where(e =>
                {
                    double d = h.GetCircleRadius(e) * 2.0;
                    return d >= MinHoleDiameterMeters && d <= maxHoleDiameter;
                })
                .ToArray();

            BoltCircleRing? primaryRing = FindPrimaryBoltCircle(
                h, view, holeCandidates, center, outerRadius, polarTolerance, maxCenterOffset);

            return new FlangeDiscGeometry
            {
                OuterCircle = outer,
                InnerBoreCircle = innerBore,
                PrimaryBoltCircle = primaryRing,
                DiscCenterOnSheet = center,
                OuterDiameterMeters = outerDiameter,
                InnerDiameterMeters = innerDiameter
            };
        }

        public static (Edge first, Edge second)? FindOppositeHoles(BoltCircleRing ring, SmartDimHelper h, IView view)
        {
            if (ring.Holes.Count < 2)
                return null;

            var polar = ring.Holes
                .Select(e =>
                {
                    double[] c = h.GetCircleCenterOnSheet(e, view);
                    double angle = Math.Atan2(c[1] - ring.DiscCenterOnSheet[1], c[0] - ring.DiscCenterOnSheet[0]);
                    return (edge: e, angle);
                })
                .OrderBy(p => p.angle)
                .ToArray();

            Edge? bestA = null;
            Edge? bestB = null;
            double bestDeltaFromPi = double.MaxValue;

            for (int i = 0; i < polar.Length; i++)
            {
                for (int j = i + 1; j < polar.Length; j++)
                {
                    double diff = Math.Abs(NormalizeAngle(polar[j].angle - polar[i].angle));
                    double deltaFromPi = Math.Abs(diff - Math.PI);
                    if (deltaFromPi < bestDeltaFromPi)
                    {
                        bestDeltaFromPi = deltaFromPi;
                        bestA = polar[i].edge;
                        bestB = polar[j].edge;
                    }
                }
            }

            return bestA != null && bestB != null ? (bestA, bestB) : null;
        }

        public static (Edge first, Edge second, double angleDegrees)? FindAdjacentHolePair(
            BoltCircleRing ring,
            SmartDimHelper h,
            IView view)
        {
            if (ring.Holes.Count < 2)
                return null;

            var polar = ring.Holes
                .Select(e =>
                {
                    double[] c = h.GetCircleCenterOnSheet(e, view);
                    double angle = Math.Atan2(c[1] - ring.DiscCenterOnSheet[1], c[0] - ring.DiscCenterOnSheet[0]);
                    return (edge: e, angle, sheetY: c[1]);
                })
                .OrderBy(p => p.angle)
                .ToArray();

            // Prefer the adjacent pair with the highest midpoint (top of flange) for readable placement.
            Edge? bestA = null;
            Edge? bestB = null;
            double bestAngle = 0;
            double bestMidY = double.MinValue;

            for (int i = 0; i < polar.Length; i++)
            {
                int next = (i + 1) % polar.Length;
                double diff = NormalizeAngle(polar[next].angle - polar[i].angle);
                if (diff <= 0.01)
                    continue;

                double midY = (polar[i].sheetY + polar[next].sheetY) / 2.0;
                // Prefer near-equal spacing pairs near the top of the view.
                bool better =
                    bestA == null ||
                    midY > bestMidY + 1e-6 ||
                    (Math.Abs(midY - bestMidY) < 1e-6 && diff < bestAngle);

                if (!better)
                    continue;

                bestAngle = diff;
                bestA = polar[i].edge;
                bestB = polar[next].edge;
                bestMidY = midY;
            }

            if (bestA == null || bestB == null)
                return null;

            return (bestA, bestB, bestAngle * 180.0 / Math.PI);
        }

        private static Edge? FindInnerBore(
            SmartDimHelper h,
            IView view,
            Edge[] innerCircles,
            double[] center,
            double maxCenterOffset)
        {
            Edge? best = null;
            double bestRadius = 0;

            foreach (Edge edge in innerCircles)
            {
                double[] c = h.GetCircleCenterOnSheet(edge, view);
                double dist = Distance2D(c, center);
                if (dist > maxCenterOffset)
                    continue;

                double radius = h.GetCircleRadius(edge);
                if (radius > bestRadius)
                {
                    bestRadius = radius;
                    best = edge;
                }
            }

            return best;
        }

        private static BoltCircleRing? FindPrimaryBoltCircle(
            SmartDimHelper h,
            IView view,
            Edge[] holeCandidates,
            double[] center,
            double outerRadius,
            double polarTolerance,
            double maxCenterOffset)
        {
            var offCenter = holeCandidates
                .Select(e =>
                {
                    double[] c = h.GetCircleCenterOnSheet(e, view);
                    double polarSheet = Distance2D(c, center);
                    // Convert sheet polar length to model meters (view scale).
                    double polar = polarSheet / Math.Max(view.ScaleDecimal, 1e-9);
                    return (edge: e, polar, diameter: Math.Round(h.GetCircleRadius(e) * 2.0, 4));
                })
                .Where(x => x.polar > maxCenterOffset && x.polar < outerRadius * 0.98)
                .ToArray();

            if (offCenter.Length < MinBoltCircleHoleCount)
                return null;

            BoltCircleRing? bestRing = null;
            int bestCount = 0;

            foreach (var diameterGroup in offCenter.GroupBy(x => x.diameter).OrderByDescending(g => g.Count()))
            {
                if (diameterGroup.Count() < MinBoltCircleHoleCount)
                    continue;

                foreach (var polarGroup in diameterGroup
                    .GroupBy(x => Math.Round(x.polar / polarTolerance) * polarTolerance)
                    .OrderByDescending(g => g.Count()))
                {
                    if (polarGroup.Count() < MinBoltCircleHoleCount)
                        continue;

                    if (polarGroup.Count() <= bestCount)
                        break;

                    bestCount = polarGroup.Count();
                    double avgPolar = polarGroup.Average(x => x.polar);
                    bestRing = new BoltCircleRing
                    {
                        PolarRadiusMeters = avgPolar,
                        HoleDiameterMeters = diameterGroup.Key,
                        Holes = polarGroup.Select(x => x.edge).ToList(),
                        DiscCenterOnSheet = center
                    };
                }

                if (bestRing != null)
                    break;
            }

            return bestRing;
        }

        private static double Distance2D(double[] a, double[] b) =>
            Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));

        private static double NormalizeAngle(double radians)
        {
            while (radians < 0)
                radians += 2.0 * Math.PI;
            while (radians > 2.0 * Math.PI)
                radians -= 2.0 * Math.PI;
            return radians;
        }
    }

    internal sealed class BoltCircleRing
    {
        public required double PolarRadiusMeters { get; init; }
        public required double HoleDiameterMeters { get; init; }
        public required IReadOnlyList<Edge> Holes { get; init; }
        public required double[] DiscCenterOnSheet { get; init; }
        public double BoltCircleDiameterMeters => PolarRadiusMeters * 2.0;
    }

    internal sealed class FlangeDiscGeometry
    {
        public required Edge OuterCircle { get; init; }
        public Edge? InnerBoreCircle { get; init; }
        public BoltCircleRing? PrimaryBoltCircle { get; init; }
        public required double[] DiscCenterOnSheet { get; init; }
        public double OuterDiameterMeters { get; init; }
        public double? InnerDiameterMeters { get; init; }
    }
}
