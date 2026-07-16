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
    }
}
