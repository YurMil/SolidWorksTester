using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Trapezoid / tapered plate: separate top &amp; bottom widths, plus taper angle.
    /// </summary>
    public static partial class SmartDimOverall
    {
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

            if (topBand.Length > 0 && botBand.Length > 0)
            {
                var top = topBand[0];
                var bot = botBand[0];

                // Same top/bottom length but shorter than outline → fillets on a rectangle;
                // fall back to overall span instead of edge-length dims.
                if (Math.Abs(top.ModelLen - bot.ModelLen) < 0.005)
                {
                    double outlineW = Math.Abs(maxX - minX);
                    if (Math.Abs(top.ModelLen - outlineW / scale) > 0.005)
                        return false;
                }
            }

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
                var top = topBand.Length > 0 ? topBand[0] : bot;

                // Skip if essentially same as top (rectangle).
                if (!any || Math.Abs(bot.ModelLen - top.ModelLen) > 0.002)
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
    }
}
