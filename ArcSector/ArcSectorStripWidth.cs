using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.ArcSector
{
    /// <summary>
    /// Branch: radial strip width on an end face (R_out − R_in), e.g. 153 mm.
    /// Independent of the sector angle branch.
    /// </summary>
    internal static class ArcSectorStripWidth
    {
        public static void Add(
            SmartDimHelper h,
            IView view,
            string viewName,
            ArcSectorProfile profile,
            double scale,
            Action<string> log)
        {
            const string key = "ArcSector_RadialStrip";
            if (h.DimensionedFeatures.Contains(key))
                return;

            double expected = profile.StripWidth;
            if (expected < 0.002)
                return;

            if (h.HasDimensionWithValueInDrawing(expected))
            {
                h.DimensionedFeatures.Add(key);
                return;
            }

            Edge? radial = null;
            if (profile.RadialEdges.Length > 0)
            {
                radial = profile.RadialEdges
                    .Select(e => (Edge: e, Len: h.GetProjectedLength(e, view) / scale))
                    .OrderBy(t => Math.Abs(t.Len - expected))
                    .First().Edge;
            }

            if (radial == null)
            {
                TryBetweenArcs(h, view, viewName, profile, expected, key, log);
                return;
            }

            double modelLen = h.GetProjectedLength(radial, view) / scale;
            double[] mid = h.GetEdgeMidpointOnSheet(radial, view);
            ArcSectorDimHelpers.OffsetAwayFromCenter(
                mid[0], mid[1], profile.CenterX, profile.CenterY, out double tx, out double ty);

            h.ClearSelection();
            if (!h.SelectEdge(radial, view, false))
            {
                TryBetweenArcs(h, view, viewName, profile, expected, key, log);
                return;
            }

            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
            {
                TryBetweenArcs(h, view, viewName, profile, expected, key, log);
                return;
            }

            h.DimensionedFeatures.Add(key);
            log($"  [ArcSector] Radial strip {modelLen * 1000:F1} mm (R_out−R_in) on {viewName}.");
        }

        private static void TryBetweenArcs(
            SmartDimHelper h,
            IView view,
            string viewName,
            ArcSectorProfile profile,
            double expected,
            string key,
            Action<string> log)
        {
            h.ClearSelection();
            if (!h.SelectEdge(profile.InnerArc, view, false) ||
                !h.SelectEdge(profile.OuterArc, view, true))
                return;

            double[] midIn = h.GetEdgeMidpointOnSheet(profile.InnerArc, view);
            double[] midOut = h.GetEdgeMidpointOnSheet(profile.OuterArc, view);
            double tx = (midIn[0] + midOut[0]) / 2.0;
            double ty = (midIn[1] + midOut[1]) / 2.0;

            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return;

            h.DimensionedFeatures.Add(key);
            log($"  [ArcSector] Radial strip {expected * 1000:F1} mm (arcs) on {viewName}.");
        }
    }
}
