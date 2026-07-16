using System;
using System.Diagnostics;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.RoundFlatPlate;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester
{
    /// <summary>
    /// Sheet-metal / plate thickness on orthographic side views.
    /// Prefers SheetMetal/BaseFlange gauge, scores parallel face pairs in model space,
    /// and ignores only short chamfer/fillet break edges (not long outline edges).
    /// </summary>
    public static class SmartDimThickness
    {
        private const double MinThicknessMeters = 0.0001;  // 0.1 mm
        private const double MaxThicknessMeters = 0.050;   // 50 mm (thick plate)
        private const double MaxCornerBreakMeters = 0.055; // long edges are outline, not chamfer legs
        private const double DimOffset = 0.008;

        public static void Add(SmartDimHelper h, IView view) => Add(h, view, null);

        public static void Add(SmartDimHelper h, IView view, Action<string>? log) =>
            Add(h, view, log, expectedThicknessMm: null);

        public static void Add(
            SmartDimHelper h,
            IView view,
            Action<string>? log,
            double? expectedThicknessMm)
        {
            string viewName = view.GetName2();
            void L(string msg) => log?.Invoke(msg);

            L($"  [Thickness] Looking for thickness edge in: {viewName}");

            if (h.DimensionedFeatures.Contains("Thickness"))
            {
                L("  [Thickness] Thickness already dimensioned, skipping");
                return;
            }

            var sw = Stopwatch.StartNew();
            bool circular = RoundFlatPlateViewAnalyzer.IsCircularFaceView(h, view);
            L($"  [Thickness] IsCircularFaceView: {circular} ({sw.ElapsedMilliseconds}ms)");
            if (circular)
            {
                L("  [Thickness] Skipping circular face view");
                return;
            }

            double? expectedModel = expectedThicknessMm is > 0
                ? expectedThicknessMm.Value / 1000.0
                : BafflePlateThickness.TryReadSheetMetalThickness(h);
            if (expectedModel.HasValue)
                L($"  [Thickness] SM gauge hint: {expectedModel.Value * 1000:F2} mm");

            // Prefer thin high-aspect side views when expected gauge is known.
            if (expectedModel.HasValue &&
                FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double ow, out double oh))
            {
                double scale = Math.Max(view.ScaleDecimal, 1e-9);
                double thinModel = Math.Min(ow, oh) / scale;
                double aspect = Math.Max(ow, oh) / Math.Max(Math.Min(ow, oh), 1e-12);
                // Face-on plate views are roughly square and much thicker outline than gauge.
                if (aspect < 1.8 && thinModel > expectedModel.Value * 3.0)
                {
                    L("  [Thickness] Skipping face-on view (not a thin side silhouette).");
                    return;
                }
            }

            sw.Restart();
            Edge[] modelEdges = h.GetViewEdges(view);
            Edge[] withSilhouette = modelEdges;
            try
            {
                withSilhouette = modelEdges.Concat(h.GetViewSilhouetteEdges(view)).Distinct().ToArray();
            }
            catch
            {
                // SW2025 silhouette can be unstable.
            }

            Edge[] linearEdges = withSilhouette
                .Where(h.IsLinear)
                .Where(e => !IsShortCornerBreakEdge(h, e, view))
                .ToArray();
            L($"  [Thickness] GetViewEdges linear (excl. short chamfer/fillet): {linearEdges.Length} ({sw.ElapsedMilliseconds}ms)");

            if (TryAddBetweenParallelEdges(h, view, linearEdges, expectedModel, L))
                return;

            if (TryAddFromShortestEdge(h, view, linearEdges, expectedModel, L))
                return;

            if (expectedModel.HasValue && TryAddFromOutlineThinSpan(h, view, expectedModel.Value, L))
                return;

            L("  [Thickness] No suitable thickness edge found");
        }

        /// <summary>
        /// Chamfer/Fillet ownership often "leaks" onto long outline edges after corner breaks.
        /// Only treat short edges as corner-break exclusions.
        /// </summary>
        internal static bool IsShortCornerBreakEdge(SmartDimHelper h, Edge edge, IView view)
        {
            string t = h.GetEdgeFeatureType(edge);
            bool cornerType =
                t.Equals("Chamfer", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Fillet", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("VarFillet", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Fillet", StringComparison.OrdinalIgnoreCase);

            if (!cornerType)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double modelLen = h.GetProjectedLength(edge, view) / scale;
            return modelLen <= MaxCornerBreakMeters;
        }

        private static bool TryAddBetweenParallelEdges(
            SmartDimHelper h,
            IView view,
            Edge[] linearEdges,
            double? expectedModel,
            Action<string> L)
        {
            if (linearEdges.Length < 2)
                return false;

            var sw = Stopwatch.StartNew();
            bool placed = TryParallelPair(h, view, linearEdges, horizontal: true, expectedModel, L)
                || TryParallelPair(h, view, linearEdges, horizontal: false, expectedModel, L);
            L($"  [Thickness] parallel-edge search: {sw.ElapsedMilliseconds}ms (placed={placed})");
            return placed;
        }

        private static bool TryParallelPair(
            SmartDimHelper h,
            IView view,
            Edge[] linearEdges,
            bool horizontal,
            double? expectedModel,
            Action<string> log)
        {
            var oriented = linearEdges
                .Where(e => horizontal
                    ? h.IsHorizontalInView(e, view, 0.004)
                    : h.IsVerticalInView(e, view, 0.004))
                .ToArray();

            if (oriented.Length < 2)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? edgeA = null;
            Edge? edgeB = null;
            double bestScore = double.MaxValue;
            double bestModel = 0;

            for (int i = 0; i < oriented.Length; i++)
            {
                for (int j = i + 1; j < oriented.Length; j++)
                {
                    double sheetDist = ParallelEdgeDistance(h, view, oriented[i], oriented[j], horizontal);
                    double modelDist = sheetDist / scale;
                    if (modelDist < MinThicknessMeters || modelDist > MaxThicknessMeters)
                        continue;

                    double lenScore = 1.0 / (1.0 +
                        Math.Min(h.GetProjectedLength(oriented[i], view),
                                 h.GetProjectedLength(oriented[j], view)));
                    double score = expectedModel.HasValue
                        ? Math.Abs(modelDist - expectedModel.Value) / Math.Max(expectedModel.Value, 1e-9)
                          + lenScore * 0.05
                        : modelDist + lenScore * 0.01;

                    if (expectedModel.HasValue &&
                        Math.Abs(modelDist - expectedModel.Value) > expectedModel.Value * 0.35 + 0.0003)
                        continue;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestModel = modelDist;
                        edgeA = oriented[i];
                        edgeB = oriented[j];
                    }
                }
            }

            if (edgeA == null || edgeB == null)
                return false;

            h.ClearSelection();
            if (!h.SelectEdge(edgeA, view, false) || !h.SelectEdge(edgeB, view, true))
                return false;

            var (sA, _) = h.GetEdgeEndpointsOnSheet(edgeA, view);
            var (sB, _) = h.GetEdgeEndpointsOnSheet(edgeB, view);
            double dimX = horizontal ? (sA[0] + sB[0]) / 2.0 + DimOffset : sA[0] + DimOffset;
            double dimY = horizontal ? sA[1] : (sA[1] + sB[1]) / 2.0;

            var dim = h.CreateLinearDimension(dimX, dimY) ?? h.CreateDimension(dimX, dimY);
            if (dim != null)
            {
                log($"  [Thickness] Thickness {bestModel * 1000:F2} mm (parallel faces) on {view.GetName2()}.");
                h.DimensionedFeatures.Add("Thickness");
                h.ClearSelection();
                return true;
            }

            h.ClearSelection();
            return false;
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

        private static bool TryAddFromShortestEdge(
            SmartDimHelper h,
            IView view,
            Edge[] linearEdges,
            double? expectedModel,
            Action<string> L)
        {
            if (linearEdges.Length == 0)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            Edge? thicknessEdge = null;
            double bestScore = double.MaxValue;
            double bestModel = 0;

            foreach (var edge in linearEdges)
            {
                double modelLen = h.GetProjectedLength(edge, view) / scale;
                if (modelLen < MinThicknessMeters || modelLen > MaxThicknessMeters)
                    continue;

                double score = expectedModel.HasValue
                    ? Math.Abs(modelLen - expectedModel.Value) / Math.Max(expectedModel.Value, 1e-9)
                    : modelLen;

                if (expectedModel.HasValue &&
                    Math.Abs(modelLen - expectedModel.Value) > expectedModel.Value * 0.35 + 0.0003)
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestModel = modelLen;
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
            if (dim != null)
            {
                L($"  [Thickness] Thickness {bestModel * 1000:F2} mm (edge) on {view.GetName2()}.");
                h.DimensionedFeatures.Add("Thickness");
                h.ClearSelection();
                return true;
            }

            L("  [Thickness] WARNING: Thickness dimension failed");
            h.ClearSelection();
            return false;
        }

        private static bool TryAddFromOutlineThinSpan(
            SmartDimHelper h,
            IView view,
            double expectedModel,
            Action<string> L)
        {
            if (!FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double ow, out double oh))
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double thinSheet = Math.Min(ow, oh);
            double thinModel = thinSheet / scale;
            if (Math.Abs(thinModel - expectedModel) > expectedModel * 0.4 + 0.0005)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(h.GetViewEdges(view), view);
            bool thinIsHeight = oh <= ow;
            h.ClearSelection();
            h.ActivateView(view);

            bool a, b;
            double tx, ty;
            if (thinIsHeight)
            {
                double midX = (minX + maxX) / 2.0;
                a = SelectEdgeAt(h, midX, minY);
                b = SelectEdgeAt(h, midX, maxY, append: true);
                tx = maxX + DimOffset;
                ty = (minY + maxY) / 2.0;
            }
            else
            {
                double midY = (minY + maxY) / 2.0;
                a = SelectEdgeAt(h, minX, midY);
                b = SelectEdgeAt(h, maxX, midY, append: true);
                tx = (minX + maxX) / 2.0;
                ty = maxY + DimOffset;
            }

            if (!a || !b)
                return false;

            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add("Thickness");
            L($"  [Thickness] Thickness {expectedModel * 1000:F2} mm (outline) on {view.GetName2()}.");
            h.ClearSelection();
            return true;
        }

        private static bool SelectEdgeAt(SmartDimHelper h, double x, double y, bool append = false)
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
    }
}
