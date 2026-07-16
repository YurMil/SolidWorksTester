using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Cylindrical;

namespace SolidWorksTester.SmartDim
{
    /// <summary>
    /// Detects bilateral symmetry in a flat drawing view and inserts
    /// horizontal / vertical centerlines via <c>InsertCenterLine2</c>
    /// between opposite outer edges.
    /// </summary>
    public static class SmartDimSymmetryCenterlines
    {
        private const double OrientTol = 0.004;
        private const double EdgeBoundTol = 0.003;
        private const double MinOuterEdgeRatio = 0.35;

        public readonly struct Axes
        {
            public Axes(bool vertical, bool horizontal)
            {
                Vertical = vertical;
                Horizontal = horizontal;
            }

            public bool Vertical { get; }
            public bool Horizontal { get; }
            public bool Any => Vertical || Horizontal;
        }

        /// <summary>
        /// On the primary flat face view only: detect mirror symmetry and place centerlines.
        /// </summary>
        public static void AddForPrimaryFlatView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Action<string>? log = null)
        {
            string viewName = view.GetName2();
            if (viewName.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                return;

            Axes axes = Detect(h, view);
            if (!axes.Any)
            {
                log?.Invoke($"  [Symmetry] None in {viewName}.");
                return;
            }

            log?.Invoke(
                $"  [Symmetry] {viewName}: " +
                $"V={(axes.Vertical ? "yes" : "no")}, H={(axes.Horizontal ? "yes" : "no")}.");

            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
                return;

            var linear = edges.Where(h.IsLinear).ToArray();
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double width = maxX - minX;
            double height = maxY - minY;
            if (width < 1e-6 || height < 1e-6)
                return;

            if (axes.Vertical)
            {
                Edge? left = FindExtremeEdge(h, view, linear, vertical: true, maximize: false, minX, maxX, height);
                Edge? right = FindExtremeEdge(h, view, linear, vertical: true, maximize: true, minX, maxX, height);
                if (left != null && right != null && !ReferenceEquals(left, right) &&
                    CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(h, drawing, view, left, right))
                {
                    log?.Invoke($"  [Symmetry] Vertical centerline added in {viewName}.");
                }
                else
                {
                    log?.Invoke($"  [Symmetry] Vertical centerline failed in {viewName}.");
                }
            }

            if (axes.Horizontal)
            {
                Edge? bottom = FindExtremeEdge(h, view, linear, vertical: false, maximize: false, minY, maxY, width);
                Edge? top = FindExtremeEdge(h, view, linear, vertical: false, maximize: true, minY, maxY, width);
                if (bottom != null && top != null && !ReferenceEquals(bottom, top) &&
                    CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(h, drawing, view, bottom, top))
                {
                    log?.Invoke($"  [Symmetry] Horizontal centerline added in {viewName}.");
                }
                else
                {
                    log?.Invoke($"  [Symmetry] Horizontal centerline failed in {viewName}.");
                }
            }
        }

        /// <summary>
        /// View-space mirror test: hole centers (preferred) or outer-edge midpoints.
        /// </summary>
        public static Axes Detect(SmartDimHelper h, IView view)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length < 2)
                return default;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double width = maxX - minX;
            double height = maxY - minY;
            if (width < 1e-6 || height < 1e-6)
                return default;

            double cx = 0.5 * (minX + maxX);
            double cy = 0.5 * (minY + maxY);
            double tol = Math.Max(0.0015, 0.012 * Math.Min(width, height));

            List<(double X, double Y)> features = CollectHoleCenters(h, view, edges, tol);
            if (features.Count == 0)
                features = CollectOuterEdgeSamples(h, view, edges, minX, minY, maxX, maxY, width, height);

            if (features.Count == 0)
                return default;

