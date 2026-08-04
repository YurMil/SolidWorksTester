using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Imports model dimensions onto drawing views (Model Items workflow).
    /// </summary>
    internal static class DrawingModelDimensionImport
    {
        private const double MinDimensionMeters = 0.0001;

        public static int CountDimensionsInDrawing(IDrawingDoc drawing)
        {
            int count = 0;
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                count += CountDimensionsInView(view);
                view = view.GetNextView() as IView;
            }

            return count;
        }

        public static int CountDimensionsInView(IView view)
        {
            int count = 0;
            var visited = new System.Collections.Generic.HashSet<int>();
            Annotation? ann = DrawingAnnotationWalk.GetFirst(view);
            int guard = 0;
            while (ann != null && guard++ < 400)
            {
                int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ann);
                if (!visited.Add(id))
                    break;

                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                    count++;

                ann = DrawingAnnotationWalk.GetNext(ann);
            }

            return count;
        }

        /// <summary>Import marked-for-drawing dimensions into all views (flange/gasket workflow).</summary>
        public static int ImportMarkedDimensionsToAllViews(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log)
        {
            int before = CountDimensionsInDrawing(drawing);

            model.ClearSelection2(true);

            try
            {
                int source = (int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel;
                int types = (int)swInsertAnnotation_e.swInsertDimensionsMarkedForDrawing;

                drawing.InsertModelAnnotations3(
                    source,
                    types,
                    true,
                    true,
                    false,
                    false);

                int removed = RemoveZeroValueDimensions(model, drawing);
                int after = CountDimensionsInDrawing(drawing);
                int added = Math.Max(0, after - before);

                log($"  Model import (all views, marked dims): {added} added" +
                    (removed > 0 ? $", {removed} zero-value removed." : "."));

                if (added == 0)
                    added = TryImportAllDimensions(model, drawing, log, before);

                return added;
            }
            catch (Exception ex)
            {
                log($"  Model dimension import warning: {ex.Message}");
                return 0;
            }
        }

        public static int ImportOnce(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView primaryView,
            Action<string> log)
        {
            string viewName = primaryView.GetName2();
            int before = CountDimensionsInView(primaryView);

            drawing.ActivateView(viewName);
            model.ClearSelection2(true);
            model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);

            try
            {
                int source = (int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel;
                int types = (int)swInsertAnnotation_e.swInsertDimensionsMarkedForDrawing;

                drawing.InsertModelAnnotations3(
                    source,
                    types,
                    false,
                    true,
                    false,
                    false);

                int removed = RemoveZeroValueDimensions(model, primaryView);
                int after = CountDimensionsInView(primaryView);
                int added = Math.Max(0, after - before);

                log($"  Imported model dimensions into {viewName}: {added} added" +
                    (removed > 0 ? $", {removed} zero-value removed." : "."));

                return added;
            }
            catch (Exception ex)
            {
                log($"  Model dimension import warning: {ex.Message}");
                return 0;
            }
        }

        private static int TryImportAllDimensions(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            int countBeforeMarkedAttempt)
        {
            try
            {
                int source = (int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel;
                const int swInsertDimensions = 2;

                drawing.InsertModelAnnotations3(
                    source,
                    swInsertDimensions,
                    true,
                    true,
                    false,
                    false);

                RemoveZeroValueDimensions(model, drawing);
                int after = CountDimensionsInDrawing(drawing);
                int added = Math.Max(0, after - countBeforeMarkedAttempt);

                if (added > 0)
                    log($"  Model import fallback (all dimensions): {added} added.");

                return added;
            }
            catch
            {
                return 0;
            }
        }

        private static int RemoveZeroValueDimensions(IModelDoc2 model, IDrawingDoc drawing)
        {
            int removed = 0;
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                removed += RemoveZeroValueDimensions(model, view);
                view = view.GetNextView() as IView;
            }

            return removed;
        }

        private static int RemoveZeroValueDimensions(IModelDoc2 model, IView view)
        {
            int removed = 0;
            Annotation? ann = DrawingAnnotationWalk.GetFirst(view);
            var toDelete = new System.Collections.Generic.List<IAnnotation>();

            while (ann != null)
            {
                DisplayDimension? displayDim = DrawingAnnotationWalk.AsDisplayDimension(ann);
                if (displayDim != null && IsZeroDimension(displayDim))
                    toDelete.Add(ann);

                ann = DrawingAnnotationWalk.GetNext(ann);
            }

            if (toDelete.Count > 0)
            {
                model.ClearSelection2(true);
                foreach (IAnnotation annotation in toDelete)
                    annotation.Select3(true, null);

                model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                model.ClearSelection2(true);
                removed = toDelete.Count;
            }

            return removed;
        }

        private static bool IsZeroDimension(DisplayDimension displayDim)
        {
            try
            {
                Dimension? dim = DrawingAnnotationWalk.GetModelDimension(displayDim);
                if (dim == null)
                    return false;

                return Math.Abs(dim.SystemValue) < MinDimensionMeters;
            }
            catch
            {
                return false;
            }
        }
    }
}
