using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>Overall thickness (gabarit) and step-depth dimensions on flange profile (side) views.</summary>
    internal static class FlangeGasketProfileDimensions
    {
        private const double MinFeatureMeters = 0.0001;
        private const double MaxSolidFlangeThicknessMeters = 0.25;
        private const double MaxStepDepthMeters = 0.035;
        private const double DimOffset = 0.010;
        private const double EnvelopeMinFraction = 0.80;
        private const double HalfThicknessTolerance = 0.04;

        public static void AddForProfileView(
            SmartDimHelper h,
            IModelDoc2 model,
            IView view,
            double? expectedThicknessMm,
            Action<string> log)
        {
            string viewName = view.GetName2();
            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            h.ClearViewCaches();
            Edge[] edges = GetOutlineEdges(h, view);
            Edge[] linear = edges.Where(h.IsLinear).ToArray();

            // Prefer long face-outline edges (flange OD projected as horizontal/vertical lines on side view).
            Edge[] longLinear = linear
                .Where(e => h.GetProjectedLength(e, view) >= 0.008)
                .ToArray();
            if (longLinear.Length >= 2)
                linear = longLinear;

            double minX, minY, maxX, maxY;
            if (linear.Length >= 2)
            {
                (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(linear, view);
            }
            else if (edges.Length > 0)
            {
                (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
                log($"  [{viewName}] Profile edges: {edges.Length} total, {linear.Length} linear — using full edge bbox.");
            }
            else if (view.GetOutline() is double[] outline && outline.Length >= 4)
            {
                minX = Math.Min(outline[0], outline[2]);
                maxX = Math.Max(outline[0], outline[2]);
                minY = Math.Min(outline[1], outline[3]);
                maxY = Math.Max(outline[1], outline[3]);
                log($"  [{viewName}] No selectable edges — using view outline for gabarit.");
            }
            else
            {
                log($"  [{viewName}] Warning: cannot resolve profile extents for gabarit thickness.");
                // Still strip half-thickness using EST alone.
                if (expectedThicknessMm is > 0)
                {
                    double expected = expectedThicknessMm.Value / 1000.0;
                    RemoveNonGabaritThicknessDimensions(h, model, view, expected, log);
                    RemoveHalfThicknessOnSiblingViews(h, model, view, expected, log);
                }

                return;
            }

            double widthSheet = maxX - minX;
            double heightSheet = maxY - minY;
            if (widthSheet <= 0 || heightSheet <= 0)
                return;

            bool thicknessVertical = heightSheet <= widthSheet;
            double bboxThicknessModel = Math.Round(Math.Min(widthSheet, heightSheet) / scale, 4);
            double overallThickness = ResolveOverallThickness(bboxThicknessModel, expectedThicknessMm, log, viewName);

            RemoveNonGabaritThicknessDimensions(h, model, view, overallThickness, log);
            RemoveHalfThicknessOnSiblingViews(h, model, view, overallThickness, log);

            bool placed = false;
            if (linear.Length >= 2)
            {
                placed = TryPlaceOverallThickness(
                    h, view, viewName, linear, minX, minY, maxX, maxY,
                    thicknessVertical, overallThickness, scale, log);
            }

            if (!placed)
            {
                placed = TryPlaceGabaritBySelectAtOutline(
                    h, view, viewName, minX, minY, maxX, maxY,
                    thicknessVertical, overallThickness, log);
            }

            if (!placed)
            {
                placed = TryPlaceGabaritByVertices(
                    h, view, viewName, edges, thicknessVertical, overallThickness, scale, log);
            }

            if (!placed)
            {
                log($"  [{viewName}] Warning: could not place gabarit thickness {overallThickness * 1000:F1} mm.");
                return;
            }

            if (linear.Length >= 2)
                TryPlaceStepDepths(h, view, viewName, linear, thicknessVertical, overallThickness, scale, log);
        }

        public static void TryAddOnce(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView? discFaceView,
            double? expectedThicknessMm,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("FlangeProfileThickness"))
                return;

            IView? profileView = FlangeGasketViewAnalyzer.FindProfileSideView(h, drawing, discFaceView);
            if (profileView == null)
            {
                // Last resort: any non-disc orthographic view with elongated outline.
                profileView = FindAnyElongatedSideView(h, drawing, discFaceView);
            }

            if (profileView == null)
            {
                log("  Warning: no profile side view found for flange thickness/steps.");
                return;
            }

            log($"  Profile view for thickness/steps: {profileView.GetName2()}.");
            AddForProfileView(h, model, profileView, expectedThicknessMm, log);
        }

        private static IView? FindAnyElongatedSideView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView? discFaceView)
        {
            IView? best = null;
            double bestAspect = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase) ||
                    (discFaceView != null && ReferenceEquals(view, discFaceView)) ||
                    FlangeGasketViewAnalyzer.IsDominantDiscFaceView(h, view))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (view.GetOutline() is double[] outline && outline.Length >= 4)
                {
                    double w = Math.Abs(outline[2] - outline[0]);
                    double ht = Math.Abs(outline[3] - outline[1]);
                    double aspect = Math.Max(w, ht) / Math.Max(Math.Min(w, ht), 1e-9);
                    if (aspect > bestAspect)
                    {
                        bestAspect = aspect;
                        best = view;
                    }
                }

                view = view.GetNextView() as IView;
            }

            return bestAspect >= 1.15 ? best : null;
        }

        private static double ResolveOverallThickness(
            double bboxThicknessMeters,
            double? expectedThicknessMm,
            Action<string> log,
            string viewName)
        {
            if (expectedThicknessMm is not > 0)
                return bboxThicknessMeters;

            double expected = expectedThicknessMm.Value / 1000.0;
            if (expected <= 0 || expected > MaxSolidFlangeThicknessMeters)
                return bboxThicknessMeters;

            // EST gabarit wins when bbox caught only a half-step feature (~half) or is close.
            bool expectedIsNearlyDouble =
                Math.Abs(expected - bboxThicknessMeters * 2.0) <= Math.Max(0.0015, expected * 0.04);
            bool expectedCloseToBBox =
                Math.Abs(expected - bboxThicknessMeters) <= Math.Max(0.0015, expected * 0.05);
            bool expectedLargerThanBBox = expected > bboxThicknessMeters * 1.08;

            if (expectedIsNearlyDouble || expectedLargerThanBBox || expectedCloseToBBox)
            {
                if (!expectedCloseToBBox)
                {
                    log($"  [{viewName}] Using EST gabarit thickness {expected * 1000:F1} mm " +
                        $"(bbox was {bboxThicknessMeters * 1000:F1} mm).");
                }

                return Math.Round(expected, 4);
            }

            return bboxThicknessMeters;
        }

        private static bool TryPlaceOverallThickness(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX,
            double minY,
            double maxX,
            double maxY,
            bool thicknessVertical,
            double overallThicknessModel,
            double scale,
            Action<string> log)
        {
            string key = $"FlangeOverallThk_{overallThicknessModel:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.DimensionedFeatures.Contains("FlangeProfileThickness"))
                return false;

            if (h.HasDimensionWithValueInDrawing(overallThicknessModel, (int)swDimensionType_e.swLinearDimension))
            {
                h.DimensionedFeatures.Add(key);
                h.DimensionedFeatures.Add("FlangeProfileThickness");
                log($"  [{viewName}] Overall thickness (gabarit) {overallThicknessModel * 1000:F1} mm (already on drawing).");
                return true;
            }

            Edge? edgeA;
            Edge? edgeB;
            double dimX;
            double dimY;

            if (thicknessVertical)
            {
                edgeA = FindEnvelopeEdge(h, view, linear, horizontal: true, target: maxY, axis: 1);
                edgeB = FindEnvelopeEdge(h, view, linear, horizontal: true, target: minY, axis: 1);
                dimX = maxX + DimOffset;
                dimY = (minY + maxY) / 2.0;
            }
            else
            {
                edgeA = FindEnvelopeEdge(h, view, linear, horizontal: false, target: minX, axis: 0);
                edgeB = FindEnvelopeEdge(h, view, linear, horizontal: false, target: maxX, axis: 0);
                dimX = (minX + maxX) / 2.0;
                dimY = maxY + DimOffset;
            }

            if (edgeA == null || edgeB == null || ReferenceEquals(edgeA, edgeB))
                return false;

            // ParallelDistance returns sheet meters — compare in model meters.
            double measuredModel = ParallelDistance(h, view, edgeA, edgeB, !thicknessVertical) / scale;
            if (measuredModel < overallThicknessModel * EnvelopeMinFraction &&
                Math.Abs(measuredModel - overallThicknessModel) > MinFeatureMeters)
            {
                if (Math.Abs(measuredModel - overallThicknessModel) > overallThicknessModel * 0.12)
                {
                    log($"  [{viewName}] Envelope span {measuredModel * 1000:F1} mm < gabarit {overallThicknessModel * 1000:F1} mm — retrying longest pair.");
                    if (!TryPlaceFromLongestThicknessPair(
                            h, view, linear, thicknessVertical, overallThicknessModel, scale,
                            out edgeA, out edgeB, out dimX, out dimY))
                        return false;
                }
            }

            h.ClearSelection();
            if (!h.SelectEdge(edgeA, view, false) || !h.SelectEdge(edgeB, view, true))
            {
                h.ClearSelection();
                return false;
            }

            DisplayDimension? dim = h.CreateDimension(dimX, dimY);
            h.ClearSelection();
            if (dim == null)
                return false;

            // If SW snapped to a half-feature, reject and delete.
            if (!IsLinearNearValue(dim, overallThicknessModel, overallThicknessModel * 0.08))
            {
                log($"  [{viewName}] Rejected non-gabarit thickness dim (wanted {overallThicknessModel * 1000:F1} mm).");
                TryDeleteDim(h, dim);
                return false;
            }

            h.DimensionedFeatures.Add(key);
            h.DimensionedFeatures.Add("FlangeProfileThickness");
            log($"  [{viewName}] Overall thickness (gabarit) {overallThicknessModel * 1000:F1} mm.");
            return true;
        }

        /// <summary>
        /// Fallback when visible linear edges are sparse (SW2025 silhouette-limited side views):
        /// pick EDGE entities at outline extremes via SelectByID2, then dimension them.
        /// </summary>
        private static bool TryPlaceGabaritBySelectAtOutline(
            SmartDimHelper h,
            IView view,
            string viewName,
            double minX,
            double minY,
            double maxX,
            double maxY,
            bool thicknessVertical,
            double overallThicknessModel,
            Action<string> log)
        {
            string key = $"FlangeOverallThk_{overallThicknessModel:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.DimensionedFeatures.Contains("FlangeProfileThickness"))
                return false;

            if (h.HasDimensionWithValueInDrawing(overallThicknessModel, (int)swDimensionType_e.swLinearDimension))
            {
                h.DimensionedFeatures.Add(key);
                h.DimensionedFeatures.Add("FlangeProfileThickness");
                log($"  [{viewName}] Overall thickness (gabarit) {overallThicknessModel * 1000:F1} mm (already on drawing).");
                return true;
            }

            h.Drawing.ActivateView(viewName);
            h.ClearSelection();

            double inset = Math.Max(0.0008, Math.Min(maxX - minX, maxY - minY) * 0.02);
            double dimX;
            double dimY;
            bool picked;

            if (thicknessVertical)
            {
                double midX = (minX + maxX) / 2.0;
                picked = TrySelectSheetEntity(h, midX, maxY - inset, append: false) &&
                         TrySelectSheetEntity(h, midX, minY + inset, append: true);
                dimX = maxX + DimOffset;
                dimY = (minY + maxY) / 2.0;
            }
            else
            {
                double midY = (minY + maxY) / 2.0;
                picked = TrySelectSheetEntity(h, minX + inset, midY, append: false) &&
                         TrySelectSheetEntity(h, maxX - inset, midY, append: true);
                dimX = (minX + maxX) / 2.0;
                dimY = maxY + DimOffset;
            }

            if (!picked)
            {
                h.ClearSelection();
                return false;
            }

            DisplayDimension? dim = h.CreateDimension(dimX, dimY);
            h.ClearSelection();
            if (dim == null)
                return false;

            if (!IsLinearNearValue(dim, overallThicknessModel, overallThicknessModel * 0.12))
            {
                log($"  [{viewName}] Outline-pick dim rejected (wanted gabarit {overallThicknessModel * 1000:F1} mm).");
                TryDeleteDim(h, dim);
                return false;
            }

            h.DimensionedFeatures.Add(key);
            h.DimensionedFeatures.Add("FlangeProfileThickness");
            log($"  [{viewName}] Overall thickness (gabarit) {overallThicknessModel * 1000:F1} mm (outline pick).");
            return true;
        }

        private static bool TrySelectSheetEntity(SmartDimHelper h, double sheetX, double sheetY, bool append)
        {
            // Prefer EDGE, then FACE — side views often expose silhouette as pickable EDGE via ray.
            if (h.Ext.SelectByID2(string.Empty, "EDGE", sheetX, sheetY, 0.0, append, 0, null, 0))
                return true;

            if (h.Ext.SelectByID2(string.Empty, "FACE", sheetX, sheetY, 0.0, append, 0, null, 0))
                return true;

            if (h.Ext.SelectByID2(string.Empty, "VERTEX", sheetX, sheetY, 0.0, append, 0, null, 0))
                return true;

            return false;
        }

        /// <summary>
        /// Last-resort gabarit: pick two vertices at opposite thickness extremes and dimension them.
        /// Works on imported solids where linear side edges are missing or unselectable.
        /// </summary>
        private static bool TryPlaceGabaritByVertices(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] edges,
            bool thicknessVertical,
            double overallThicknessModel,
            double scale,
            Action<string> log)
        {
            string key = $"FlangeOverallThk_{overallThicknessModel:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.DimensionedFeatures.Contains("FlangeProfileThickness"))
                return false;

            var vertices = new List<(Vertex v, double x, double y)>();
            foreach (Edge edge in edges)
            {
                try
                {
                    if (edge.GetStartVertex() is Vertex sv)
                    {
                        double[] sheet = h.TransformToSheet((double[])sv.GetPoint(), view);
                        vertices.Add((sv, sheet[0], sheet[1]));
                    }

                    if (edge.GetEndVertex() is Vertex ev)
                    {
                        double[] sheet = h.TransformToSheet((double[])ev.GetPoint(), view);
                        vertices.Add((ev, sheet[0], sheet[1]));
                    }
                }
                catch
                {
                    // ignore bad vertices
                }
            }

            if (vertices.Count < 2)
                return false;

            // Deduplicate by sheet position.
            var unique = vertices
                .GroupBy(p => (Math.Round(p.x, 4), Math.Round(p.y, 4)))
                .Select(g => g.First())
                .ToList();

            (Vertex a, Vertex b, double dimX, double dimY)? pair = null;
            double bestErr = double.MaxValue;

            for (int i = 0; i < unique.Count; i++)
            {
                for (int j = i + 1; j < unique.Count; j++)
                {
                    double dx = Math.Abs(unique[i].x - unique[j].x);
                    double dy = Math.Abs(unique[i].y - unique[j].y);
                    double spanModel = (thicknessVertical ? dy : dx) / scale;
                    double cross = thicknessVertical ? dx : dy;

                    // Thickness pair: span near gabarit, small lateral offset.
                    if (cross > Math.Max(0.01, (thicknessVertical ? dx + dy : dx + dy) * 0.35))
                        continue;

                    double err = Math.Abs(spanModel - overallThicknessModel);
                    if (err < bestErr && err <= overallThicknessModel * 0.15)
                    {
                        bestErr = err;
                        double dimX = thicknessVertical
                            ? Math.Max(unique[i].x, unique[j].x) + DimOffset
                            : (unique[i].x + unique[j].x) / 2.0;
                        double dimY = thicknessVertical
                            ? (unique[i].y + unique[j].y) / 2.0
                            : Math.Max(unique[i].y, unique[j].y) + DimOffset;
                        pair = (unique[i].v, unique[j].v, dimX, dimY);
                    }
                }
            }

            if (pair == null)
                return false;

            h.ClearSelection();
            if (!h.SelectVertex(pair.Value.a, view, false) || !h.SelectVertex(pair.Value.b, view, true))
            {
                h.ClearSelection();
                return false;
            }

            DisplayDimension? dim = h.CreateLinearDimension(pair.Value.dimX, pair.Value.dimY);
            h.ClearSelection();
            if (dim == null)
                return false;

            if (!IsLinearNearValue(dim, overallThicknessModel, overallThicknessModel * 0.12))
            {
                log($"  [{viewName}] Vertex gabarit rejected (wanted {overallThicknessModel * 1000:F1} mm).");
                TryDeleteDim(h, dim);
                return false;
            }

            h.DimensionedFeatures.Add(key);
            h.DimensionedFeatures.Add("FlangeProfileThickness");
            log($"  [{viewName}] Overall thickness (gabarit) {overallThicknessModel * 1000:F1} mm (vertices).");
            return true;
        }

        private static bool TryPlaceFromLongestThicknessPair(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool thicknessVertical,
            double overallThicknessModel,
            double scale,
            out Edge? edgeA,
            out Edge? edgeB,
            out double dimX,
            out double dimY)
        {
            edgeA = null;
            edgeB = null;
            dimX = 0;
            dimY = 0;

            bool horizontal = thicknessVertical;
            var oriented = linear
                .Where(e => horizontal ? h.IsHorizontalInView(e, view) : h.IsVerticalInView(e, view))
                .ToArray();

            double best = 0;
            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double distModel = ParallelDistance(h, view, oriented[i], oriented[j], horizontal) / scale;
                    if (distModel <= best)
                        continue;
                    if (Math.Abs(distModel - overallThicknessModel) > overallThicknessModel * 0.15 &&
                        distModel < overallThicknessModel * EnvelopeMinFraction)
                        continue;

                    best = distModel;
                    edgeA = oriented[i];
                    edgeB = oriented[j];
                }
            }

            if (edgeA == null || edgeB == null)
                return false;

            var midA = h.GetEdgeMidpointOnSheet(edgeA, view);
            var midB = h.GetEdgeMidpointOnSheet(edgeB, view);
            dimX = Math.Max(midA[0], midB[0]) + DimOffset;
            dimY = (midA[1] + midB[1]) / 2.0;
            return true;
        }

        private static bool IsLinearNearValue(DisplayDimension dim, double expectedMeters, double tolMeters)
        {
            try
            {
                Dimension? modelDim = dim.GetDimension2(0) as Dimension;
                if (modelDim == null)
                    return false;
                return Math.Abs(Math.Abs(modelDim.SystemValue) - Math.Abs(expectedMeters)) <= tolMeters;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteDim(SmartDimHelper h, DisplayDimension dim)
        {
            try
            {
                Annotation? ann = dim.GetAnnotation() as Annotation;
                if (ann == null)
                    return;
                h.Model.ClearSelection2(true);
                ann.Select3(false, null);
                h.Model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                h.Model.ClearSelection2(true);
            }
            catch
            {
                // ignore
            }
        }

        private static void TryPlaceStepDepths(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            bool thicknessVertical,
            double overallThickness,
            double scale,
            Action<string> log)
        {
            var gaps = CollectThinDirectionGaps(h, view, linear, thicknessVertical);
            if (gaps.Count == 0)
                return;

            double maxStep = Math.Min(MaxStepDepthMeters, overallThickness * 0.35);
            double halfThickness = overallThickness / 2.0;

            var stepGaps = gaps
                .Select(g => g with { Distance = g.Distance / scale }) // sheet → model
                .Where(g => g.Distance >= MinFeatureMeters && g.Distance <= maxStep)
                .Where(g => Math.Abs(g.Distance - halfThickness) > overallThickness * HalfThicknessTolerance)
                .Where(g => Math.Abs(g.Distance - overallThickness) > MinFeatureMeters)
                .GroupBy(g => Math.Round(g.Distance, 4))
                .OrderBy(g => g.Key)
                .ToList();

            int stepIndex = 0;
            foreach (var group in stepGaps)
            {
                double step = group.Key;
                string stepKey = $"FlangeStep_{step:F4}_{viewName}_{stepIndex}";
                if (h.DimensionedFeatures.Contains(stepKey))
                    continue;

                if (h.HasDimensionWithValueInDrawing(step, (int)swDimensionType_e.swLinearDimension))
                    continue;

                ParallelGap gap = group.First();
                if (!PlaceGapDimension(h, view, gap, gap.IsHorizontal, belowView: false))
                    continue;

                h.DimensionedFeatures.Add(stepKey);
                log($"  [{viewName}] Step / groove depth {step * 1000:F1} mm.");
                stepIndex++;
            }
        }

        private static void RemoveHalfThicknessOnSiblingViews(
            SmartDimHelper h,
            IModelDoc2 model,
            IView profileView,
            double overallThickness,
            Action<string> log)
        {
            IView? view = (h.Drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!ReferenceEquals(view, profileView) &&
                    !view.GetName2().Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase) &&
                    !FlangeGasketViewAnalyzer.IsDominantDiscFaceView(h, view))
                {
                    RemoveNonGabaritThicknessDimensions(h, model, view, overallThickness, log);
                }

                view = view.GetNextView() as IView;
            }
        }

        /// <summary>
        /// Removes imported half-thickness dims (e.g. 54.3 when gabarit is 108.6)
        /// and near-duplicates that are not the true gabarit.
        /// </summary>
        private static void RemoveNonGabaritThicknessDimensions(
            SmartDimHelper h,
            IModelDoc2 model,
            IView view,
            double overallThickness,
            Action<string> log)
        {
            if (overallThickness < MinFeatureMeters * 2)
                return;

            double half = overallThickness / 2.0;
            double halfTol = Math.Max(SmartDimConstants.DimensionValueToleranceMeters, overallThickness * HalfThicknessTolerance);

            int removedHalf = h.DeleteLinearDimensionsNearValue(model, view, half, halfTol);
            if (removedHalf > 0)
                log($"  [{view.GetName2()}] Removed {removedHalf} half-thickness dim(s) (~{half * 1000:F1} mm).");

            // Also drop other linear thickness-like dims that are below gabarit but above small steps
            // (incomplete model import measuring a face without the raised-face step).
            double looseTol = Math.Max(0.0008, overallThickness * 0.03);
            int removedPartial = 0;
            foreach (double candidate in CollectLinearValuesInView(h, view))
            {
                if (Math.Abs(candidate - overallThickness) <= looseTol)
                    continue;
                if (candidate <= MaxStepDepthMeters)
                    continue;
                if (candidate >= overallThickness * 0.95)
                    continue;
                if (candidate < overallThickness * 0.35)
                    continue;

                removedPartial += h.DeleteLinearDimensionsNearValue(model, view, candidate, looseTol);
            }

            if (removedPartial > 0)
                log($"  [{view.GetName2()}] Removed {removedPartial} partial thickness dim(s) (keeping gabarit {overallThickness * 1000:F1} mm).");
        }

        private static List<double> CollectLinearValuesInView(SmartDimHelper h, IView view)
        {
            var values = new List<double>();
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    Dimension? modelDim = displayDim?.GetDimension2(0) as Dimension;
                    if (displayDim != null &&
                        modelDim != null &&
                        displayDim.Type2 == (int)swDimensionType_e.swLinearDimension)
                    {
                        values.Add(Math.Abs(modelDim.SystemValue));
                    }
                }

                ann = ann.GetNext3();
            }

            return values;
        }

        private static Edge? FindEnvelopeEdge(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool horizontal,
            double target,
            int axis)
        {
            Edge? best = null;
            double bestScore = double.MinValue;

            foreach (Edge edge in linear)
            {
                if (horizontal ? !h.IsHorizontalInView(edge, view) : !h.IsVerticalInView(edge, view))
                    continue;

                var (s, e) = h.GetEdgeEndpointsOnSheet(edge, view);
                double edgeLen = axis == 0
                    ? Math.Abs(s[1] - e[1])
                    : Math.Abs(s[0] - e[0]);
                double avgCoord = axis == 0
                    ? (s[0] + e[0]) / 2.0
                    : (s[1] + e[1]) / 2.0;

                double score = -Math.Abs(avgCoord - target) + edgeLen * 0.1;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = edge;
                }
            }

            return best;
        }

        private static List<ParallelGap> CollectThinDirectionGaps(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool thicknessVertical)
        {
            var gaps = new List<ParallelGap>();
            CollectOrientedGaps(h, view, linear, horizontal: thicknessVertical, gaps);
            return gaps;
        }

        private static void CollectOrientedGaps(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool horizontal,
            List<ParallelGap> gaps)
        {
            var oriented = linear
                .Where(e => horizontal ? h.IsHorizontalInView(e, view) : h.IsVerticalInView(e, view))
                .ToArray();

            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double dist = ParallelDistance(h, view, oriented[i], oriented[j], horizontal);
                    if (dist < MinFeatureMeters)
                        continue;

                    gaps.Add(new ParallelGap(oriented[i], oriented[j], dist, horizontal));
                }
            }
        }

        private static bool PlaceGapDimension(
            SmartDimHelper h,
            IView view,
            ParallelGap gap,
            bool horizontal,
            bool belowView)
        {
            h.ClearSelection();
            if (!h.SelectEdge(gap.EdgeA, view, false))
                return false;
            if (!h.SelectEdge(gap.EdgeB, view, true))
            {
                h.ClearSelection();
                return false;
            }

            var midA = h.GetEdgeMidpointOnSheet(gap.EdgeA, view);
            var midB = h.GetEdgeMidpointOnSheet(gap.EdgeB, view);
            double dimX = horizontal
                ? (midA[0] + midB[0]) / 2.0 + DimOffset
                : Math.Max(midA[0], midB[0]) + DimOffset;
            double dimY = horizontal
                ? Math.Max(midA[1], midB[1]) + (belowView ? DimOffset : DimOffset * 0.5)
                : (midA[1] + midB[1]) / 2.0 - (belowView ? DimOffset : 0);

            DisplayDimension? dim = h.CreateDimension(dimX, dimY);
            h.ClearSelection();
            return dim != null;
        }

        private static Edge[] GetOutlineEdges(SmartDimHelper h, IView view)
        {
            // Prefer visible model edges only — silhouette API is slow/unstable on SW2025 HLV.
            return h.GetViewEdgesCached(view);
        }

        private static double ParallelDistance(
            SmartDimHelper h,
            IView view,
            Edge a,
            Edge b,
            bool horizontal)
        {
            var midA = h.GetEdgeMidpointOnSheet(a, view);
            var midB = h.GetEdgeMidpointOnSheet(b, view);
            return horizontal
                ? Math.Abs(midA[1] - midB[1])
                : Math.Abs(midA[0] - midB[0]);
        }

        private sealed record ParallelGap(Edge EdgeA, Edge EdgeB, double Distance, bool IsHorizontal);
    }
}
