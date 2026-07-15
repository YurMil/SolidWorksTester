using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Minimal pipeline: standard views + shared cleanup/quality when no family pipeline applies.
    /// </summary>
    internal static class GenericFallbackDrawingPipeline
    {
        public static void Process(
            ISldWorks swApp,
            IModelDoc2 drawingModel,
            string partPath,
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            Action<string> log)
        {
            var drawing = (IDrawingDoc)drawingModel;

            log($"Using generic fallback pipeline ({route.Summary}).");

            DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log);
            DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log);
            DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log);
            DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log);

            log("Checking for duplicate dimensions...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(
                drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
            DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing);
            DrawingPipelineShared.AdjustSheetScaleIfNeeded(drawingModel, drawing, log);
            DrawingPipelineShared.ValidateEstDrawingQuality(drawing, analysis, log);
            drawingModel.ForceRebuild3(true);
        }
    }
}
