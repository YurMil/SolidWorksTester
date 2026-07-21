using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>
    /// Flange/gasket dimension pipeline: model import → smart fill → profile thickness/steps.
    /// </summary>
    internal static class FlangeGasketDimensionPipeline
    {
        public static void Apply(
            IModelDoc2 model,
            IDrawingDoc drawing,
            SmartDimHelper dimHelper,
            IView? discFaceView,
            PartAnalysisResult? analysis,
            Action<string> log)
        {
            using var timer = new PipelineStopwatch(log, "flange/gasket dimensions");

            timer.Measure("Step 1: import marked dims", () =>
            {
                if (analysis?.IsImportedGeometry == true ||
                    analysis?.ImportFeatureCount > 0)
                {
                    log("  Step 1: skip model import (imported / dumb solid — no marked dims).");
                    return;
                }

                log("  Step 1: import model dimensions to all views...");
                DrawingModelDimensionImport.ImportMarkedDimensionsToAllViews(model, drawing, log);
            });

            timer.Measure("Step 2: dedupe after import", () =>
            {
                log("  Step 2: dedupe after model import...");
                DrawingDimensionDeduper.RemoveDuplicateDimensions(
                    model, drawing, log, SmartDimConstants.IsometricViewName);
            });

            timer.Measure("Step 2b: upgrade hole qty callouts", () =>
            {
                log("  Step 2b: upgrade imported hole dims to quantity callouts...");
                FlangeGasketImportedDimUpgrader.UpgradeDiscFaceDimensions(
                    dimHelper, model, discFaceView, log);
            });

            timer.Step("Step 3: smart dimensions");
            log("  Step 3: smart dimensions (missing only)...");
            IView? dimView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (dimView != null)
            {
                string vName = dimView.GetName2();
                if (vName.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    dimView = dimView.GetNextView() as IView;
                    continue;
                }

                PipelineStopwatch.Run(log, $"FlangeGasketDimensions: {vName}", () =>
                {
                    log($"  Dimensions: {vName}");
                    FlangeGasketDimensions.AddForView(
                        dimHelper, model, drawing, dimView, discFaceView, log);
                });
                dimView = dimView.GetNextView() as IView;
            }

            timer.FinishStep();

            double? expectedThicknessMm = ResolveExpectedThicknessMm(analysis);
            timer.Measure("Step 4: profile thickness/gabarit", () =>
            {
                log("  Step 4: profile thickness and step depths (gabarit)...");
                FlangeGasketProfileDimensions.TryAddOnce(
                    dimHelper, model, drawing, discFaceView, expectedThicknessMm, log);
            });

            timer.Measure("Step 5: dedupe after smart dims", () =>
            {
                log("  Step 5: dedupe after smart dimensions...");
                DrawingDimensionDeduper.RemoveDuplicateDimensions(
                    model, drawing, log, SmartDimConstants.IsometricViewName);
                // Prefer "24x Ø" over bare Ø when both exist (side-view imports).
                DrawingDimensionDeduper.RemoveQuantityPrefixedDiameterDuplicates(
                    model, drawing, log, preferQuantityPrefix: true, SmartDimConstants.IsometricViewName);
            });

            timer.Measure("Step 6: ensure hole qty callouts", () =>
            {
                log("  Step 6: ensure hole quantity callouts...");
                FlangeGasketImportedDimUpgrader.EnsureHoleQuantityCallouts(
                    dimHelper, model, discFaceView, log);
            });
        }

        private static double? ResolveExpectedThicknessMm(PartAnalysisResult? analysis)
        {
            if (analysis?.EstProperties != null)
            {
                EstPartProperties est = analysis.EstProperties;
                // Blind flanges often store gabarit thickness in DIM1 when DIM3 is empty.
                if (est.Dim3Mm is > 0 and <= 500)
                    return est.Dim3Mm;
                if (est.Dim1Mm is > 0 and <= 500)
                    return est.Dim1Mm;
            }

            // Imported STEP: use part bbox short axis as gabarit thickness seed.
            if (analysis?.BboxShortMeters is > 0.0004 and <= 0.5)
                return analysis.BboxShortMeters * 1000.0;

            return null;
        }
    }
}
