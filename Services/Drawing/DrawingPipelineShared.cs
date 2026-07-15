using System;
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

            IView? firstViewNode = drawing.GetFirstView() as IView;
            IView? viewNodeToDelete = firstViewNode?.GetNextView() as IView;

            while (viewNodeToDelete != null)
            {
                string viewName = viewNodeToDelete.GetName2();
                log($"  Deleting view: {viewName}");
                model.ClearSelection2(true);
                modelDocExt.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                model.EditDelete();
                viewNodeToDelete = firstViewNode?.GetNextView() as IView;
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

            IView? frontView = drawing.CreateDrawViewFromModelView3(
                partPath, "*Front", DrawingViewLayout.FrontX, DrawingViewLayout.FrontY, 0.0);
            if (frontView == null)
                throw new InvalidOperationException("Failed to create front view.");

            frontView.SetName2("Drawing View1");
            log("  Front view created.");

            model.ClearSelection2(true);
            modelDocExt.SelectByID2("Drawing View1", "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            IView? topView = drawing.CreateUnfoldedViewAt3(
                DrawingViewLayout.TopX, DrawingViewLayout.TopY, 0.0, false);
            if (topView != null)
            {
                topView.SetName2("Drawing View2");
                log("  Top view created.");
            }

            model.ClearSelection2(true);
            modelDocExt.SelectByID2("Drawing View1", "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            IView? rightView = drawing.CreateUnfoldedViewAt3(
                DrawingViewLayout.RightX, DrawingViewLayout.RightY, 0.0, false);
            if (rightView != null)
            {
                rightView.SetName2("Drawing View3");
                log("  Right view created.");
            }

            model.ForceRebuild3(true);
        }

        public static void CreateIsometricView(
            IModelDoc2 model,
            IDrawingDoc drawing,
            string partPath,
            Action<string> log)
        {
            log("Creating isometric view...");
            IView? isoView = drawing.CreateDrawViewFromModelView3(
                partPath,
                "*Isometric",
                DrawingViewLayout.IsometricX,
                DrawingViewLayout.IsometricY,
                0.0);

            if (isoView == null)
            {
                log("  Warning: isometric view was not created.");
                return;
            }

            isoView.SetName2(SmartDim.SmartDimConstants.IsometricViewName);
            log("  Isometric view created.");
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

        public static void AdjustSheetScaleIfNeeded(IModelDoc2 model, IDrawingDoc drawing, Action<string> log)
        {
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet == null)
                return;

            double[] sheetProps = (double[])sheet.GetProperties2();
            string sheetName = sheet.GetName();
            double sheetWidth = sheetProps[5];
            double sheetHeight = sheetProps[6];
            const double margin = 0.02;

            double availableWidth = sheetWidth - (margin * 2.0);
            double availableHeight = sheetHeight - (margin * 2.0);

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            IView? currentView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            int viewCount = 0;

            while (currentView != null)
            {
                double[]? viewOutline = currentView.GetOutline() as double[];
                if (viewOutline != null)
                {
                    minX = Math.Min(minX, viewOutline[0]);
                    minY = Math.Min(minY, viewOutline[1]);
                    maxX = Math.Max(maxX, viewOutline[2]);
                    maxY = Math.Max(maxY, viewOutline[3]);
                    viewCount++;
                }

                currentView = currentView.GetNextView() as IView;
            }

            if (viewCount == 0)
                return;

            double occupiedWidth = maxX - minX;
            double occupiedHeight = maxY - minY;
            double scaleFactorX = availableWidth / occupiedWidth;
            double scaleFactorY = availableHeight / occupiedHeight;
            double finalScaleFactor = Math.Min(scaleFactorX, scaleFactorY);

            if (finalScaleFactor >= 1.0)
            {
                log("  Sheet scale: no change needed.");
                return;
            }

            double currentDenominator = sheetProps[3];
            double newDenominator = Math.Ceiling(currentDenominator / finalScaleFactor);
            log($"  Auto sheet scale: 1:{newDenominator}");

            string slddrtPath = sheet.GetTemplateName();
            drawing.SetupSheet6(
                sheetName,
                (int)sheetProps[0],
                (int)sheetProps[1],
                (int)sheetProps[2],
                (int)newDenominator,
                sheetProps[4] == 1,
                slddrtPath,
                sheetWidth,
                sheetHeight,
                "Default",
                false,
                0, 0, 0, 0, 0, 0);
        }

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
    }
}
