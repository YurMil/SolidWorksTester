using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Lofted-bend shell dimensions matched to EST reference drawings:
    /// side (View1) — height, OD (+ prefix), id (+ prefix), axis centerline;
    /// end (View2) — wall thickness, weld-gap centerline + linear gap at outer tips;
    /// flat pattern — length + width + bend notes.
    /// </summary>
    internal static partial class LoftedBendsDimensions
    {
        private static void TrySideViewAxisCenterline(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            if (h.HasCenterLineInView(view))
            {
                log($"  Centerline already present in {view.GetName2()}.");
                return;
            }

            // Prefer top↔bottom circular rims (same pair used for height) for axis centerline.
            Edge[] edges = CollectSideEdges(h, view);
            var circles = edges.Where(h.IsCircular).OrderByDescending(h.GetCircleRadius).ToArray();
            if (circles.Length >= 2)
            {
                Edge top = circles.OrderByDescending(e => h.GetCircleCenterOnSheet(e, view)[1]).First();
                Edge bot = circles.OrderBy(e => h.GetCircleCenterOnSheet(e, view)[1]).First();
                if (!ReferenceEquals(top, bot) &&
                    CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(h, drawing, view, top, bot))
                {
                    log($"  Axis centerline on {view.GetName2()} (rim circles).");
                    return;
                }
            }

            var horiz = edges
                .Where(e => h.IsLinear(e) && h.IsHorizontalInView(e, view, 0.006))
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .ToArray();

            if (horiz.Length >= 2)
            {
                Edge top = horiz.OrderByDescending(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
                Edge bot = horiz.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
                if (CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(h, drawing, view, top, bot))
                {
                    log($"  Axis centerline on {view.GetName2()} (top↔bottom).");
                    return;
                }
            }

            CylindricalDimCenterlines.Add(h, model, drawing, view, log);
        }

        private static Edge[] CollectSideEdges(SmartDimHelper h, IView view)
        {
            var list = new System.Collections.Generic.List<Edge>();
            list.AddRange(h.GetViewEdges(view));
            try
            {
                // Side OD often lives on silhouette generators — use carefully (select only).
                list.AddRange(h.GetViewSilhouetteEdges(view));
            }
            catch
            {
                // SW2025 silhouette can be unstable; model edges only.
            }

            return list.Distinct().ToArray();
        }

        private static void EnsureHeightOnSideView(
            SmartDimHelper h,
            IView view,
            double? expectedHeight,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("LoftHeight"))
                return;
            if (expectedHeight.HasValue && h.HasDimensionWithValueInDrawing(expectedHeight.Value))
            {
                h.DimensionedFeatures.Add("LoftHeight");
                return;
            }

            Edge[] edges = CollectSideEdges(h, view);
            if (edges.Length < 2)
            {
                log($"  Height: no edges on {view.GetName2()}.");
                return;
            }

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double textX = minX - DimOffset;
            double textY = (minY + maxY) / 2.0;

            // 1) Nearly-horizontal linear pair (top↔bottom).
            if (TryDimSeparation(
                    h, view, edges, expectedHeight, horizontalSpan: false,
                    textX, textY, "LoftHeight", log, "Height", prefix: null,
                    preferLinearOriented: true))
                return;

            // 2) Circular rims at top/bottom (common for shell side views).
            var circles = edges.Where(h.IsCircular).ToArray();
            if (TryDimCircularYPair(h, view, circles, expectedHeight, textX, textY, "LoftHeight", log, "Height"))
                return;

            // 3) Any edge pair whose Y-separation matches expected height.
            if (TryDimSeparation(
                    h, view, edges, expectedHeight, horizontalSpan: false,
                    textX, textY, "LoftHeight", log, "Height", prefix: null,
                    preferLinearOriented: false))
                return;

            log($"  Height: failed on {view.GetName2()}.");
        }

        private static void EnsureOuterDiameterOnSideView(
            SmartDimHelper h,
            IView view,
            ShellSizeHints hints,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("LoftOd"))
                return;

            Edge[] edges = CollectSideEdges(h, view);
            if (edges.Length == 0)
            {
                log($"  OD: no edges on {view.GetName2()}.");
                return;
            }

            // Description may omit oD — infer from largest rim circle (never place Ø-dim on that circle).
            hints.OuterDiameterMeters ??= InferDiameterFromRimCircles(h, edges, preferOuter: true);
            double? expectedOd = hints.OuterDiameterMeters;

            if (expectedOd.HasValue && h.HasDimensionWithValue(view, expectedOd.Value))
            {
                h.DimensionedFeatures.Add("LoftOd");
                return;
            }

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double textX = (minX + maxX) / 2.0;
            double textY = maxY + DimOffset;
            double? minSpan = hints.ThicknessMeters.HasValue
                ? hints.ThicknessMeters.Value * 8.0
                : 0.040;

            // Linear OD only (user reference: "OD 612" between outer verticals) — NEVER CreateDiameterDimension
            // on edge-on rims (that spawned ghost "OD 4140" = 2×OD on the end view).
            if (TryDimOutlineSpan(
                    h, view, edges, expectedOd, horizontalSpan: true,
                    textX, textY, "LoftOd", log, "OD", prefix: "OD ",
                    insetModel: 0))
                return;

            if (TryDimSeparation(
                    h, view, edges, expectedOd, horizontalSpan: true,
                    textX, textY, "LoftOd", log, "OD", prefix: "OD ",
                    preferLinearOriented: true, minSpanMeters: minSpan))
                return;

            if (TryDimLongestMatchingEdge(
                    h, view, edges, expectedOd, horizontal: true,
                    textX, textY, "LoftOd", log, "OD", prefix: "OD "))
                return;

            if (TryDimSeparation(
                    h, view, edges, expectedOd, horizontalSpan: true,
                    textX, textY, "LoftOd", log, "OD", prefix: "OD ",
                    preferLinearOriented: false, minSpanMeters: minSpan))
                return;

            log($"  OD: failed on {view.GetName2()} (hint={Fmt(expectedOd)}).");
        }

        private static void EnsureInnerDiameterOnSideView(
            SmartDimHelper h,
            IView view,
            ShellSizeHints hints,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("LoftId"))
                return;

            Edge[] edges = CollectSideEdges(h, view);
            hints.OuterDiameterMeters ??= InferDiameterFromRimCircles(h, edges, preferOuter: true);

            double? expectedId = null;
            if (hints.OuterDiameterMeters.HasValue && hints.ThicknessMeters.HasValue)
                expectedId = hints.OuterDiameterMeters.Value - 2.0 * hints.ThicknessMeters.Value;
            expectedId ??= InferDiameterFromRimCircles(h, edges, preferOuter: false);

            if (expectedId is null or <= 0)
                return;
            if (h.HasDimensionWithValue(view, expectedId.Value))
            {
                h.DimensionedFeatures.Add("LoftId");
                return;
            }

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double textX = (minX + maxX) / 2.0;
            double textY = maxY + DimOffset * 2.2;
            double inset = hints.ThicknessMeters ?? 0.006;
            double? minSpan = inset * 8.0;

            // Linear id between inner walls (inset by thickness from outer outline).
            if (TryDimOutlineSpan(
                    h, view, edges, expectedId, horizontalSpan: true,
                    textX, textY, "LoftId", log, "id", prefix: "id ",
                    insetModel: inset))
                return;

            if (TryDimSeparation(
                    h, view, edges, expectedId, horizontalSpan: true,
                    textX, textY, "LoftId", log, "id", prefix: "id ",
                    preferLinearOriented: true, minSpanMeters: minSpan))
                return;

            log($"  id: failed on {view.GetName2()} (hint={Fmt(expectedId)}).");
        }

        /// <summary>
        /// Largest (OD) or second-largest (ID) rim-circle diameter — hint only, not a placed Ø dim.
        /// </summary>
        private static double? InferDiameterFromRimCircles(
            SmartDimHelper h,
            Edge[] edges,
            bool preferOuter)
        {
            double[] diameters = edges
                .Where(h.IsCircular)
                .Select(h.GetCircleRadius)
                .Where(r => r >= 0.020)
                .Select(r => Math.Round(r * 2.0, 5))
                .Distinct()
                .OrderByDescending(d => d)
                .ToArray();

            if (diameters.Length == 0)
                return null;
            if (preferOuter)
                return diameters[0];
            if (diameters.Length >= 2)
                return diameters[1];
            return null;
        }

        /// <summary>
        /// Linear dimension across view outline left↔right (or top↔bottom), optionally inset (for id).
        /// </summary>
        private static bool TryDimOutlineSpan(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double? expected,
            bool horizontalSpan,
            double textX,
            double textY,
            string key,
            Action<string> log,
            string label,
            string? prefix,
            double insetModel)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double insetSheet = Math.Max(insetModel, 0) * scale;

            double spanModel = horizontalSpan
                ? (maxX - minX) / scale - 2.0 * insetModel
                : (maxY - minY) / scale - 2.0 * insetModel;

            if (spanModel < MinSize)
                return false;

            if (expected.HasValue)
            {
                double err = Math.Abs(spanModel - expected.Value) / Math.Max(expected.Value, 1e-9);
                // Outline can include padding; allow looser match then still place if selection works.
                if (err > 0.35 && insetModel <= 0)
                    log($"  {label}: outline span {spanModel * 1000:F0} vs expected {expected.Value * 1000:F0}.");
            }

            double midX = (minX + maxX) / 2.0;
            double midY = (minY + maxY) / 2.0;
            double x1, y1, x2, y2;
            if (horizontalSpan)
            {
                x1 = minX + insetSheet;
                x2 = maxX - insetSheet;
                y1 = y2 = midY;
            }
            else
            {
                y1 = minY + insetSheet;
                y2 = maxY - insetSheet;
                x1 = x2 = midX;
            }

            h.ClearSelection();
            bool a = SelectEdgeNearSheetPoint(h, view, x1, y1);
            bool b = SelectEdgeNearSheetPoint(h, view, x2, y2, append: true);
            if (!a || !b)
            {
                // Nudge inward a bit more for HLV hidden edges.
                h.ClearSelection();
                double nudge = Math.Max(0.0015, insetSheet * 0.25);
                if (horizontalSpan)
                {
                    a = SelectEdgeNearSheetPoint(h, view, minX + insetSheet + nudge, midY);
                    b = SelectEdgeNearSheetPoint(h, view, maxX - insetSheet - nudge, midY, append: true);
                }
                else
                {
                    a = SelectEdgeNearSheetPoint(h, view, midX, minY + insetSheet + nudge);
                    b = SelectEdgeNearSheetPoint(h, view, midX, maxY - insetSheet - nudge, append: true);
                }
            }

            if (!a || !b)
                return false;

            DisplayDimension? dim = h.CreateLinearDimension(textX, textY);
            if (dim == null)
                return false;

            // Reject accidental angular / wrong-magnitude dims.
            try
            {
                if (dim.Type2 != (int)SolidWorks.Interop.swconst.swDimensionType_e.swLinearDimension)
                {
                    if (dim.GetAnnotation() is IAnnotation ann)
                    {
                        h.ClearSelection();
                        ann.Select3(false, null);
                        h.Model.Extension.DeleteSelection2(0);
                    }

                    return false;
                }

                if (dim.GetDimension2(0) is Dimension md)
                {
                    double val = Math.Abs(md.SystemValue);
                    if (expected.HasValue &&
                        Math.Abs(val - expected.Value) / Math.Max(expected.Value, 1e-9) > 0.25)
                    {
                        if (dim.GetAnnotation() is IAnnotation ann)
                        {
                            h.ClearSelection();
                            ann.Select3(false, null);
                            h.Model.Extension.DeleteSelection2(0);
                        }

                        log($"  {label}: placed value {val * 1000:F0} rejected (expected {expected.Value * 1000:F0}).");
                        return false;
                    }

                    // Guard against 2×OD ghost (e.g. 4140 when OD is 2070).
                    if (expected.HasValue &&
                        Math.Abs(val - expected.Value * 2.0) / Math.Max(expected.Value * 2.0, 1e-9) < 0.05)
                    {
                        if (dim.GetAnnotation() is IAnnotation ann)
                        {
                            h.ClearSelection();
                            ann.Select3(false, null);
                            h.Model.Extension.DeleteSelection2(0);
                        }

                        log($"  {label}: rejected 2× diameter ghost {val * 1000:F0}.");
                        return false;
                    }
                }
            }
            catch
            {
                // keep dim if type check fails
            }

            ApplyPrefix(dim, prefix);
            h.DimensionedFeatures.Add(key);
            log($"  {label} linear on {view.GetName2()}.");
            return true;
        }

        private static bool SelectEdgeNearSheetPoint(
            SmartDimHelper h,
            IView view,
            double sheetX,
            double sheetY,
            bool append = false)
        {
            try
            {
                h.ActivateView(view);
                return h.Ext.SelectByID2(
                    string.Empty,
                    SmartDimConstants.EdgeSelectType,
                    sheetX,
                    sheetY,
                    0.0,
                    append,
                    0,
                    null,
                    0);
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyPrefix(DisplayDimension dim, string? prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return;
            try
            {
                dim.SetText(
                    (int)SolidWorks.Interop.swconst.swDimensionTextParts_e.swDimensionTextPrefix,
                    prefix);
            }
            catch
            {
                // optional
            }
        }

        private static bool TryDimCircularYPair(
            SmartDimHelper h,
            IView view,
            Edge[] circles,
            double? expected,
            double textX,
            double textY,
            string key,
            Action<string> log,
            string label)
        {
            if (circles.Length < 2 || h.DimensionedFeatures.Contains(key))
                return h.DimensionedFeatures.Contains(key);

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? bestA = null, bestB = null;
            double bestErr = double.MaxValue;

            for (int i = 0; i < circles.Length; i++)
            {
                for (int j = i + 1; j < circles.Length; j++)
                {
                    double y1 = h.GetCircleCenterOnSheet(circles[i], view)[1];
                    double y2 = h.GetCircleCenterOnSheet(circles[j], view)[1];
                    double span = Math.Abs(y1 - y2) / scale;
                    if (span < MinSize)
                        continue;
                    double err = expected.HasValue
                        ? Math.Abs(span - expected.Value) / Math.Max(expected.Value, 1e-9)
                        : 0;
                    if (expected.HasValue && err > 0.20)
                        continue;
                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestA = circles[i];
                        bestB = circles[j];
                    }
                }
            }

            if (bestA == null || bestB == null)
                return false;

            h.ClearSelection();
            if (!h.SelectEdge(bestA, view, false) || !h.SelectEdge(bestB, view, true))
                return false;

            DisplayDimension? dim = h.CreateLinearDimension(textX, textY);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add(key);
            log($"  {label} on {view.GetName2()} (circular rims).");
            return true;
        }

        private static bool TryDimLongestMatchingEdge(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double? expected,
            bool horizontal,
            double textX,
            double textY,
            string key,
            Action<string> log,
            string label,
            string? prefix)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? best = null;
            double bestErr = double.MaxValue;

            foreach (Edge e in edges.Where(h.IsLinear))
            {
                bool orientOk = horizontal
                    ? h.IsHorizontalInView(e, view, 0.006)
                    : h.IsVerticalInView(e, view, 0.006);
                if (!orientOk)
                    continue;

                double len = h.GetProjectedLength(e, view) / scale;
                if (len < MinSize)
                    continue;
                double err = expected.HasValue
                    ? Math.Abs(len - expected.Value) / Math.Max(expected.Value, 1e-9)
                    : 1.0 / len;
                if (expected.HasValue && err > 0.20)
                    continue;
                if (err < bestErr)
                {
                    bestErr = err;
                    best = e;
                }
            }

            if (best == null)
                return false;

            h.ClearSelection();
            if (!h.SelectEdge(best, view, false))
                return false;

            DisplayDimension? dim = h.CreateLinearDimension(textX, textY);
            if (dim == null)
                return false;

            if (!string.IsNullOrEmpty(prefix))
                ApplyPrefix(dim, prefix);

            h.DimensionedFeatures.Add(key);
            log($"  {label} on {view.GetName2()} (longest edge).");
            return true;
        }

        /// <summary>
        /// Dimension the best edge pair whose sheet separation matches <paramref name="expected"/>.
        /// </summary>
        private static bool TryDimSeparation(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double? expected,
            bool horizontalSpan,
            double textX,
            double textY,
            string key,
            Action<string> log,
            string label,
            string? prefix,
            bool preferLinearOriented,
            double? minSpanMeters = null)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? bestA = null, bestB = null;
            double bestErr = double.MaxValue;
            double bestSpan = 0;

            for (int i = 0; i < edges.Length; i++)
            {
                for (int j = i + 1; j < edges.Length; j++)
                {
                    Edge a = edges[i];
                    Edge b = edges[j];

                    if (preferLinearOriented)
                    {
                        // OD/id: prefer nearly-vertical edges; height: nearly-horizontal.
                        bool orientA = horizontalSpan
                            ? (h.IsLinear(a) && h.IsVerticalInView(a, view, 0.006))
                            : (h.IsLinear(a) && h.IsHorizontalInView(a, view, 0.006));
                        bool orientB = horizontalSpan
                            ? (h.IsLinear(b) && h.IsVerticalInView(b, view, 0.006))
                            : (h.IsLinear(b) && h.IsHorizontalInView(b, view, 0.006));
                        if (!orientA || !orientB)
                            continue;
                    }

                    double[] pa = EdgeRefPointOnSheet(h, a, view);
                    double[] pb = EdgeRefPointOnSheet(h, b, view);
                    double span = (horizontalSpan
                        ? Math.Abs(pa[0] - pb[0])
                        : Math.Abs(pa[1] - pb[1])) / scale;

                    if (span < MinSize)
                        continue;
                    if (minSpanMeters.HasValue && span < minSpanMeters.Value)
                        continue;

                    double err = expected.HasValue
                        ? Math.Abs(span - expected.Value) / Math.Max(expected.Value, 1e-9)
                        : 0;
                    if (expected.HasValue && err > 0.20)
                        continue;

                    if (err < bestErr || (Math.Abs(err - bestErr) < 1e-9 && span > bestSpan))
                    {
                        bestErr = err;
                        bestSpan = span;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (bestA == null || bestB == null)
                return false;

            h.ClearSelection();
            if (!h.SelectEdge(bestA, view, false) || !h.SelectEdge(bestB, view, true))
                return false;

            DisplayDimension? dim = h.CreateLinearDimension(textX, textY);
            if (dim == null)
                return false;

            if (!string.IsNullOrEmpty(prefix))
                ApplyPrefix(dim, prefix);

            h.DimensionedFeatures.Add(key);
            log($"  {label} {bestSpan * 1000:F1} mm on {view.GetName2()}.");
            return true;
        }

        private static double[] EdgeRefPointOnSheet(SmartDimHelper h, Edge edge, IView view) =>
            h.IsCircular(edge)
                ? h.GetCircleCenterOnSheet(edge, view)
                : h.GetEdgeMidpointOnSheet(edge, view);

        // ── Flat pattern: length (long) + width (short = h) ───────────────
    }
}
