using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    public static class DrawingDimensionCollector
    {
        public static IReadOnlyList<DrawingDimensionSample> Collect(IDrawingDoc drawing)
        {
            var list = new List<DrawingDimensionSample>();
            IView? sheetView = drawing.GetFirstView() as IView;
            IView? view = sheetView?.GetNextView() as IView;

            while (view != null)
            {
                string viewName = view.GetName2();
                CollectFromView(view, viewName, list);
                view = view.GetNextView() as IView;
            }

            return list;
        }

        private static void CollectFromView(IView view, string viewName, List<DrawingDimensionSample> list)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? display = ann.GetSpecificAnnotation() as DisplayDimension;
                    Dimension? modelDim = display?.GetDimension2(0) as Dimension;
                    if (display != null && modelDim != null)
                    {
                        double valueMeters = Math.Abs(modelDim.SystemValue);
                        list.Add(new DrawingDimensionSample
                        {
                            Type = display.Type2,
                            ValueMm = Math.Round(valueMeters * 1000.0, 3),
                            Prefix = display.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty,
                            ViewName = viewName
                        });
                    }
                }

                ann = ann.GetNext3() as Annotation;
            }
        }
    }
}
