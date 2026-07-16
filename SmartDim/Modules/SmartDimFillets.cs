using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester
{
    /// <summary>
    /// Places radial dimensions for corner/edge fillets on flat-plate views.
    /// One callout per distinct radius (small corner R — not large profile arcs).
    /// </summary>
    public static class SmartDimFillets
    {
        private const double MinRadiusMeters = 0.0004;     // 0.4 mm
        private const double MaxCornerRadiusMeters = 0.040; // 40 mm
        private const double DimOffset = 0.010;
        private const double CenterBucketMeters = 0.0005;  // 0.5 mm

        private static readonly HashSet<string> FilletTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fillet", "VarFillet", "ConstCircFillet"
        };

        public static void Add(SmartDimHelper h, IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();
            Edge[] edges = h.GetViewEdges(view);
            if (edges.Length == 0)
                return;

            var candidates = new List<(Edge Edge, double Radius)>();
            foreach (Edge edge in edges.Where(h.IsCircular))
            {
                if (h.IsFullCircle(edge))
                    continue;

                double r = h.GetCircleRadius(edge);
                if (r < MinRadiusMeters || r > MaxCornerRadiusMeters)
                    continue;

                // Face-on corner fillets only (axis ≈ view normal). Skip edge-on rim arcs.
                if (!h.IsCircleProfileInView(edge, view))
                    continue;

                string type = h.GetEdgeFeatureType(edge);
                bool isFillet = FilletTypes.Contains(type) ||
                                type.Contains("Fillet", StringComparison.OrdinalIgnoreCase) ||
                                h.EdgeTouchesFilletFeature(edge);

                if (!isFillet)
                {
                    // Untagged small arcs only — avoid Extrude profile corners without Fillet feature.
                    if (!string.IsNullOrEmpty(type) &&
                        !type.Contains("Fillet", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                candidates.Add((edge, Math.Round(r, 5)));
            }

            if (candidates.Count == 0)
                return;

            int created = 0;
            foreach (var group in candidates.GroupBy(c => c.Radius).OrderBy(g => g.Key))
            {
                string key = $"Flat_Fillet_R_{group.Key:F5}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                // Only treat an existing radial/diameter dim as "already placed".
                if (h.HasDimensionWithValueInDrawing(
                        group.Key,
                        (int)swDimensionType_e.swRadialDimension) ||
                    h.HasDimensionWithValueInDrawing(
                        group.Key * 2.0,
                        (int)swDimensionType_e.swDiameterDimension))
                {
                    h.DimensionedFeatures.Add(key);
                    continue;
                }

                // Unique corner centers (top+bottom arcs of same fillet share a center in face view).
                int cornerQty = group
                    .Select(c =>
                    {
                        double[] ctr = h.GetCircleCenterOnSheet(c.Edge, view);
                        return (
                            Math.Round(ctr[0] / CenterBucketMeters),
                            Math.Round(ctr[1] / CenterBucketMeters));
                    })
                    .Distinct()
                    .Count();

                Edge edge = group
                    .OrderByDescending(c => h.GetProjectedLength(c.Edge, view))
                    .First().Edge;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                h.ClearSelection();
                if (!h.SelectEdge(edge, view, false))
                    continue;

                DisplayDimension? dim =
                    h.Model.AddRadialDimension2(mid[0] + DimOffset, mid[1] + DimOffset, 0.0) as DisplayDimension;

                if (dim == null)
                {
                    // Explicit radial type — never fall back to diameter (would show Ø10 for R5).
                    int err = 0;
                    dim = h.Ext.AddSpecificDimension(
                        mid[0] + DimOffset, mid[1] + DimOffset, 0.0,
                        (int)swDimensionType_e.swRadialDimension,
                        ref err) as DisplayDimension;
                }

                if (dim == null)
                    continue;

                if (cornerQty > 1)
                {
                    dim.SetText(
                        (int)swDimensionTextParts_e.swDimensionTextPrefix,
                        $"{cornerQty}x ");
                }

                h.DimensionedFeatures.Add(key);
                created++;
                log?.Invoke($"  [Fillet] R{group.Key * 1000:F1} mm on {viewName}" +
                            (cornerQty > 1 ? $" (×{cornerQty})" : "") + ".");
            }

            h.ClearSelection();
            if (created == 0)
                log?.Invoke($"  [Fillet] Candidates found but none placed on {viewName}.");
        }
    }
}
