using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── Edge classification ──────────────────────────────────────────

        /// <summary>True when the edge curve is a straight line.</summary>
        public bool IsLinear(Edge edge)
        {
            try
            {
                ICurve? curve = edge.GetCurve() as ICurve;
                return curve != null && curve.IsLine();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True when the edge curve is a circle or circular arc.</summary>
        public bool IsCircular(Edge edge)
        {
            try
            {
                ICurve? curve = edge.GetCurve() as ICurve;
                return curve != null && curve.IsCircle();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Radius of a circular edge in model units (meters).</summary>
        public double GetCircleRadius(Edge edge)
        {
            ICurve curve = (ICurve)edge.GetCurve();
            double[] cp = (double[])curve.CircleParams;
            // [centerX, centerY, centerZ, axisX, axisY, axisZ, radius]
            return cp[6];
        }

        /// <summary>Center point of a circular edge in model coordinates.</summary>
        public double[] GetCircleCenter(Edge edge)
        {
            ICurve curve = (ICurve)edge.GetCurve();
            double[] cp = (double[])curve.CircleParams;
            return new[] { cp[0], cp[1], cp[2] };
        }

        /// <summary>
        /// True for closed circular profiles usable as Ø dimensions.
        /// Native SW circles often have null start/end vertices; imported STEP/IGES/X_T
        /// circles frequently keep coincident vertices or a closed/periodic param range.
        /// </summary>
        public bool IsFullCircle(Edge edge)
        {
            try
            {
                if (edge.GetStartVertex() == null && edge.GetEndVertex() == null)
                    return true;

                if (!IsCircular(edge))
                    return false;

                ICurve curve = (ICurve)edge.GetCurve();
                double start = 0, end = 0;
                bool isClosed = false, isPeriodic = false;
                curve.GetEndParams(out start, out end, out isClosed, out isPeriodic);
                if (isClosed || isPeriodic)
                    return true;

                double span = Math.Abs(end - start);
                // Nearly full turn in curve parameter space (typically radians).
                if (span >= Math.PI * 1.9)
                    return true;

                // Coincident start/end vertices on a circular edge ≈ closed loop from import.
                if (edge.GetStartVertex() is Vertex sv && edge.GetEndVertex() is Vertex ev)
                {
                    double[] sp = (double[])sv.GetPoint();
                    double[] ep = (double[])ev.GetPoint();
                    double dx = sp[0] - ep[0], dy = sp[1] - ep[1], dz = sp[2] - ep[2];
                    double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double tol = Math.Max(1e-8, GetCircleRadius(edge) * 1e-5);
                    if (dist <= tol && span >= Math.PI)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Circular edge seen face-on that can drive OD/hole dims — full circle or long profile arc.
        /// </summary>
        public bool IsDimensionableCircleInView(Edge edge, IView view)
        {
            if (!IsCircular(edge) || !IsCircleProfileInView(edge, view))
                return false;

            if (IsFullCircle(edge))
                return true;

            // Imported discs sometimes tessellate the OD into long arcs (>270°).
            try
            {
                ICurve curve = (ICurve)edge.GetCurve();
                double start = 0, end = 0;
                bool isClosed = false, isPeriodic = false;
                curve.GetEndParams(out start, out end, out isClosed, out isPeriodic);
                return Math.Abs(end - start) >= Math.PI * 1.5;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True when a circular edge is seen as a true profile (cylinder axis normal to the view).
        /// </summary>
        public bool IsCircleProfileInView(Edge edge, IView view)
        {
            if (!IsCircular(edge))
                return false;

            try
            {
                ICurve curve = (ICurve)edge.GetCurve();
                double[] cp = (double[])curve.CircleParams;
                double[] axis = { cp[3], cp[4], cp[5] };

                IMathUtility mathUtil = (IMathUtility)SwApp.GetMathUtility();
                IMathVector mathVec = (IMathVector)mathUtil.CreateVector(axis);
                IMathTransform xform = (IMathTransform)view.ModelToViewTransform;
                IMathVector viewVec = (IMathVector)mathVec.MultiplyTransform(xform);
                double[] vecData = (double[])viewVec.ArrayData;

                return Math.Abs(vecData[0]) < 0.001 && Math.Abs(vecData[1]) < 0.001;
            }
            catch
            {
                return false;
            }
        }

        // ── Sheet transforms ─────────────────────────────────────────────

        /// <summary>Transforms a model-space point to sheet coordinates for the given view.</summary>
        public double[] TransformToSheet(double[] modelPt, IView view)
        {
            IMathUtility mathUtil = (IMathUtility)SwApp.GetMathUtility();
            IMathTransform xform = (IMathTransform)view.ModelToViewTransform;

            IMathPoint mp = (IMathPoint)mathUtil.CreatePoint(modelPt);
            IMathPoint tp = (IMathPoint)mp.MultiplyTransform(xform);
            return (double[])tp.ArrayData;
        }

        /// <summary>Edge endpoints in sheet coordinates.</summary>
        public (double[] start, double[] end) GetEdgeEndpointsOnSheet(Edge edge, IView view)
        {
            Vertex? sv = edge.GetStartVertex() as Vertex;
            Vertex? ev = edge.GetEndVertex() as Vertex;

            double[] startModel = sv != null ? (double[])sv.GetPoint() : GetCircleCenter(edge);
            double[] endModel = ev != null ? (double[])ev.GetPoint() : GetCircleCenter(edge);

            return (TransformToSheet(startModel, view), TransformToSheet(endModel, view));
        }

        /// <summary>Circle center in sheet coordinates.</summary>
        public double[] GetCircleCenterOnSheet(Edge edge, IView view) =>
            TransformToSheet(GetCircleCenter(edge), view);

        /// <summary>Edge midpoint in sheet coordinates.</summary>
        public double[] GetEdgeMidpointOnSheet(Edge edge, IView view)
        {
            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            return new[] { (s[0] + e[0]) / 2.0, (s[1] + e[1]) / 2.0, 0.0 };
        }

        // ── In-view edge metrics ─────────────────────────────────────────

        /// <summary>True when a linear edge is approximately horizontal on the sheet.</summary>
        public bool IsHorizontalInView(
            Edge edge,
            IView view,
            double tol = SmartDim.SmartDimConstants.SheetOrientationToleranceMeters)
        {
            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            return Math.Abs(s[1] - e[1]) < tol;
        }

        /// <summary>True when a linear edge is approximately vertical on the sheet.</summary>
        public bool IsVerticalInView(
            Edge edge,
            IView view,
            double tol = SmartDim.SmartDimConstants.SheetOrientationToleranceMeters)
        {
            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            return Math.Abs(s[0] - e[0]) < tol;
        }

        /// <summary>Projected edge length on the sheet plane.</summary>
        public double GetProjectedLength(Edge edge, IView view)
        {
            if (IsCircular(edge) && !IsFullCircle(edge))
            {
                // Chord length between arc ends (not arc length) — useful for span checks.
                var (s0, e0) = GetEdgeEndpointsOnSheet(edge, view);
                double dx0 = e0[0] - s0[0];
                double dy0 = e0[1] - s0[1];
                return Math.Sqrt(dx0 * dx0 + dy0 * dy0);
            }

            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            double dx = e[0] - s[0];
            double dy = e[1] - s[1];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Axis-aligned bounding box of edges in sheet coordinates.
        /// Partial arcs use sampled points on the arc — NOT full center±R
        /// (full-circle bounds blow up circular-segment plates to Ø×Ø).
        /// </summary>
        public (double minX, double minY, double maxX, double maxY) ComputeEdgesBoundingBox(
            Edge[] edges,
            IView view)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            void Expand(double x, double y)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            foreach (Edge edge in edges)
            {
                if (IsCircular(edge) && IsFullCircle(edge))
                {
                    double[] c = GetCircleCenterOnSheet(edge, view);
                    double r = GetCircleRadius(edge) * Math.Max(view.ScaleDecimal, 1e-9);
                    Expand(c[0] - r, c[1] - r);
                    Expand(c[0] + r, c[1] + r);
                    continue;
                }

                if (IsCircular(edge))
                {
                    ExpandPartialArcOnSheet(edge, view, Expand);
                    continue;
                }

                var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
                Expand(s[0], s[1]);
                Expand(e[0], e[1]);
            }

            if (minX > maxX)
                return (0, 0, 0, 0);

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Samples a partial circular edge onto the sheet and expands an AABB.
        /// Includes endpoints plus interior samples (and axis-aligned extremes if they lie on the arc).
        /// </summary>
        private void ExpandPartialArcOnSheet(Edge edge, IView view, Action<double, double> expand)
        {
            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            expand(s[0], s[1]);
            expand(e[0], e[1]);

            try
            {
                ICurve curve = (ICurve)edge.GetCurve();
                double start = 0, end = 0;
                bool isClosed = false, isPeriodic = false;
                curve.GetEndParams(out start, out end, out isClosed, out isPeriodic);

                const int samples = 48;
                for (int i = 1; i < samples; i++)
                {
                    double t = start + (end - start) * (i / (double)samples);
                    if (curve.Evaluate2(t, 0) is not double[] pt || pt.Length < 3)
                        continue;

                    double[] sheet = TransformToSheet(new[] { pt[0], pt[1], pt[2] }, view);
                    expand(sheet[0], sheet[1]);
                }
            }
            catch
            {
                // Endpoints already applied.
            }
        }

        /// <summary>
        /// Model-space distance from a linear chord to the farthest point of a partial arc
        /// (sagitta / tip width) — uses sheet AABB of the two edges only.
        /// </summary>
        public double EstimateChordToArcTipWidthModel(Edge chord, Edge arc, IView view)
        {
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            var (minX, minY, maxX, maxY) = ComputeEdgesBoundingBox(new[] { chord, arc }, view);
            double sheetW = Math.Abs(maxX - minX);
            double sheetH = Math.Abs(maxY - minY);

            // Tip width is the thin span for a vertical-chord segment; for horizontal chord, height.
            bool chordVertical = IsVerticalInView(chord, view, 0.01);
            double tipSheet = chordVertical ? sheetW : (IsHorizontalInView(chord, view, 0.01) ? sheetH : Math.Min(sheetW, sheetH));
            return tipSheet / scale;
        }
    }
}
