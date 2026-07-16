using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.ArcSector
{
    /// <summary>
    /// Detects flat annular-sector plates: two large concentric arcs + radial end edges
    /// (e.g. EST-P61728 — not a tube, not a rounded-end sliver).
    /// </summary>
    internal static class ArcSectorViewAnalyzer
    {
        private const double MinProfileRadiusMeters = 0.050;
        private const double CenterBucketMeters = 0.002;
        private const double MinRadiusSpreadMeters = 0.008;

        public static bool DetectFromDrawing(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!view.GetName2().Equals(
                        SmartDimConstants.IsometricViewName,
                        StringComparison.OrdinalIgnoreCase) &&
                    TryGetProfile(h, view, out _))
                {
                    return true;
                }

                view = view.GetNextView() as IView;
            }

            return false;
        }

        public static bool TryGetProfile(SmartDimHelper h, IView view, out ArcSectorProfile profile)
        {
            profile = default;
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length < 3)
                return false;

            var arcs = CollectProfileArcs(h, view, edges, requireProfileNormal: true);
            if (arcs.Count < 2)
                arcs = CollectProfileArcs(h, view, edges, requireProfileNormal: false);

            if (arcs.Count < 2)
                return false;

            foreach (var group in arcs.GroupBy(a =>
                         (Math.Round(a.Cx / CenterBucketMeters),
                          Math.Round(a.Cy / CenterBucketMeters))))
            {
                var list = group.OrderBy(a => a.R).ToList();
                if (list.Count < 2)
                    continue;

                var inner = list.First();
                var outer = list.Last();
                if (outer.R - inner.R < MinRadiusSpreadMeters)
                    continue;

                double cx = list.Average(a => a.Cx);
                double cy = list.Average(a => a.Cy);
                Edge[] radials = FindRadialEdges(h, view, edges, cx, cy, inner.R, outer.R);
                if (radials.Length == 0)
                    continue;

                profile = new ArcSectorProfile(
                    inner.Edge, outer.Edge, inner.R, outer.R, cx, cy, radials);
                return true;
            }

            return false;
        }

        private static Edge[] FindRadialEdges(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double cx,
            double cy,
            double rInner,
            double rOuter)
        {
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double strip = (rOuter - rInner) * scale;
            double midR = 0.5 * (rInner + rOuter) * scale;
            var result = new List<Edge>();

            foreach (Edge edge in edges.Where(h.IsLinear))
            {
                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                double dx = mid[0] - cx;
                double dy = mid[1] - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < midR * 0.55 || dist > midR * 1.45)
                    continue;

                double len = h.GetProjectedLength(edge, view);
                if (len < strip * 0.35 || len > strip * 2.5)
                    continue;

                if (!IsAwayFromCenter(h, view, edge, cx, cy))
                    continue;

                result.Add(edge);
            }

            return result
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .Take(2)
                .ToArray();
        }

        private static List<(Edge Edge, double R, double Cx, double Cy)> CollectProfileArcs(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            bool requireProfileNormal)
        {
            var arcs = new List<(Edge Edge, double R, double Cx, double Cy)>();
            foreach (Edge edge in edges.Where(h.IsCircular))
            {
                if (h.IsFullCircle(edge))
                    continue;

                double r = h.GetCircleRadius(edge);
                if (r < MinProfileRadiusMeters)
                    continue;

                if (requireProfileNormal && !h.IsCircleProfileInView(edge, view))
                    continue;

                double[] c = h.GetCircleCenterOnSheet(edge, view);
                if (c == null || c.Length < 2)
                    continue;

                arcs.Add((edge, r, c[0], c[1]));
            }

            return arcs;
        }

        private static bool IsAwayFromCenter(
            SmartDimHelper h,
            IView view,
            Edge edge,
            double cx,
            double cy)
        {
            double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
            double vx = mid[0] - cx;
            double vy = mid[1] - cy;
            return Math.Sqrt(vx * vx + vy * vy) > 1e-6;
        }
    }
}
