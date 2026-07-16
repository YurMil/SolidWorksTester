using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.ArcSector
{
    /// <summary>Branch: hole diameters + two position coordinates per hole.</summary>
    internal static class ArcSectorHoles
    {
        public static void Add(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            string viewName,
            Edge[] edges,
            ArcSectorProfile profile,
            double minX,
            double minY,
            double maxX,
            double maxY,
            Action<string> log)
        {
            var holes = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .Where(e =>
                {
                    double d = h.GetCircleRadius(e) * 2.0;
                    return d >= ArcSectorDimHelpers.MinHoleDiameterMeters &&
                           d <= ArcSectorDimHelpers.MaxHoleDiameterMeters;
                })
                .GroupBy(e =>
                {
                    double[] c = h.GetCircleCenterOnSheet(e, view);
                    return (
                        Math.Round(c[0], 4),
                        Math.Round(c[1], 4),
                        Math.Round(h.GetCircleRadius(e) * 2.0, 4));
                })
                .Select(g => g.First())
                .ToList();

            if (holes.Count == 0)
            {
                log($"  [ArcSector] No holes on {viewName}.");
                return;
            }

            SmartDimHoles.AddForStandardViews(h, view, log: log);

            int index = 0;
            foreach (Edge hole in holes)
            {
                index++;
                string keyBase = $"ArcSector_HolePos_{index}";
                if (h.DimensionedFeatures.Contains(keyBase))
                    continue;

                double[] hc = h.GetCircleCenterOnSheet(hole, view);
                bool placed = false;

                Edge? radial = profile.RadialEdges
                    .OrderBy(e =>
                    {
                        double[] m = h.GetEdgeMidpointOnSheet(e, view);
                        double dx = m[0] - hc[0];
                        double dy = m[1] - hc[1];
                        return dx * dx + dy * dy;
                    })
                    .FirstOrDefault();

                if (radial != null)
                {
                    h.ClearSelection();
                    if (h.SelectEdge(radial, view, false) && h.SelectEdge(hole, view, true))
                    {
                        var dim = h.CreateLinearDimension(
                            (h.GetEdgeMidpointOnSheet(radial, view)[0] + hc[0]) / 2.0,
                            Math.Min(minY, hc[1]) - ArcSectorDimHelpers.DimOffset);
                        if (dim != null)
                        {
                            placed = true;
                            log($"  [ArcSector] Hole {index}: position from radial end.");
                        }
                    }
                }

                h.ClearSelection();
                if (h.SelectEdge(profile.InnerArc, view, false) && h.SelectEdge(hole, view, true))
                {
                    var dim = h.CreateLinearDimension(
                        hc[0] + ArcSectorDimHelpers.DimOffset,
                        (h.GetEdgeMidpointOnSheet(profile.InnerArc, view)[1] + hc[1]) / 2.0);
                    if (dim != null)
                    {
                        placed = true;
                        log($"  [ArcSector] Hole {index}: position from inner arc.");
                    }
                }

                if (!placed)
                    TryFromOutline(h, view, hole, hc, minX, minY, log, index);

                h.DimensionedFeatures.Add(keyBase);
                h.TryInsertCenterMark(drawing, view, hole);
            }
        }

        private static void TryFromOutline(
            SmartDimHelper h,
            IView view,
            Edge hole,
            double[] hc,
            double minX,
            double minY,
            Action<string> log,
            int index)
        {
            h.ClearSelection();
            h.ActivateView(view);
            if (ArcSectorDimHelpers.SelectAt(h, minX, hc[1]) && h.SelectEdge(hole, view, true))
            {
                var dim = h.CreateLinearDimension(
                    (minX + hc[0]) / 2.0,
                    minY - ArcSectorDimHelpers.DimOffset);
                if (dim != null)
                    log($"  [ArcSector] Hole {index}: X from left outline.");
            }

            h.ClearSelection();
            h.ActivateView(view);
            if (ArcSectorDimHelpers.SelectAt(h, hc[0], minY) && h.SelectEdge(hole, view, true))
            {
                var dim = h.CreateLinearDimension(
                    minX - ArcSectorDimHelpers.DimOffset,
                    (minY + hc[1]) / 2.0);
                if (dim != null)
                    log($"  [ArcSector] Hole {index}: Y from bottom outline.");
            }
        }
    }
}
