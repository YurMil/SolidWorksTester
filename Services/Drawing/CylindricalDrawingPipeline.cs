using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Drawing pipeline optimized for cylindrical / pipe-like parts.
    /// Three orthographic views + isometric, centerlines, model-driven dimensions.
    /// </summary>
    internal static class CylindricalDrawingPipeline
    {
        public static void Process(
            ISldWorks swApp,
            IModelDoc2 drawingModel,
            string partPath,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            var drawing = (IDrawingDoc)drawingModel;

            log("Using cylindrical drawing pipeline (3 views + isometric).");

            DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log);
            DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log);
            DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log);
            DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log);
            ApplyAnnotations(swApp, drawingModel, drawing, analysis, log);
            log("Checking for duplicate dimensions...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                drawingModel, drawing, log, "Drawing View4");
            DrawingDimensionDeduper.RemoveQuantityPrefixedDiameterDuplicates(
                drawingModel, drawing, log, "Drawing View4");
            DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing);
            DrawingPipelineShared.AdjustSheetScaleIfNeeded(drawingModel, drawing, log);
            drawingModel.ForceRebuild3(true);
        }

        private static void ApplyAnnotations(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            log("Adding cylindrical annotations...");
            IModelDocExtension modelDocExt = model.Extension;
            SmartDimHelper dimHelper = new SmartDimHelper(swApp, model, drawing, modelDocExt);
            dimHelper.SuppressDimInput();

            try
            {
                IView? primaryView = null;
                IView? dimView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;

                while (dimView != null)
                {
                    if (!SolidWorksComException.IsAlive(swApp))
                        throw new InvalidOperationException("SOLIDWORKS connection lost during cylindrical annotations.");

                    string vName = dimView.GetName2();
                    log($"  Centerlines: {vName}");
                    CylindricalDimCenterlines.Add(dimHelper, model, drawing, dimView, log);
                    if (primaryView == null && !vName.Equals("Drawing View4", StringComparison.OrdinalIgnoreCase))
                        primaryView = dimView;

                    dimView = dimView.GetNextView() as IView;
                }

                if (primaryView != null)
                {
                    CylindricalDimModelImport.ImportOnce(model, drawing, primaryView, log);
                    log("Checking for duplicate dimensions after model import...");
                    DrawingDimensionDeduper.RemoveDuplicateDimensions(
                        model, drawing, log, "Drawing View4");
                    DrawingDimensionDeduper.RemoveQuantityPrefixedDiameterDuplicates(
                        model, drawing, log, "Drawing View4");
                }

                dimView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
                while (dimView != null)
                {
                    if (!SolidWorksComException.IsAlive(swApp))
                        throw new InvalidOperationException("SOLIDWORKS connection lost during cylindrical annotations.");

                    string vName = dimView.GetName2();
                    bool isIsometric = vName.Equals("Drawing View4", StringComparison.OrdinalIgnoreCase);

                    if (!isIsometric)
                    {
                        log($"  Dimensions: {vName}");
                        CylindricalDimSizes.Add(dimHelper, dimView, analysis.IsHollow, log);

                        if (analysis.HasChamfers)
                            CylindricalDimChamfers.Add(dimHelper, dimView, log);
                    }

                    dimView = dimView.GetNextView() as IView;
                }
            }
            finally
            {
                dimHelper.RestoreDimInput();
            }
        }
    }
}
