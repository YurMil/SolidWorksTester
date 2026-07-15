using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Center marks on end-face views; centerlines on side views (tube/pipe axis).
    /// </summary>
    public static class CylindricalDimCenterlines
    {
        public static void Add(SmartDimHelper h, IModelDoc2 model, IDrawingDoc drawing, IView view, Action<string> log)
        {
            string viewName = view.GetName2();

            if (CylindricalViewAnalyzer.IsIsometricView(view))
            {
                log($"  Centerlines skipped in {viewName} (isometric).");
                return;
            }

            try
            {
                if (!SolidWorksComException.IsAlive(h.SwApp))
                    throw new InvalidOperationException("SOLIDWORKS connection lost.");

                drawing.ActivateView(viewName);
                Edge[] edges = h.GetViewEdgesCached(view);

                if (CylindricalViewAnalyzer.IsEndFaceView(h, view, edges))
                {
                    if (TryAddEndFaceCenterMarks(h, drawing, view, edges, viewName, log))
                        return;

                    log($"  Warning: end-face center marks failed in {viewName}, trying centerline fallback.");
                }

                if (h.HasCenterLineInView(view))
                {
                    log($"  Centerline already present in {viewName}.");
                    return;
                }

                if (CylindricalDimCenterlinesLegacy.TryAddSideViewCenterline(h, model, drawing, view, log))
                {
                    log($"  Centerline added in {viewName} (side view).");
                    return;
                }

                log($"  Centerlines skipped in {viewName} (side centerline not created).");
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log($"  Warning: centerlines failed in {viewName}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                log($"  Warning: centerlines failed in {viewName}: {ex.Message}");
            }
            finally
            {
                try { h.ClearSelection(); } catch { /* ignore */ }
            }
        }

        private static bool TryAddEndFaceCenterMarks(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] edges,
            string viewName,
            Action<string> log)
        {
            Edge[] profileCircles = CylindricalViewAnalyzer.GetEndFaceCircles(h, view, edges);
            if (profileCircles.Length == 0)
                return false;

            bool added = false;
            foreach (Edge circle in profileCircles.Take(2))
            {
                if (TryInsertCenterMark(h, drawing, view, circle))
                    added = true;
            }

            if (added)
                log($"  Center marks added in {viewName} (end-face view).");

            return added;
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
    }
}
