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
            log("  Step 1: import model dimensions to all views...");
            DrawingModelDimensionImport.ImportMarkedDimensionsToAllViews(model, drawing, log);

            log("  Step 2: dedupe after model import...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                model, drawing, log, SmartDimConstants.IsometricViewName);

            log("  Step 2b: upgrade imported hole dims to quantity callouts...");
            FlangeGasketImportedDimUpgrader.UpgradeDiscFaceDimensions(
                dimHelper, model, discFaceView, log);

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

                log($"  Dimensions: {vName}");
                FlangeGasketDimensions.AddForView(
                    dimHelper, model, drawing, dimView, discFaceView, log);
                dimView = dimView.GetNextView() as IView;
            }

            double? expectedThicknessMm = ResolveExpectedThicknessMm(analysis);
            log("  Step 4: profile thickness and step depths (gabarit)...");
            FlangeGasketProfileDimensions.TryAddOnce(
                dimHelper, model, drawing, discFaceView, expectedThicknessMm, log);

            log("  Step 5: dedupe after smart dimensions...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                model, drawing, log, SmartDimConstants.IsometricViewName);
            // Prefer "24x Ø" over bare Ø when both exist (side-view imports).
            DrawingDimensionDeduper.RemoveQuantityPrefixedDiameterDuplicates(
                model, drawing, log, preferQuantityPrefix: true, SmartDimConstants.IsometricViewName);

            log("  Step 6: ensure hole quantity callouts...");
            FlangeGasketImportedDimUpgrader.EnsureHoleQuantityCallouts(
                dimHelper, model, discFaceView, log);
        }

        private static double? ResolveExpectedThicknessMm(PartAnalysisResult? analysis)
        {
            if (analysis?.EstProperties == null)
                return null;

            EstPartProperties est = analysis.EstProperties;
            // Blind flanges often store gabarit thickness in DIM1 when DIM3 is empty.
            if (est.Dim3Mm is > 0 and <= 500)
                return est.Dim3Mm;
            if (est.Dim1Mm is > 0 and <= 500)
                return est.Dim1Mm;
            return null;
        }
    }
}
