using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Module F: Bend dimensions — 1 flange length + 1 bend radius per bend feature.
    /// Applied to standard views ONLY (NOT flat pattern).
    /// No angular dimensions are created.
    /// </summary>
    public static class SmartDimBends
    {
        // Sheet metal bend feature type names
        private static readonly HashSet<string> BendFeatureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EdgeFlange", "Hem", "Jog", "SketchBend", "SM3dBend",
            "SMMiteredFlange", "MiterFlange", "LoftedBend", "OneBend", "Fold"
        };

        public static void Add(SmartDimHelper h, IView view)
        {
            string viewName = view.GetName2();
            Console.WriteLine($"  [Bends] Scanning for bend features in: {viewName}");

            Edge[] allEdges = h.GetViewEdges(view);
            if (allEdges.Length == 0) return;

            // Group edges by their owning bend feature
            var bendEdgeGroups = new Dictionary<string, List<Edge>>();

            foreach (var edge in allEdges)
            {
                string featureType = h.GetEdgeFeatureType(edge);
                if (BendFeatureTypes.Contains(featureType))
                {
                    Feature feat = h.GetEdgeFeature(edge);
                    if (feat == null) continue;
                    string key = feat.Name;

                    if (!bendEdgeGroups.ContainsKey(key))
                        bendEdgeGroups[key] = new List<Edge>();
                    bendEdgeGroups[key].Add(edge);
                }
            }

            if (bendEdgeGroups.Count == 0)
            {
                Console.WriteLine($"  [Bends] No bend features found");
                return;
            }

            Console.WriteLine($"  [Bends] Found {bendEdgeGroups.Count} bend feature(s)");


            foreach (var kvp in bendEdgeGroups)
            {
                string featName = kvp.Key;
                var edges = kvp.Value;

                // ── Find bend radius arcs to ensure we are in a profile view ──
                var arcEdges = edges.Where(e => h.IsCircleProfileInView(e, view) && !h.IsFullCircle(e)).ToList();
                if (arcEdges.Count == 0) continue; // Not a side profile view, skip this bend here

                if (h.DimensionedFeatures.Contains(featName)) continue;
                h.DimensionedFeatures.Add(featName);

                // ── 1. Flange length dimension (1 linear edge) ──
                var linearEdges = edges.Where(e => h.IsLinear(e)).ToList();
                if (linearEdges.Count > 0)
                {
                    // Pick the longest linear edge as the flange length representative
                    Edge flangeEdge = linearEdges
                        .OrderByDescending(e => h.GetProjectedLength(e, view))
                        .First();

                    double[] midPt = h.GetEdgeMidpointOnSheet(flangeEdge, view);

                    h.ClearSelection();
                    h.SelectEdge(flangeEdge, view, false);
                    var dim = h.CreateDimension(midPt[0], midPt[1] - 0.008);
                    if (dim != null)
                        Console.WriteLine($"  [Bends] {featName}: Flange length dimension created");
                }

                // ── 2. Bend radius dimension (1 arc/circular edge) ──
                // Pick the first arc edge — represents the bend radius
                Edge radiusEdge = arcEdges[0];
                double radius = h.GetCircleRadius(radiusEdge);
                double[] center = h.GetCircleCenterOnSheet(radiusEdge, view);

                h.ClearSelection();
                h.SelectEdge(radiusEdge, view, false);
                var dim2 = h.CreateDimension(center[0] + 0.008, center[1] + 0.008);
                if (dim2 != null)
                    Console.WriteLine($"  [Bends] {featName}: Bend radius R{radius * 1000:F2}mm dimension created");
            }

            h.ClearSelection();
        }
    }
}
