using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Imported
{
    /// <summary>
    /// Geometry-based dimensions for imported dumb solids: arcs as diameters,
    /// center marks, thickness, holes — one cached edge pass per view.
    /// </summary>
    internal static class ImportedDimGeometry
    {
        private const double MinHoleDiameterMeters = 0.001;
        private const double MaxHoleDiameterMeters = 0.08;
        private const double MinBendRadiusMeters = 0.004;
        private const double MaxBendRadiusMeters = 0.12;
        private const double MinThicknessMeters = 0.0003;
        private const double MaxThicknessMeters = 0.025;
        private const double DimOffset = 0.012;
        private const int MaxCenterMarksPerView = 12;

        public static void AddForImportedView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            bool isPrimaryView,
            Action<string> log)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
                return;

            string viewName = view.GetName2();

            if (isPrimaryView)
            {
                SmartDimOverall.Add(h, view);
                AddThicknessDimensions(h, view, edges, viewName, log);
                AddProfileArcDiameters(h, view, edges, viewName, log);
            }

            AddCircularFeatureDimensions(h, drawing, view, edges, viewName, log);
            AddHolePositions(h, view, edges, viewName, isPrimaryView, log);
            ImportedDimSlots.Add(h, view, log, edges);
        }

        private static void AddCircularFeatureDimensions(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] edges,
            string viewName,
            Action<string> log)
        {
            var circular = edges.Where(h.IsCircular).ToArray();
            if (circular.Length == 0)
                return;

            var holeCandidates = circular.Where(e => IsHoleLikeEdge(h, view, e)).ToArray();
            var holeGroups = GroupByDiameter(holeCandidates, h, MinHoleDiameterMeters, MaxHoleDiameterMeters);
            int centerMarkCount = 0;
            int groupIndex = 0;

            foreach (var kvp in holeGroups.OrderBy(g => g.Key))
            {
                double diameter = kvp.Key;
                var group = kvp.Value;
                string key = $"ImpHole_{diameter:F4}_{viewName}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                Edge representative = PickRepresentativeEdge(h, view, group);
                double[] center = h.GetCircleCenterOnSheet(representative, view);

                h.ClearSelection();
                DisplayDimension? dim = h.CreateDiameterDimension(
                    representative,
                    view,
                    center[0] + DimOffset + groupIndex * 0.004,
                    center[1] + DimOffset);

                if (dim != null)
                {
                    if (group.Count > 1)
                        dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, $"{group.Count}x ");

                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Hole Ø{diameter * 1000:F1} mm × {group.Count}.");
                    groupIndex++;
                }

                if (centerMarkCount < MaxCenterMarksPerView &&
                    h.TryInsertCenterMark(drawing, view, representative))
                {
                    centerMarkCount++;
                }
            }
        }

        private static void AddProfileArcDiameters(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            string viewName,
            Action<string> log)
        {
            var bendArcs = edges
                .Where(e => h.IsCircular(e) && !h.IsFullCircle(e))
                .Where(e =>
                {
                    double r = h.GetCircleRadius(e);
                    return r >= MinBendRadiusMeters && r <= MaxBendRadiusMeters &&
                           h.GetProjectedLength(e, view) >= r * view.ScaleDecimal * 0.35;
                })
                .OrderByDescending(h.GetCircleRadius)
                .Take(3)
                .ToArray();

            int arcIndex = 0;
            foreach (Edge arc in bendArcs)
            {
                double diameter = Math.Round(h.GetCircleRadius(arc) * 2.0, 4);
                string key = $"ImpArc_{diameter:F4}_{viewName}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                double[] center = h.GetCircleCenterOnSheet(arc, view);
                DisplayDimension? dim = h.CreateDiameterDimension(
                    arc,
                    view,
                    center[0] - DimOffset - arcIndex * 0.005,
                    center[1] + DimOffset + arcIndex * 0.008);

                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Bend/profile arc Ø{diameter * 1000:F1} mm.");
                    arcIndex++;
                }
            }
        }

        private static void AddThicknessDimensions(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            string viewName,
            Action<string> log)
        {
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length < 2)
                return;

            int placed = 0;
            if (TryPlaceThicknessPair(h, view, linear, horizontal: true, viewName, log))
                placed++;
            if (placed < 2 && TryPlaceThicknessPair(h, view, linear, horizontal: false, viewName, log))
                placed++;
        }

        private static bool TryPlaceThicknessPair(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool horizontal,
            string viewName,
            Action<string> log)
        {
            var oriented = linear
                .Where(e => horizontal ? h.IsHorizontalInView(e, view) : h.IsVerticalInView(e, view))
                .Where(e => h.GetProjectedLength(e, view) >= 0.003)
                .ToArray();

            if (oriented.Length < 2)
                return false;

            Edge? bestA = null;
            Edge? bestB = null;
            double bestDist = double.MaxValue;

            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double dist = ParallelDistance(h, view, oriented[i], oriented[j], horizontal);
                    if (dist < MinThicknessMeters || dist > MaxThicknessMeters)
                        continue;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestA = oriented[i];
                        bestB = oriented[j];
                    }
                }
            }

            if (bestA == null || bestB == null)
                return false;

            string key = $"ImpThk_{Math.Round(bestDist, 4):F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key))
                return false;

            h.ClearSelection();
            h.SelectEdge(bestA, view, false);
            h.SelectEdge(bestB, view, true);

            var midA = h.GetEdgeMidpointOnSheet(bestA, view);
            var midB = h.GetEdgeMidpointOnSheet(bestB, view);
            double dimX = horizontal ? (midA[0] + midB[0]) / 2.0 + 0.008 : midA[0] + 0.008;
            double dimY = horizontal ? midA[1] : (midA[1] + midB[1]) / 2.0;

            if (h.CreateDimension(dimX, dimY) == null)
            {
                h.ClearSelection();
                return false;
            }

            h.DimensionedFeatures.Add(key);
            log($"  [{viewName}] Thickness {bestDist * 1000:F1} mm.");
            h.ClearSelection();
            return true;
        }

        private static void AddHolePositions(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            string viewName,
            bool isPrimaryView,
            Action<string> log)
        {
            if (!isPrimaryView)
                return;

            var holes = edges
                .Where(h.IsCircular)
                .Where(e => IsHoleLikeEdge(h, view, e))
                .Where(e =>
                {
                    double d = h.GetCircleRadius(e) * 2.0;
                    return d >= MinHoleDiameterMeters && d <= MaxHoleDiameterMeters;
                })
                .ToArray();

            if (holes.Length == 0)
                return;

            var linear = edges.Where(h.IsLinear).ToArray();
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);

            Edge? leftBound = FindBoundaryEdge(h, linear, view, minX, vertical: false);
            Edge? bottomBound = FindBoundaryEdge(h, linear, view, minY, vertical: true);

            var groups = GroupByDiameter(holes, h, MinHoleDiameterMeters, MaxHoleDiameterMeters);
            foreach (var kvp in groups)
            {
                double diameter = kvp.Key;
                string key = $"ImpHolePos_{diameter:F4}_{viewName}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                Edge nearest = kvp.Value
                    .OrderBy(e => h.GetCircleCenterOnSheet(e, view)[0])
                    .First();

                if (leftBound != null)
                {
                    h.ClearSelection();
                    h.SelectEdge(leftBound, view, false);
                    h.SelectEdge(nearest, view, true);

                    double[] c = h.GetCircleCenterOnSheet(nearest, view);
                    if (h.CreateDimension((minX + c[0]) / 2.0, c[1] - 0.008) != null)
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  [{viewName}] Hole position (X) for Ø{diameter * 1000:F1} mm.");
                        h.ClearSelection();
                        continue;
                    }
                }

                if (bottomBound != null)
                {
                    h.ClearSelection();
                    h.SelectEdge(bottomBound, view, false);
                    h.SelectEdge(nearest, view, true);

                    double[] c = h.GetCircleCenterOnSheet(nearest, view);
                    if (h.CreateDimension(c[0] - 0.008, (minY + c[1]) / 2.0) != null)
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  [{viewName}] Hole position (Y) for Ø{diameter * 1000:F1} mm.");
                    }
                }

                h.ClearSelection();
            }
        }

        private static bool IsHoleLikeEdge(SmartDimHelper h, IView view, Edge edge)
        {
            if (h.IsFullCircle(edge))
                return true;

            double r = h.GetCircleRadius(edge);
            double arcLen = h.GetProjectedLength(edge, view);
            double scaledR = r * view.ScaleDecimal;
            if (scaledR < 0.0005)
                return false;

            double fullCircumference = 2.0 * Math.PI * scaledR;
            double arcFraction = arcLen / Math.Max(fullCircumference, 0.0001);

            // Large-span arcs on profile views are bends, not holes.
            if (arcFraction > 0.42 && r >= MinBendRadiusMeters)
                return false;

            return true;
        }

        private static Dictionary<double, List<Edge>> GroupByDiameter(
            Edge[] circular,
            SmartDimHelper h,
            double minDiameter,
            double maxDiameter)
        {
            var groups = new Dictionary<double, List<Edge>>();
            foreach (Edge edge in circular)
            {
                double d = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (d < minDiameter || d > maxDiameter)
                    continue;

                if (!groups.ContainsKey(d))
                    groups[d] = new List<Edge>();
                groups[d].Add(edge);
            }

            return groups;
        }

        private static Edge PickRepresentativeEdge(SmartDimHelper h, IView view, List<Edge> group)
        {
            Edge? full = group.FirstOrDefault(h.IsFullCircle);
            if (full != null)
                return full;

            return group.OrderByDescending(e => h.GetProjectedLength(e, view)).First();
        }

        private static Edge? FindBoundaryEdge(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            double targetCoord,
            bool vertical)
        {
            const double tol = 0.002;
            Edge? best = null;
            double bestScore = double.MinValue;

            foreach (Edge edge in linear)
            {
                if (vertical && !h.IsVerticalInView(edge, view))
                    continue;
                if (!vertical && !h.IsHorizontalInView(edge, view))
                    continue;

                var mid = h.GetEdgeMidpointOnSheet(edge, view);
                double coord = vertical ? mid[0] : mid[1];
                double dist = Math.Abs(coord - targetCoord);
                if (dist > tol)
                    continue;

                double score = h.GetProjectedLength(edge, view) - dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = edge;
                }
            }

            return best;
        }

        private static double ParallelDistance(
            SmartDimHelper h,
            IView view,
            Edge a,
            Edge b,
            bool horizontal)
        {
            var midA = h.GetEdgeMidpointOnSheet(a, view);
            var midB = h.GetEdgeMidpointOnSheet(b, view);
            return horizontal
                ? Math.Abs(midA[1] - midB[1])
                : Math.Abs(midA[0] - midB[0]);
        }
    }
}
