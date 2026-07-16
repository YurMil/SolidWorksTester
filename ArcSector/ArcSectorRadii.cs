using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.ArcSector
{
    /// <summary>Branch: inner / outer profile radii (R_in, R_out).</summary>
    internal static class ArcSectorRadii
    {
        public static void Add(
            SmartDimHelper h,
            IView view,
            string viewName,
            ArcSectorProfile profile,
            Action<string> log)
        {
            TryOne(h, view, viewName, profile.InnerArc, profile.InnerRadius,
                profile.CenterX, profile.CenterY, "ArcSector_R_In", log);
            TryOne(h, view, viewName, profile.OuterArc, profile.OuterRadius,
                profile.CenterX, profile.CenterY, "ArcSector_R_Out", log);
        }

        private static void TryOne(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge arc,
            double radiusModel,
            double cx,
            double cy,
            string key,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains(key) ||
                h.HasDimensionWithValueInDrawing(radiusModel, (int)swDimensionType_e.swRadialDimension) ||
                h.HasDimensionWithValueInDrawing(radiusModel * 2.0, (int)swDimensionType_e.swDiameterDimension))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            h.ClearSelection();
            if (!h.SelectEdge(arc, view, false))
                return;

            double[] mid = h.GetEdgeMidpointOnSheet(arc, view);
            ArcSectorDimHelpers.OffsetAwayFromCenter(
                mid[0], mid[1], cx, cy, out double tx, out double ty);

            DisplayDimension? dim =
                h.Model.AddRadialDimension2(tx, ty, 0.0) as DisplayDimension;

            if (dim == null)
            {
                int err = 0;
                dim = h.Ext.AddSpecificDimension(
                    tx, ty, 0.0,
                    (int)swDimensionType_e.swRadialDimension,
                    ref err) as DisplayDimension;
            }

            if (dim == null)
                return;

            h.DimensionedFeatures.Add(key);
            log($"  [ArcSector] {key} R{radiusModel * 1000:F1} mm on {viewName}.");
        }
    }
}
