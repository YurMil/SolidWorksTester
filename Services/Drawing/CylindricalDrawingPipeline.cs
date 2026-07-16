using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;

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
            DrawingRouteDecision route,
            Action<string> log)
        {
            var drawing = (IDrawingDoc)drawingModel;
            using var timer = new PipelineStopwatch(log, $"pipeline {route.PipelineLabel}");

            log($"Using cylindrical drawing pipeline (3 views + isometric). [{route.PipelineLabel}]");

            timer.Measure("delete existing views", () =>
                DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log));
            timer.Measure("create standard views", () =>
                DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log));
            timer.Measure("create isometric view", () =>
                DrawingPipelineShared.CreateIsometricView(drawingModel, drawing, partPath, log));
            timer.Measure("apply HLV display mode", () =>
                DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log));
            timer.Measure("apply annotations", () =>
                ApplyAnnotations(swApp, drawingModel, drawing, analysis, log));
            timer.Measure("dedupe dimensions", () =>
            {
                log("Checking for duplicate dimensions...");
                DrawingDimensionDeduper.RemoveDuplicateDimensions(
                    drawingModel, drawing, log, "Drawing View4");
                DrawingDimensionDeduper.RemoveQuantityPrefixedDiameterDuplicates(
                    drawingModel, drawing, log, "Drawing View4");
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
                PreCacheOrthographicEdges(dimHelper, drawing);

                var (cutCount, holeCount, chamferCount) = ProbeReferencedPart(drawing, log);
                bool runChamfers = analysis.HasChamfers || chamferCount > 0;

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
                        CylindricalDimSizes.Add(dimHelper, dimView, analysis, log);

                        if (runChamfers)
                            CylindricalDimChamfers.Add(dimHelper, dimView, log);

                        CylindricalDimCuts.Add(dimHelper, dimView, log);
                    }

                    dimView = dimView.GetNextView() as IView;
                }
            }
            finally
            {
                dimHelper.ClearViewCaches();
                dimHelper.RestoreDimInput();
            }
        }

        private static (int Cuts, int Holes, int Chamfers) ProbeReferencedPart(
            IDrawingDoc drawing,
            Action<string> log)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            IModelDoc2? partDoc = null;
            while (view != null)
            {
                try
                {
                    if (view.ReferencedDocument is IModelDoc2 doc)
                    {
                        partDoc = doc;
                        break;
                    }

                    object[]? comps = view.GetVisibleComponents() as object[];
                    if (comps is { Length: > 0 } &&
                        comps[0] is Component2 comp &&
                        comp.GetModelDoc2() is IModelDoc2 fromComp)
                    {
                        partDoc = fromComp;
                        break;
                    }
                }
                catch
                {
                    // try next view
                }

                view = view.GetNextView() as IView;
            }

            var counts = CylindricalDimCuts.ProbeModelFeatures(partDoc);
            if (counts.Cuts > 0 || counts.Holes > 0 || counts.Chamfers > 0)
            {
                log($"  Model features: cuts={counts.Cuts}, holes={counts.Holes}, " +
                    $"chamfer/fillet={counts.Chamfers} — secondary dims enabled.");
            }
            else
            {
                log("  Model features: no cut/hole/chamfer ops — secondary dim scan still runs on edges.");
            }

            return counts;
        }

        private static void PreCacheOrthographicEdges(SmartDimHelper dimHelper, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!view.GetName2().Equals("Drawing View4", StringComparison.OrdinalIgnoreCase))
                    dimHelper.PreCacheViewEdges(view);

                view = view.GetNextView() as IView;
            }
        }
    }
}
