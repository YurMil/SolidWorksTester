using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.ArcSector
{
    /// <summary>Branch: overall bounding-box width / height via outline picks.</summary>
    internal static class ArcSectorOverall
    {
        public static void Add(
            SmartDimHelper h,
            IView view,
            string viewName,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double scale,
            Action<string> log)
        {
            double widthModel = Math.Abs(maxX - minX) / scale;
            double heightModel = Math.Abs(maxY - minY) / scale;

            if (widthModel >= 0.02)
                TryOutlineSpan(h, view, viewName, minX, minY, maxX, maxY,
                    horizontal: true, widthModel, "Overall_W", log);

            if (heightModel >= 0.02)
                TryOutlineSpan(h, view, viewName, minX, minY, maxX, maxY,
                    horizontal: false, heightModel, "Overall_H", log);
        }

        private static void TryOutlineSpan(
            SmartDimHelper h,
            IView view,
            string viewName,
            double minX,
            double minY,
            double maxX,
            double maxY,
            bool horizontal,
            double expectedModel,
            string key,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(expectedModel))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            double midX = (minX + maxX) / 2.0;
            double midY = (minY + maxY) / 2.0;
            h.ClearSelection();
            h.ActivateView(view);

            bool a, b;
            if (horizontal)
            {
                double y = maxY - 0.003;
                a = ArcSectorDimHelpers.SelectAt(h, minX, y);
                b = ArcSectorDimHelpers.SelectAt(h, maxX, y, append: true);
            }
            else
            {
                a = ArcSectorDimHelpers.SelectAt(h, midX, minY);
                b = ArcSectorDimHelpers.SelectAt(h, midX, maxY, append: true);
            }

            if (!a || !b)
                return;

            double tx = horizontal ? midX : maxX + ArcSectorDimHelpers.DimOffset;
            double ty = horizontal ? maxY + ArcSectorDimHelpers.DimOffset : midY;
            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return;

            try
            {
                if (dim.GetDimension2(0) is Dimension modelDim)
                {
                    int max = (int)swArcEndCondition_e.swArcEndConditionMax;
                    modelDim.SetArcEndCondition(1, max);
                    modelDim.SetArcEndCondition(2, max);
                }
            }
            catch
            {
                // ignore
            }

            h.DimensionedFeatures.Add(key);
            log($"  [ArcSector] {(horizontal ? "Width" : "Height")} {expectedModel * 1000:F1} mm (bbox) on {viewName}.");
        }
    }
}
