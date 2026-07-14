using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Geometric dimensions for cylindrical parts: OD, wall thickness, inner diameter in parentheses, length.
    /// </summary>
    public static class CylindricalDimSizes
    {
        private const double MinSizeMeters = 0.0001;
        private const double DimOffset = 0.012;

        /// <summary>End-face view below front view (top view in standard 3-view layout).</summary>
        public const string EndFaceViewName = "Drawing View2";

        public static void Add(
            SmartDimHelper h,
            IView view,
            bool isHollow,
            Action<string> log)
        {
            string viewName = view.GetName2();
            Edge[] edges = h.GetViewEdges(view);
            if (edges.Length == 0)
                return;

            var profileCircles = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinSizeMeters)
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();

            if (profileCircles.Length > 0)
            {
                bool isEndFaceView = viewName.Equals(EndFaceViewName, StringComparison.OrdinalIgnoreCase);
                AddEndViewDimensions(h, view, viewName, profileCircles, isHollow, isEndFaceView, log);
            }
            else
            {
                AddSideViewLength(h, view, viewName, edges, log);
            }
        }

        private static void AddEndViewDimensions(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] profileCircles,
            bool isHollow,
            bool isEndFaceView,
            Action<string> log)
        {
            Edge outer = profileCircles[0];
            double outerRadius = h.GetCircleRadius(outer);
            if (outerRadius > MinSizeMeters)
                TryDiameterDimension(h, view, viewName, outer, outerRadius * 2.0, "OD", log);

            if (isHollow && isEndFaceView && profileCircles.Length >= 2)
            {
                Edge inner = profileCircles[^1];
                double innerRadius = h.GetCircleRadius(inner);
                if (innerRadius > MinSizeMeters &&
                    Math.Abs(outerRadius - innerRadius) > MinSizeMeters)
                {
                    TryWallThickness(h, view, viewName, inner, outer, log);
                    TryInnerDiameterInParentheses(h, view, viewName, inner, innerRadius * 2.0, log);
                }
            }
        }

        private static void AddSideViewLength(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] edges,
            Action<string> log)
        {
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length == 0)
                return;

            var vertical = linear.Where(e => h.IsVerticalInView(e, view)).ToArray();
            if (vertical.Length >= 2)
            {
                Edge top = vertical.OrderByDescending(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
                Edge bottom = vertical.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
                TryLinearDimension(h, view, viewName, top, bottom, "Length", log);
                return;
            }

            var horizontal = linear.Where(e => h.IsHorizontalInView(e, view)).ToArray();
            if (horizontal.Length >= 2)
            {
                Edge right = horizontal.OrderByDescending(e => h.GetEdgeMidpointOnSheet(e, view)[0]).First();
                Edge left = horizontal.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[0]).First();
                TryLinearDimension(h, view, viewName, right, left, "Length", log);
            }
        }

        private static void TryDiameterDimension(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge circle,
            double diameter,
            string label,
            Action<string> log)
        {
            string key = $"Cyl_{label}_{diameter:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(circle, view, false))
                    return;

                double[] center = h.GetCircleCenterOnSheet(circle, view);
                DisplayDimension? dim = h.CreateDimension(center[0], center[1] + DimOffset);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  {label} Ø{diameter * 1000:F1} mm in {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryWallThickness(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge inner,
            Edge outer,
            Action<string> log)
        {
            double wall = h.GetCircleRadius(outer) - h.GetCircleRadius(inner);
            if (wall < MinSizeMeters)
                return;

            string key = $"Cyl_Wall_{wall:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValue(view, wall))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(inner, view, false))
                    return;
                if (!h.SelectEdge(outer, view, true))
                    return;

                double[] center = h.GetCircleCenterOnSheet(outer, view);
                // Main wall thickness at the bottom of the end-face view (below center).
                DisplayDimension? dim = h.CreateDimension(
                    center[0] + DimOffset,
                    center[1] - DimOffset * 2.0);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  Wall thickness {wall * 1000:F1} mm in {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryInnerDiameterInParentheses(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge innerCircle,
            double innerDiameter,
            Action<string> log)
        {
            string key = $"Cyl_ID_{innerDiameter:F4}";
            if (h.DimensionedFeatures.Contains(key))
                return;

            if (h.TrySetParenthesesOnDiameter(view, innerDiameter))
            {
                h.DimensionedFeatures.Add(key);
                log($"  ID (Ø{innerDiameter * 1000:F1} mm) marked with parentheses on {viewName}.");
                return;
            }

            if (h.HasDimensionWithValueInDrawing(innerDiameter))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(innerCircle, view, false))
                    return;

                double[] center = h.GetCircleCenterOnSheet(innerCircle, view);
                // Inner diameter in parentheses, just below the wall thickness on the end-face view.
                DisplayDimension? dim = h.CreateDimension(center[0], center[1] - DimOffset * 3.2);
                if (dim != null)
                {
                    dim.ShowParenthesis = true;
                    h.DimensionedFeatures.Add(key);
                    log($"  ID (Ø{innerDiameter * 1000:F1} mm) in parentheses on {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryLinearDimension(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge edgeA,
            Edge edgeB,
            string label,
            Action<string> log)
        {
            var (sA, eA) = h.GetEdgeEndpointsOnSheet(edgeA, view);
            var (sB, eB) = h.GetEdgeEndpointsOnSheet(edgeB, view);
            double length = Math.Max(
                Math.Abs(sA[1] - sB[1]),
                Math.Abs(sA[0] - sB[0]));

            if (length < MinSizeMeters)
                return;

            string key = $"Cyl_{label}_{length:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(length))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(edgeA, view, false))
                    return;
                if (!h.SelectEdge(edgeB, view, true))
                    return;

                double dimX = (sA[0] + sB[0]) / 2.0 + DimOffset;
                double dimY = (sA[1] + sB[1]) / 2.0;
                DisplayDimension? dim = h.CreateDimension(dimX, dimY);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  {label} {length * 1000:F1} mm in {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }
    }
}
