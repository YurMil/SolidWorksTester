using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>
    /// Smart dimensions for flat sheet-metal plates with a rounded end profile.
    /// Primary face view: overall W×H, outer arc Ø, hole Ø + positions, bottom tip.
    /// </summary>
    internal static class RoundedFlatPlateDimensions
    {
        private const double MinHoleDiameterMeters = 0.001;
        private const double MaxHoleDiameterMeters = 0.12;
        private const double MinProfileArcRadiusMeters = 0.02;
        private const double DimOffset = 0.012;
        private const double CenterBucketMeters = 0.0005; // 0.5 mm
        private const double OrientTol = 0.004;

        public static void AddForPrimaryView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
                return;

            string viewName = view.GetName2();
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            var linear = edges.Where(h.IsLinear).ToArray();

            TryOverallHeight(h, view, viewName, linear, minX, minY, maxX, maxY, scale, log);
            TryOverallWidth(h, view, viewName, linear, minX, minY, maxX, maxY, scale, log);
            TryOuterArcDiameter(h, view, viewName, log);
            TryBottomTipWidth(h, view, viewName, linear, minX, minY, maxX, maxY, scale, log);
            SmartDimFillets.Add(h, view, log);
            SmartDimChamfers.Add(h, view, log);
            AddHoleDimensions(h, drawing, view, edges, viewName, minX, minY, maxX, maxY, log);
        }

        /// <summary>Side views only — thickness when primary view used model import.</summary>
        public static void AddSideViewOnly(
            SmartDimHelper h,
            IView view,
            Action<string> log,
            double? expectedThicknessMm = null)
        {
            SmartDimThickness.Add(h, view, log, expectedThicknessMm);
        }

        public static void AddThicknessOnce(SmartDimHelper h, IDrawingDoc drawing, Action<string> log) =>
            RoundFlatPlateThickness.TryAddOnce(h, drawing, log);

        private static void TryOverallHeight(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double scale,
            Action<string> log)
        {
            double heightModel = Math.Round(Math.Abs(maxY - minY) / scale, 4);
            string key = "RoundedOverall_H";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(heightModel))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            // 1) Chord / longest upright edge (segment plates often have ONLY this linear edge).
            Edge? chord = RoundedFlatPlateViewAnalyzer.GetLongestChordEdge(h, view, linear);
            if (chord != null)
            {
                double chordModel = Math.Round(h.GetProjectedLength(chord, view) / scale, 4);
                // Prefer chord when it spans most of the bbox height (segment / rounded-end back).
                bool chordIsHeight = chordModel >= heightModel * 0.75 ||
                    h.IsVerticalInView(chord, view, OrientTol);

                if (chordIsHeight && chordModel >= 0.02 &&
                    (TryDimensionSingleEdge(
                         h, view, chord,
                         minX - DimOffset, (minY + maxY) / 2.0) ||
                     TryDimensionEdgeByVertices(
                         h, view, chord,
                         minX - DimOffset, (minY + maxY) / 2.0)))
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Overall height {chordModel * 1000:F1} mm (chord).");
                    return;
                }
            }

            Edge? tallVert = linear
                .Where(e => h.IsVerticalInView(e, view, OrientTol))
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            if (tallVert != null && !ReferenceEquals(tallVert, chord))
            {
                double vertModel = Math.Round(h.GetProjectedLength(tallVert, view) / scale, 4);
                if (vertModel >= 0.02 &&
                    (TryDimensionSingleEdge(
                         h, view, tallVert,
                         minX - DimOffset, (minY + maxY) / 2.0) ||
                     TryDimensionEdgeByVertices(
                         h, view, tallVert,
                         minX - DimOffset, (minY + maxY) / 2.0)))
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Overall height {vertModel * 1000:F1} mm (vertical edge).");
                    return;
                }
            }

            Edge? topEdge = FindBoundaryEdge(h, linear, view, maxY, horizontal: true, preferLong: true);
            Edge? bottomEdge = FindBoundaryEdge(h, linear, view, minY, horizontal: true, preferLong: false);

            if (topEdge != null && bottomEdge != null && !ReferenceEquals(topEdge, bottomEdge))
            {
                h.ClearSelection();
                if (h.SelectEdge(topEdge, view, false) && h.SelectEdge(bottomEdge, view, true))
                {
                    var dim = h.CreateLinearDimension(maxX + DimOffset, (minY + maxY) / 2.0)
                        ?? h.CreateDimension(maxX + DimOffset, (minY + maxY) / 2.0);
                    if (dim != null)
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  [{viewName}] Overall height {heightModel * 1000:F1} mm.");
                        h.ClearSelection();
                        return;
                    }
                }
            }

            if (TryOutlinePick(h, view, minX, minY, maxX, maxY, horizontalSpan: false, key, heightModel, log))
            {
                log($"  [{viewName}] Overall height {heightModel * 1000:F1} mm (outline pick).");
                return;
            }

            log($"  [{viewName}] WARNING: overall height not placed.");
        }

        private static void TryOverallWidth(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double scale,
            Action<string> log)
        {
            string key = "RoundedOverall_W";
            if (h.DimensionedFeatures.Contains(key))
                return;

            double bboxWidthModel = Math.Round(Math.Abs(maxX - minX) / scale, 4);
            Edge? leftEdge = FindExtremeVertical(
                h, linear, view, rightmost: false, preferUpper: true, midY: (minY + maxY) / 2.0);
            Edge? rightEdge = FindExtremeVertical(
                h, linear, view, rightmost: true, preferUpper: true, midY: (minY + maxY) / 2.0);
            Edge? outerArc = RoundedFlatPlateViewAnalyzer.GetOuterProfileArc(h, view);

            // Segment plates: single chord may not pass strict "vertical" — use longest linear.
            leftEdge ??= RoundedFlatPlateViewAnalyzer.GetLongestChordEdge(h, view, linear);

            // 1) Top horizontal ONLY when it spans ~full bbox (square-end plates).
            //    Side-lobe plates have a short top edge (<< overall to arc tip) — skip it.
            Edge? topEdge = FindBoundaryEdge(h, linear, view, maxY, horizontal: true, preferLong: true);
            if (topEdge != null)
            {
                double topWidthModel = Math.Round(h.GetProjectedLength(topEdge, view) / scale, 4);
                bool spansOverall = bboxWidthModel < 0.01 ||
                    topWidthModel >= bboxWidthModel * 0.85;
                if (spansOverall && topWidthModel >= 0.01 &&
                    (h.HasDimensionWithValueInDrawing(topWidthModel) ||
                     TryDimensionSingleEdge(
                         h, view, topEdge,
                         (minX + maxX) / 2.0, maxY + DimOffset)))
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Overall width {topWidthModel * 1000:F1} mm (top edge).");
                    return;
                }
            }

            // 2) Two distinct verticals (parallel sides).
            if (leftEdge != null && rightEdge != null && !ReferenceEquals(leftEdge, rightEdge))
            {
                double[] lMid = h.GetEdgeMidpointOnSheet(leftEdge, view);
                double[] rMid = h.GetEdgeMidpointOnSheet(rightEdge, view);
                double widthModel = Math.Round(Math.Abs(rMid[0] - lMid[0]) / scale, 4);

                if (h.HasDimensionWithValueInDrawing(widthModel))
                {
                    h.DimensionedFeatures.Add(key);
                    return;
                }

                h.ClearSelection();
                if (h.SelectEdge(leftEdge, view, false) && h.SelectEdge(rightEdge, view, true))
                {
                    double tx = (lMid[0] + rMid[0]) / 2.0;
                    double ty = Math.Max(lMid[1], rMid[1]) + DimOffset;
                    if (h.CreateLinearDimension(tx, ty) != null || h.CreateDimension(tx, ty) != null)
                    {
                        h.DimensionedFeatures.Add(key);
                        log($"  [{viewName}] Overall width {widthModel * 1000:F1} mm.");
                        h.ClearSelection();
                        return;
                    }
                }
            }

            // 3) Chord/left → outer arc tip (Shift / ArcEndCondition Max or Min).
            if (leftEdge != null && outerArc != null)
            {
                double arcRadius = h.GetCircleRadius(outerArc);
                double tipFromPair = Math.Round(
                    h.EstimateChordToArcTipWidthModel(leftEdge, outerArc, view), 4);

                // Two geometries:
                // - Circular segment: tip/sagitta ≤ R (e.g. 407 on D2350).
                // - Body + side lobe: overall left→tip >> R (e.g. 275 with R75) — use full bbox.
                bool segmentSagitta = tipFromPair > 0.005 && tipFromPair <= arcRadius * 1.15;
                double expectedWidth = segmentSagitta
                    ? tipFromPair
                    : Math.Round(bboxWidthModel, 4);

                // Prefer EST Dim2 when it matches the overall-to-tip case.
                // (Caller may not pass it — bbox is enough for 275.)

                if (expectedWidth >= 0.005 &&
                    h.HasDimensionWithValueInDrawing(expectedWidth))
                {
                    h.DimensionedFeatures.Add(key);
                    return;
                }

                double[] lMid = h.GetEdgeMidpointOnSheet(leftEdge, view);
                double[] arcC = h.GetCircleCenterOnSheet(outerArc, view);
                double tx = (lMid[0] + maxX) / 2.0;
                double ty = Math.Max(lMid[1], arcC[1]) + DimOffset;

                if (expectedWidth >= 0.005 &&
                    TryWidthToArcMaxTangent(
                        h, view, leftEdge, outerArc, expectedWidth, arcRadius,
                        segmentSagitta, tx, ty, log, viewName))
                {
                    h.DimensionedFeatures.Add(key);
                    return;
                }
            }

            if (h.HasDimensionWithValueInDrawing(bboxWidthModel))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            // 4) Fallback outline — only accept if value matches bbox tip width.
            if (leftEdge != null)
            {
                double[] lMid = h.GetEdgeMidpointOnSheet(leftEdge, view);
                if (TryOutlinePick(h, view, lMid[0], minY, maxX, maxY, horizontalSpan: true, key, bboxWidthModel, log) &&
                    h.HasDimensionWithValueInDrawing(bboxWidthModel))
                {
                    log($"  [{viewName}] Overall width {bboxWidthModel * 1000:F1} mm (outline pick).");
                    return;
                }
            }

            log($"  [{viewName}] WARNING: overall width not placed.");
        }

        /// <summary>
        /// Line → arc overall width using arc MAX/MIN end condition (Shift-equivalent).
        /// </summary>
        /// <param name="segmentSagitta">
        /// True for chord→tip on a circular segment (value ≤ R).
        /// False for body left-edge → lobe tip (value often >> R, e.g. 275 with R75).
        /// </param>
        private static bool TryWidthToArcMaxTangent(
            SmartDimHelper h,
            IView view,
            Edge leftEdge,
            Edge outerArc,
            double expectedWidthModel,
            double arcRadiusModel,
            bool segmentSagitta,
            double textX,
            double textY,
            Action<string> log,
            string viewName)
        {
            h.ClearSelection();
            if (!h.SelectEdge(leftEdge, view, false) || !h.SelectEdge(outerArc, view, true))
                return false;

            DisplayDimension? dim =
                h.CreateLinearDimension(textX, textY) ?? h.CreateDimension(textX, textY);
            if (dim == null)
                return false;

            Dimension? modelDim = dim.GetDimension2(0) as Dimension;
            if (modelDim == null)
            {
                TryDeleteDisplayDimension(h, dim);
                return false;
            }

            int max = (int)swArcEndCondition_e.swArcEndConditionMax;
            int min = (int)swArcEndCondition_e.swArcEndConditionMin;
            int center = (int)swArcEndCondition_e.swArcEndConditionCenter;
            int none = (int)swArcEndCondition_e.swArcEndConditionNone;

            (int, int)[] trials =
            {
                (none, max),
                (center, max),
                (max, none),
                (max, center),
                (none, min),
                (center, min),
                (min, none),
                (min, center),
                (max, max),
                (min, min)
            };

            double bestVal = double.MaxValue;
            double bestErr = double.MaxValue;
            (int, int) bestPair = (none, max);
            double maxPlausible = segmentSagitta
                ? arcRadiusModel * 1.25
                : Math.Max(expectedWidthModel * 1.35, arcRadiusModel * 2.5);

            foreach ((int a, int b) in trials)
            {
                try
                {
                    modelDim.SetArcEndCondition(a, b);
                    h.Model.ForceRebuild3(false);
                    double val = Math.Abs(modelDim.SystemValue);
                    if (val < 0.002 || val > maxPlausible)
                        continue;

                    double err = Math.Abs(val - expectedWidthModel);
                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestVal = val;
                        bestPair = (a, b);
                    }
                }
                catch
                {
                    // try next
                }
            }

            double tol = Math.Max(0.004, expectedWidthModel * 0.05);
            if (bestErr <= tol)
            {
                try
                {
                    modelDim.SetArcEndCondition(bestPair.Item1, bestPair.Item2);
                    h.Model.ForceRebuild3(false);
                }
                catch { /* keep current */ }

                log($"  [{viewName}] Overall width {bestVal * 1000:F1} mm (arc max/min / Shift).");
                h.ClearSelection();
                return true;
            }

            double leftover = Math.Abs(modelDim.SystemValue);
            log($"  [{viewName}] Arc width dim was {leftover * 1000:F1} mm (wanted {expectedWidthModel * 1000:F1}); deleted.");
            TryDeleteDisplayDimension(h, dim);
            h.ClearSelection();
            return false;
        }

        private static void TryDeleteDisplayDimension(SmartDimHelper h, DisplayDimension dim)
        {
            try
            {
                h.ClearSelection();
                if (dim.GetAnnotation() is IAnnotation ann)
                {
                    ann.Select3(false, null);
                    h.Model.EditDelete();
                }
            }
            catch
            {
                // best-effort cleanup
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static bool TryDimensionSingleEdge(
            SmartDimHelper h,
            IView view,
            Edge edge,
            double textX,
            double textY)
        {
            h.ClearSelection();
            if (!h.SelectEdge(edge, view, false))
                return false;

            bool ok = h.CreateLinearDimension(textX, textY) != null ||
                      h.CreateDimension(textX, textY) != null;
            h.ClearSelection();
            return ok;
        }

        private static bool TryDimensionEdgeByVertices(
            SmartDimHelper h,
            IView view,
            Edge edge,
            double textX,
            double textY)
        {
            try
            {
                Vertex? a = edge.GetStartVertex() as Vertex;
                Vertex? b = edge.GetEndVertex() as Vertex;
                if (a == null || b == null)
                    return false;

                h.ClearSelection();
                if (!h.SelectVertex(a, view, false) || !h.SelectVertex(b, view, true))
                    return false;

                bool ok = h.CreateLinearDimension(textX, textY) != null ||
                          h.CreateDimension(textX, textY) != null;
                h.ClearSelection();
                return ok;
            }
            catch
            {
                h.ClearSelection();
                return false;
            }
        }

        /// <summary>
        /// Short flat at the rounded tip (e.g. 20 mm) — horizontal edge near the bottom extreme.
        /// </summary>
        private static void TryBottomTipWidth(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] linear,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double scale,
            Action<string> log)
        {
            string key = "RoundedBottomTip";
            if (h.DimensionedFeatures.Contains(key))
                return;

            double outlineH = Math.Abs(maxY - minY);
            double outlineW = Math.Abs(maxX - minX);
            if (outlineH < 1e-6 || outlineW < 1e-6)
                return;

            // Tip flats sit near the bottom; much shorter than the plate width.
            Edge? tip = linear
                .Where(e => h.IsHorizontalInView(e, view, OrientTol))
                .Select(e =>
                {
                    double[] mid = h.GetEdgeMidpointOnSheet(e, view);
                    double len = h.GetProjectedLength(e, view);
                    return (Edge: e, Mid: mid, Len: len, ModelLen: len / scale);
                })
                .Where(t => Math.Abs(t.Mid[1] - minY) <= Math.Max(0.004, outlineH * 0.08))
                .Where(t => t.ModelLen >= 0.002 && t.ModelLen <= outlineW / scale * 0.55)
                .OrderBy(t => t.Len)
                .Select(t => t.Edge)
                .FirstOrDefault();

            if (tip == null)
                return;

            double tipModel = Math.Round(h.GetProjectedLength(tip, view) / scale, 4);
            if (h.HasDimensionWithValueInDrawing(tipModel))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            double[] mid = h.GetEdgeMidpointOnSheet(tip, view);
            h.ClearSelection();
            if (!h.SelectEdge(tip, view, false))
                return;

            var dim = h.CreateLinearDimension(mid[0], mid[1] - DimOffset)
                ?? h.CreateDimension(mid[0], mid[1] - DimOffset);
            if (dim == null)
                return;

            h.DimensionedFeatures.Add(key);
            log($"  [{viewName}] Bottom tip {tipModel * 1000:F1} mm.");
            h.ClearSelection();
        }

        private static void TryOuterArcDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            Action<string> log)
        {
            Edge? outerArc = RoundedFlatPlateViewAnalyzer.GetOuterProfileArc(h, view);
            if (outerArc == null)
            {
                log($"  [{viewName}] WARNING: outer profile arc not found.");
                return;
            }

            double radius = h.GetCircleRadius(outerArc);
            double diameter = Math.Round(radius * 2.0, 4);
            if (diameter < MinProfileArcRadiusMeters * 2.0)
                return;

            string key = $"RoundedArc_OD_{diameter:F4}";
            if (h.DimensionedFeatures.Contains(key) ||
                h.HasDimensionWithValueInDrawing(diameter) ||
                h.HasDimensionWithValueInDrawing(radius))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            double[] center = h.GetCircleCenterOnSheet(outerArc, view);
            double tipX = center[0];
            // Place callout near the arc bulge (toward the thin side of the bbox).
            var (minX, _, maxX, _) = h.ComputeEdgesBoundingBox(h.GetViewEdgesCached(view), view);
            tipX = Math.Abs(maxX - center[0]) >= Math.Abs(center[0] - minX)
                ? maxX - DimOffset
                : minX + DimOffset;

            DisplayDimension? dim = h.CreateDiameterDimension(
                outerArc,
                view,
                tipX,
                center[1] + DimOffset);

            if (dim == null)
            {
                h.ClearSelection();
                if (h.SelectEdge(outerArc, view, false))
                {
                    dim = h.Model.AddRadialDimension2(tipX, center[1] + DimOffset, 0.0) as DisplayDimension;
                }
            }

            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Outer arc Ø{diameter * 1000:F1} mm (R{radius * 1000:F1}).");
            }
            else
            {
                log($"  [{viewName}] WARNING: outer arc dimension failed (Ø{diameter * 1000:F0}).");
            }

            h.ClearSelection();
        }

        private static void AddHoleDimensions(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] edges,
            string viewName,
            double minX,
            double minY,
            double maxX,
            double maxY,
            Action<string> log)
        {
            var holeCandidates = edges
                .Where(h.IsCircular)
                .Where(e => IsHoleLikeEdge(h, view, e))
                .Where(e =>
                {
                    double d = h.GetCircleRadius(e) * 2.0;
                    return d >= MinHoleDiameterMeters && d <= MaxHoleDiameterMeters;
                })
                .ToArray();

            if (holeCandidates.Length == 0)
                return;

            var linear = edges.Where(h.IsLinear).ToArray();
            // Use extreme verticals by edge X — not bbox min/max (arc often owns maxX).
            Edge? leftBound = FindExtremeVertical(h, linear, view, rightmost: false, preferUpper: true, midY: (minY + maxY) / 2.0)
                ?? FindBoundaryEdge(h, linear, view, minX, horizontal: false, preferLong: true);
            Edge? rightBound = FindExtremeVertical(h, linear, view, rightmost: true, preferUpper: true, midY: (minY + maxY) / 2.0);
            Edge? topBound = FindBoundaryEdge(h, linear, view, maxY, horizontal: true, preferLong: true);
            Edge? bottomBound = FindBoundaryEdge(h, linear, view, minY, horizontal: true, preferLong: false);

            var groups = GroupByDiameter(holeCandidates, h);
            int groupIndex = 0;
            int centerMarks = 0;

            foreach (var kvp in groups.OrderBy(g => g.Key))
            {
                double diameter = kvp.Key;
                List<Edge> uniqueHoles = UniqueBySheetCenter(h, view, kvp.Value);
                if (uniqueHoles.Count == 0)
                    continue;

                string holeKey = $"RoundedHole_{diameter:F4}";
                if (!h.DimensionedFeatures.Contains(holeKey))
                {
                    Edge representative = uniqueHoles.FirstOrDefault(h.IsFullCircle) ??
                        uniqueHoles.OrderByDescending(e => h.GetProjectedLength(e, view)).First();

                    double[] center = h.GetCircleCenterOnSheet(representative, view);
                    h.ClearSelection();

                    DisplayDimension? dim = h.CreateDiameterDimension(
                        representative,
                        view,
                        center[0] + DimOffset + groupIndex * 0.004,
                        center[1] + DimOffset);

                    if (dim != null)
                    {
                        if (uniqueHoles.Count > 1)
                        {
                            dim.SetText(
                                (int)swDimensionTextParts_e.swDimensionTextPrefix,
                                $"{uniqueHoles.Count}x ");
                        }

                        h.DimensionedFeatures.Add(holeKey);
                        log($"  [{viewName}] Hole Ø{diameter * 1000:F1} mm × {uniqueHoles.Count}.");
                        groupIndex++;
                    }
                }

                // Sort by Y descending (top hole first) — matches typical plate layout.
                List<Edge> ordered = uniqueHoles
                    .OrderByDescending(e => h.GetCircleCenterOnSheet(e, view)[1])
                    .ThenBy(e => h.GetCircleCenterOnSheet(e, view)[0])
                    .ToList();

                string posKey = $"RoundedHolePos_{diameter:F4}";
                if (!h.DimensionedFeatures.Contains(posKey))
                {
                    bool anyPos = false;

                    // Vertical positions: try nearer bound first, then the other (not else-if).
                    double[] holeC = h.GetCircleCenterOnSheet(ordered[0], view);
                    bool nearerTop = Math.Abs(maxY - holeC[1]) <= Math.Abs(holeC[1] - minY);

                    Edge? firstVertBound = nearerTop ? topBound : bottomBound;
                    Edge? secondVertBound = nearerTop ? bottomBound : topBound;
                    string firstLabel = nearerTop ? "top" : "bottom";
                    string secondLabel = nearerTop ? "bottom" : "top";

                    if (firstVertBound != null && TryDimEdgeToHole(
                            h, view, firstVertBound, ordered[0],
                            maxX + DimOffset,
                            (holeC[1] + (nearerTop ? maxY : minY)) / 2.0))
                    {
                        log($"  [{viewName}] Hole from {firstLabel} for Ø{diameter * 1000:F1} mm.");
                        anyPos = true;
                    }
                    else if (secondVertBound != null && TryDimEdgeToHole(
                            h, view, secondVertBound, ordered[0],
                            maxX + DimOffset,
                            (holeC[1] + (nearerTop ? minY : maxY)) / 2.0))
                    {
                        log($"  [{viewName}] Hole from {secondLabel} for Ø{diameter * 1000:F1} mm.");
                        anyPos = true;
                    }
                    else
                    {
                        // Outline pick: horizontal edge band → hole center.
                        if (TryHoleVerticalOutlinePick(h, view, ordered[0], minX, minY, maxX, maxY, nearerTop))
                        {
                            log($"  [{viewName}] Hole vertical (outline) for Ø{diameter * 1000:F1} mm.");
                            anyPos = true;
                        }
                        else
                        {
                            log($"  [{viewName}] WARNING: hole vertical position not placed for Ø{diameter * 1000:F1} mm.");
                        }
                    }

                    // (3) Spacing between consecutive holes along the column.
                    for (int i = 0; i < ordered.Count - 1; i++)
                    {
                        double[] c0 = h.GetCircleCenterOnSheet(ordered[i], view);
                        double[] c1 = h.GetCircleCenterOnSheet(ordered[i + 1], view);
                        double tx = Math.Max(c0[0], c1[0]) + DimOffset;
                        double ty = (c0[1] + c1[1]) / 2.0;

                        h.ClearSelection();
                        if (!h.SelectEdge(ordered[i], view, false) ||
                            !h.SelectEdge(ordered[i + 1], view, true))
                            continue;

                        if (h.CreateLinearDimension(tx, ty) != null ||
                            h.CreateDimension(tx, ty) != null)
                        {
                            log($"  [{viewName}] Hole spacing for Ø{diameter * 1000:F1} mm.");
                            anyPos = true;
                        }
                    }

                    // Horizontal side offsets — always try LEFT (drawing convention / user mark),
                    // then RIGHT if that is the nearer manufacturing reference.
                    Edge nearest = ordered
                        .OrderBy(e => h.GetCircleCenterOnSheet(e, view)[0])
                        .First();
                    double[] nc = h.GetCircleCenterOnSheet(nearest, view);

                    double leftX = leftBound != null
                        ? h.GetEdgeMidpointOnSheet(leftBound, view)[0]
                        : minX;
                    double rightX = rightBound != null
                        ? h.GetEdgeMidpointOnSheet(rightBound, view)[0]
                        : maxX;

                    if (leftBound != null && TryDimEdgeToHole(
                            h, view, leftBound, nearest,
                            (nc[0] + leftX) / 2.0,
                            nc[1] - 0.010))
                    {
                        log($"  [{viewName}] Hole from left for Ø{diameter * 1000:F1} mm.");
                        anyPos = true;
                    }
                    else if (rightBound != null && TryDimEdgeToHole(
                            h, view, rightBound, nearest,
                            (nc[0] + rightX) / 2.0,
                            nc[1] - 0.010))
                    {
                        log($"  [{viewName}] Hole from right for Ø{diameter * 1000:F1} mm.");
                        anyPos = true;
                    }
                    else
                    {
                        log($"  [{viewName}] WARNING: hole side offset not placed for Ø{diameter * 1000:F1} mm.");
                    }

                    if (anyPos)
                        h.DimensionedFeatures.Add(posKey);
                }

                if (centerMarks < 6)
                {
                    Edge markEdge = ordered.FirstOrDefault(h.IsFullCircle) ?? ordered[0];
                    if (h.TryInsertCenterMark(drawing, view, markEdge))
                        centerMarks++;
                }

                h.ClearSelection();
            }
        }

        private static bool TryDimEdgeToHole(
            SmartDimHelper h,
            IView view,
            Edge bound,
            Edge hole,
            double textX,
            double textY)
        {
            h.ClearSelection();
            if (!h.SelectEdge(bound, view, false) || !h.SelectEdge(hole, view, true))
                return false;

            return h.CreateLinearDimension(textX, textY) != null ||
                   h.CreateDimension(textX, textY) != null;
        }

        private static bool TryHoleVerticalOutlinePick(
            SmartDimHelper h,
            IView view,
            Edge hole,
            double minX, double minY, double maxX, double maxY,
            bool fromTop)
        {
            double[] c = h.GetCircleCenterOnSheet(hole, view);
            h.ClearSelection();
            h.ActivateView(view);

            bool a = fromTop
                ? SelectEdgeAt(h, view, c[0], maxY)
                : SelectEdgeAt(h, view, c[0], minY);
            if (!a || !h.SelectEdge(hole, view, true))
                return false;

            double ty = fromTop ? (c[1] + maxY) / 2.0 : (c[1] + minY) / 2.0;
            return h.CreateLinearDimension(maxX + DimOffset, ty) != null ||
                   h.CreateDimension(maxX + DimOffset, ty) != null;
        }

        private static bool TryOutlinePick(
            SmartDimHelper h,
            IView view,
            double minX, double minY, double maxX, double maxY,
            bool horizontalSpan,
            string key,
            double expectedModel,
            Action<string> log,
            double? pickY = null)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            double midX = (minX + maxX) / 2.0;
            double midY = pickY ?? (minY + maxY) / 2.0;
            h.ClearSelection();
            h.ActivateView(view);

            bool a, b;
            if (horizontalSpan)
            {
                a = SelectEdgeAt(h, view, minX, midY);
                b = SelectEdgeAt(h, view, maxX, midY, append: true);
            }
            else
            {
                a = SelectEdgeAt(h, view, midX, minY);
                b = SelectEdgeAt(h, view, midX, maxY, append: true);
            }

            if (!a || !b)
                return false;

            double tx = horizontalSpan ? midX : maxX + DimOffset;
            double ty = horizontalSpan ? maxY + DimOffset : midY;
            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add(key);
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

        private static bool IsHoleLikeEdge(SmartDimHelper h, IView view, Edge edge)
        {
            if (h.IsFullCircle(edge))
                return true;

            double r = h.GetCircleRadius(edge);
            double arcLen = h.GetProjectedLength(edge, view);
            double scaledR = r * view.ScaleDecimal;
            if (scaledR < 0.0005)
                return false;

            double fullCircumference = 2.0 * Math.PI * scaledR;
            double arcFraction = arcLen / Math.Max(fullCircumference, 0.0001);

            return !(arcFraction > 0.42 && r >= MinProfileArcRadiusMeters);
        }

        private static Dictionary<double, List<Edge>> GroupByDiameter(Edge[] circular, SmartDimHelper h)
        {
            var groups = new Dictionary<double, List<Edge>>();
            foreach (Edge edge in circular)
            {
                double d = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (!groups.ContainsKey(d))
                    groups[d] = new List<Edge>();
                groups[d].Add(edge);
            }

            return groups;
        }

        private static List<Edge> UniqueBySheetCenter(SmartDimHelper h, IView view, IEnumerable<Edge> edges)
        {
            var seen = new HashSet<(long, long)>();
            var result = new List<Edge>();
            foreach (Edge edge in edges
                .OrderByDescending(e => h.IsFullCircle(e) ? 1 : 0)
                .ThenByDescending(e => h.GetProjectedLength(e, view)))
            {
                double[] c = h.GetCircleCenterOnSheet(edge, view);
                var key = (
                    (long)Math.Round(c[0] / CenterBucketMeters),
                    (long)Math.Round(c[1] / CenterBucketMeters));
                if (seen.Add(key))
                    result.Add(edge);
            }

            return result;
        }

        private static Edge? FindExtremeVertical(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            bool rightmost,
            bool preferUpper,
            double midY)
        {
            Edge? best = null;
            double bestX = rightmost ? double.MinValue : double.MaxValue;
            double bestLen = -1;

            foreach (Edge edge in linear)
            {
                if (!h.IsVerticalInView(edge, view, OrientTol))
                    continue;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                double len = h.GetProjectedLength(edge, view);
                bool upper = mid[1] >= midY - 0.002;

                // Prefer upper-half edges when X is tied (square top vs short tip scraps).
                bool betterX = rightmost ? mid[0] > bestX + 1e-6 : mid[0] < bestX - 1e-6;
                bool sameX = Math.Abs(mid[0] - bestX) <= 1e-6;
                bool betterTie = sameX && (
                    (preferUpper && upper && best != null &&
                     h.GetEdgeMidpointOnSheet(best, view)[1] < midY - 0.002) ||
                    len > bestLen);

                if (best == null || betterX || betterTie)
                {
                    best = edge;
                    bestX = mid[0];
                    bestLen = len;
                }
            }

            return best;
        }

        private static Edge? FindBoundaryEdge(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            double targetCoord,
            bool horizontal,
            bool preferLong)
        {
            double tol = 0.006;
            Edge? best = null;
            double bestScore = double.MinValue;

            foreach (Edge edge in linear)
            {
                bool orientOk = horizontal
                    ? h.IsHorizontalInView(edge, view, OrientTol)
                    : h.IsVerticalInView(edge, view, OrientTol);
                if (!orientOk)
                    continue;

                var mid = h.GetEdgeMidpointOnSheet(edge, view);
                double coord = horizontal ? mid[1] : mid[0];
                double dist = Math.Abs(coord - targetCoord);
                if (dist > tol)
                    continue;

                double len = h.GetProjectedLength(edge, view);
                double score = (preferLong ? len * 2.0 : len * 0.25) - dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = edge;
                }
            }

            return best;
        }
    }
}
