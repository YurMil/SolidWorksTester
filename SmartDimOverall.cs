using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Module A: Overall bounding dimensions (width + height).
    /// Applied to ALL views including flat pattern.
    /// Creates exactly 2 dimensions per view.
    /// </summary>
    public static class SmartDimOverall
    {
        public static void Add(SmartDimHelper h, IView view)
        {
            string viewName = view.GetName2();
            Console.WriteLine($"  [Overall] Adding bounding dimensions to: {viewName}");

            Edge[] allEdges = h.GetViewEdges(view);
            if (allEdges.Length == 0)
            {
                Console.WriteLine($"  [Overall] No edges found in view {viewName}");
                return;
            }

            // Get edges bounding box in sheet coordinates
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);
            double dimOffset = 0.012; // 12mm offset for dimension text placement

            // Filter to only linear edges for outer boundary selection
            var linearEdges = allEdges.Where(e => h.IsLinear(e)).ToArray();

            // ── Horizontal overall dimension (width) ──
            // Find the longest horizontal edge near the top of the bounding box
            Edge topEdge = null;
            double bestTopScore = double.MinValue;
            Edge bottomEdge = null;
            double bestBottomScore = double.MinValue;

            foreach (var edge in linearEdges)
            {
                if (!h.IsHorizontalInView(edge, view)) continue;

                var (s, e) = h.GetEdgeEndpointsOnSheet(edge, view);
                double edgeLen = Math.Abs(s[0] - e[0]);
                double avgY = (s[1] + e[1]) / 2.0;

                // Score = proximity to top/bottom + length preference
                double topScore = -Math.Abs(avgY - maxY) + edgeLen * 0.1;
                double bottomScore = -Math.Abs(avgY - minY) + edgeLen * 0.1;

                if (topScore > bestTopScore) { bestTopScore = topScore; topEdge = edge; }
                if (bottomScore > bestBottomScore) { bestBottomScore = bottomScore; bottomEdge = edge; }
            }

            if (topEdge != null && bottomEdge != null && topEdge != bottomEdge)
            {
                double heightVal = Math.Round(Math.Abs(maxY - minY), 3);
                string key = $"Overall_{heightVal}";
                if (!h.DimensionedFeatures.Contains(key))
                {
                    h.ClearSelection();
                    h.SelectEdge(topEdge, view, false);
                    h.SelectEdge(bottomEdge, view, true);
                    double dimX = maxX + dimOffset;
                    double dimY = (minY + maxY) / 2.0;
                    var dim = h.CreateDimension(dimX, dimY);
                    if (dim != null)
                    {
                        Console.WriteLine($"  [Overall] Height dimension created ({heightVal * 1000} mm)");
                        h.DimensionedFeatures.Add(key);
                    }
                }
            }

            // ── Vertical overall dimension (height → width label) ──
            // Find the longest vertical edge near left and right
            Edge leftEdge = null;
            double bestLeftScore = double.MinValue;
            Edge rightEdge = null;
            double bestRightScore = double.MinValue;

            foreach (var edge in linearEdges)
            {
                if (!h.IsVerticalInView(edge, view)) continue;

                var (s, e) = h.GetEdgeEndpointsOnSheet(edge, view);
                double edgeLen = Math.Abs(s[1] - e[1]);
                double avgX = (s[0] + e[0]) / 2.0;

                double leftScore = -Math.Abs(avgX - minX) + edgeLen * 0.1;
                double rightScore = -Math.Abs(avgX - maxX) + edgeLen * 0.1;

                if (leftScore > bestLeftScore) { bestLeftScore = leftScore; leftEdge = edge; }
                if (rightScore > bestRightScore) { bestRightScore = rightScore; rightEdge = edge; }
            }

            if (leftEdge != null && rightEdge != null && leftEdge != rightEdge)
            {
                double widthVal = Math.Round(Math.Abs(maxX - minX), 3);
                string key = $"Overall_{widthVal}";
                if (!h.DimensionedFeatures.Contains(key))
                {
                    h.ClearSelection();
                    h.SelectEdge(leftEdge, view, false);
                    h.SelectEdge(rightEdge, view, true);
                    double dimX = (minX + maxX) / 2.0;
                    double dimY = maxY + dimOffset;
                    var dim = h.CreateDimension(dimX, dimY);
                    if (dim != null)
                    {
                        Console.WriteLine($"  [Overall] Width dimension created ({widthVal * 1000} mm)");
                        h.DimensionedFeatures.Add(key);
                    }
                }
            }

            h.ClearSelection();
        }
    }
}
