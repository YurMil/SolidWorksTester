using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Imported
{
    /// <summary>
    /// Detects slot-like parallel edge pairs on imported profile views and dimensions the opening width.
    /// </summary>
    internal static class ImportedDimSlots
    {
        private const double MinSlotWidthMeters = 0.001;
        private const double MaxSlotWidthMeters = 0.15;
        private const double MinEdgeLengthRatio = 0.08;

        public static void Add(SmartDimHelper h, IView view, Action<string> log, Edge[]? edges = null)
        {
            string viewName = view.GetName2();
            Edge[] allEdges = edges ?? h.GetViewEdgesCached(view);
            if (allEdges.Length < 4)
                return;

            var linear = allEdges.Where(h.IsLinear).ToArray();
            if (linear.Length < 4)
                return;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;
            double minEdgeLen = Math.Min(bboxWidth, bboxHeight) * MinEdgeLengthRatio;

            bool placed = TrySlotBetweenParallelEdges(
                h, view, viewName, linear, vertical: true, minX, maxX, minEdgeLen, log);
            if (!placed)
            {
                TrySlotBetweenParallelEdges(
                    h, view, viewName, linear, vertical: false, minY, maxY, minEdgeLen, log);
            }
        }

        private static bool TrySlotBetweenParallelEdges(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            bool vertical,
            double boundaryMin,
            double boundaryMax,
            double minEdgeLen,
            Action<string> log)
        {
            var candidates = linear
                .Where(e => vertical
                    ? h.IsVerticalInView(e, view, 0.004)
                    : h.IsHorizontalInView(e, view, 0.004))
                .Where(e => h.GetProjectedLength(e, view) >= minEdgeLen)
                .ToArray();

            if (candidates.Length < 2)
                return false;

            Edge? bestA = null;
            Edge? bestB = null;
            double bestGap = double.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                for (int j = i + 1; j < candidates.Length; j++)
                {
                    Edge a = candidates[i];
                    Edge b = candidates[j];
                    double coordA = GetCoord(h, a, view, vertical);
                    double coordB = GetCoord(h, b, view, vertical);
                    double gap = Math.Abs(coordB - coordA);

                    if (gap < MinSlotWidthMeters || gap > MaxSlotWidthMeters)
                        continue;

                    if (IsOnOuterBoundary(coordA, boundaryMin, boundaryMax) &&
                        IsOnOuterBoundary(coordB, boundaryMin, boundaryMax))
                        continue;

                    if (gap < bestGap)
                    {
                        bestGap = gap;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (bestA == null || bestB == null)
                return false;

            string key = $"Slot_{Math.Round(bestGap, 4)}_{viewName}";
            if (h.DimensionedFeatures.Contains(key))
                return true;

            h.ClearSelection();
            h.SelectEdge(bestA, view, false);
            h.SelectEdge(bestB, view, true);

            double[] midA = h.GetEdgeMidpointOnSheet(bestA, view);
            double[] midB = h.GetEdgeMidpointOnSheet(bestB, view);
            double dimX = vertical ? (midA[0] + midB[0]) / 2.0 + 0.012 : (midA[0] + midB[0]) / 2.0;
            double dimY = vertical ? (midA[1] + midB[1]) / 2.0 : (midA[1] + midB[1]) / 2.0 + 0.012;

            if (h.CreateDimension(dimX, dimY) != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Slot/opening width {bestGap * 1000:F1} mm dimensioned.");
                return true;
            }

            return false;
        }

        private static double GetCoord(SmartDimHelper h, Edge edge, IView view, bool vertical) =>
            vertical
                ? h.GetEdgeMidpointOnSheet(edge, view)[0]
                : h.GetEdgeMidpointOnSheet(edge, view)[1];

        private static bool IsOnOuterBoundary(double coord, double min, double max)
        {
            const double tol = 0.002;
            return Math.Abs(coord - min) < tol || Math.Abs(coord - max) < tol;
        }
    }
}
