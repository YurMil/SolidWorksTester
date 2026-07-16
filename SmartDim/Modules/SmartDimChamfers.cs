using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Places chamfer leg dimensions on flat-plate views when Chamfer features are present.
    /// Dedupes by leg length; avoids treating chamfer legs as sheet thickness.
    /// </summary>
    public static class SmartDimChamfers
    {
        private const double MinLegMeters = 0.0004;
        private const double MaxLegMeters = 0.050;
        private const double DimOffset = 0.010;

        private static readonly HashSet<string> ChamferTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Chamfer"
        };

        public static void Add(SmartDimHelper h, IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();
            Edge[] edges = h.GetViewEdges(view);
            if (edges.Length == 0)
                return;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            var candidates = new List<(Edge Edge, double LegModel)>();

            foreach (Edge edge in edges.Where(h.IsLinear))
            {
                if (!ChamferTypes.Contains(h.GetEdgeFeatureType(edge)))
                    continue;

                double legModel = h.GetProjectedLength(edge, view) / scale;
                if (legModel < MinLegMeters || legModel > MaxLegMeters)
                    continue;

                candidates.Add((edge, Math.Round(legModel, 5)));
            }

            if (candidates.Count == 0)
                return;

            int created = 0;
            foreach (var group in candidates.GroupBy(c => c.LegModel).OrderBy(g => g.Key))
            {
                string key = $"Flat_Chamfer_{group.Key:F5}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;
                if (h.HasDimensionWithValueInDrawing(group.Key))
                {
                    h.DimensionedFeatures.Add(key);
                    continue;
                }

                Edge edge = group
                    .OrderByDescending(c => h.GetProjectedLength(c.Edge, view))
                    .First().Edge;

                // Prefer dim between chamfer edge and a long parallel outer edge when available.
                Edge? parallel = edges
                    .Where(e => !ReferenceEquals(e, edge) && h.IsLinear(e))
                    .Where(e => IsSameOrientation(h, edge, e, view))
                    .Where(e => !ChamferTypes.Contains(h.GetEdgeFeatureType(e)))
                    .OrderByDescending(e => h.GetProjectedLength(e, view))
                    .FirstOrDefault();

                h.ClearSelection();
                bool selected = parallel != null
                    ? h.SelectEdge(edge, view, false) && h.SelectEdge(parallel, view, true)
                    : h.SelectEdge(edge, view, false);

                if (!selected)
                    continue;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                DisplayDimension? dim = h.CreateLinearDimension(mid[0] + DimOffset, mid[1] + DimOffset)
                    ?? h.CreateDimension(mid[0] + DimOffset, mid[1] + DimOffset);

                if (dim == null)
                    continue;

                h.DimensionedFeatures.Add(key);
                created++;
                log?.Invoke($"  [Chamfer] {group.Key * 1000:F1} mm on {viewName}" +
                            (group.Count() > 1 ? $" (×{group.Count()})" : "") + ".");

                TryAddChamferAngle(h, view, viewName, edge, edges, log);
            }

            h.ClearSelection();
            if (created == 0 && candidates.Count > 0)
                log?.Invoke($"  [Chamfer] No chamfer dims placed on {viewName}.");
        }

        private static void TryAddChamferAngle(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge chamferEdge,
            Edge[] edges,
            Action<string>? log)
        {
            const string key = "Flat_Chamfer_Angle";
            if (h.DimensionedFeatures.Contains(key))
                return;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? neighbour = edges
                .Where(e => !ReferenceEquals(e, chamferEdge) && h.IsLinear(e))
                .Where(e =>
                {
                    string t = h.GetEdgeFeatureType(e);
                    if (!ChamferTypes.Contains(t))
                        return true;
                    // Long outline mis-tagged as Chamfer after corner break.
                    return h.GetProjectedLength(e, view) / scale > MaxLegMeters;
                })
                .OrderBy(e =>
                {
                    double[] a = h.GetEdgeMidpointOnSheet(chamferEdge, view);
                    double[] b = h.GetEdgeMidpointOnSheet(e, view);
                    double dx = a[0] - b[0];
                    double dy = a[1] - b[1];
                    return dx * dx + dy * dy;
                })
                .FirstOrDefault();

            if (neighbour == null)
                return;

            h.ClearSelection();
            if (!h.SelectEdge(chamferEdge, view, false) || !h.SelectEdge(neighbour, view, true))
                return;

            double[] mid = h.GetEdgeMidpointOnSheet(chamferEdge, view);
            if (h.CreateAngularDimension(mid[0] - DimOffset, mid[1] + DimOffset) == null)
                return;

            h.DimensionedFeatures.Add(key);
            log?.Invoke($"  [Chamfer] Angle on {viewName}.");
        }

        private static bool IsSameOrientation(SmartDimHelper h, Edge a, Edge b, IView view)
        {
            bool aH = h.IsHorizontalInView(a, view, 0.004);
            bool bH = h.IsHorizontalInView(b, view, 0.004);
            bool aV = h.IsVerticalInView(a, view, 0.004);
            bool bV = h.IsVerticalInView(b, view, 0.004);
            return (aH && bH) || (aV && bV);
        }
    }
}
