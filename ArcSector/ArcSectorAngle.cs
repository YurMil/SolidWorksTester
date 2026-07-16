using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.ArcSector
{
    /// <summary>Branch: angular sweep between the two radial end faces.</summary>
    internal static class ArcSectorAngle
    {
        public static void Add(
            SmartDimHelper h,
            IView view,
            string viewName,
            ArcSectorProfile profile,
            double minX,
            double minY,
            double maxX,
            double maxY,
            Action<string> log)
        {
            const string key = "ArcSector_Angle";
            Edge[] radials = profile.RadialEdges;
            if (radials.Length < 2 || h.DimensionedFeatures.Contains(key))
                return;

            h.ClearSelection();
            if (!h.SelectEdge(radials[0], view, false) || !h.SelectEdge(radials[1], view, true))
                return;

            double tx = (minX + maxX) / 2.0;
            double ty = Math.Max(maxY, profile.CenterY) + ArcSectorDimHelpers.DimOffset;
            if (profile.CenterY > maxY)
                ty = (profile.CenterY + maxY) / 2.0;
            else if (profile.CenterY < minY)
                ty = (profile.CenterY + minY) / 2.0;

            var dim = h.CreateAngularDimension(tx, ty);
            if (dim == null)
                return;

            h.DimensionedFeatures.Add(key);
            log($"  [ArcSector] Sector angle on {viewName}.");
        }
    }
}
