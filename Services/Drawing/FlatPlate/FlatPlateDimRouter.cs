using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.FlangeGasket;
using SolidWorksTester.RoundFlatPlate;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing.FlatPlate
{
    /// <summary>Routes flat-plate dimension modules by resolved sub-kind.</summary>
    internal static class FlatPlateDimRouter
    {
        public static void ApplyAll(
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            SmartDimHelper dimHelper,
            FlatPlateDimContext context,
            Action<string> log)
        {
            context.ExpectedThicknessMm = analysis.EstProperties.Dim1Mm is double t && t > 0.4 && t <= 80
                ? t
                : null;

            if (context.SubKind == FlatPlateSubKind.BafflePlate)
            {
                context.DimensionSamples = BafflePlateDimensionPipeline.Apply(
                    model, drawing, dimHelper, context.PrimaryFlatView, analysis, log);
                // Baffle pipeline already deduped after import; outer passes would re-pay HLV cost.
                context.SkipPostPipelineDedupe = true;
                context.SkipAutoArrange = true;
                return;
            }

            if (context.SubKind == FlatPlateSubKind.FlangeGasket)
            {
                FlangeGasketDimensionPipeline.Apply(
                    model, drawing, dimHelper, context.DiscFaceView, analysis, log);
                return;
            }

            TryModelImport(model, drawing, analysis, context, log);

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
                ApplyForView(model, drawing, dimHelper, context, dimView, log);
                dimView = dimView.GetNextView() as IView;
            }

            if (context.UsesDiscStyleThickness)
            {
                RoundFlatPlateThickness.TryAddOnce(
                    dimHelper, drawing, log, useOutlineSidePick: true,
                    expectedThicknessMm: context.ExpectedThicknessMm);
            }
        }

        public static void ApplyForView(
            IModelDoc2 model,
            IDrawingDoc drawing,
            SmartDimHelper dimHelper,
            FlatPlateDimContext context,
            IView view,
            Action<string> log)
        {
            bool isPrimary = context.PrimaryFlatView != null &&
                ReferenceEquals(view, context.PrimaryFlatView);

            switch (context.SubKind)
            {
                case FlatPlateSubKind.RoundDisc:
                    RoundFlatPlateDimensions.AddForView(dimHelper, model, drawing, view, log);
                    break;

                case FlatPlateSubKind.RoundedEnd:
                    if (isPrimary && context.ModelImportUsed)
                        break;
                    if (isPrimary)
                        RoundedFlatPlateDimensions.AddForPrimaryView(dimHelper, drawing, view, log);
                    else
                        RoundedFlatPlateDimensions.AddSideViewOnly(
                            dimHelper, view, log, context.ExpectedThicknessMm);
                    break;

                default:
                    ApplyGenericView(dimHelper, context, view, isPrimary, log);
                    break;
            }
        }

        private static void ApplyGenericView(
            SmartDimHelper dimHelper,
            FlatPlateDimContext context,
            IView view,
            bool isPrimary,
            Action<string> log)
        {
            bool skipPrimarySmart = isPrimary && context.ModelImportUsed;

            if (!skipPrimarySmart && (context.PrimaryFlatView == null || isPrimary))
                SmartDimOverall.Add(dimHelper, view, log);

            SmartDimThickness.Add(dimHelper, view, log, context.ExpectedThicknessMm);

            if (!skipPrimarySmart)
            {
                SmartDimFillets.Add(dimHelper, view, log);
                SmartDimChamfers.Add(dimHelper, view, log);
                SmartDimHoles.AddForStandardViews(dimHelper, view, log: log);
                SmartDimHolePositions.Add(dimHelper, view, log);
                SmartDimCutouts.Add(dimHelper, view, log);
            }
            else
            {
                // Even with model import, still add missing fillet/chamfer callouts.
                SmartDimFillets.Add(dimHelper, view, log);
                SmartDimChamfers.Add(dimHelper, view, log);
            }
        }

        private static void TryModelImport(
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            FlatPlateDimContext context,
            Action<string> log)
        {
            if (context.SkipsModelImport ||
                !analysis.CanImportSketchDimensions ||
                context.PrimaryFlatView == null)
            {
                return;
            }

            int added = DrawingModelDimensionImport.ImportOnce(
                model, drawing, context.PrimaryFlatView, log);

            context.ModelImportUsed = added > 0;
            if (!context.ModelImportUsed)
                return;

            log("Using sketch-driven model dimensions on primary view (smart dims skipped there).");
            log("Checking for duplicate dimensions after model import...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                model, drawing, log, SmartDimConstants.IsometricViewName);
        }
    }
}
