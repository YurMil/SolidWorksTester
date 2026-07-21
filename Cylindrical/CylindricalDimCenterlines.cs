using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Center marks on end-face views; centerlines on side views (tube/pipe axis).
    /// </summary>
    public static class CylindricalDimCenterlines
    {
        /// <summary>
        /// Adds center marks / centerlines for one view.
        /// When <paramref name="sideCenterlineAlreadyAdded"/> is true, side-view axis lines are skipped
        /// (one side view is enough; repeating ActivateView/select on View3 crashes some SW builds).
        /// Returns whether a side-view centerline was added in this call.
        /// </summary>
        public static bool Add(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            Action<string> log,
            bool sideCenterlineAlreadyAdded = false)
        {
            string viewName;
            try
            {
                viewName = view.GetName2();
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log($"  Warning: centerlines aborted (COM dead before view name): {ex.Message}");
                throw;
            }

            if (CylindricalViewAnalyzer.IsIsometricView(view))
            {
                log($"  Centerlines skipped in {viewName} (isometric).");
                return false;
            }

            bool addedSide = false;

            try
            {
                if (!SolidWorksComException.IsAlive(h.SwApp))
                    throw new InvalidOperationException("SOLIDWORKS connection lost.");

                // Do not ActivateView here — SelectEdge / InsertCenterMark activate as needed.
                Edge[] edges = h.GetViewEdgesCached(view);

                if (CylindricalViewAnalyzer.IsEndFaceView(h, view, edges))
                {
                    if (TryAddEndFaceCenterMarks(h, drawing, view, edges, viewName, log))
                        return false;

                    if (TryAddCutPipeCenterMarkOrLine(h, drawing, view, edges, viewName, log))
                        return false;

                    log($"  Warning: end-face center marks failed in {viewName}, trying centerline fallback.");
                }

                if (sideCenterlineAlreadyAdded)
                {
                    log($"  Centerlines skipped in {viewName} (side axis already placed on another view).");
                    return false;
                }

                if (h.HasCenterLineInView(view))
                {
                    log($"  Centerline already present in {viewName}.");
                    return false;
                }

                if (CylindricalDimCenterlinesLegacy.TryAddSideViewCenterline(h, model, drawing, view, log))
                {
                    log($"  Centerline added in {viewName} (side view).");
                    addedSide = true;
                    return true;
                }

                log($"  Centerlines skipped in {viewName} (side centerline not created).");
                return false;
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log($"  Warning: centerlines failed in {viewName}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                log($"  Warning: centerlines failed in {viewName}: {ex.Message}");
                return addedSide;
            }
            finally
            {
                try
                {
                    if (SolidWorksComException.IsAlive(h.SwApp))
                        h.ClearSelection();
                }
                catch
                {
                    // ignore
                }
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

        /// <summary>
        /// Cut/half pipes: center mark on outer arc, else centerline across the flat cut chord.
        /// </summary>
        private static bool TryAddCutPipeCenterMarkOrLine(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Edge[] edges,
            string viewName,
            Action<string> log)
        {
            if (!CylindricalViewAnalyzer.IsCutPipeEndView(h, view, edges))
                return false;

            Edge[] profiles = CylindricalViewAnalyzer.GetEndFaceCircles(h, view, edges);
            if (profiles.Length == 0)
                return false;

            Edge outer = profiles[0];
            if (TryInsertCenterMark(h, drawing, view, outer) ||
                h.TryInsertCenterMark(drawing, view, outer))
            {
                log($"  Center mark added in {viewName} (cut-pipe arc).");
                return true;
            }

            double[] center = h.GetCircleCenterOnSheet(outer, view);
            double rSheet = h.GetCircleRadius(outer) * Math.Max(view.ScaleDecimal, 1e-9);

            Edge? chord = edges
                .Where(h.IsLinear)
                .Select(e =>
                {
                    double[] mid = h.GetEdgeMidpointOnSheet(e, view);
                    double dx = mid[0] - center[0];
                    double dy = mid[1] - center[1];
                    return (E: e, Dist: Math.Sqrt(dx * dx + dy * dy), Len: h.GetProjectedLength(e, view));
                })
                .Where(x => x.Dist < rSheet * 0.35 && x.Len > rSheet * 0.70)
                .OrderByDescending(x => x.Len)
                .Select(x => x.E)
                .FirstOrDefault();

            if (chord == null || h.HasCenterLineInView(view))
                return false;

            bool horiz = h.IsHorizontalInView(chord, view, 0.006);
            double[] chordMid = h.GetEdgeMidpointOnSheet(chord, view);
            Edge? mate = edges
                .Where(h.IsLinear)
                .Where(e => !ReferenceEquals(e, chord))
                .Where(e => horiz
                    ? h.IsHorizontalInView(e, view, 0.006)
                    : h.IsVerticalInView(e, view, 0.006))
                .OrderByDescending(e =>
                {
                    double[] m = h.GetEdgeMidpointOnSheet(e, view);
                    return horiz ? Math.Abs(m[1] - chordMid[1]) : Math.Abs(m[0] - chordMid[0]);
                })
                .FirstOrDefault();

            if (mate != null &&
                CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(h, drawing, view, chord, mate))
            {
                log($"  Centerline added in {viewName} (cut-pipe).");
                return true;
            }

            return false;
        }

        private static bool TryInsertCenterMark(SmartDimHelper h, IDrawingDoc drawing, IView view, Edge circle)
        {
            try
            {
                if (!SolidWorksComException.IsAlive(h.SwApp))
                    return false;

                h.ClearSelection();
                if (!h.SelectEdge(circle, view, false))
                    return false;

                return DrawingAnnotationWalk.TryInsertCenterMark2(drawing, 1, true);
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                throw;
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (SolidWorksComException.IsAlive(h.SwApp))
                        h.ClearSelection();
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
