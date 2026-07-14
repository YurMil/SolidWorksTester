using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Imports driving model dimensions once into the primary view only.
    /// </summary>
    public static class CylindricalDimModelImport
    {
        private const double MinDimensionMeters = 0.0001;

        public static void ImportOnce(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView primaryView,
            Action<string> log)
        {
            string viewName = primaryView.GetName2();
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
                    true);

                int removed = RemoveZeroValueDimensions(model, primaryView);
                log($"  Imported model dimensions into {viewName} (primary view only)" +
                    (removed > 0 ? $", removed {removed} zero-value." : "."));
            }
            catch (Exception ex)
            {
                log($"  Model dimension import warning: {ex.Message}");
            }
        }

        private static int RemoveZeroValueDimensions(IModelDoc2 model, IView view)
        {
            int removed = 0;
            Annotation? ann = view.GetFirstAnnotation3();
            var toDelete = new System.Collections.Generic.List<IAnnotation>();

            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null && IsZeroDimension(displayDim))
                        toDelete.Add(ann);
                }

                ann = ann.GetNext3();
            }

            if (toDelete.Count > 0)
            {
                model.ClearSelection2(true);
                foreach (IAnnotation annotation in toDelete)
                    annotation.Select3(true, null);

                model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                model.ClearSelection2(true);
            }

            return removed;
        }

        private static bool IsZeroDimension(DisplayDimension displayDim)
        {
            try
            {
                Dimension? dim = displayDim.GetDimension2(0) as Dimension;
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
