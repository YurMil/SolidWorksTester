using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Overall bounding dimensions (width + height) for flat-plate / sheet-metal views.
    /// Handles rectangles and trapezoids (top/bottom widths differ; slanted side).
    /// <para>
    /// Split by algorithm:
    /// <list type="bullet">
    /// <item><see cref="TryDimVerticalSpan"/> — height between top/bottom (or left vertical on trapezoid)</item>
    /// <item><see cref="TryDimHorizontalSpan"/> — width between left/right extremes</item>
    /// <item><see cref="TryDimTrapezoidWidths"/> / <see cref="TryDimTaperAngle"/> — tapered plates</item>
    /// <item>Helpers — boundary/arc pick, outline pick, single-edge length dims</item>
    /// </list>
    /// </para>
    /// </summary>
    public static partial class SmartDimOverall
    {
        private const double DimOffset = 0.012;
        private const double OrientTol = 0.004;

        /// <summary>Entry: collect edges → height → trapezoid/overall width → taper angle.</summary>
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
    }
}
