using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Center marks / centerlines for cylindrical parts.
    /// Strategy is selected by <see cref="SolidWorksCapabilityRouter"/> (2022–2026).
    /// </summary>
    public static class CylindricalDimCenterlines
    {
        private const double MinRadius = 0.0005;

        public static void Add(SmartDimHelper h, IModelDoc2 model, IDrawingDoc drawing, IView view, Action<string> log)
        {
            string viewName = view.GetName2();

            try
            {
                if (!SolidWorksComException.IsAlive(h.SwApp))
                    throw new InvalidOperationException("SOLIDWORKS connection lost.");

                drawing.ActivateView(viewName);

                Edge[] edges = h.GetViewEdges(view);
                var profileCircles = edges
                    .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinRadius)
                    .OrderByDescending(h.GetCircleRadius)
                    .ToArray();

                if (profileCircles.Length == 0)
                {
                    if (CylindricalDimCenterlinesLegacy.TryAddSideViewCenterline(h, model, drawing, view, log))
                    {
                        log($"  Centerline added in {viewName} (side-view API).");
                        return;
                    }

                    log($"  Centerlines skipped in {viewName} (no circular profile or side centerline).");
                    return;
                }

                bool added = false;
                foreach (Edge circle in profileCircles.Take(2))
                {
                    if (TryInsertCenterMark(h, drawing, view, circle))
                        added = true;
                }

                if (added)
                    log($"  Center marks added in {viewName}.");
                else
                    log($"  Warning: could not add center marks in {viewName}.");
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log($"  Warning: center marks failed in {viewName}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                log($"  Warning: center marks failed in {viewName}: {ex.Message}");
            }
            finally
            {
                try { h.ClearSelection(); } catch { /* ignore */ }
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
    }
}
