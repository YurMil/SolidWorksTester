using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>
    /// Smart dimensions for flat sheet-metal plates with a rounded end profile.
    /// </summary>
    internal static class RoundedFlatPlateDimensions
    {
        private const double MinHoleDiameterMeters = 0.001;
        private const double MaxHoleDiameterMeters = 0.12;
        private const double MinProfileArcRadiusMeters = 0.02;
        private const double DimOffset = 0.012;

        public static void AddForPrimaryView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
                return;

            string viewName = view.GetName2();
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            var linear = edges.Where(h.IsLinear).ToArray();

            TryOverallHeight(h, view, viewName, linear, minY, maxY, log);
            TryOverallWidth(h, view, viewName, linear, edges, minX, maxX, maxY, log);
            TryOuterArcDiameter(h, view, viewName, log);
            AddHoleDimensions(h, drawing, view, edges, viewName, minX, minY, log);
        }

        /// <summary>Side views only — thickness when primary view used model import.</summary>
        public static void AddSideViewOnly(
            SmartDimHelper h,
            IView view,
            Action<string> log)
        {
            SmartDimThickness.Add(h, view);
        }

        public static void AddThicknessOnce(SmartDimHelper h, IDrawingDoc drawing, Action<string> log) =>
            RoundFlatPlateThickness.TryAddOnce(h, drawing, log);

        private static void TryOverallHeight(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minY,
            double maxY,
            Action<string> log)
        {
            Edge? topEdge = FindBoundaryEdge(h, linear, view, maxY, horizontal: true);
            Edge? bottomEdge = FindBoundaryEdge(h, linear, view, minY, horizontal: true);
            if (topEdge == null || bottomEdge == null || ReferenceEquals(topEdge, bottomEdge))
                return;

            double heightVal = Math.Round(Math.Abs(maxY - minY), 4);
            string key = $"RoundedOverall_H_{heightVal:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(heightVal))
                return;

            h.ClearSelection();
            h.SelectEdge(topEdge, view, false);
            h.SelectEdge(bottomEdge, view, true);

            var (minX, _, maxX, _) = h.ComputeEdgesBoundingBox(h.GetViewEdgesCached(view), view);
            var dim = h.CreateDimension(maxX + DimOffset, (minY + maxY) / 2.0);
            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Overall height {heightVal * 1000:F1} mm.");
            }

            h.ClearSelection();
        }

        private static void TryOverallWidth(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            Edge[] edges,
            double minX,
            double maxX,
            double maxY,
            Action<string> log)
        {
            Edge? leftEdge = FindBoundaryEdge(h, linear, view, minX, horizontal: false);
            if (leftEdge == null)
                return;

            double widthVal = Math.Round(Math.Abs(maxX - minX), 4);
            string key = $"RoundedOverall_W_{widthVal:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(widthVal))
                return;

            Edge? rightRef = FindRightWidthReference(h, view, linear, edges, maxX);
            if (rightRef == null)
                return;

            h.ClearSelection();
            h.SelectEdge(leftEdge, view, false);
            h.SelectEdge(rightRef, view, true);

            var dim = h.CreateDimension((minX + maxX) / 2.0, maxY + DimOffset);
            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Overall width {widthVal * 1000:F1} mm.");
            }

            h.ClearSelection();
        }

        private static Edge? FindRightWidthReference(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            Edge[] edges,
            double maxX)
        {
            Edge? outerArc = RoundedFlatPlateViewAnalyzer.GetOuterProfileArc(h, view);
            if (outerArc != null)
                return outerArc;

            const double tol = 0.003;
            Edge? bestVertical = null;
            double bestScore = double.MinValue;

            foreach (Edge edge in linear)
            {
                if (!h.IsVerticalInView(edge, view))
                    continue;

                var mid = h.GetEdgeMidpointOnSheet(edge, view);
                if (Math.Abs(mid[0] - maxX) > tol)
                    continue;

                double score = h.GetProjectedLength(edge, view);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestVertical = edge;
                }
            }

            if (bestVertical != null)
                return bestVertical;

            return edges
                .Where(h.IsLinear)
                .OrderByDescending(e =>
                {
                    var (s, ePt) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Max(s[0], ePt[0]);
                })
                .FirstOrDefault(e =>
                {
                    var (s, ePt) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Max(s[0], ePt[0]) >= maxX - tol;
                });
        }

        private static void TryOuterArcDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            Action<string> log)
        {
            Edge? outerArc = RoundedFlatPlateViewAnalyzer.GetOuterProfileArc(h, view);
            if (outerArc == null)
                return;

            double diameter = Math.Round(h.GetCircleRadius(outerArc) * 2.0, 4);
            if (diameter < MinProfileArcRadiusMeters * 2.0)
                return;

            string key = $"RoundedArc_OD_{diameter:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            double[] center = h.GetCircleCenterOnSheet(outerArc, view);
            DisplayDimension? dim = h.CreateDiameterDimension(
                outerArc,
                view,
                center[0] + DimOffset,
                center[1] + DimOffset);

            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Outer arc Ø{diameter * 1000:F1} mm.");
            }
        }

        private static void AddHoleDimensions(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] edges,
            string viewName,
            double minX,
            double minY,
            Action<string> log)
        {
            var holeCandidates = edges
                .Where(h.IsCircular)
                .Where(e => IsHoleLikeEdge(h, view, e))
                .Where(e =>
                {
                    double d = h.GetCircleRadius(e) * 2.0;
                    return d >= MinHoleDiameterMeters && d <= MaxHoleDiameterMeters;
                })
                .ToArray();

            if (holeCandidates.Length == 0)
                return;

            var linear = edges.Where(h.IsLinear).ToArray();
            Edge? leftBound = FindBoundaryEdge(h, linear, view, minX, horizontal: false);

            var groups = GroupByDiameter(holeCandidates, h);
            int groupIndex = 0;
            int centerMarks = 0;

            foreach (var kvp in groups.OrderBy(g => g.Key))
            {
                double diameter = kvp.Key;
                string holeKey = $"RoundedHole_{diameter:F4}_{viewName}";
                if (!h.DimensionedFeatures.Contains(holeKey))
                {
                    Edge representative = kvp.Value.FirstOrDefault(h.IsFullCircle) ??
                        kvp.Value.OrderByDescending(e => h.GetProjectedLength(e, view)).First();

                    double[] center = h.GetCircleCenterOnSheet(representative, view);
                    h.ClearSelection();

                    DisplayDimension? dim = h.CreateDiameterDimension(
                        representative,
                        view,
                        center[0] + DimOffset + groupIndex * 0.004,
                        center[1] + DimOffset);

                    if (dim != null)
                    {
                        if (kvp.Value.Count > 1)
                            dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, $"{kvp.Value.Count}x ");

                        h.DimensionedFeatures.Add(holeKey);
                        log($"  [{viewName}] Hole Ø{diameter * 1000:F1} mm × {kvp.Value.Count}.");
                        groupIndex++;
                    }
                }

                string posKey = $"RoundedHolePos_{diameter:F4}_{viewName}";
                if (!h.DimensionedFeatures.Contains(posKey) && leftBound != null)
                {
                    Edge nearest = kvp.Value
                        .OrderBy(e => h.GetCircleCenterOnSheet(e, view)[0])
                        .First();

                    h.ClearSelection();
                    h.SelectEdge(leftBound, view, false);
                    h.SelectEdge(nearest, view, true);

                    double[] c = h.GetCircleCenterOnSheet(nearest, view);
                    if (h.CreateDimension((minX + c[0]) / 2.0, c[1] - 0.008) != null)
                    {
                        h.DimensionedFeatures.Add(posKey);
                        log($"  [{viewName}] Hole position for Ø{diameter * 1000:F1} mm.");
                    }
                }

                if (centerMarks < 6)
                {
                    Edge markEdge = kvp.Value.FirstOrDefault(h.IsFullCircle) ?? kvp.Value[0];
                    if (h.TryInsertCenterMark(drawing, view, markEdge))
                        centerMarks++;
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

            return !(arcFraction > 0.42 && r >= MinProfileArcRadiusMeters);
        }

        private static Dictionary<double, List<Edge>> GroupByDiameter(Edge[] circular, SmartDimHelper h)
        {
            var groups = new Dictionary<double, List<Edge>>();
            foreach (Edge edge in circular)
            {
                double d = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (!groups.ContainsKey(d))
                    groups[d] = new List<Edge>();
                groups[d].Add(edge);
            }

            return groups;
        }

        private static Edge? FindBoundaryEdge(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            double targetCoord,
            bool horizontal)
        {
            const double tol = 0.002;
            Edge? best = null;
            double bestScore = double.MinValue;

            foreach (Edge edge in linear)
            {
                if (horizontal && !h.IsHorizontalInView(edge, view))
                    continue;
                if (!horizontal && !h.IsVerticalInView(edge, view))
                    continue;

                var mid = h.GetEdgeMidpointOnSheet(edge, view);
                double coord = horizontal ? mid[1] : mid[0];
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
    }
}
