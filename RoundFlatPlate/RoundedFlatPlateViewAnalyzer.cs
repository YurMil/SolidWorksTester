using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>
    /// Detects flat sheet-metal plates with a straight edge and rounded end (not full discs),
    /// including circular-segment "sliver" plates (one chord + one large arc).
    /// </summary>
    internal static class RoundedFlatPlateViewAnalyzer
    {
        private const double MinArcRadiusMeters = 0.015;
        private const double MinStraightEdgeMeters = 0.04;
        private const double OrientTol = 0.008;

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

            if (partialArcs.Length == 0 || linear.Length == 0)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            // GetProjectedLength is sheet-space — convert to model meters before comparing to radius.
            double longestStraightModel = linear
                .Select(e => h.GetProjectedLength(e, view) / scale)
                .DefaultIfEmpty(0)
                .Max();

            if (longestStraightModel < MinStraightEdgeMeters)
                return false;

            double maxArcRadius = partialArcs.Max(h.GetCircleRadius);

            // Classic rounded-end: straight is a meaningful fraction of the arc radius.
            // Segment/sliver: chord can be shorter relative to a huge R (D2350) — still valid.
            if (longestStraightModel >= maxArcRadius * 0.35)
                return true;

            // Segment: one dominant chord + one large partial arc, few edges.
            return linear.Length <= 3 &&
                   partialArcs.Length >= 1 &&
                   maxArcRadius >= 0.05 &&
                   longestStraightModel >= 0.08;
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

        /// <summary>
        /// Largest partial arc in the view. Does not require the rounded-end heuristic
        /// (callers that need the heuristic should check <see cref="IsRoundedEndProfileView"/>).
        /// </summary>
        public static Edge? GetOuterProfileArc(SmartDimHelper h, IView view)
        {
            return h.GetViewEdgesCached(view)
                .Where(e => h.IsCircular(e) && !h.IsFullCircle(e))
                .Where(e => h.GetCircleRadius(e) >= MinArcRadiusMeters)
                .OrderByDescending(h.GetCircleRadius)
                .FirstOrDefault();
        }

        /// <summary>True when the face view is essentially one chord + one large arc (circular segment).</summary>
        public static bool IsCircularSegmentView(SmartDimHelper h, IView view)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            var linear = edges.Where(h.IsLinear).ToArray();
            Edge? arc = GetOuterProfileArc(h, view);
            if (arc == null || linear.Length == 0 || linear.Length > 3)
                return false;

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double r = h.GetCircleRadius(arc);
            double longest = linear.Max(e => h.GetProjectedLength(e, view) / scale);
            // Segment sagitta is typically << diameter; chord is the long straight.
            return r >= 0.08 && longest >= 0.08 && longest < r * 2.2;
        }

        /// <summary>Longest linear edge — the chord on segment plates (orientation-tolerant).</summary>
        public static Edge? GetLongestChordEdge(SmartDimHelper h, IView view, Edge[] linear)
        {
            if (linear.Length == 0)
                return null;

            // Prefer near-vertical / near-horizontal long edges, else absolute longest.
            Edge? oriented = linear
                .Where(e => h.IsVerticalInView(e, view, OrientTol) ||
                            h.IsHorizontalInView(e, view, OrientTol))
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .FirstOrDefault();

            return oriented ?? linear.OrderByDescending(e => h.GetProjectedLength(e, view)).First();
        }
    }
}
