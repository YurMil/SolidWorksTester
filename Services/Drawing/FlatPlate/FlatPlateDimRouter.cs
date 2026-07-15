using System;
using SolidWorks.Interop.sldworks;
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
                RoundFlatPlateThickness.TryAddOnce(dimHelper, drawing, log);
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
                        RoundedFlatPlateDimensions.AddSideViewOnly(dimHelper, view, log);
                    break;

                default:
                    ApplyGenericView(dimHelper, context, view, isPrimary);
                    break;
            }
        }

        private static void ApplyGenericView(
            SmartDimHelper dimHelper,
            FlatPlateDimContext context,
            IView view,
            bool isPrimary)
        {
            bool skipPrimarySmart = isPrimary && context.ModelImportUsed;

            if (!skipPrimarySmart && (context.PrimaryFlatView == null || isPrimary))
                SmartDimOverall.Add(dimHelper, view);

            SmartDimThickness.Add(dimHelper, view);

            if (!skipPrimarySmart)
            {
                SmartDimHoles.AddForStandardViews(dimHelper, view);
                SmartDimHolePositions.Add(dimHelper, view);
                SmartDimCutouts.Add(dimHelper, view);
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
