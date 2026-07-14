using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.RoundFlatPlate;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Drawing pipeline for flat sheet metal plates without bends.
    /// Three orthographic views + isometric (no flat pattern).
    /// </summary>
    internal static class FlatPlateDrawingPipeline
    {
        public static void Process(
            ISldWorks swApp,
            IModelDoc2 drawingModel,
            string partPath,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            var drawing = (IDrawingDoc)drawingModel;

            log("Using flat-plate drawing pipeline (3 views + isometric, no flat pattern).");

            DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log);
            DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log);
            DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log);
            ApplyDimensions(swApp, drawingModel, drawing, analysis, log);
            log("Checking for duplicate dimensions...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
            DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing);
            DrawingPipelineShared.AdjustSheetScaleIfNeeded(drawingModel, drawing, log);
            drawingModel.ForceRebuild3(true);
        }

        private static void ApplyDimensions(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            log("Adding dimensions (standard views only)...");
            IModelDocExtension modelDocExt = model.Extension;
            SmartDimHelper dimHelper = new SmartDimHelper(swApp, model, drawing, modelDocExt);
            dimHelper.SuppressDimInput();

            try
            {
                bool isRoundProfile = analysis.IsRoundFlatProfile ||
                    RoundFlatPlateViewAnalyzer.DetectFromDrawing(dimHelper, drawing);

                if (isRoundProfile)
                    log("Round flat plate mode: OD, centerlines, side-view thickness.");

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

                    if (isRoundProfile)
                    {
                        RoundFlatPlateDimensions.AddForView(dimHelper, model, drawing, dimView, log);
                    }
                    else
                    {
                        SmartDimOverall.Add(dimHelper, dimView);
                        SmartDimThickness.Add(dimHelper, dimView);
                        SmartDimHoles.AddForStandardViews(dimHelper, dimView);
                        SmartDimHolePositions.Add(dimHelper, dimView);
                        SmartDimCutouts.Add(dimHelper, dimView);
                    }

                    dimView = dimView.GetNextView() as IView;
                }

                if (isRoundProfile)
                    RoundFlatPlateDimensions.AddThicknessOnce(dimHelper, drawing, log);
            }
            finally
            {
                dimHelper.RestoreDimInput();
            }
        }
    }
}
