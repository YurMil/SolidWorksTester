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

        /// <summary>True for closed circular profiles (no start/end vertex).</summary>
        public bool IsFullCircle(Edge edge) =>
            edge.GetStartVertex() == null && edge.GetEndVertex() == null;

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
            var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
            double dx = e[0] - s[0];
            double dy = e[1] - s[1];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Axis-aligned bounding box of edges in sheet coordinates.</summary>
        public (double minX, double minY, double maxX, double maxY) ComputeEdgesBoundingBox(
            Edge[] edges,
            IView view)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Edge edge in edges)
            {
                if (IsCircular(edge))
                {
                    double[] c = GetCircleCenterOnSheet(edge, view);
                    double r = GetCircleRadius(edge) * view.ScaleDecimal;
                    minX = Math.Min(minX, c[0] - r);
                    minY = Math.Min(minY, c[1] - r);
                    maxX = Math.Max(maxX, c[0] + r);
                    maxY = Math.Max(maxY, c[1] + r);
                }
                else
                {
                    var (s, e) = GetEdgeEndpointsOnSheet(edge, view);
                    minX = Math.Min(minX, Math.Min(s[0], e[0]));
                    minY = Math.Min(minY, Math.Min(s[1], e[1]));
                    maxX = Math.Max(maxX, Math.Max(s[0], e[0]));
                    maxY = Math.Max(maxY, Math.Max(s[1], e[1]));
                }
            }

            return (minX, minY, maxX, maxY);
        }
    }
}
