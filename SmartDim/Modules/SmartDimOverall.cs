using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester
{
    /// <summary>
    /// Overall bounding dimensions (width + height) for flat-plate / sheet-metal views.
    /// Handles rectangles and trapezoids (top/bottom widths differ; slanted side).
    /// </summary>
    public static class SmartDimOverall
    {
        private const double DimOffset = 0.012;
        private const double OrientTol = 0.004;

        public static void Add(SmartDimHelper h, IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();
            log?.Invoke($"  [Overall] Adding bounding dimensions to: {viewName}");

            Edge[] modelEdges = h.GetViewEdges(view);
            Edge[] allEdges = CollectEdgesWithSilhouette(h, view, modelEdges);
            if (allEdges.Length == 0)
            {
                log?.Invoke($"  [Overall] No edges found in view {viewName}");
                return;
            }

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);
            double outlineH = Math.Abs(maxY - minY);
            double outlineW = Math.Abs(maxX - minX);
            if (outlineH < 1e-6 && outlineW < 1e-6)
                return;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            var linear = allEdges
                .Where(h.IsLinear)
                .Where(e => !SmartDimThickness.IsShortCornerBreakEdge(h, e, view))
                .ToArray();
            if (linear.Length < 2)
                linear = allEdges.Where(h.IsLinear).ToArray();

            bool heightOk = TryDimVerticalSpan(
                h, view, viewName, linear, allEdges, minX, minY, maxX, maxY, outlineH, log);

            // Trapezoid / tapered plates: dimension top and bottom horizontal edges separately.
            bool widthOk = TryDimTrapezoidWidths(h, view, viewName, linear, minX, minY, maxX, maxY, scale, log);
            if (!widthOk)
            {
                widthOk = TryDimHorizontalSpan(
                    h, view, viewName, linear, allEdges, minX, minY, maxX, maxY, outlineW, log);
            }

            if (!heightOk || !widthOk)
            {
                log?.Invoke($"  [Overall] Partial: height={(heightOk ? "ok" : "MISS")}, " +
                            $"width={(widthOk ? "ok" : "MISS")} on {viewName}.");
            }

            TryDimTaperAngle(h, view, viewName, linear, minX, minY, maxX, maxY, log);

            h.ClearSelection();
        }

        private static Edge[] CollectEdgesWithSilhouette(SmartDimHelper h, IView view, Edge[] modelEdges)
        {
            var list = modelEdges.ToList();
            try
            {
                list.AddRange(h.GetViewSilhouetteEdges(view));
            }
            catch
            {
                // SW2025 silhouette can be unstable — model edges only.
            }

            return list.Distinct().ToArray();
        }

        /// <summary>
        /// Places length dims on the longest top and bottom horizontal edges (e.g. 150 / 40).
        /// </summary>
        private static bool TryDimTrapezoidWidths(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX, double minY, double maxX, double maxY,
            double scale,
            Action<string>? log)
        {
            double outlineH = Math.Abs(maxY - minY);
            if (outlineH < 1e-6)
                return false;

            var horizontals = linear
                .Where(e => h.IsHorizontalInView(e, view, OrientTol))
                .Select(e =>
                {
                    double[] mid = h.GetEdgeMidpointOnSheet(e, view);
                    double len = h.GetProjectedLength(e, view);
                    return (Edge: e, Mid: mid, Len: len, ModelLen: len / scale);
                })
                .Where(t => t.ModelLen >= 0.005)
                .ToArray();

            if (horizontals.Length == 0)
                return false;

            var topBand = horizontals
                .Where(t => Math.Abs(t.Mid[1] - maxY) <= Math.Max(0.006, outlineH * 0.12))
                .OrderByDescending(t => t.Len)
                .ToArray();
            var botBand = horizontals
                .Where(t => Math.Abs(t.Mid[1] - minY) <= Math.Max(0.006, outlineH * 0.12))
                .OrderByDescending(t => t.Len)
                .ToArray();

            bool any = false;

            if (topBand.Length > 0)
            {
                var top = topBand[0];
                if (TryDimensionEdgeLength(
                        h, view, top.Edge, top.ModelLen,
                        (minX + maxX) / 2.0, maxY + DimOffset,
                        "Overall_W_Top", "Overall_W"))
                {
                    log?.Invoke($"  [Overall] Top width {top.ModelLen * 1000:F1} mm on {viewName}.");
                    any = true;
                }
            }

            if (botBand.Length > 0)
            {
                var bot = botBand[0];
                // Skip if essentially same as top (rectangle).
                if (!any || Math.Abs(bot.ModelLen - topBand[0].ModelLen) > 0.002)
                {
                    if (TryDimensionEdgeLength(
                            h, view, bot.Edge, bot.ModelLen,
                            (minX + maxX) / 2.0, minY - DimOffset,
                            "Overall_W_Bottom", null))
                    {
                        log?.Invoke($"  [Overall] Bottom width {bot.ModelLen * 1000:F1} mm on {viewName}.");
                        any = true;
                    }
                }
            }

            return any;
        }

        private static bool TryDimensionEdgeLength(
            SmartDimHelper h,
            IView view,
            Edge edge,
            double modelLen,
            double textX,
            double textY,
            string key,
            string? alsoMarkKey)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;
            if (h.HasDimensionWithValueInDrawing(modelLen))
            {
                h.DimensionedFeatures.Add(key);
                if (alsoMarkKey != null)
                    h.DimensionedFeatures.Add(alsoMarkKey);
                return true;
            }

            h.ClearSelection();
            if (!h.SelectEdge(edge, view, false))
                return false;

            var dim = h.CreateLinearDimension(textX, textY) ?? h.CreateDimension(textX, textY);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add(key);
            if (alsoMarkKey != null)
                h.DimensionedFeatures.Add(alsoMarkKey);
            return true;
        }

        /// <summary>Angular dim between bottom (or top) horizontal and the long slanted side.</summary>
        private static void TryDimTaperAngle(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX, double minY, double maxX, double maxY,
            Action<string>? log)
        {
            const string key = "Overall_TaperAngle";
            if (h.DimensionedFeatures.Contains(key))
                return;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? baseEdge = linear
                .Where(e => h.IsHorizontalInView(e, view, OrientTol))
                .OrderBy(e => Math.Abs(h.GetEdgeMidpointOnSheet(e, view)[1] - minY))
                .ThenByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (baseEdge == null)
                return;

            // Slanted = long linear edge that is neither H nor V.
            Edge? slant = linear
                .Where(e => !ReferenceEquals(e, baseEdge))
                .Where(e => !h.IsHorizontalInView(e, view, OrientTol))
                .Where(e => !h.IsVerticalInView(e, view, OrientTol))
                .Where(e => h.GetProjectedLength(e, view) / scale >= 0.05)
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (slant == null)
                return;

            h.ClearSelection();
            if (!h.SelectEdge(baseEdge, view, false) || !h.SelectEdge(slant, view, true))
                return;

            double[] mid = h.GetEdgeMidpointOnSheet(slant, view);
            var dim = h.CreateAngularDimension(mid[0] + DimOffset, mid[1] - DimOffset);
            if (dim == null)
                return;

            h.DimensionedFeatures.Add(key);
            log?.Invoke($"  [Overall] Taper angle on {viewName}.");
        }

        private static bool TryDimVerticalSpan(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            Edge[] allEdges,
            double minX, double minY, double maxX, double maxY,
            double outlineH,
            Action<string>? log)
        {
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double heightModel = outlineH / scale;
            if (h.DimensionedFeatures.Contains("Overall_H") ||
                h.HasDimensionWithValueInDrawing(heightModel))
            {
                h.DimensionedFeatures.Add("Overall_H");
                return true;
            }

            Edge? top = FindBoundaryEdge(h, linear, view, maxY, horizontal: true, preferLong: true);
            Edge? bot = FindBoundaryEdge(h, linear, view, minY, horizontal: true, preferLong: true);

            // Prefer left vertical length when top/bottom widths differ (trapezoid height along straight side).
            Edge? leftVert = linear
                .Where(e => h.IsVerticalInView(e, view, OrientTol))
                .OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[0])
                .ThenByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (leftVert != null)
            {
                double leftLen = h.GetProjectedLength(leftVert, view) / scale;
                if (leftLen > heightModel * 0.85)
                {
                    if (TryDimensionEdgeLength(
                            h, view, leftVert, leftLen,
                            minX - DimOffset, (minY + maxY) / 2.0,
                            "Overall_H", null))
                    {
                        log?.Invoke($"  [Overall] Height {leftLen * 1000:F1} mm on {viewName}.");
                        return true;
                    }
                }
            }

            if (top == null || bot == null || ReferenceEquals(top, bot))
            {
                top ??= FindExtremeArc(h, allEdges, view, verticalExtreme: true, maximize: true);
                bot ??= FindExtremeArc(h, allEdges, view, verticalExtreme: true, maximize: false);
            }

            if (top == null || bot == null || ReferenceEquals(top, bot))
                return TryOutlinePick(h, view, minX, minY, maxX, maxY, horizontalSpan: false, "Overall_H", heightModel, log);

            h.ClearSelection();
            if (!h.SelectEdge(top, view, false) || !h.SelectEdge(bot, view, true))
                return false;

            var dim = h.CreateLinearDimension(maxX + DimOffset, (minY + maxY) / 2.0)
                ?? h.CreateDimension(maxX + DimOffset, (minY + maxY) / 2.0);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add("Overall_H");
            log?.Invoke($"  [Overall] Height {heightModel * 1000:F1} mm on {viewName}.");
            return true;
        }

        private static bool TryDimHorizontalSpan(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            Edge[] allEdges,
            double minX, double minY, double maxX, double maxY,
            double outlineW,
            Action<string>? log)
        {
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double widthModel = outlineW / scale;
            if (h.DimensionedFeatures.Contains("Overall_W") ||
                h.HasDimensionWithValueInDrawing(widthModel))
            {
                h.DimensionedFeatures.Add("Overall_W");
                return true;
            }

            Edge? left = FindBoundaryEdge(h, linear, view, minX, horizontal: false, preferLong: true);
            Edge? right = FindBoundaryEdge(h, linear, view, maxX, horizontal: false, preferLong: true);

            if (left == null || right == null || ReferenceEquals(left, right))
            {
                left ??= FindExtremeArc(h, allEdges, view, verticalExtreme: false, maximize: false);
                right ??= FindExtremeArc(h, allEdges, view, verticalExtreme: false, maximize: true);
            }

            if (left == null || right == null || ReferenceEquals(left, right))
            {
                // Outline pick at TOP of view — better for tapered plates than mid-height.
                if (TryOutlinePick(h, view, minX, minY, maxX, maxY, horizontalSpan: true, "Overall_W", widthModel, log,
                        pickY: maxY - 0.002))
                    return true;
                return TryOutlinePick(h, view, minX, minY, maxX, maxY, horizontalSpan: true, "Overall_W", widthModel, log);
            }

            h.ClearSelection();
            if (!h.SelectEdge(left, view, false) || !h.SelectEdge(right, view, true))
                return false;

            var dim = h.CreateLinearDimension((minX + maxX) / 2.0, maxY + DimOffset)
                ?? h.CreateDimension((minX + maxX) / 2.0, maxY + DimOffset);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add("Overall_W");
            log?.Invoke($"  [Overall] Width {widthModel * 1000:F1} mm on {viewName}.");
            return true;
        }

        private static Edge? FindBoundaryEdge(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            double extremeCoord,
            bool horizontal,
            bool preferLong)
        {
            Edge? best = null;
            double bestScore = double.MinValue;
            double tol = Math.Max(0.003, Math.Abs(extremeCoord) * 1e-9 + 0.002);

            foreach (Edge edge in linear)
            {
                bool orientOk = horizontal
                    ? h.IsHorizontalInView(edge, view, OrientTol)
                    : h.IsVerticalInView(edge, view, OrientTol);
                if (!orientOk)
                    continue;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                double coord = horizontal ? mid[1] : mid[0];
                double proximity = -Math.Abs(coord - extremeCoord);
                if (Math.Abs(coord - extremeCoord) > tol + h.GetProjectedLength(edge, view) * 0.02)
                {
                    if (Math.Abs(coord - extremeCoord) > 0.008)
                        continue;
                }

                double len = h.GetProjectedLength(edge, view);
                double score = proximity + (preferLong ? len * 2.0 : len * 0.1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = edge;
                }
            }

            return best;
        }

        private static Edge? FindExtremeArc(
            SmartDimHelper h,
            Edge[] edges,
            IView view,
            bool verticalExtreme,
            bool maximize)
        {
            Edge? best = null;
            double bestCoord = maximize ? double.MinValue : double.MaxValue;

            foreach (Edge edge in edges.Where(h.IsCircular))
            {
                double[] c = h.GetCircleCenterOnSheet(edge, view);
                double r = h.GetCircleRadius(edge) * Math.Max(view.ScaleDecimal, 1e-9);
                double coord = verticalExtreme
                    ? (maximize ? c[1] + r : c[1] - r)
                    : (maximize ? c[0] + r : c[0] - r);

                if (maximize ? coord > bestCoord : coord < bestCoord)
                {
                    bestCoord = coord;
                    best = edge;
                }
            }

            return best;
        }

        private static bool TryOutlinePick(
            SmartDimHelper h,
            IView view,
            double minX, double minY, double maxX, double maxY,
            bool horizontalSpan,
            string key,
            double expectedModel,
            Action<string>? log,
            double? pickY = null)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            double midX = (minX + maxX) / 2.0;
            double midY = pickY ?? (minY + maxY) / 2.0;
            h.ClearSelection();
            h.ActivateView(view);

            bool a, b;
            if (horizontalSpan)
            {
                a = SelectEdgeAt(h, view, minX, midY);
                b = SelectEdgeAt(h, view, maxX, midY, append: true);
            }
            else
            {
                a = SelectEdgeAt(h, view, midX, minY);
                b = SelectEdgeAt(h, view, midX, maxY, append: true);
            }

            if (!a || !b)
                return false;

            double tx = horizontalSpan ? midX : maxX + DimOffset;
            double ty = horizontalSpan ? maxY + DimOffset : midY;
            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add(key);
            log?.Invoke($"  [Overall] {(horizontalSpan ? "Width" : "Height")} {expectedModel * 1000:F1} mm (outline pick).");
            return true;
        }

        private static bool SelectEdgeAt(SmartDimHelper h, IView view, double x, double y, bool append = false)
        {
            try
            {
                return h.Ext.SelectByID2(
                    string.Empty,
                    SmartDim.SmartDimConstants.EdgeSelectType,
                    x, y, 0.0,
                    append, 0, null, 0);
            }
            catch
            {
                return false;
            }
        }
    }
}
