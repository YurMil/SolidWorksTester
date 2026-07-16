using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>Height (Overall_H): top/bottom edges, trapezoid left vertical, or outline pick.</summary>
    public static partial class SmartDimOverall
    {
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
            if (IsTrapezoidTopBottom(h, view, top, bot, scale) &&
                TryDimTrapezoidHeightAlongLeft(h, view, viewName, linear, minX, minY, maxY, heightModel, log))
            {
                return true;
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

            ApplyArcEndMax(dim);

            h.DimensionedFeatures.Add("Overall_H");
            log?.Invoke($"  [Overall] Height {heightModel * 1000:F1} mm on {viewName}.");
            return true;
        }

        private static bool IsTrapezoidTopBottom(
            SmartDimHelper h,
            IView view,
            Edge? top,
            Edge? bot,
            double scale)
        {
            if (top == null || bot == null)
                return false;

            double topLen = h.GetProjectedLength(top, view) / scale;
            double botLen = h.GetProjectedLength(bot, view) / scale;
            return Math.Abs(topLen - botLen) > 0.005;
        }

        private static bool TryDimTrapezoidHeightAlongLeft(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX,
            double minY,
            double maxY,
            double heightModel,
            Action<string>? log)
        {
            Edge? leftVert = linear
                .Where(e => h.IsVerticalInView(e, view, OrientTol))
                .OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[0])
                .ThenByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (leftVert == null)
                return false;

            double leftLen = h.GetProjectedLength(leftVert, view) / Math.Max(view.ScaleDecimal, 1e-9);
            if (leftLen <= heightModel * 0.85)
                return false;

            if (!TryDimensionEdgeLength(
                    h, view, leftVert, leftLen,
                    minX - DimOffset, (minY + maxY) / 2.0,
                    "Overall_H", null))
            {
                return false;
            }

            log?.Invoke($"  [Overall] Height {leftLen * 1000:F1} mm on {viewName}.");
            return true;
        }
    }
}
