using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Imported;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Drawing pipeline for imported STEP/IGES/Interconnect dumb solids.
    /// Uses shape recognition to pick geometry-based dimension algorithms.
    /// </summary>
    internal static class ImportedGeometryDrawingPipeline
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

            log($"Using imported-geometry pipeline (shape: {analysis.ImportedShape}). [{route.PipelineLabel}]");

            timer.Measure("delete existing views", () =>
                DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log));
            timer.Measure("create standard views", () =>
                DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log));
            timer.Measure("create isometric view", () =>
                DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log));
            timer.Measure("apply HLV display mode", () =>
                DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log));
            timer.Measure("apply dimensions", () =>
                ApplyDimensions(swApp, drawingModel, drawing, analysis, log));
            timer.Measure("dedupe dimensions", () =>
            {
                log("Checking for duplicate dimensions...");
                DrawingDimensionDeduper.RemoveDuplicateDimensions(
                    drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
            });
            timer.Measure("auto-arrange dimensions", () =>
                DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing));
            timer.Measure("sheet layout normalize", () =>
                SheetLayoutNormalizer.Arrange(drawingModel, drawing, log));
            timer.Measure("force rebuild", () =>
                drawingModel.ForceRebuild3(true));
        }

        private static void ApplyDimensions(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            log("Adding imported-geometry dimensions...");
            IModelDocExtension modelDocExt = model.Extension;
            SmartDimHelper dimHelper = new SmartDimHelper(swApp, model, drawing, modelDocExt);
            dimHelper.SuppressDimInput();

            try
            {
                PreCacheAllViewEdges(dimHelper, drawing);

                ImportedViewClassification classification =
                    ImportedGeometryViewAnalyzer.Classify(dimHelper, drawing, analysis);

                if (classification.ProfileView != null)
                    log($"  Profile view: {classification.ProfileView.GetName2()}");
                if (classification.LengthPrimaryView != null)
                    log($"  Length view: {classification.LengthPrimaryView.GetName2()}");

                IView? dimView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
                while (dimView != null)
                {
                    string vName = dimView.GetName2();
                    if (vName.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                    {
                        dimView = dimView.GetNextView() as IView;
                        continue;
                    }

                    log($"  Dimensions: {vName} (role: {ImportedGeometryViewAnalyzer.GetRole(classification, dimView)})");
                    ImportedDimStrategy.ApplyForView(
                        dimHelper, model, drawing, dimView, analysis, classification, log);

                    dimView = dimView.GetNextView() as IView;
                }
            }
            finally
            {
                dimHelper.ClearViewCaches();
                dimHelper.RestoreDimInput();
            }
        }

        private static void PreCacheAllViewEdges(SmartDimHelper dimHelper, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                dimHelper.PreCacheViewEdges(view);
                view = view.GetNextView() as IView;
            }
        }
    }
}
