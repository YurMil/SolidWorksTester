using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>
    /// Detects flat sheet-metal plates with a straight edge and rounded end (not full discs).
    /// </summary>
    internal static class RoundedFlatPlateViewAnalyzer
    {
        private const double MinArcRadiusMeters = 0.015;
        private const double MinStraightEdgeMeters = 0.04;

        public static bool IsRoundedEndProfileView(SmartDimHelper h, IView view)
        {
            if (RoundFlatPlateViewAnalyzer.IsCircularFaceView(h, view))
                return false;

            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
                return false;

            var linear = edges.Where(h.IsLinear).ToArray();
            var partialArcs = edges
                .Where(e => h.IsCircular(e) && !h.IsFullCircle(e))
                .Where(e => h.GetCircleRadius(e) >= MinArcRadiusMeters)
                .ToArray();

            if (partialArcs.Length == 0)
                return false;

            double longestVertical = linear
                .Where(e => h.IsVerticalInView(e, view))
                .Select(e => h.GetProjectedLength(e, view))
                .DefaultIfEmpty(0)
                .Max();

            double longestHorizontal = linear
                .Where(e => h.IsHorizontalInView(e, view))
                .Select(e => h.GetProjectedLength(e, view))
                .DefaultIfEmpty(0)
                .Max();

            double longestStraight = System.Math.Max(longestVertical, longestHorizontal);
            if (longestStraight < MinStraightEdgeMeters)
                return false;

            double maxArcRadius = partialArcs.Max(h.GetCircleRadius);
            return longestStraight >= maxArcRadius * 0.35;
        }

        public static bool DetectFromDrawing(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (IsRoundedEndProfileView(h, view))
                    return true;

                view = view.GetNextView() as IView;
            }

            return false;
        }

        public static Edge? GetOuterProfileArc(SmartDimHelper h, IView view)
        {
            if (!IsRoundedEndProfileView(h, view))
                return null;

            return h.GetViewEdgesCached(view)
                .Where(e => h.IsCircular(e) && !h.IsFullCircle(e))
                .Where(e => h.GetCircleRadius(e) >= MinArcRadiusMeters)
                .OrderByDescending(h.GetCircleRadius)
                .FirstOrDefault();
        }
    }
}
