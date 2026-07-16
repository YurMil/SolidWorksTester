using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.Services.Drawing.Routing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Drawing pipeline for sheet-metal Lofted Bends shells (EST SHELL family).
    /// Three orthographic views + isometric + flat pattern (inner face up):
    /// side — height, OD, axis centerline; end — wall thickness + weld-gap centerline/dim; FP — length/width.
    /// </summary>
    internal static class LoftedBendsDrawingPipeline
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
            using var timer = new PipelineStopwatch(log, $"pipeline {route.PipelineLabel}");

            log($"Using Lofted Bends shell pipeline (3 views + iso + flat pattern, inner-up). [{route.PipelineLabel}]");

            timer.Measure("delete existing views", () =>
                DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log));
            timer.Measure("create standard views", () =>
                DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log));
            timer.Measure("create isometric view", () =>
                DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log));
            timer.Measure("create flat pattern (inner up)", () =>
                LoftedBendsFlatPattern.Create(drawingModel, drawing, partPath, log));
            timer.Measure("apply HLV display mode", () =>
                DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log));
            timer.Measure("mass units kg", () =>
                DrawingMassUnits.EnsureKilograms(drawingModel, drawing, log));
            timer.Measure("apply dimensions", () =>
                LoftedBendsDimensions.Apply(swApp, drawingModel, drawing, analysis, log));
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
