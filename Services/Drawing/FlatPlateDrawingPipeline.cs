using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.FlatPlate;
using SolidWorksTester.Services.Drawing.Routing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Drawing pipeline for flat sheet metal plates without bends.
    /// Three orthographic views + isometric (iso skipped for baffle family).
    /// </summary>
    internal static class FlatPlateDrawingPipeline
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

            bool baffle = DrawingSheetProfile.IsBaffleFamily(analysis, route);
            log(baffle
                ? $"Using flat-plate drawing pipeline (3 views, no isometric). [{route.PipelineLabel}]"
                : $"Using flat-plate drawing pipeline (3 views + isometric, no flat pattern). [{route.PipelineLabel}]");

            bool formatSwitched = false;
            if (baffle)
            {
                timer.Measure("sheet format EST_A*L", () =>
                    formatSwitched = DrawingSheetProfile.ApplyForBaffleIfNeeded(
                        drawingModel, drawing, analysis, log));
            }

            timer.Measure("delete existing views", () =>
                DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log));

            timer.Measure("create standard views", () =>
                DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log));

            if (formatSwitched)
            {
                timer.Measure("relayout views on new format", () =>
                    DrawingSheetProfile.RelayoutOrthographicViews(drawing, log));
            }

            if (!baffle)
            {
                timer.Measure("create isometric view", () =>
                    DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log));
            }
            else
            {
                log("  Skip isometric (baffle family — HLV black blot on dense holes).");
            }

            timer.Measure("apply HLV display mode", () =>
                DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log));

            // After views exist, referenced part is loaded — fix SW-Mass grams vs kg stamp.
            timer.Measure("mass units kg", () =>
                DrawingMassUnits.EnsureKilograms(drawingModel, drawing, log));

            FlatPlateDimContext? dimContext = null;
            timer.Measure("apply dimensions", () =>
                dimContext = ApplyDimensions(swApp, drawingModel, drawing, analysis, route, log));

            if (dimContext?.SkipPostPipelineDedupe == true)
            {
                log("  Skip post-pipeline dedupe (already cleaned inside sub-pipeline).");
            }
            else
            {
                timer.Measure("dedupe dimensions", () =>
                {
                    log("Checking for duplicate dimensions...");
                    DrawingDimensionDeduper.RemoveDuplicateDimensions(
                        drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
                });
            }

            if (dimContext?.SkipAutoArrange == true)
            {
                log("  Skip auto-arrange (baffle/import dims keep model placement).");
            }
            else
            {
                timer.Measure("auto-arrange dimensions", () =>
                    DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing));
            }

            if (baffle || dimContext?.SubKind == FlatPlateSubKind.BafflePlate)
            {
                timer.Measure("baffle family notes", () =>
                    BafflePlateNotes.InsertFamilyNotes(drawingModel, drawing, analysis, log));
            }

            timer.Measure("EST quality validate", () =>
            {
                IReadOnlyList<DrawingDimensionSample> samples;
                if (dimContext?.DimensionSamples != null)
                {
                    log("  EST validate from dedupe snapshot (no COM collect).");
                    samples = dimContext.DimensionSamples;
                }
                else
                {
                    samples = DrawingDimensionCollector.Collect(drawing);
                }

                DrawingPipelineShared.ValidateEstDrawingQuality(analysis, samples, log);
            });

            // After dims + notes: outlines include annotations — final pack for all families.
            timer.Measure("sheet layout normalize", () =>
                SheetLayoutNormalizer.Arrange(drawingModel, drawing, log));

            timer.Measure("force rebuild", () =>
                drawingModel.ForceRebuild3(true));
        }

        private static FlatPlateDimContext ApplyDimensions(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            Action<string> log)
        {
            log("Adding dimensions (standard views only)...");
            IModelDocExtension modelDocExt = model.Extension;
            SmartDimHelper dimHelper = new SmartDimHelper(swApp, model, drawing, modelDocExt);
            dimHelper.SuppressDimInput();

            try
            {
                FlatPlateDimContext context = PipelineStopwatch.Run(log, "resolve flat-plate sub-kind", () =>
                    FlatPlateSubKindResolver.Resolve(analysis, route, dimHelper, drawing));

                log(FlatPlateSubKindResolver.DescribeSubKind(context.SubKind));

                if (context.PrimaryFlatView != null)
                    log($"Primary flat view: {context.PrimaryFlatView.GetName2()}");

                if (context.SubKind != FlatPlateSubKind.BafflePlate)
                {
                    PipelineStopwatch.Run(log, "pre-cache orthographic edges", () =>
                        PreCacheOrthographicEdges(dimHelper, drawing, log));
                }
                else
                {
                    log("  Skip edge pre-cache for baffle (dense hole array).");
                }

                FlatPlateDimRouter.ApplyAll(model, drawing, analysis, dimHelper, context, log);
                return context;
            }
            finally
            {
                dimHelper.ClearViewCaches();
                dimHelper.RestoreDimInput();
            }
        }

        private static void PreCacheOrthographicEdges(
            SmartDimHelper dimHelper,
            IDrawingDoc drawing,
            Action<string> log)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (!name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    PipelineStopwatch.Run(log, $"pre-cache edges: {name}", () =>
                    {
                        dimHelper.PreCacheViewEdges(view);
                        int count = dimHelper.GetViewEdgesCached(view).Length;
                        log($"    {name}: {count} edge(s)");
                    });
                }

                view = view.GetNextView() as IView;
            }
        }
    }
}
