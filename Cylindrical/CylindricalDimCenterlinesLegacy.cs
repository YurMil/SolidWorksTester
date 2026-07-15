using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Side-view centerline helpers for cylindrical parts (2022–2026).
    /// Uses parallel outer edges first; avoids silhouette-edge APIs (RPC instability).
    /// </summary>
    internal static class CylindricalDimCenterlinesLegacy
    {
        private const double MinEdgeLengthRatio = 0.50;

        public static bool TryAddSideViewCenterline(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            string viewName = view.GetName2();

            if (TryInsertCenterlineFromOuterEdges(h, drawing, view))
                return true;

            if (TryInsertCenterlineFromBBoxMidline(h, drawing, view))
                return true;

            if (TryInsertCenterlinePerSolidWorksHelp(h, model, drawing, view))
                return true;

            log($"  Side-view centerline not created in {viewName}.");
            return false;
        }

        private static bool TryInsertCenterlineFromOuterEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length < 2)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;

            if (TryCenterlineForOrientation(h, drawing, view, linear, vertical: true, bboxHeight))
                return true;

            if (TryCenterlineForOrientation(h, drawing, view, linear, vertical: false, bboxWidth))
                return true;

            return false;
        }

        private static bool TryCenterlineForOrientation(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] linear,
            bool vertical,
            double bboxPrimarySize)
        {
            double minLength = bboxPrimarySize * MinEdgeLengthRatio;

            return TryCenterlineBetweenOutermostEdges(
                       h, drawing, view, linear, vertical, minLength,
                       e => h.GetEdgeMidpointOnSheet(e, view)[vertical ? 0 : 1])
                   || TryCenterlineBetweenLongestParallelPair(
                       h, drawing, view, linear, vertical, minLength);
        }

        /// <summary>
        /// Fallback: pick the two longest parallel edges (tube side views in HLV often have 4 parallels).
        /// </summary>
        private static bool TryCenterlineBetweenLongestParallelPair(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] linear,
            bool vertical,
            double minLength)
        {
            var candidates = linear
                .Where(e => vertical
                    ? h.IsVerticalInView(e, view, 0.004)
                    : h.IsHorizontalInView(e, view, 0.004))
                .Where(e => h.GetProjectedLength(e, view) >= minLength)
                .OrderByDescending(e => h.GetProjectedLength(e, view))
                .ToArray();

            if (candidates.Length < 2)
                return false;

            Func<Edge, double> coord = e => h.GetEdgeMidpointOnSheet(e, view)[vertical ? 0 : 1];

            for (int i = 0; i < candidates.Length; i++)
            {
                for (int j = i + 1; j < candidates.Length; j++)
                {
                    if (Math.Abs(coord(candidates[j]) - coord(candidates[i])) < minLength * 0.12)
                        continue;

                    if (TryInsertCenterlineBetweenEdges(h, drawing, view, candidates[i], candidates[j]))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Uses bbox midline between the outermost parallel edge pair on each side of the view.
        /// </summary>
        private static bool TryInsertCenterlineFromBBoxMidline(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length < 2)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double width = maxX - minX;
            double height = maxY - minY;

            if (height >= width)
            {
                Edge? left = FindExtremeEdge(h, view, linear, vertical: true, maximize: false, minX, maxX);
                Edge? right = FindExtremeEdge(h, view, linear, vertical: true, maximize: true, minX, maxX);
                if (left != null && right != null && !ReferenceEquals(left, right))
                    return TryInsertCenterlineBetweenEdges(h, drawing, view, left, right);
            }
            else
            {
                Edge? bottom = FindExtremeEdge(h, view, linear, vertical: false, maximize: false, minY, maxY);
                Edge? top = FindExtremeEdge(h, view, linear, vertical: false, maximize: true, minY, maxY);
                if (bottom != null && top != null && !ReferenceEquals(bottom, top))
                    return TryInsertCenterlineBetweenEdges(h, drawing, view, bottom, top);
            }

            return false;
        }

        private static Edge? FindExtremeEdge(
            SmartDimHelper h,
            IView view,
            Edge[] linear,
            bool vertical,
            bool maximize,
            double boundMin,
            double boundMax)
        {
            const double tol = 0.003;
            Edge? best = null;
            double bestLen = 0;

            foreach (Edge edge in linear)
            {
                if (vertical ? !h.IsVerticalInView(edge, view, 0.004) : !h.IsHorizontalInView(edge, view, 0.004))
                    continue;

                double coord = h.GetEdgeMidpointOnSheet(edge, view)[vertical ? 0 : 1];
                double dist = maximize ? Math.Abs(coord - boundMax) : Math.Abs(coord - boundMin);
                if (dist > tol)
                    continue;

                double len = h.GetProjectedLength(edge, view);
                if (len > bestLen)
                {
                    bestLen = len;
                    best = edge;
                }
            }

            return best;
        }

        private static bool TryInsertCenterlinePerSolidWorksHelp(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view)
        {
            Face2? cylFace = h.FindBestCylindricalFaceInView(view);
            if (cylFace == null)
                return false;

            int before = view.GetCenterLineCount();
            string viewName = view.GetName2();

            try
            {
                h.ClearSelection();
                drawing.ActivateView(viewName);
                h.SelectDrawingComponent(view);

                double[] pickPoint = h.GetCylindricalFacePickPointOnSheet(cylFace, view);
                if (!h.SelectFaceBySheetPoint(view, pickPoint) && !h.SelectFace(cylFace, view, false))
                    return false;

                if (drawing.InsertCenterLine2() != null)
                    return view.GetCenterLineCount() > before || h.HasCenterLineInView(view);

                if (drawing.InsertCenterLine())
                    return view.GetCenterLineCount() > before || h.HasCenterLineInView(view);
            }
            catch
            {
                return false;
            }
            finally
            {
                h.ClearSelection();
            }

            return false;
        }

        private static bool TryCenterlineBetweenOutermostEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] linear,
            bool vertical,
            double minLength,
            Func<Edge, double> coordSelector)
        {
            var candidates = linear
                .Where(e => vertical
                    ? h.IsVerticalInView(e, view, 0.004)
                    : h.IsHorizontalInView(e, view, 0.004))
                .Where(e => h.GetProjectedLength(e, view) >= minLength)
                .ToArray();

            if (candidates.Length < 2)
                return false;

            Edge edgeA = candidates.OrderBy(coordSelector).First();
            Edge edgeB = candidates.OrderByDescending(coordSelector).First();
            if (ReferenceEquals(edgeA, edgeB))
                return false;

            if (Math.Abs(coordSelector(edgeB) - coordSelector(edgeA)) < minLength * 0.12)
                return false;

            return TryInsertCenterlineBetweenEdges(h, drawing, view, edgeA, edgeB);
        }

        private static bool TryInsertCenterlineBetweenEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge edgeA,
            Edge edgeB)
        {
            try
            {
                int before = view.GetCenterLineCount();
                h.ClearSelection();
                drawing.ActivateView(view.GetName2());

                if (!h.SelectEdge(edgeA, view, false))
                    return false;
                if (!h.SelectEdge(edgeB, view, true))
                    return false;

                if (drawing.InsertCenterLine2() != null)
                    return view.GetCenterLineCount() > before || h.HasCenterLineInView(view);

                if (drawing.InsertCenterLine())
                    return view.GetCenterLineCount() > before || h.HasCenterLineInView(view);
            }
            catch
            {
                return false;
            }
            finally
            {
                h.ClearSelection();
            }

            return false;
        }
    }
}
