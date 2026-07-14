using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Dimensions chamfer/fillet edges and miter cut lines visible in drawing views.
    /// </summary>
    public static class CylindricalDimChamfers
    {
        private static readonly System.Collections.Generic.HashSet<string> ChamferFeatureTypes =
            new(StringComparer.OrdinalIgnoreCase) { "Chamfer", "Fillet" };

        private const double MinEdgeLength = 0.0005;
        private const double DimOffset = 0.010;

        public static void Add(SmartDimHelper h, IView view, Action<string> log)
        {
            string viewName = view.GetName2();
            Edge[] edges = h.GetViewEdges(view);
            int created = 0;

            foreach (Edge edge in edges.Where(h.IsLinear))
            {
                string featureType = h.GetEdgeFeatureType(edge);
                if (!ChamferFeatureTypes.Contains(featureType))
                    continue;

                double length = h.GetProjectedLength(edge, view);
                if (length < MinEdgeLength)
                    continue;

                string key = $"Cyl_Chamfer_{featureType}_{length:F4}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                if (TryDimensionEdge(h, view, edge, key))
                {
                    h.DimensionedFeatures.Add(key);
                    created++;
                }
            }

            if (created > 0)
                log($"  Chamfer/fillet dimensions in {viewName}: {created}.");
        }

        private static bool TryDimensionEdge(SmartDimHelper h, IView view, Edge edge, string key)
        {
            try
            {
                Edge[] all = h.GetViewEdges(view);
                Edge? parallel = all
                    .Where(e => !ReferenceEquals(e, edge) && h.IsLinear(e))
                    .Where(e => IsParallelInView(h, edge, e, view))
                    .OrderByDescending(e => h.GetProjectedLength(e, view))
                    .FirstOrDefault();

                h.ClearSelection();
                if (parallel != null)
                {
                    if (!h.SelectEdge(edge, view, false))
                        return false;
                    if (!h.SelectEdge(parallel, view, true))
                        return false;
                }
                else if (!h.SelectEdge(edge, view, false))
                {
                    return false;
                }

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                return h.CreateDimension(mid[0] + DimOffset, mid[1] + DimOffset) != null;
            }
            catch
            {
                return false;
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static bool IsParallelInView(SmartDimHelper h, Edge a, Edge b, IView view)
        {
            bool aHoriz = h.IsHorizontalInView(a, view);
            bool bHoriz = h.IsHorizontalInView(b, view);
            bool aVert = h.IsVerticalInView(a, view);
            bool bVert = h.IsVerticalInView(b, view);
            return (aHoriz && bHoriz) || (aVert && bVert);
        }
    }
}
