using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Cylindrical;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>Dimensions for round / disc-like flat sheet metal plates.</summary>
    internal static class RoundFlatPlateDimensions
    {
        public static void AddForView(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            string viewName = view.GetName2();
            Edge? outerCircle = RoundFlatPlateViewAnalyzer.GetOuterProfileCircle(h, view);

            if (outerCircle != null)
            {
                CylindricalDimCenterlines.Add(h, model, drawing, view, log);

                double outerDiameter = h.GetCircleRadius(outerCircle) * 2.0;
                TryOuterDiameter(h, view, viewName, outerCircle, outerDiameter, log);

                SmartDimHoles.AddForStandardViews(h, view, excludeDiameter: outerDiameter);
                return;
            }

            SmartDimHoles.AddForStandardViews(h, view);
            SmartDimHolePositions.Add(h, view);
            SmartDimCutouts.Add(h, view);
        }

        /// <summary>Called once after all views — thickness belongs on the best side view only.</summary>
        public static void AddThicknessOnce(SmartDimHelper h, IDrawingDoc drawing, Action<string> log) =>
            RoundFlatPlateThickness.TryAddOnce(h, drawing, log);

        private static void TryOuterDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge outerCircle,
            double diameter,
            Action<string> log)
        {
            string key = $"RoundPlate_OD_{diameter:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(outerCircle, view, false))
                    return;

                double[] center = h.GetCircleCenterOnSheet(outerCircle, view);
                var dim = h.CreateDimension(center[0], center[1] + 0.012);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  OD Ø{diameter * 1000:F1} mm in {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }
    }
}
