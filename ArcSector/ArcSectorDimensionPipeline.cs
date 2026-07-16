using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.ArcSector
{
    /// <summary>
    /// Orchestrator for annular-sector flat plates.
    /// Runs independent algorithm branches so each can evolve without touching the others:
    /// <list type="bullet">
    /// <item><see cref="ArcSectorRadii"/> — R_in / R_out</item>
    /// <item><see cref="ArcSectorAngle"/> — sector sweep</item>
    /// <item><see cref="ArcSectorStripWidth"/> — radial end strip (R_out − R_in)</item>
    /// <item><see cref="ArcSectorOverall"/> — bbox W/H</item>
    /// <item><see cref="ArcSectorHoles"/> — Ø + two coordinates</item>
    /// </list>
    /// </summary>
    internal static class ArcSectorDimensionPipeline
    {
        public static void AddForPrimaryView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            if (!ArcSectorViewAnalyzer.TryGetProfile(h, view, out ArcSectorProfile profile))
            {
                log("  [ArcSector] Profile not resolved — falling back to overall/holes.");
                SmartDimOverall.Add(h, view, log);
                SmartDimHoles.AddForStandardViews(h, view, log: log);
                return;
            }

            string viewName = view.GetName2();
            Edge[] edges = h.GetViewEdgesCached(view);
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            log($"  [ArcSector] R_in={profile.InnerRadius * 1000:F1} mm, " +
                $"R_out={profile.OuterRadius * 1000:F1} mm on {viewName}.");

            ArcSectorRadii.Add(h, view, viewName, profile, log);
            ArcSectorAngle.Add(h, view, viewName, profile, minX, minY, maxX, maxY, log);
            ArcSectorStripWidth.Add(h, view, viewName, profile, scale, log);
            ArcSectorOverall.Add(h, view, viewName, minX, minY, maxX, maxY, scale, log);
            ArcSectorHoles.Add(h, drawing, view, viewName, edges, profile, minX, minY, maxX, maxY, log);
        }

        public static void AddSideViewOnly(
            SmartDimHelper h,
            IView view,
            Action<string> log,
            double? expectedThicknessMm)
        {
            SmartDimThickness.Add(h, view, log, expectedThicknessMm);
        }
    }
}
