using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.RoundFlatPlate;

namespace SolidWorksTester
{
    /// <summary>
    /// Module B: Metal thickness dimension.
    /// Applied to standard views only (NOT flat pattern or circular face views).
    /// Creates 1 dimension showing sheet metal thickness.
    /// </summary>
    public static class SmartDimThickness
    {
        private const double MinThicknessMeters = 0.0001;  // 0.1 mm
        private const double MaxThicknessMeters = 0.025;   // 25 mm

        public static void Add(SmartDimHelper h, IView view)
        {
            string viewName = view.GetName2();
            Console.WriteLine($"  [Thickness] Looking for thickness edge in: {viewName}");

            if (RoundFlatPlateViewAnalyzer.IsCircularFaceView(h, view))
            {
                Console.WriteLine($"  [Thickness] Skipping circular face view");
                return;
            }

            if (h.DimensionedFeatures.Contains("Thickness"))
            {
                Console.WriteLine($"  [Thickness] Thickness already dimensioned, skipping");
                return;
            }

            if (TryAddBetweenParallelEdges(h, view))
                return;

            TryAddFromShortestEdge(h, view);
        }

        /// <summary>
        /// Thickness on side views: distance between two parallel face edges (more reliable than single edge length).
        /// </summary>
        private static bool TryAddBetweenParallelEdges(SmartDimHelper h, IView view)
        {
            Edge[] linearEdges = h.GetViewEdges(view).Where(h.IsLinear).ToArray();
            if (linearEdges.Length < 2)
                return false;

            if (TryParallelPair(h, view, linearEdges, horizontal: true))
                return true;

            return TryParallelPair(h, view, linearEdges, horizontal: false);
        }

        private static bool TryParallelPair(
            SmartDimHelper h,
            IView view,
            Edge[] linearEdges,
            bool horizontal)
        {
            var oriented = linearEdges
                .Where(e => horizontal ? h.IsHorizontalInView(e, view) : h.IsVerticalInView(e, view))
                .ToArray();

            if (oriented.Length < 2)
                return false;

            Edge? edgeA = null;
            Edge? edgeB = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double distance = ParallelEdgeDistance(h, view, oriented[i], oriented[j], horizontal);
                    if (distance < MinThicknessMeters || distance > MaxThicknessMeters)
                        continue;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        edgeA = oriented[i];
                        edgeB = oriented[j];
                    }
                }
            }

            if (edgeA == null || edgeB == null)
                return false;

            h.ClearSelection();
            h.SelectEdge(edgeA, view, false);
            h.SelectEdge(edgeB, view, true);

            var (sA, _) = h.GetEdgeEndpointsOnSheet(edgeA, view);
            var (sB, _) = h.GetEdgeEndpointsOnSheet(edgeB, view);
            double dimX = horizontal ? (sA[0] + sB[0]) / 2.0 + 0.008 : sA[0] + 0.008;
            double dimY = horizontal ? sA[1] : (sA[1] + sB[1]) / 2.0;

            var dim = h.CreateDimension(dimX, dimY);
            if (dim != null)
            {
                Console.WriteLine($"  [Thickness] Thickness dimension created: {bestDistance * 1000:F2}mm (parallel edges)");
                h.DimensionedFeatures.Add("Thickness");
                h.ClearSelection();
                return true;
            }

            h.ClearSelection();
            return false;
        }

        private static double ParallelEdgeDistance(
            SmartDimHelper h,
            IView view,
            Edge edgeA,
            Edge edgeB,
            bool horizontal)
        {
            var midA = h.GetEdgeMidpointOnSheet(edgeA, view);
            var midB = h.GetEdgeMidpointOnSheet(edgeB, view);
            return horizontal
                ? Math.Abs(midA[1] - midB[1])
                : Math.Abs(midA[0] - midB[0]);
        }

        private static void TryAddFromShortestEdge(SmartDimHelper h, IView view)
        {
            Edge[] linearEdges = h.GetViewEdges(view).Where(h.IsLinear).ToArray();
            if (linearEdges.Length == 0)
            {
                Console.WriteLine($"  [Thickness] No suitable thickness edge found");
                return;
            }

            Edge? thicknessEdge = null;
            double minLen = double.MaxValue;

            foreach (var edge in linearEdges)
            {
                double len = h.GetProjectedLength(edge, view);
                if (len < minLen && len >= MinThicknessMeters && len <= MaxThicknessMeters)
                {
                    minLen = len;
                    thicknessEdge = edge;
                }
            }

            if (thicknessEdge == null)
            {
                Console.WriteLine($"  [Thickness] No suitable thickness edge found");
                return;
            }

            double[] midPt = h.GetEdgeMidpointOnSheet(thicknessEdge, view);

            h.ClearSelection();
            h.SelectEdge(thicknessEdge, view, false);

            double dimX = midPt[0] + 0.008;
            double dimY = midPt[1];
            var dim = h.CreateDimension(dimX, dimY);
            if (dim != null)
            {
                Console.WriteLine($"  [Thickness] Thickness dimension created: {minLen * 1000:F2}mm");
                h.DimensionedFeatures.Add("Thickness");
            }
            else
                Console.WriteLine($"  [Thickness] WARNING: Thickness dimension failed");

            h.ClearSelection();
        }
    }
}
