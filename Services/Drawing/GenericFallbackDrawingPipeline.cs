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
            using var timer = new PipelineStopwatch(log, "pipeline GenericFallback");

            log($"Using generic fallback pipeline ({route.Summary}).");

            timer.Measure("delete existing views", () =>
                DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log));
            timer.Measure("create standard views", () =>
                DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log));
            timer.Measure("create isometric view", () =>
                DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log));
            timer.Measure("apply HLV display mode", () =>
                DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log));
            timer.Measure("dedupe dimensions", () =>
            {
                log("Checking for duplicate dimensions...");
                DrawingDimensionDeduper.RemoveDuplicateDimensions(
                    drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
            });
            timer.Measure("auto-arrange dimensions", () =>
                DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing));
            timer.Measure("EST quality validate", () =>
                DrawingPipelineShared.ValidateEstDrawingQuality(drawing, analysis, log));
            timer.Measure("sheet layout normalize", () =>
                SheetLayoutNormalizer.Arrange(drawingModel, drawing, log));
            timer.Measure("force rebuild", () =>
                drawingModel.ForceRebuild3(true));
        }
    }
}
