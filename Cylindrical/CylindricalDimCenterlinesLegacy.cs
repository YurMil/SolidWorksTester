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

            if (TryInsertCenterlinePerSolidWorksHelp(h, model, drawing, view))
                return true;

            log($"  Side-view centerline not created in {viewName}.");
            return false;
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

        private static bool TryInsertCenterlineFromOuterEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view)
        {
            Edge[] edges = h.GetViewEdges(view);
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length < 2)
                return false;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;

            if (bboxHeight >= bboxWidth)
            {
                return TryCenterlineBetweenOutermostEdges(
                    h, drawing, view, linear, vertical: true, bboxHeight,
                    e => h.GetEdgeMidpointOnSheet(e, view)[0]);
            }

            return TryCenterlineBetweenOutermostEdges(
                h, drawing, view, linear, vertical: false, bboxWidth,
                e => h.GetEdgeMidpointOnSheet(e, view)[1]);
        }

        private static bool TryCenterlineBetweenOutermostEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] linear,
            bool vertical,
            double bboxPrimarySize,
            Func<Edge, double> coordSelector)
        {
            double minLength = bboxPrimarySize * 0.65;
            var candidates = linear
                .Where(e => vertical
                    ? h.IsVerticalInView(e, view, 0.003)
                    : h.IsHorizontalInView(e, view, 0.003))
                .Where(e => h.GetProjectedLength(e, view) >= minLength)
                .ToArray();

            if (candidates.Length < 2)
                return false;

            Edge edgeA = candidates.OrderBy(coordSelector).First();
            Edge edgeB = candidates.OrderByDescending(coordSelector).First();
            if (ReferenceEquals(edgeA, edgeB))
                return false;

            if (Math.Abs(coordSelector(edgeB) - coordSelector(edgeA)) < minLength * 0.15)
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