            bool vertical = SymmetryMirror.IsMirrored(features, cx, cy, aboutVertical: true, tol);
            bool horizontal = SymmetryMirror.IsMirrored(features, cx, cy, aboutVertical: false, tol);
            return new Axes(vertical, horizontal);
        }

        /// <summary>Pure mirror check — unit-testable without SOLIDWORKS.</summary>
        internal static class SymmetryMirror
        {
            public static bool IsMirrored(
                IReadOnlyList<(double X, double Y)> points,
                double cx,
                double cy,
                bool aboutVertical,
                double tol)
            {
                if (points.Count == 0)
                    return false;

                double tolSq = tol * tol;
                foreach ((double x, double y) in points)
                {
                    double mx = aboutVertical ? (2.0 * cx - x) : x;
                    double my = aboutVertical ? y : (2.0 * cy - y);

                    bool found = false;
                    foreach ((double qx, double qy) in points)
                    {
                        double dx = qx - mx;
                        double dy = qy - my;
                        if (dx * dx + dy * dy <= tolSq)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return false;
                }

                return true;
            }
        }

        private static List<(double X, double Y)> CollectHoleCenters(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double mergeTol)
        {
            var centers = new List<(double X, double Y)>();
            double mergeTolSq = mergeTol * mergeTol;

            foreach (Edge edge in edges)
            {
                if (!h.IsCircular(edge) || !h.IsFullCircle(edge))
                    continue;

                // Skip tiny arcs / huge outer discs — keep hole-scale circles.
                double rModel = h.GetCircleRadius(edge);
                if (rModel < 0.0008 || rModel > 0.080)
                    continue;

                double[] c = h.GetCircleCenterOnSheet(edge, view);
                if (c == null || c.Length < 2)
                    continue;

                if (centers.Any(p =>
                {
                    double dx = p.X - c[0];
                    double dy = p.Y - c[1];
                    return dx * dx + dy * dy <= mergeTolSq;
                }))
                {
                    continue;
                }

                centers.Add((c[0], c[1]));
            }

            return centers;
        }

        private static List<(double X, double Y)> CollectOuterEdgeSamples(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double width,
            double height)
        {
            var samples = new List<(double X, double Y)>();
            double band = Math.Max(0.002, 0.04 * Math.Min(width, height));

            foreach (Edge edge in edges.Where(h.IsLinear))
            {
                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                bool nearLeft = Math.Abs(mid[0] - minX) <= band;
                bool nearRight = Math.Abs(mid[0] - maxX) <= band;
                bool nearBottom = Math.Abs(mid[1] - minY) <= band;
                bool nearTop = Math.Abs(mid[1] - maxY) <= band;
                if (!(nearLeft || nearRight || nearBottom || nearTop))
                    continue;

                samples.Add((mid[0], mid[1]));
            }

            return samples;
        }

        private static Edge? FindExtremeEdge(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool vertical,
            bool maximize,
            double boundMin,
            double boundMax,
            double spanAlong)
        {
            double minLen = spanAlong * MinOuterEdgeRatio;
            Edge? best = null;
            double bestLen = 0;

            foreach (Edge edge in linear)
            {
                if (vertical
                    ? !h.IsVerticalInView(edge, view, OrientTol)
                    : !h.IsHorizontalInView(edge, view, OrientTol))
                {
                    continue;
                }

                double coord = h.GetEdgeMidpointOnSheet(edge, view)[vertical ? 0 : 1];
                double dist = maximize ? Math.Abs(coord - boundMax) : Math.Abs(coord - boundMin);
                if (dist > EdgeBoundTol)
                    continue;

                double len = h.GetProjectedLength(edge, view);
                if (len < minLen)
                    continue;

                if (len > bestLen)
                {
                    bestLen = len;
                    best = edge;
                }
            }

            // Rounded-corner plates: outer straight may be shorter than 35% — relax once.
            if (best == null)
            {
                foreach (Edge edge in linear)
                {
                    if (vertical
                        ? !h.IsVerticalInView(edge, view, OrientTol)
                        : !h.IsHorizontalInView(edge, view, OrientTol))
                    {
                        continue;
                    }

                    double coord = h.GetEdgeMidpointOnSheet(edge, view)[vertical ? 0 : 1];
                    double dist = maximize ? Math.Abs(coord - boundMax) : Math.Abs(coord - boundMin);
                    if (dist > EdgeBoundTol)
                        continue;

                    double len = h.GetProjectedLength(edge, view);
                    if (len > bestLen)
                    {
                        bestLen = len;
                        best = edge;
                    }
                }
            }

            return best;
        }
    }
}
