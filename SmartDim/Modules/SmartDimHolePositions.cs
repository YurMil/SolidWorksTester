using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Module D: Hole positions — distance from nearest edge to first hole in a pattern.
    /// Applied to standard views ONLY (NOT flat pattern).
    /// Creates 1 positioning dimension per hole group (from edge to nearest hole).
    /// </summary>
    public static class SmartDimHolePositions
    {
        public static void Add(SmartDimHelper h, IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();
            log?.Invoke($"  [HolePos] Adding hole position dimensions to: {viewName}");

            Edge[] allEdges = h.GetViewEdges(view);

            // Find circular edges (holes)
            var circularEdges = allEdges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .ToArray();

            if (circularEdges.Length == 0) return;

            // Find outer boundary linear edges
            var linearEdges = allEdges.Where(e => h.IsLinear(e)).ToArray();
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);
            double tolerance = 0.001; // 1mm tolerance for boundary detection

            // Find the best left edge (closest to minX, vertical) and bottom edge (closest to minY, horizontal)
            Edge leftBoundary = FindBestBoundaryEdge(h, linearEdges, view, "left", minX, tolerance);
            Edge bottomBoundary = FindBestBoundaryEdge(h, linearEdges, view, "bottom", minY, tolerance);

            if (leftBoundary == null && bottomBoundary == null)
            {
                log?.Invoke($"  [HolePos] No boundary edges found for positioning");
                return;
            }

            // Group holes by diameter (same as SmartDimHoles)
            var groups = new Dictionary<double, List<Edge>>();
            foreach (var edge in circularEdges)
            {
                double diameter = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (!groups.ContainsKey(diameter))
                    groups[diameter] = new List<Edge>();
                groups[diameter].Add(edge);
            }

            foreach (var kvp in groups)
            {
                double diameter = kvp.Key;
                string key = $"HolePos_{diameter}";
                if (h.DimensionedFeatures.Contains(key)) continue;

                var group = kvp.Value;

                // Find the hole center closest to the left boundary edge (smallest X)
                Edge nearestHoleToLeft = null;
                double nearestX = double.MaxValue;

                foreach (var hole in group)
                {
                    double[] c = h.GetCircleCenterOnSheet(hole, view);
                    if (c[0] < nearestX) { nearestX = c[0]; nearestHoleToLeft = hole; }
                }

                // Create horizontal position dimension: left boundary → nearest hole
                if (leftBoundary != null && nearestHoleToLeft != null)
                {
                    h.ClearSelection();
                    h.SelectEdge(leftBoundary, view, false);
                    h.SelectEdge(nearestHoleToLeft, view, true);

                    double[] holeCenter = h.GetCircleCenterOnSheet(nearestHoleToLeft, view);
                    double dimX = (minX + holeCenter[0]) / 2.0;
                    double dimY = minY - 0.008; // 8mm below the view
                    var dim = h.CreateDimension(dimX, dimY);
                    if (dim != null)
                        log?.Invoke($"  [HolePos] Horizontal position dimension created");
                }

                // Find the hole center closest to the bottom boundary edge (smallest Y)
                Edge nearestHoleToBottom = null;
                double nearestY = double.MaxValue;

                foreach (var hole in group)
                {
                    double[] c = h.GetCircleCenterOnSheet(hole, view);
                    if (c[1] < nearestY) { nearestY = c[1]; nearestHoleToBottom = hole; }
                }

                // Create vertical position dimension: bottom boundary → nearest hole
                if (bottomBoundary != null && nearestHoleToBottom != null)
                {
                    h.ClearSelection();
                    h.SelectEdge(bottomBoundary, view, false);
                    h.SelectEdge(nearestHoleToBottom, view, true);

                    double[] holeCenter = h.GetCircleCenterOnSheet(nearestHoleToBottom, view);
                    double dimX = maxX + 0.015;
                    double dimY = (minY + holeCenter[1]) / 2.0;
                    var dim = h.CreateDimension(dimX, dimY);
                    if (dim != null)
                        log?.Invoke($"  [HolePos] Vertical position dimension created");
                }
                
                h.DimensionedFeatures.Add(key);
            }

            h.ClearSelection();
        }

        private static Edge FindBestBoundaryEdge(SmartDimHelper h, Edge[] linearEdges, IView view,
            string side, double targetCoord, double tol)
        {
            Edge best = null;
            double bestScore = double.MinValue;

            foreach (var edge in linearEdges)
            {
                var (s, e) = h.GetEdgeEndpointsOnSheet(edge, view);
                double len = h.GetProjectedLength(edge, view);

                if (side == "left" && h.IsVerticalInView(edge, view))
                {
                    double avgX = (s[0] + e[0]) / 2.0;
                    double score = -Math.Abs(avgX - targetCoord) + len * 0.5;
                    if (score > bestScore) { bestScore = score; best = edge; }
                }
                else if (side == "bottom" && h.IsHorizontalInView(edge, view))
                {
                    double avgY = (s[1] + e[1]) / 2.0;
                    double score = -Math.Abs(avgY - targetCoord) + len * 0.5;
                    if (score > bestScore) { bestScore = score; best = edge; }
                }
            }
            return best;
        }
    }
}
