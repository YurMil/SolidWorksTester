using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>
    /// Places sheet-metal thickness on side views of round flat plates.
    /// Side views may expose only arcs/silhouettes, so several strategies are used.
    /// </summary>
    internal static class RoundFlatPlateThickness
    {
        private const double MinThicknessMeters = 0.0001;
        private const double MaxThicknessMeters = 0.050; // thick plate
        private const double MinSideViewAspectRatio = 2.0;
        private const double DimOffset = 0.008;

        /// <summary>Finds the best side view and places one thickness dimension for the drawing.</summary>
        public static void TryAddOnce(SmartDimHelper h, IDrawingDoc drawing, Action<string> log) =>
            TryAddOnce(h, drawing, log, useOutlineSidePick: true, expectedThicknessMm: null);

        /// <param name="useOutlineSidePick">
        /// Kept for callers; outline is always preferred first (rim circles inflate COM bbox on edge-on views).
        /// </param>
        /// <param name="expectedThicknessMm">Optional expected gauge in mm (overrides sheet-metal read when set).</param>
        public static void TryAddOnce(
            SmartDimHelper h,
            IDrawingDoc drawing,
            Action<string> log,
            bool useOutlineSidePick,
            double? expectedThicknessMm)
        {
            if (h.DimensionedFeatures.Contains("Thickness"))
                return;

            double? expectedModel = expectedThicknessMm is > 0
                ? expectedThicknessMm.Value / 1000.0
                : TryReadSheetMetalThickness(h);

            // Prefer sheet outline for side pick — COM edge bbox of rim circles inflates to ~OD×OD
            // on edge-on views and wrongly rejects the thin silhouette.
            IView? sideView = FindBestSideViewByOutline(drawing, expectedModel, out double bboxThicknessSheet)
                ?? FindBestSideView(h, drawing, expectedModel, out bboxThicknessSheet);

            if (sideView == null)
            {
                log("  Warning: no side view found for round-plate thickness.");
                return;
            }

            // Edge distances are in sheet space — scale model gauge to sheet for matching.
            double? expectedSheet = expectedModel.HasValue
                ? expectedModel.Value * Math.Max(sideView.ScaleDecimal, 1e-9)
                : null;

            string viewName = sideView.GetName2();
            log($"  Thickness target view: {viewName} (bbox gauge {bboxThicknessSheet * 1000:F2} mm sheet" +
                (expectedModel.HasValue ? $", expected {expectedModel.Value * 1000:F2} mm model" : "") + ").");

            if (TryAddBetweenParallelEdges(h, sideView, expectedSheet))
            {
                log($"  Thickness dimension placed in {viewName} (parallel edges).");
                return;
            }

            if (TryAddFromShortestLinearEdge(h, sideView, expectedSheet))
            {
                log($"  Thickness dimension placed in {viewName} (short edge).");
                return;
            }

            if (TryAddFromExtremeVertices(h, sideView, expectedSheet, bboxThicknessSheet))
            {
                log($"  Thickness dimension placed in {viewName} (extreme vertices).");
                return;
            }

            if (TryAddFromOutlineThinSpan(h, sideView, expectedSheet, bboxThicknessSheet))
            {
                log($"  Thickness dimension placed in {viewName} (outline thin span).");
                return;
            }

            log($"  Warning: could not place thickness dimension in {viewName}.");
        }

        /// <summary>
        /// Side view = orthographic view with largest aspect ratio from GetOutline (no edge COM).
        /// Thickness check uses model-space gauge (sheet outline / scale).
        /// </summary>
        private static IView? FindBestSideViewByOutline(
            IDrawingDoc drawing,
            double? expectedModel,
            out double bboxThickness)
        {
            bboxThickness = 0;
            IView? bestView = null;
            double bestScore = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (!FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double width, out double height) &&
                    !TryGetViewOutlineSize(view, out width, out height))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                double scale = Math.Max(view.ScaleDecimal, 1e-9);
                double thinSheet = Math.Min(width, height);
                double wideSheet = Math.Max(width, height);
                double thinModel = thinSheet / scale;
                double aspect = wideSheet / Math.Max(thinSheet, 1e-12);

                if (thinModel < MinThicknessMeters || thinModel > MaxThicknessMeters)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (aspect < MinSideViewAspectRatio)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                // Prefer views whose thin outline matches expected SM gauge.
                double score = aspect;
                if (expectedModel.HasValue)
                {
                    double err = Math.Abs(thinModel - expectedModel.Value) / Math.Max(expectedModel.Value, 1e-9);
                    // Soft preference — do not hard-reject (outline padding often drifts).
                    score = aspect / (1.0 + err * 8.0);
                    if (err > 1.25 && aspect < 4.0)
                    {
                        view = view.GetNextView() as IView;
                        continue;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestView = view;
                    bboxThickness = thinSheet;
                }

                view = view.GetNextView() as IView;
            }

            // Last resort: highest-aspect non-iso view with thin dim in plate range.
            if (bestView == null && expectedModel.HasValue)
                bestView = FindHighestAspectSideView(drawing, expectedModel.Value, out bboxThickness);

            return bestView;
        }

        private static IView? FindHighestAspectSideView(
            IDrawingDoc drawing,
            double expectedModel,
            out double bboxThickness)
        {
            bboxThickness = 0;
            IView? best = null;
            double bestAspect = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (!name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase) &&
                    FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double width, out double height))
                {
                    double scale = Math.Max(view.ScaleDecimal, 1e-9);
                    double thinSheet = Math.Min(width, height);
                    double wideSheet = Math.Max(width, height);
                    double thinModel = thinSheet / scale;
                    double aspect = wideSheet / Math.Max(thinSheet, 1e-12);

                    if (aspect >= MinSideViewAspectRatio &&
                        thinModel >= MinThicknessMeters &&
                        thinModel <= MaxThicknessMeters &&
                        aspect > bestAspect)
                    {
                        // Prefer closer to expected gauge among high-aspect views.
                        double err = Math.Abs(thinModel - expectedModel) / Math.Max(expectedModel, 1e-9);
                        double score = aspect / (1.0 + err * 5.0);
                        if (score > bestAspect)
                        {
                            bestAspect = score;
                            best = view;
                            bboxThickness = thinSheet;
                        }
                    }
                }

                view = view.GetNextView() as IView;
            }

            return best;
        }

        private static IView? FindBestSideView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            double? expectedModel,
            out double bboxThickness)
        {
            bboxThickness = 0;
            IView? bestView = null;
            double bestScore = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (RoundFlatPlateViewAnalyzer.IsCircularFaceView(h, view))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                // Always prefer sheet outline over COM edge bbox (rim circles → OD×OD).
                if (!TryGetViewOutlineSize(view, out double width, out double height) &&
                    !FlatPlateViewAnalyzer.TryGetOutlineSize(view, out width, out height))
                {
                    Edge[] linearOnly = h.GetViewEdges(view).Where(h.IsLinear).ToArray();
                    if (linearOnly.Length == 0)
                    {
                        view = view.GetNextView() as IView;
                        continue;
                    }

                    var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(linearOnly, view);
                    width = maxX - minX;
                    height = maxY - minY;
                }

                double scale = Math.Max(view.ScaleDecimal, 1e-9);
                double thinSheet = Math.Min(width, height);
                double wideSheet = Math.Max(width, height);
                double thinModel = thinSheet / scale;
                double aspect = wideSheet / Math.Max(thinSheet, 1e-12);

                if (thinModel < MinThicknessMeters || thinModel > MaxThicknessMeters ||
                    aspect < MinSideViewAspectRatio)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                double score = aspect;
                if (expectedModel.HasValue)
                {
                    double err = Math.Abs(thinModel - expectedModel.Value) / Math.Max(expectedModel.Value, 1e-9);
                    if (err > 0.5)
                    {
                        view = view.GetNextView() as IView;
                        continue;
                    }

                    score = aspect / (1.0 + err * 10.0);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestView = view;
                    bboxThickness = thinSheet;
                }

                view = view.GetNextView() as IView;
            }

            return bestView;
        }

        private static bool TryGetViewOutlineSize(IView view, out double width, out double height)
        {
            width = 0;
            height = 0;

            if (view.GetOutline() is not double[] outline || outline.Length < 4)
                return false;

            width = Math.Abs(outline[2] - outline[0]);
            height = Math.Abs(outline[3] - outline[1]);
            return width > 0 && height > 0;
        }

        private static Edge[] GetAllOutlineEdges(SmartDimHelper h, IView view)
        {
            var set = new HashSet<Edge>();
            foreach (Edge edge in h.GetViewEdges(view))
                set.Add(edge);
            foreach (Edge edge in h.GetViewSilhouetteEdges(view))
                set.Add(edge);
            return set.ToArray();
        }

        private static double? TryReadSheetMetalThickness(SmartDimHelper h)
        {
            IView? anyView = (h.Drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (anyView != null)
            {
                Component2? comp = h.GetViewComponent(anyView);
                if (comp != null)
                {
                    double? fromPart = TryReadFromPartDoc(comp.GetModelDoc2() as IModelDoc2);
                    if (fromPart.HasValue)
                        return fromPart.Value;
                }

                anyView = anyView.GetNextView() as IView;
            }

            return null;
        }

        private static double? TryReadFromPartDoc(IModelDoc2? partDoc)
        {
            if (partDoc == null)
                return null;

            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                string typeName = feat.GetTypeName2();
                try
                {
                    if (typeName.Equals("BaseFlange", StringComparison.OrdinalIgnoreCase) ||
                        typeName.Equals("TabAndSlot", StringComparison.OrdinalIgnoreCase))
                    {
                        object? defObj = feat.GetDefinition();
                        if (defObj == null)
                            goto next;

                        if (defObj is IBaseFlangeFeatureData baseFlange)
                        {
                            baseFlange.AccessSelections(partDoc, null);
                            try
                            {
                                double t = baseFlange.Thickness;
                                if (t > MinThicknessMeters && t <= MaxThicknessMeters)
                                    return t;
                            }
                            finally
                            {
                                baseFlange.ReleaseSelectionAccess();
                            }
                        }
                    }
                    else if (typeName.Equals("SheetMetal", StringComparison.OrdinalIgnoreCase))
                    {
                        object? defObj = feat.GetDefinition();
                        if (defObj is ISheetMetalFeatureData smData)
                        {
                            smData.AccessSelections(partDoc, null);
                            try
                            {
                                double t = smData.Thickness;
                                if (t > MinThicknessMeters && t <= MaxThicknessMeters)
                                    return t;
                            }
                            finally
                            {
                                smData.ReleaseSelectionAccess();
                            }
                        }
                    }
                }
                catch
                {
                    // try next feature
                }

            next:
                feat = feat.GetNextFeature() as Feature;
            }

            return null;
        }

        private static bool TryAddBetweenParallelEdges(SmartDimHelper h, IView view, double? expected)
        {
            Edge[] linearEdges = GetAllOutlineEdges(h, view).Where(h.IsLinear).ToArray();
            if (linearEdges.Length < 2)
                return false;

            if (TryParallelPair(h, view, linearEdges, horizontal: true, expected))
                return true;

            return TryParallelPair(h, view, linearEdges, horizontal: false, expected);
        }

        private static bool TryParallelPair(
            SmartDimHelper h,
            IView view,
            Edge[] linearEdges,
            bool horizontal,
            double? expectedSheet)
        {
            double orientTol = 0.002;
            var oriented = linearEdges
                .Where(e => horizontal ? h.IsHorizontalInView(e, view, orientTol) : h.IsVerticalInView(e, view, orientTol))
                .ToArray();

            if (oriented.Length < 2)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double minSheet = MinThicknessMeters * scale;
            double maxSheet = MaxThicknessMeters * scale;

            Edge? edgeA = null;
            Edge? edgeB = null;
            double bestDistance = double.MaxValue;
            double bestScore = double.MaxValue;

            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double distance = ParallelEdgeDistance(h, view, oriented[i], oriented[j], horizontal);
                    if (distance < minSheet || distance > maxSheet)
                        continue;

                    double score = expectedSheet.HasValue
                        ? Math.Abs(distance - expectedSheet.Value)
                        : distance;

                    if (expectedSheet.HasValue &&
                        Math.Abs(distance - expectedSheet.Value) > expectedSheet.Value * 0.4 + 0.0002)
                        continue;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestDistance = distance;
                        edgeA = oriented[i];
                        edgeB = oriented[j];
                    }
                }
            }

            if (edgeA == null || edgeB == null)
                return false;

            return PlaceLinearDimension(h, view, edgeA, edgeB, horizontal, bestDistance);
        }

        private static bool TryAddFromShortestLinearEdge(SmartDimHelper h, IView view, double? expectedSheet)
        {
            Edge[] linearEdges = GetAllOutlineEdges(h, view).Where(h.IsLinear).ToArray();
            if (linearEdges.Length == 0)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double minSheet = MinThicknessMeters * scale;
            double maxSheet = MaxThicknessMeters * scale;

            Edge? thicknessEdge = null;
            double bestScore = double.MaxValue;

            foreach (Edge edge in linearEdges)
            {
                double len = h.GetProjectedLength(edge, view);
                if (len < minSheet || len > maxSheet)
                    continue;

                double score = expectedSheet.HasValue ? Math.Abs(len - expectedSheet.Value) : len;
                if (expectedSheet.HasValue &&
                    Math.Abs(len - expectedSheet.Value) > expectedSheet.Value * 0.4 + 0.0002)
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    thicknessEdge = edge;
                }
            }

            if (thicknessEdge == null)
                return false;

            double[] midPt = h.GetEdgeMidpointOnSheet(thicknessEdge, view);
            h.ClearSelection();
            if (!h.SelectEdge(thicknessEdge, view, false))
                return false;

            var dim = h.CreateLinearDimension(midPt[0] + DimOffset, midPt[1])
                ?? h.CreateDimension(midPt[0] + DimOffset, midPt[1]);
            h.ClearSelection();
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add("Thickness");
            return true;
        }

        /// <summary>
        /// Last resort for disc side views that only expose two rim circles (no linear edges):
        /// pick entities at the thin outline extremes via SelectByID.
        /// </summary>
        private static bool TryAddFromOutlineThinSpan(
            SmartDimHelper h,
            IView view,
            double? expectedSheet,
            double bboxThicknessSheet)
        {
            if (!TryGetViewOutlineSize(view, out double width, out double height) &&
                !FlatPlateViewAnalyzer.TryGetOutlineSize(view, out width, out height))
                return false;

            double thin = Math.Min(width, height);
            double wide = Math.Max(width, height);
            if (thin < MinThicknessMeters * Math.Max(view.ScaleDecimal, 1e-9))
                return false;

            double target = expectedSheet ?? bboxThicknessSheet;
            if (Math.Abs(thin - target) > Math.Max(target * 0.5, 0.0005) && expectedSheet.HasValue)
            {
                // Outline thin doesn't match expected — still try if aspect looks like a side view.
                if (wide / Math.Max(thin, 1e-12) < MinSideViewAspectRatio)
                    return false;
            }

            bool thinIsVertical = height <= width;
            if (view.GetOutline() is not double[] o || o.Length < 4)
                return false;

            double minX = Math.Min(o[0], o[2]);
            double maxX = Math.Max(o[0], o[2]);
            double minY = Math.Min(o[1], o[3]);
            double maxY = Math.Max(o[1], o[3]);
            double midX = (minX + maxX) / 2.0;
            double midY = (minY + maxY) / 2.0;

            h.ClearSelection();
            h.ActivateView(view);
            bool a, b;
            double tx, ty;
            if (thinIsVertical)
            {
                a = SelectEdgeAt(h, view, midX, minY);
                b = SelectEdgeAt(h, view, midX, maxY, append: true);
                tx = maxX + DimOffset;
                ty = midY;
            }
            else
            {
                a = SelectEdgeAt(h, view, minX, midY);
                b = SelectEdgeAt(h, view, maxX, midY, append: true);
                tx = midX;
                ty = maxY + DimOffset;
            }

            if (!a || !b)
                return false;

            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            h.ClearSelection();
            if (dim == null)
                return false;

            // Reject wrong-magnitude dims (e.g. accidentally OD).
            try
            {
                if (dim.GetDimension2(0) is Dimension md)
                {
                    double val = Math.Abs(md.SystemValue);
                    double scale = Math.Max(view.ScaleDecimal, 1e-9);
                    double expectedModel = (expectedSheet ?? thin) / scale;
                    if (val > MaxThicknessMeters ||
                        (expectedSheet.HasValue &&
                         Math.Abs(val - expectedModel) > expectedModel * 0.4 + 0.0005))
                    {
                        if (dim.GetAnnotation() is IAnnotation ann)
                        {
                            h.ClearSelection();
                            ann.Select3(false, null);
                            h.Model.Extension.DeleteSelection2(0);
                        }

                        return false;
                    }
                }
            }
            catch
            {
                // keep dim
            }

            h.DimensionedFeatures.Add("Thickness");
            return true;
        }

        private static bool SelectEdgeAt(SmartDimHelper h, IView view, double x, double y, bool append = false)
        {
            try
            {
                return h.Ext.SelectByID2(
                    string.Empty,
                    SmartDim.SmartDimConstants.EdgeSelectType,
                    x, y, 0.0,
                    append, 0, null, 0);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAddFromExtremeVertices(
            SmartDimHelper h,
            IView view,
            double? expected,
            double bboxThickness)
        {
            double target = expected ?? bboxThickness;
            if (target < MinThicknessMeters || target > MaxThicknessMeters)
                return false;

            Edge[] edges = GetAllOutlineEdges(h, view);
            var points = CollectSheetPoints(h, view, edges);
            if (points.Count < 2)
                return false;

            double minX = points.Min(p => p[0]);
            double maxX = points.Max(p => p[0]);
            double minY = points.Min(p => p[1]);
            double maxY = points.Max(p => p[1]);
            double width = maxX - minX;
            double height = maxY - minY;

            bool thicknessIsVertical = height <= width;
            double tol = Math.Max(0.0003, target * 0.15);

            if (thicknessIsVertical)
            {
                var low = points.Where(p => Math.Abs(p[1] - minY) <= tol).ToArray();
                var high = points.Where(p => Math.Abs(p[1] - maxY) <= tol).ToArray();
                return TryDimensionBetweenPointGroups(h, view, low, high, vertical: true, target);
            }

            var left = points.Where(p => Math.Abs(p[0] - minX) <= tol).ToArray();
            var right = points.Where(p => Math.Abs(p[0] - maxX) <= tol).ToArray();
            return TryDimensionBetweenPointGroups(h, view, left, right, vertical: false, target);
        }

        private static List<double[]> CollectSheetPoints(SmartDimHelper h, IView view, Edge[] edges)
        {
            var points = new List<double[]>();
            const double mergeTol = 0.00005;

            foreach (Edge edge in edges)
            {
                AddPoint(h, view, edge, points, mergeTol, useStart: true);
                AddPoint(h, view, edge, points, mergeTol, useStart: false);

                if (h.IsCircular(edge) && h.IsCircleProfileInView(edge, view))
                {
                    double[] center = h.GetCircleCenterOnSheet(edge, view);
                    double r = h.GetCircleRadius(edge) * view.ScaleDecimal;
                    points.Add(new[] { center[0], center[1] + r, 0.0 });
                    points.Add(new[] { center[0], center[1] - r, 0.0 });
                    points.Add(new[] { center[0] + r, center[1], 0.0 });
                    points.Add(new[] { center[0] - r, center[1], 0.0 });
                }
            }

            return points;
        }

        private static void AddPoint(
            SmartDimHelper h,
            IView view,
            Edge edge,
            List<double[]> points,
            double mergeTol,
            bool useStart)
        {
            Vertex? vtx = useStart
                ? edge.GetStartVertex() as Vertex
                : edge.GetEndVertex() as Vertex;

            if (vtx == null)
                return;

            double[] modelPt = (double[])vtx.GetPoint();
            double[] sheetPt = h.TransformToSheet(modelPt, view);

            foreach (double[] existing in points)
            {
                if (Math.Abs(existing[0] - sheetPt[0]) < mergeTol &&
                    Math.Abs(existing[1] - sheetPt[1]) < mergeTol)
                    return;
            }

            points.Add(sheetPt);
        }

        private static bool TryDimensionBetweenPointGroups(
            SmartDimHelper h,
            IView view,
            double[][] groupA,
            double[][] groupB,
            bool vertical,
            double target)
        {
            if (groupA.Length == 0 || groupB.Length == 0)
                return false;

            double bestScore = double.MaxValue;
            Edge? edgeA = null;
            Edge? edgeB = null;

            Edge[] edges = GetAllOutlineEdges(h, view);
            foreach (Edge edge in edges.Where(h.IsLinear))
            {
                var mid = h.GetEdgeMidpointOnSheet(edge, view);
                if (groupA.Any(p => Distance(p, mid) < 0.001))
                {
                    foreach (Edge other in edges.Where(h.IsLinear))
                    {
                        if (ReferenceEquals(edge, other))
                            continue;

                        var midOther = h.GetEdgeMidpointOnSheet(other, view);
                        if (!groupB.Any(p => Distance(p, midOther) < 0.001))
                            continue;

                        double dist = vertical
                            ? Math.Abs(mid[1] - midOther[1])
                            : Math.Abs(mid[0] - midOther[0]);

                        if (dist < MinThicknessMeters || dist > MaxThicknessMeters)
                            continue;

                        double score = Math.Abs(dist - target);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            edgeA = edge;
                            edgeB = other;
                        }
                    }
                }
            }

            if (edgeA == null || edgeB == null)
                return false;

            return PlaceLinearDimension(h, view, edgeA, edgeB, !vertical, target);
        }

        private static bool PlaceLinearDimension(
            SmartDimHelper h,
            IView view,
            Edge edgeA,
            Edge edgeB,
            bool horizontalEdges,
            double expectedDistance)
        {
            h.ClearSelection();
            if (!h.SelectEdge(edgeA, view, false))
                return false;
            if (!h.SelectEdge(edgeB, view, true))
            {
                h.ClearSelection();
                return false;
            }

            var (sA, _) = h.GetEdgeEndpointsOnSheet(edgeA, view);
            var (sB, _) = h.GetEdgeEndpointsOnSheet(edgeB, view);
            double dimX = horizontalEdges
                ? (sA[0] + sB[0]) / 2.0 + DimOffset
                : Math.Max(sA[0], sB[0]) + DimOffset;
            double dimY = horizontalEdges
                ? Math.Max(sA[1], sB[1]) + DimOffset
                : (sA[1] + sB[1]) / 2.0;

            var dim = h.CreateDimension(dimX, dimY);
            h.ClearSelection();
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add("Thickness");
            return true;
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

        private static double Distance(double[] a, double[] b) =>
            Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
    }
}
