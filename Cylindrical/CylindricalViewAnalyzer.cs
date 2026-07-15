using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>Classifies cylindrical part drawing views as end-face vs side profile.</summary>
    internal static class CylindricalViewAnalyzer
    {
        private const double MinProfileRadiusMeters = 0.0005;

        public static bool IsIsometricView(IView view) =>
            view.GetName2().Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when the view shows a circular end section (tube/pipe end face), not a length side view.
        /// </summary>
        public static bool IsEndFaceView(SmartDimHelper h, IView view, Edge[] edges)
        {
            if (edges.Length == 0)
                return false;

            var profileCircles = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinProfileRadiusMeters)
                .Where(e => h.IsCircleProfileInView(e, view))
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();

            if (profileCircles.Length == 0)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;
            double bboxMax = Math.Max(bboxWidth, bboxHeight);
            double bboxMin = Math.Min(bboxWidth, bboxHeight);

            double outerRadius = h.GetCircleRadius(profileCircles[0]);
            double outerDiameterSheet = outerRadius * 2.0 * view.ScaleDecimal;

            // Side views are elongated (length >> OD). End face is roughly square with OD ≈ bbox.
            if (bboxMax > outerDiameterSheet * 1.35 && bboxMin <= outerDiameterSheet * 1.05)
                return false;

            return true;
        }

        public static Edge[] GetEndFaceCircles(SmartDimHelper h, IView view, Edge[] edges) =>
            edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinProfileRadiusMeters)
                .Where(e => h.IsCircleProfileInView(e, view))
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();

        public static bool IsSideView(SmartDimHelper h, IView view, Edge[] edges) =>
            !IsEndFaceView(h, view, edges);
    }
}
