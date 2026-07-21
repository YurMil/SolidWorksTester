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
            Annotation? ann = DrawingAnnotationWalk.GetFirst(view);
            while (ann != null)
            {
                DisplayDimension? display = DrawingAnnotationWalk.AsDisplayDimension(ann);
                Dimension? modelDim = display != null
                    ? DrawingAnnotationWalk.GetModelDimension(display)
                    : null;
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

                ann = DrawingAnnotationWalk.GetNext(ann);
            }
        }
    }
}
