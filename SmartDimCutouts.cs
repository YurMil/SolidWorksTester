using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Module E: Cutout (slot/notch) dimensions — width, height, and 2 edge offsets.
    /// Applied to standard views ONLY (NOT flat pattern).
    /// Identifies Cut-Extrude features and creates bounding + position dimensions.
    /// </summary>
    public static class SmartDimCutouts
    {
        public static void Add(SmartDimHelper h, IView view)
        {
            string viewName = view.GetName2();
            Console.WriteLine($"  [Cutouts] Scanning for cutout features in: {viewName}");

            Edge[] allEdges = h.GetViewEdges(view);
            var linearEdges = allEdges.Where(e => h.IsLinear(e)).ToArray();
            if (linearEdges.Length == 0) return;

            var (viewMinX, viewMinY, viewMaxX, viewMaxY) = h.ComputeEdgesBoundingBox(allEdges, view);

            // Find edges belonging to Cut-Extrude features (excluding circles/holes)
            var cutoutEdgeGroups = new Dictionary<string, List<Edge>>();

            foreach (var edge in linearEdges)
            {
                string featureType = h.GetEdgeFeatureType(edge);
                // NormalCut is the sheet metal specific cut type; also check CutExtrude
                if (featureType == "NormalCut" || featureType == "CutExtrude" || featureType == "Cut")
                {
                    Feature feat = h.GetEdgeFeature(edge);
                    if (feat == null) continue;
                    string featName = feat.Name;

                    if (h.DimensionedFeatures.Contains(featName)) continue;
                    h.DimensionedFeatures.Add(featName);

                    if (!cutoutEdgeGroups.ContainsKey(featName))
                        cutoutEdgeGroups[featName] = new List<Edge>();
                    cutoutEdgeGroups[featName].Add(edge);
                }
            }

            if (cutoutEdgeGroups.Count == 0)
            {
                Console.WriteLine($"  [Cutouts] No cutout features found");
                return;
            }

            Console.WriteLine($"  [Cutouts] Found {cutoutEdgeGroups.Count} cutout feature(s)");

            foreach (var kvp in cutoutEdgeGroups)
            {
                string featName = kvp.Key;
                var edges = kvp.Value;

                if (edges.Count < 2) continue;

                // Compute the bounding box of this cutout's edges
                var (cMinX, cMinY, cMaxX, cMaxY) = h.ComputeEdgesBoundingBox(edges.ToArray(), view);
                double cutWidth = cMaxX - cMinX;
                double cutHeight = cMaxY - cMinY;

                // If the cutout is viewed from the side (edge view), one of its dimensions will be the sheet thickness
                if (cutWidth < 0.001 || cutHeight < 0.001) continue;

                Console.WriteLine($"  [Cutouts] {featName}: {cutWidth * 1000:F1} x {cutHeight * 1000:F1} mm");

                // Find horizontal and vertical edges within this cutout for dimensioning
                var horizEdges = edges.Where(e => h.IsHorizontalInView(e, view)).ToList();
                var vertEdges = edges.Where(e => h.IsVerticalInView(e, view)).ToList();

                // Cutout width dimension (between left and right vertical edges)
                if (vertEdges.Count >= 2)
                {
                    var leftCutEdge = vertEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[0] + end[0]) / 2.0;
                    }).First();
                    var rightCutEdge = vertEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[0] + end[0]) / 2.0;
                    }).Last();

                    if (leftCutEdge != rightCutEdge)
                    {
                        h.ClearSelection();
                        h.SelectEdge(leftCutEdge, view, false);
                        h.SelectEdge(rightCutEdge, view, true);
                        var dim = h.CreateDimension((cMinX + cMaxX) / 2.0, cMinY - 0.006);
                        if (dim != null) Console.WriteLine($"  [Cutouts] {featName} width dimension created");
                    }
                }

                // Cutout height dimension (between top and bottom horizontal edges)
                if (horizEdges.Count >= 2)
                {
                    var bottomCutEdge = horizEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[1] + end[1]) / 2.0;
                    }).First();
                    var topCutEdge = horizEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[1] + end[1]) / 2.0;
                    }).Last();

                    if (topCutEdge != bottomCutEdge)
                    {
                        h.ClearSelection();
                        h.SelectEdge(topCutEdge, view, false);
                        h.SelectEdge(bottomCutEdge, view, true);
                        var dim = h.CreateDimension(cMaxX + 0.008, (cMinY + cMaxY) / 2.0);
                        if (dim != null) Console.WriteLine($"  [Cutouts] {featName} height dimension created");
                    }
                }

                // Position from left boundary to cutout
                var outerLeftEdge = linearEdges
                    .Where(e => h.IsVerticalInView(e, view))
                    .OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[0] + end[0]) / 2.0;
                    })
                    .FirstOrDefault();

                if (outerLeftEdge != null && vertEdges.Count > 0)
                {
                    var nearestCutVertEdge = vertEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[0] + end[0]) / 2.0;
                    }).First();

                    h.ClearSelection();
                    h.SelectEdge(outerLeftEdge, view, false);
                    h.SelectEdge(nearestCutVertEdge, view, true);
                    var dim = h.CreateDimension((viewMinX + cMinX) / 2.0, viewMaxY + 0.006);
                    if (dim != null) Console.WriteLine($"  [Cutouts] {featName} left offset dimension created");
                }

                // Position from bottom boundary to cutout
                var outerBottomEdge = linearEdges
                    .Where(e => h.IsHorizontalInView(e, view))
                    .OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[1] + end[1]) / 2.0;
                    })
                    .FirstOrDefault();

                if (outerBottomEdge != null && horizEdges.Count > 0)
                {
                    var nearestCutHorizEdge = horizEdges.OrderBy(e => {
                        var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                        return (s[1] + end[1]) / 2.0;
                    }).First();

                    h.ClearSelection();
                    h.SelectEdge(outerBottomEdge, view, false);
                    h.SelectEdge(nearestCutHorizEdge, view, true);
                    var dim = h.CreateDimension(viewMaxX + 0.012, (viewMinY + cMinY) / 2.0);
                    if (dim != null) Console.WriteLine($"  [Cutouts] {featName} bottom offset dimension created");
                }
            }

            h.ClearSelection();
        }
    }
}
