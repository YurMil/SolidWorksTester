using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing
{
    internal static class DrawingPipelineShared
    {
        public static void DeleteExistingViews(IModelDoc2 model, IDrawingDoc drawing, Action<string> log)
        {
            log("Removing existing views...");
            IModelDocExtension modelDocExt = model.Extension;

            // Collect names first — live COM iteration while deleting can hang or invalidate pointers.
            var viewNames = new List<string>();
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                viewNames.Add(view.GetName2());
                view = view.GetNextView() as IView;
            }

            foreach (string viewName in viewNames)
            {
                log($"  Deleting view: {viewName}");
                model.ClearSelection2(true);
                if (modelDocExt.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0))
                    model.EditDelete();
            }

            model.ForceRebuild3(true);
        }

        public static void CreateStandardThreeViews(
            IModelDoc2 model,
            IDrawingDoc drawing,
            string partPath,
            Action<string> log)
        {
            log("Creating standard views (front, top, right)...");
            IModelDocExtension modelDocExt = model.Extension;

            var layout = GetSheetLayout(drawing);

            IView? frontView = drawing.CreateDrawViewFromModelView3(
                partPath, "*Front", layout.FrontX, layout.FrontY, 0.0);
            if (frontView == null)
                throw new InvalidOperationException("Failed to create front view.");

            frontView.SetName2("Drawing View1");
            string frontName = frontView.GetName2();
            frontView.UseSheetScale = 1;
            log($"  Front view created at ({layout.FrontX:F3}, {layout.FrontY:F3}).");

            model.ClearSelection2(true);
            if (!modelDocExt.SelectByID2(frontName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0))
                throw new InvalidOperationException($"Failed to select front view '{frontName}'.");
            IView? topView = drawing.CreateUnfoldedViewAt3(
                layout.TopX, layout.TopY, 0.0, false);
            if (topView != null)
            {
                topView.SetName2("Drawing View2");
                topView.UseSheetScale = 1;
                log("  Top view created.");
            }

            model.ClearSelection2(true);
            if (!modelDocExt.SelectByID2(frontName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0))
                throw new InvalidOperationException($"Failed to select front view '{frontName}'.");
            IView? rightView = drawing.CreateUnfoldedViewAt3(
                layout.RightX, layout.RightY, 0.0, false);
            if (rightView != null)
            {
                rightView.SetName2("Drawing View3");
                rightView.UseSheetScale = 1;
                log("  Right view created.");
            }

            model.ForceRebuild3(true);
        }

        private static DrawingViewLayout.SheetLayout GetSheetLayout(IDrawingDoc drawing)
        {
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet?.GetProperties2() is double[] props && props.Length >= 7)
                return DrawingViewLayout.ForSheet(props[5], props[6]);

            return DrawingViewLayout.ForSheet(0.420, 0.297);
        }

        public static void CreateIsometricView(
            IModelDoc2 model,
            IDrawingDoc drawing,
            string partPath,
            Action<string> log)
        {
            log("Creating isometric view...");

            // Seed only — final position comes from SheetLayoutNormalizer (top-right work area).
            double x = DrawingViewLayout.IsometricX;
            double y = DrawingViewLayout.IsometricY;
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet?.GetProperties2() is double[] props && props.Length >= 7)
            {
                x = props[5] * 0.78;
                y = props[6] * 0.78;
            }

            IView? isoView = drawing.CreateDrawViewFromModelView3(
                partPath,
                "*Isometric",
                x,
                y,
                0.0);

            if (isoView == null)
            {
                log("  Warning: isometric view was not created.");
                return;
            }

            isoView.SetName2(SmartDim.SmartDimConstants.IsometricViewName);
            isoView.UseSheetScale = 1;
            log($"  Isometric view created (seed {x:F3}, {y:F3}; final pack later).");
            model.ForceRebuild3(true);
        }

        public static void AutoArrangeDimensions(IModelDoc2 model, IDrawingDoc drawing)
        {
            IView? dimViewAuto = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (dimViewAuto != null)
            {
                model.ClearSelection2(true);
                Annotation? swAnn = dimViewAuto.GetFirstAnnotation3();
                int dimCount = 0;

                while (swAnn != null)
                {
                    if (swAnn.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                    {
                        swAnn.Select3(true, null);
                        dimCount++;
                    }

                    swAnn = swAnn.GetNext3();
                }

                if (dimCount > 0)
                    model.Extension.AlignDimensions((int)swAlignDimensionType_e.swAlignDimensionType_AutoArrange, 0.005);

                dimViewAuto = dimViewAuto.GetNextView() as IView;
            }

            model.ClearSelection2(true);
        }

        /// <summary>
        /// Legacy name — delegates to <see cref="SheetLayoutNormalizer.Arrange"/>
        /// (outline-based pack + standard 1:N scale steps).
        /// </summary>
        public static void AdjustSheetScaleIfNeeded(IModelDoc2 model, IDrawingDoc drawing, Action<string> log) =>
            SheetLayoutNormalizer.Arrange(model, drawing, log);

        public static void ValidateEstDrawingQuality(
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            if (!analysis.EstProperties.HasAnyDimension)
                return;

            log("Checking EST dimension quality...");
            EstDrawingQualityValidator.ValidateAndLog(analysis, drawing, log);
        }

        /// <summary>Validate using a pre-collected sample list (no extra annotation COM walk).</summary>
        public static void ValidateEstDrawingQuality(
            PartAnalysisResult analysis,
            IReadOnlyList<DrawingDimensionSample> drawingDimensions,
            Action<string> log)
        {
            if (!analysis.EstProperties.HasAnyDimension)
                return;

            log("Checking EST dimension quality...");
            EstDrawingQualityValidator.ValidateAndLog(analysis, drawingDimensions, log);
        }
    }
}
