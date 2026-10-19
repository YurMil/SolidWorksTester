using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>Width (Overall_W): left/right extremes, extreme arcs, or outline pick.</summary>
    public static partial class SmartDimOverall
    {
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

            if (TryDimHorizontalExtremes(
                    h, view, viewName, allEdges, minX, maxX, maxY, widthModel, log))
                return true;

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

            ApplyArcEndMax(dim);

            h.DimensionedFeatures.Add("Overall_W");
            log?.Invoke($"  [Overall] Width {widthModel * 1000:F1} mm on {viewName}.");
            return true;
        }

        /// <summary>
        /// Overall width between the real left and right extremes.
        /// A chamfer tip is a vertex; a corner radius is an arc measured to its outer tangent.
        /// </summary>
        private static bool TryDimHorizontalExtremes(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] allEdges,
            double minX,
            double maxX,
            double maxY,
            double widthModel,
            Action<string>? log)
        {
            WidthExtreme? left = FindWidthExtreme(h, allEdges, view, maximize: false);
            WidthExtreme? right = FindWidthExtreme(h, allEdges, view, maximize: true);
            if (left == null || right == null)
                return false;

            if (Math.Abs(right.SheetX - left.SheetX) < 1e-4)
                return false;

            if (left.Vertex == null && right.Vertex == null && ReferenceEquals(left.Edge, right.Edge))
                return false;

            h.ClearSelection();
            if (!SelectWidthExtreme(h, view, left, maximize: false, append: false) ||
                !SelectWidthExtreme(h, view, right, maximize: true, append: true))
                return false;

            // Outside a partial top-edge dim (Overall_W_Top sits at one offset).
            double stack = h.DimensionedFeatures.Contains("Overall_W_Top") ? 2.4 : 1.0;
            double textX = (minX + maxX) / 2.0;
            double textY = maxY + DimOffset * stack;
            DisplayDimension? dim = h.CreateHorizontalDimension(textX, textY);
            if (dim == null)
                return false;

            ApplyArcEndMax(dim);
            h.DimensionedFeatures.Add("Overall_W");
            log?.Invoke($"  [Overall] Width {widthModel * 1000:F1} mm (outline extremes) on {viewName}.");
            return true;
        }

        private sealed class WidthExtreme
        {
            public required Edge Edge { get; init; }
            public Vertex? Vertex { get; init; }
            public double SheetX { get; init; }
        }

        private static WidthExtreme? FindWidthExtreme(
            SmartDimHelper h,
            Edge[] edges,
            IView view,
            bool maximize)
        {
            WidthExtreme? best = null;

            foreach (Edge edge in edges)
            {
                if (h.IsCircular(edge))
                {
                    // Full circles are holes. Corner radii are arcs and can be the outline extreme.
                    if (h.IsFullCircle(edge))
                        continue;

                    double[] center = h.GetCircleCenterOnSheet(edge, view);
                    double radius = h.GetCircleRadius(edge) * Math.Max(view.ScaleDecimal, 1e-9);
                    double x = maximize ? center[0] + radius : center[0] - radius;
                    if (IsMoreExtreme(x, best, maximize))
                    {
                        best = new WidthExtreme { Edge = edge, Vertex = null, SheetX = x };
                    }

                    continue;
                }

                if (!h.IsLinear(edge))
                    continue;

                ConsiderVertex(h, view, edge, edge.GetStartVertex() as Vertex, maximize, ref best);
                ConsiderVertex(h, view, edge, edge.GetEndVertex() as Vertex, maximize, ref best);
            }

            return best;
        }

        private static void ConsiderVertex(
            SmartDimHelper h,
            IView view,
            Edge edge,
            Vertex? vertex,
            bool maximize,
            ref WidthExtreme? best)
        {
            if (vertex?.GetPoint() is not double[] point)
                return;

            double x = h.TransformToSheet(point, view)[0];
            if (!IsMoreExtreme(x, best, maximize))
                return;

            best = new WidthExtreme { Edge = edge, Vertex = vertex, SheetX = x };
        }

        private static bool IsMoreExtreme(double x, WidthExtreme? best, bool maximize)
        {
            if (best == null)
                return true;

            return maximize ? x > best.SheetX + 1e-6 : x < best.SheetX - 1e-6;
        }

        private static bool SelectWidthExtreme(
            SmartDimHelper h,
            IView view,
            WidthExtreme extreme,
            bool maximize,
            bool append)
        {
            if (extreme.Vertex != null)
                return h.SelectVertex(extreme.Vertex, view, append);

            double[] center = h.GetCircleCenterOnSheet(extreme.Edge, view);
            double radius = h.GetCircleRadius(extreme.Edge) * Math.Max(view.ScaleDecimal, 1e-9);
            double x = maximize ? center[0] + radius : center[0] - radius;
            return h.SelectEdgeAtPoint(extreme.Edge, view, new[] { x, center[1], 0.0 }, append);
        }
    }
}
