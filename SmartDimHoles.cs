using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester
{
    /// <summary>
    /// Module C: Hole diameters with quantity count.
    /// Applied to ALL views (standard + flat pattern).
    /// Groups identical holes by diameter, creates 1 dimension per group with "Nx " prefix.
    /// </summary>
    public static class SmartDimHoles
    {
        public static void Add(SmartDimHelper h, IView view)
        {
            if (!view.IsFlatPatternView()) return;
            AddHoleDiameters(h, view);
        }

        /// <summary>Hole diameters on standard orthographic views (flat plate pipeline).</summary>
        public static void AddForStandardViews(SmartDimHelper h, IView view, double? excludeDiameter = null)
        {
            if (view.IsFlatPatternView()) return;
            AddHoleDiameters(h, view, excludeDiameter);
        }

        private static void AddHoleDiameters(SmartDimHelper h, IView view, double? excludeDiameter = null)
        {
            string viewName = view.GetName2();
            Console.WriteLine($"  [Holes] Scanning for circular edges in: {viewName}");

            Edge[] allEdges = h.GetViewEdges(view);

            // Find all full-circle edges (hole boundaries)
            var circularEdges = allEdges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .ToArray();

            if (circularEdges.Length == 0)
            {
                Console.WriteLine($"  [Holes] No full-circle edges found");
                return;
            }

            // Group by diameter (rounded to 0.1mm = 0.0001m)
            var groups = new Dictionary<double, List<Edge>>();
            foreach (var edge in circularEdges)
            {
                double radius = h.GetCircleRadius(edge);
                double diameter = Math.Round(radius * 2.0, 4); // round to 0.1mm
                if (!groups.ContainsKey(diameter))
                    groups[diameter] = new List<Edge>();
                groups[diameter].Add(edge);
            }

            Console.WriteLine($"  [Holes] Found {groups.Count} unique hole diameter groups");

            // Get view bounding box for dimension placement
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);
            int groupIndex = 0;

            foreach (var kvp in groups)
            {
                double diameter = kvp.Key;

                if (excludeDiameter.HasValue &&
                    Math.Abs(diameter - excludeDiameter.Value) < 0.0001)
                {
                    Console.WriteLine($"  [Holes] Skipping outer profile Ø{diameter * 1000:F1}mm");
                    continue;
                }

                string key = $"Hole_{diameter}";
                if (h.DimensionedFeatures.Contains(key)) continue;

                var group = kvp.Value;
                int qty = group.Count;
                Edge representative = group[0];

                // Select the first circle edge to create diameter dimension
                h.ClearSelection();
                h.SelectEdge(representative, view, false);

                // Place dimension text slightly offset from the hole center
                double[] center = h.GetCircleCenterOnSheet(representative, view);
                double dimX = center[0] + 0.015 + groupIndex * 0.005;
                double dimY = center[1] + 0.010;

                DisplayDimension dim = h.CreateDimension(dimX, dimY);
                if (dim != null)
                {
                    // Add quantity prefix if multiple holes of same size
                    if (qty > 1)
                    {
                        dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, $"{qty}x ");
                    }
                    Console.WriteLine($"  [Holes] ⌀{diameter * 1000:F1}mm × {qty} pcs — dimension created");
                    h.DimensionedFeatures.Add(key);
                }
                else
                {
                    Console.WriteLine($"  [Holes] WARNING: Diameter dimension failed for ⌀{diameter * 1000:F1}mm");
                }

                groupIndex++;
            }

            h.ClearSelection();
        }
    }
}
