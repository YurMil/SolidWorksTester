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
        /// True when the view shows a circular / semi-circular end section (full or cut pipe),
        /// not a length side view.
        /// </summary>
        public static bool IsEndFaceView(SmartDimHelper h, IView view, Edge[] edges)
        {
            if (edges.Length == 0)
                return false;

            Edge[] profileCircles = GetEndFaceCircles(h, view, edges);
            if (profileCircles.Length == 0)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;
            double bboxMax = Math.Max(bboxWidth, bboxHeight);
            double bboxMin = Math.Min(bboxWidth, bboxHeight);

            double outerRadius = h.GetCircleRadius(profileCircles[0]);
            double outerDiameterSheet = outerRadius * 2.0 * Math.Max(view.ScaleDecimal, 1e-9);

            // Side views are elongated (length >> OD).
            // Full end face ≈ square; cut/half pipe end ≈ OD × ~OD/2 — still not a long side silhouette.
            if (bboxMax > outerDiameterSheet * 1.35 && bboxMin <= outerDiameterSheet * 1.05)
                return false;

            return true;
        }

        /// <summary>
        /// Outer/inner profile circles or arcs on an end-face view (full pipe OR lengthwise-cut pipe).
        /// Dedupes by radius so multi-segment arcs of the same wall count once.
        /// </summary>
        public static Edge[] GetEndFaceCircles(SmartDimHelper h, IView view, Edge[] edges)
        {
            var circular = edges
                .Where(e => h.IsCircular(e) && h.GetCircleRadius(e) > MinProfileRadiusMeters)
                .Where(e => h.IsFullCircle(e) || h.IsCircleProfileInView(e, view))
                .ToArray();

            if (circular.Length == 0)
                return Array.Empty<Edge>();

            // One representative edge per distinct radius (prefer longer projected arc / full circle).
            return circular
                .GroupBy(e => Math.Round(h.GetCircleRadius(e), 5))
                .Select(g => g
                    .OrderByDescending(e => h.IsFullCircle(e) ? 1 : 0)
                    .ThenByDescending(e => h.GetProjectedLength(e, view))
                    .First())
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();
        }

        public static bool IsSideView(SmartDimHelper h, IView view, Edge[] edges) =>
            !IsEndFaceView(h, view, edges);

        /// <summary>True when end profile is arc-based (cut/half pipe), not a closed ring.</summary>
        public static bool IsCutPipeEndView(SmartDimHelper h, IView view, Edge[] edges)
        {
            Edge[] profiles = GetEndFaceCircles(h, view, edges);
            if (profiles.Length == 0)
                return false;
            return profiles.Any(e => !h.IsFullCircle(e));
        }
    }
}
