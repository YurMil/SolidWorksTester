using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Fast gabarit thickness for dense baffle plates.
    /// Prefers Level 2: model planar faces / outer-rim edges → IView.GetCorresponding → SelectFace/Edge.
    /// Pixel picks fail at small sheet separation (e.g. 6 mm @ 1:20 ≈ 0.3 mm).
    /// </summary>
    internal static partial class BafflePlateThickness
    {
        private const double MinThicknessModelMeters = 0.0005; // 0.5 mm
        private const double MaxThicknessModelMeters = 0.025;  // 25 mm
        private const double OutlineMarginAllowanceSheet = 0.040;
        private const double WideMatchRelativeTol = 0.35;
        private const double DimOffset = 0.010;
        private const double ValueMatchRelativeTol = 0.02;
        private const double MinThicknessFaceArea = 0.02; // m² — skip tiny tab scraps

        /// <param name="thicknessView">View that received the thickness dimension (for Step 5 sample).</param>
        public static bool TryAddOnce(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView? primaryFlatView,
            PartAnalysisResult? analysis,
            Action<string> log,
            out IView? thicknessView)
        {
            thicknessView = null;

            if (h.DimensionedFeatures.Contains("Thickness"))
            {
                log("  Thickness already marked — skip gabarit.");
                return false;
            }

            double? expectedModel = TryReadSheetMetalThickness(h);
            double? plateSpanModel = ResolvePlateSpanMeters(analysis, primaryFlatView);

            IView? sideView = FindSideViewByWideSpan(
                drawing, primaryFlatView, expectedModel, plateSpanModel, log);
            if (sideView == null)
            {
                log("  Warning: no thin side view for baffle gabarit (wide-span / margin-aware).");
                return false;
            }

            string viewName = sideView.GetName2();
            double scale = Math.Max(sideView.ScaleDecimal, 1e-9);

            if (!FlatPlateViewAnalyzer.TryGetOutlineSize(sideView, out double width, out double height))
            {
                log($"  Warning: no outline on {viewName}.");
                return false;
            }

            double thinModelApprox = Math.Min(width, height) / scale;
            log($"  Gabarit target: {viewName} (outline thin≈{thinModelApprox * 1000:F1} mm" +
                (expectedModel.HasValue ? $", SM {expectedModel.Value * 1000:F2} mm" : "") +
                (plateSpanModel.HasValue ? $", span {plateSpanModel.Value * 1000:F0} mm" : "") + ").");

            // Rim first: one face walk with early-exit. Skip planar (failed + full walk) and
            // Level-1 picks (0.3 mm sheet gap @ 1:20 → CreateDimension null).
            string? method = null;
            if (TryDimensionByOuterRimEdges(h, sideView, expectedModel, log))
                method = "outer-rim edges + SelectEntity";
            else if (TryDimensionByCorrespondingFaces(h, sideView, expectedModel, log))
                method = "GetCorresponding planar faces";
            else
            {
                log($"  Warning: could not place baffle gabarit on {viewName} (rim/faces failed).");
                return false;
            }

            h.DimensionedFeatures.Add("Thickness");
            thicknessView = sideView;
            log($"  Gabarit thickness placed on {viewName} ({method}, value validated).");
            return true;
        }

        public static double? TryReadSheetMetalThickness(SmartDimHelper h)
        {
            IView? anyView = (h.Drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (anyView != null)
            {
                Component2? comp = h.GetViewComponent(anyView);
                if (comp != null)
                {
                    double? t = TryReadFromPartDoc(comp.GetModelDoc2() as IModelDoc2);
                    if (t.HasValue)
                        return t;
                }

                anyView = anyView.GetNextView() as IView;
            }

            return null;
        }

        private static double? ResolvePlateSpanMeters(PartAnalysisResult? analysis, IView? primaryFlatView)
        {
            if (analysis != null)
            {
                double span = Math.Max(analysis.BboxLongMeters, analysis.BboxMidMeters);
                if (span >= 0.010)
                    return span;
            }

            if (primaryFlatView != null &&
                FlatPlateViewAnalyzer.TryGetOutlineSize(primaryFlatView, out double w, out double h))
            {
                double scale = Math.Max(primaryFlatView.ScaleDecimal, 1e-9);
                return Math.Max(w, h) / scale;
            }

            return null;
        }

        private static IView? FindSideViewByWideSpan(
            IDrawingDoc drawing,
            IView? primaryFlatView,
            double? expectedThicknessModel,
            double? plateSpanModel,
            Action<string> log)
        {
            IView? best = null;
            double bestScore = double.MaxValue;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (primaryFlatView != null && ReferenceEquals(view, primaryFlatView))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (!FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double width, out double height))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                double scale = Math.Max(view.ScaleDecimal, 1e-9);
                double thinSheet = Math.Min(width, height);
                double wideSheet = Math.Max(width, height);
                double wideModel = wideSheet / scale;

                double maxThinSheet = OutlineMarginAllowanceSheet +
                    (expectedThicknessModel ?? MinThicknessModelMeters) * scale;
                maxThinSheet = Math.Max(maxThinSheet, 0.050);

                if (thinSheet > maxThinSheet)
                {
                    log($"    skip {name}: thin sheet {thinSheet * 1000:F1} mm > margin cap {maxThinSheet * 1000:F1} mm");
                    view = view.GetNextView() as IView;
                    continue;
                }

                double score;
                if (plateSpanModel.HasValue && plateSpanModel.Value > 1e-6)
                {
                    double relErr = Math.Abs(wideModel - plateSpanModel.Value) / plateSpanModel.Value;
                    if (relErr > WideMatchRelativeTol)
                    {
                        log($"    skip {name}: wide {wideModel * 1000:F0} mm vs span {plateSpanModel.Value * 1000:F0} mm " +
                            $"(rel {relErr:P0})");
                        view = view.GetNextView() as IView;
                        continue;
                    }

                    score = relErr;
                }
                else
                {
                    double aspect = wideSheet / Math.Max(thinSheet, 1e-12);
                    if (aspect < 3.0)
                    {
                        log($"    skip {name}: aspect {aspect:F1} (no plate span)");
                        view = view.GetNextView() as IView;
                        continue;
                    }

                    score = 1.0 / aspect;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = view;
                }

                view = view.GetNextView() as IView;
            }

            return best;
        }

        // ── Level 2: GetCorresponding planar faces ─────────────────────────
    }
}
