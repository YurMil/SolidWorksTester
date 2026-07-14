using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Inserts drawing center marks and centerlines using IDrawingDoc commands only.
    /// Follows the SOLIDWORKS API centerline drawing example (component + face + InsertCenterLine2).
    /// </summary>
    public static class CylindricalDimCenterlines
    {
        private const double MinRadius = 0.0005;

        public static void Add(SmartDimHelper h, IModelDoc2 model, IDrawingDoc drawing, IView view, Action<string> log)
        {
            string viewName = view.GetName2();
            drawing.ActivateView(viewName);

            bool added = false;

            if (TryInsertCenterlinePerSolidWorksHelp(h, model, drawing, view))
                added = true;

            if (!added && TryInsertCenterlineFromSilhouetteEdges(h, drawing, view))
                added = true;

            if (!added && TryInsertCenterlineFromOuterEdges(h, drawing, view))
                added = true;

            Edge[] edges = h.GetViewEdges(view);
            var profileCircles = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinRadius)
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();

            if (profileCircles.Length > 0)
            {
                if (TryAutoInsertCenterMarks(view))
                    added = true;
                else
                {
                    foreach (Edge circle in profileCircles.Take(2))
                    {
                        if (TryInsertCenterMark(h, drawing, view, circle))
                            added = true;
                    }
                }
            }

            if (added)
                log($"  Centerlines added in {viewName}.");
            else
                log($"  Warning: could not add centerlines in {viewName}.");
        }

        /// <summary>
        /// Official pattern from SOLIDWORKS help:
        /// ActivateView → Select COMPONENT → Select FACE → InsertCenterLine2.
        /// </summary>
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

        private static bool TryInsertCenterlineFromSilhouetteEdges(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view)
        {
            Edge[] silhouette = h.GetViewSilhouetteEdges(view);
            if (silhouette.Length < 2)
                return false;

            Edge[] linear = silhouette.Where(h.IsLinear).ToArray();
            if (linear.Length < 2)
                return false;

            Edge[] edges = h.GetViewEdges(view);
            if (edges.Length == 0)
                edges = linear;

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

        private static bool TryAutoInsertCenterMarks(IView view)
        {
            try
            {
                int before = view.GetCenterMarkCount();
                view.AutoInsertCenterMarks2(
                    1, 0, true, true, true, 0.0, 0.0, true, true, 0.0);

                return view.GetCenterMarkCount() > before;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInsertCenterMark(SmartDimHelper h, IDrawingDoc drawing, IView view, Edge circle)
        {
            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(circle, view, false))
                    return false;

                return drawing.InsertCenterMark2(1, true) != null;
            }
            catch
            {
                return false;
            }
            finally
            {
                h.ClearSelection();
            }
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
