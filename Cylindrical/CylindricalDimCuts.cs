using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Dimensions pipe/tube secondary operations visible in orthographic views:
    /// Cut-Extrude notches, longitudinal cuts, miter (angled) ends, and hole cuts.
    /// </summary>
    public static class CylindricalDimCuts
    {
        private static readonly HashSet<string> CutFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cut", "CutExtrude", "CutThin", "ExtrudeCut",
            "RevolveCut", "SweepCut", "LoftCut", "SurfCut", "NormalCut",
            "IceCutFeature", "CutSplit"
        };

        private static readonly HashSet<string> HoleFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "HoleWiz", "AdvHoleWiz", "HoleSeries", "SimpleHole", "HoleSeriesWizard"
        };

        private const double MinEdgeLength = 0.0008;   // 0.8 mm
        private const double MinHoleRadius = 0.0008;
        private const double DimOffset = 0.010;
        private const double AngleTolMeters = SmartDimConstants.SheetOrientationToleranceMeters;

        public static void Add(SmartDimHelper h, IView view, Action<string> log)
        {
            string viewName = view.GetName2();
            Edge[] allEdges = h.GetViewEdges(view);
            if (allEdges.Length == 0)
                return;

            var cutGroups = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
            var holeCircles = new List<(string Feat, Edge Edge)>();
            var slantedCuts = new List<(string Feat, Edge Edge)>();

            foreach (Edge edge in allEdges)
            {
                string type = h.GetEdgeFeatureType(edge);
                Feature? feat = h.GetEdgeFeature(edge);
                string featName = feat?.Name ?? type;
                if (string.IsNullOrWhiteSpace(featName))
                    continue;

                if (HoleFeatureTypes.Contains(type) && h.IsCircular(edge))
                {
                    if (h.GetCircleRadius(edge) >= MinHoleRadius)
                        holeCircles.Add((featName, edge));
                    continue;
                }

                if (!CutFeatureTypes.Contains(type))
                    continue;

                if (h.IsCircular(edge))
                {
                    if (h.GetCircleRadius(edge) >= MinHoleRadius)
                        holeCircles.Add((featName, edge));
                    continue;
                }

                if (!h.IsLinear(edge))
                    continue;

                double len = h.GetProjectedLength(edge, view);
                if (len < MinEdgeLength)
                    continue;

                if (!cutGroups.TryGetValue(featName, out List<Edge>? list))
                {
                    list = new List<Edge>();
                    cutGroups[featName] = list;
                }

                list.Add(edge);

                if (!h.IsHorizontalInView(edge, view, AngleTolMeters) &&
                    !h.IsVerticalInView(edge, view, AngleTolMeters))
                {
                    slantedCuts.Add((featName, edge));
                }
            }

            int created = 0;

            foreach (var kvp in cutGroups)
            {
                if (h.DimensionedFeatures.Contains($"CylCut_{kvp.Key}"))
                    continue;

                int n = DimensionCutGroup(h, view, viewName, kvp.Key, kvp.Value, log);
                if (n > 0)
                {
                    h.DimensionedFeatures.Add($"CylCut_{kvp.Key}");
                    created += n;
                }
            }

            foreach (var (feat, edge) in slantedCuts)
            {
                string key = $"CylMiter_{feat}_{h.GetProjectedLength(edge, view):F4}";
                if (h.DimensionedFeatures.Contains(key))
                    continue;

                if (TryAngularMiter(h, view, edge, allEdges, log))
                {
                    h.DimensionedFeatures.Add(key);
                    created++;
                }
                else if (TryEdgeLength(h, view, edge, key, log))
                {
                    h.DimensionedFeatures.Add(key);
                    created++;
                }
            }

            foreach (var (feat, edge) in holeCircles)
            {
                double dia = h.GetCircleRadius(edge) * 2.0;
                string key = $"CylHole_{feat}_{dia:F4}";
                if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(dia))
                    continue;

                if (TryHoleDiameter(h, view, edge, dia, log))
                {
                    h.DimensionedFeatures.Add(key);
                    created++;
                }
            }

            if (created > 0)
                log($"  Cut/miter/hole dimensions in {viewName}: {created}.");
        }

        /// <summary>
        /// Quick model-tree probe for logging (optional). Returns cut / hole / chamfer counts.
        /// </summary>
        public static (int Cuts, int Holes, int Chamfers) ProbeModelFeatures(IModelDoc2? partDoc)
        {
            int cuts = 0, holes = 0, chamfers = 0;
            if (partDoc == null)
                return (0, 0, 0);

            try
            {
                Feature? feat = partDoc.FirstFeature() as Feature;
                while (feat != null)
                {
                    string type = feat.GetTypeName2() ?? string.Empty;
                    if (CutFeatureTypes.Contains(type))
                        cuts++;
                    else if (HoleFeatureTypes.Contains(type))
                        holes++;
                    else if (type.Equals("Chamfer", StringComparison.OrdinalIgnoreCase) ||
                             type.Equals("Fillet", StringComparison.OrdinalIgnoreCase))
                        chamfers++;

                    feat = feat.GetNextFeature() as Feature;
                }
            }
            catch
            {
                // ignore COM failures
            }

            return (cuts, holes, chamfers);
        }

        private static int DimensionCutGroup(
            SmartDimHelper h,
            IView view,
            string viewName,
            string featName,
            List<Edge> edges,
            Action<string> log)
        {
            if (edges.Count < 1)
                return 0;

            var (cMinX, cMinY, cMaxX, cMaxY) = h.ComputeEdgesBoundingBox(edges.ToArray(), view);
            double cutW = cMaxX - cMinX;
            double cutH = cMaxY - cMinY;
            if (cutW < MinEdgeLength && cutH < MinEdgeLength)
                return 0;

            var horiz = edges.Where(e => h.IsHorizontalInView(e, view)).ToList();
            var vert = edges.Where(e => h.IsVerticalInView(e, view)).ToList();
            int created = 0;

            // Size: width between extreme vertical cut edges
            if (vert.Count >= 2)
            {
                var left = vert.OrderBy(e => MidX(h, e, view)).First();
                var right = vert.OrderBy(e => MidX(h, e, view)).Last();
                double span = Math.Abs(MidX(h, right, view) - MidX(h, left, view));
                if (span >= MinEdgeLength &&
                    !h.HasDimensionWithValueInDrawing(span) &&
                    TryLinearPair(h, view, left, right, (cMinX + cMaxX) / 2.0, cMinY - DimOffset * 0.6))
                {
                    log($"  Cut «{featName}» width {span * 1000:F1} mm in {viewName}.");
                    created++;
                }
            }

            // Size: height between extreme horizontal cut edges
            if (horiz.Count >= 2)
            {
                var bottom = horiz.OrderBy(e => MidY(h, e, view)).First();
                var top = horiz.OrderBy(e => MidY(h, e, view)).Last();
                double span = Math.Abs(MidY(h, top, view) - MidY(h, bottom, view));
                if (span >= MinEdgeLength &&
                    !h.HasDimensionWithValueInDrawing(span) &&
                    TryLinearPair(h, view, bottom, top, cMaxX + DimOffset * 0.8, (cMinY + cMaxY) / 2.0))
                {
                    log($"  Cut «{featName}» height {span * 1000:F1} mm in {viewName}.");
                    created++;
                }
            }

            // Single long cut edge (e.g. longitudinal half-pipe cut) — dimension its length once.
            if (created == 0 && edges.Count >= 1)
            {
                Edge longest = edges.OrderByDescending(e => h.GetProjectedLength(e, view)).First();
                double len = h.GetProjectedLength(longest, view);
                string key = $"CylCutLen_{featName}_{len:F4}";
                if (!h.DimensionedFeatures.Contains(key) &&
                    !h.HasDimensionWithValueInDrawing(len) &&
                    TryEdgeLength(h, view, longest, key, log))
                {
                    h.DimensionedFeatures.Add(key);
                    created++;
                }
            }

            // Position from outer silhouette (left / bottom) when we have a clear cut edge.
            Edge[] allLinear = h.GetViewEdges(view).Where(h.IsLinear).ToArray();
            if (vert.Count > 0)
            {
                Edge? outerLeft = allLinear
                    .Where(e => h.IsVerticalInView(e, view))
                    .OrderBy(e => MidX(h, e, view))
                    .FirstOrDefault();
                Edge cutLeft = vert.OrderBy(e => MidX(h, e, view)).First();
                if (outerLeft != null && !ReferenceEquals(outerLeft, cutLeft))
                {
                    double offset = Math.Abs(MidX(h, cutLeft, view) - MidX(h, outerLeft, view));
                    string key = $"CylCutOffX_{featName}_{offset:F4}";
                    if (offset >= MinEdgeLength &&
                        !h.DimensionedFeatures.Contains(key) &&
                        !h.HasDimensionWithValueInDrawing(offset) &&
                        TryLinearPair(h, view, outerLeft, cutLeft,
                            (MidX(h, outerLeft, view) + MidX(h, cutLeft, view)) / 2.0,
                            cMaxY + DimOffset * 0.6))
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  Cut «{featName}» X-offset {offset * 1000:F1} mm in {viewName}.");
                        created++;
                    }
                }
            }

            if (horiz.Count > 0)
            {
                Edge? outerBottom = allLinear
                    .Where(e => h.IsHorizontalInView(e, view))
                    .OrderBy(e => MidY(h, e, view))
                    .FirstOrDefault();
                Edge cutBottom = horiz.OrderBy(e => MidY(h, e, view)).First();
                if (outerBottom != null && !ReferenceEquals(outerBottom, cutBottom))
                {
                    double offset = Math.Abs(MidY(h, cutBottom, view) - MidY(h, outerBottom, view));
                    string key = $"CylCutOffY_{featName}_{offset:F4}";
                    if (offset >= MinEdgeLength &&
                        !h.DimensionedFeatures.Contains(key) &&
                        !h.HasDimensionWithValueInDrawing(offset) &&
                        TryLinearPair(h, view, outerBottom, cutBottom,
                            cMaxX + DimOffset * 1.2,
                            (MidY(h, outerBottom, view) + MidY(h, cutBottom, view)) / 2.0))
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  Cut «{featName}» Y-offset {offset * 1000:F1} mm in {viewName}.");
                        created++;
                    }
                }
            }

            return created;
        }

        private static bool TryAngularMiter(
            SmartDimHelper h,
            IView view,
            Edge slanted,
            Edge[] allEdges,
            Action<string> log)
        {
            // Reference = longest axis-aligned edge in the view (pipe axis / end).
            Edge? axis = allEdges
                .Where(h.IsLinear)
                .Where(e => !ReferenceEquals(e, slanted))
                .Where(e => h.IsHorizontalInView(e, view) || h.IsVerticalInView(e, view))
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (axis == null)
                return false;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(slanted, view, false))
                    return false;
                if (!h.SelectEdge(axis, view, true))
                    return false;

                double[] mid = h.GetEdgeMidpointOnSheet(slanted, view);
                DisplayDimension? dim = h.CreateAngularDimension(
                    mid[0] + DimOffset, mid[1] + DimOffset);
                if (dim == null)
                    return false;

                log($"  Miter/angle cut dimension in {view.GetName2()}.");
                return true;
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

        private static bool TryEdgeLength(
            SmartDimHelper h,
            IView view,
            Edge edge,
            string key,
            Action<string> log)
        {
            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(edge, view, false))
                    return false;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                DisplayDimension? dim = h.CreateDimension(mid[0] + DimOffset, mid[1] + DimOffset);
                if (dim == null)
                    return false;

                double len = h.GetProjectedLength(edge, view);
                log($"  Cut edge length {len * 1000:F1} mm in {view.GetName2()}.");
                return true;
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

        private static bool TryHoleDiameter(
            SmartDimHelper h,
            IView view,
            Edge circle,
            double diameter,
            Action<string> log)
        {
            try
            {
                DisplayDimension? dim = h.CreateDiameterDimension(
                    circle,
                    view,
                    h.GetCircleCenterOnSheet(circle, view)[0] + DimOffset,
                    h.GetCircleCenterOnSheet(circle, view)[1] + DimOffset);
                if (dim == null)
                    return false;

                log($"  Cut/hole Ø{diameter * 1000:F1} mm in {view.GetName2()}.");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLinearPair(
            SmartDimHelper h,
            IView view,
            Edge a,
            Edge b,
            double textX,
            double textY)
        {
            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(a, view, false))
                    return false;
                if (!h.SelectEdge(b, view, true))
                    return false;
                return h.CreateDimension(textX, textY) != null;
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

        private static double MidX(SmartDimHelper h, Edge e, IView view)
        {
            double[] m = h.GetEdgeMidpointOnSheet(e, view);
            return m[0];
        }

        private static double MidY(SmartDimHelper h, Edge e, IView view)
        {
            double[] m = h.GetEdgeMidpointOnSheet(e, view);
            return m[1];
        }
    }
}
