using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    /// <summary>
    /// Shared pick / create helpers for overall dims (boundary edges, arcs, outline click, arc Max).
    /// </summary>
    public static partial class SmartDimOverall
    {
        private static bool TryDimensionEdgeLength(
            SmartDimHelper h,
            IView view,
            Edge edge,
            double modelLen,
            double textX,
            double textY,
            string key,
            string? alsoMarkKey)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;
            if (h.HasDimensionWithValueInDrawing(modelLen))
            {
                h.DimensionedFeatures.Add(key);
                if (alsoMarkKey != null)
                    h.DimensionedFeatures.Add(alsoMarkKey);
                return true;
            }

            h.ClearSelection();
            if (!h.SelectEdge(edge, view, false))
                return false;

            var dim = h.CreateLinearDimension(textX, textY) ?? h.CreateDimension(textX, textY);
            if (dim == null)
                return false;

            h.DimensionedFeatures.Add(key);
            if (alsoMarkKey != null)
                h.DimensionedFeatures.Add(alsoMarkKey);
            return true;
        }

        private static Edge? FindBoundaryEdge(
            SmartDimHelper h,
            Edge[] linear,
            IView view,
            double extremeCoord,
            bool horizontal,
            bool preferLong)
        {
            Edge? best = null;
            double bestScore = double.MinValue;
            double tol = Math.Max(0.003, Math.Abs(extremeCoord) * 1e-9 + 0.002);

            foreach (Edge edge in linear)
            {
                bool orientOk = horizontal
                    ? h.IsHorizontalInView(edge, view, OrientTol)
                    : h.IsVerticalInView(edge, view, OrientTol);
                if (!orientOk)
                    continue;

                double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
                double coord = horizontal ? mid[1] : mid[0];
                double proximity = -Math.Abs(coord - extremeCoord);
                if (Math.Abs(coord - extremeCoord) > tol + h.GetProjectedLength(edge, view) * 0.02)
                {
                    if (Math.Abs(coord - extremeCoord) > 0.008)
                        continue;
                }

                double len = h.GetProjectedLength(edge, view);
                double score = proximity + (preferLong ? len * 2.0 : len * 0.1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = edge;
                }
            }

            return best;
        }

        private static Edge? FindExtremeArc(
            SmartDimHelper h,
            Edge[] edges,
            IView view,
            bool verticalExtreme,
            bool maximize)
        {
            Edge? best = null;
            double bestCoord = maximize ? double.MinValue : double.MaxValue;

            foreach (Edge edge in edges.Where(h.IsCircular))
            {
                double[] c = h.GetCircleCenterOnSheet(edge, view);
                double r = h.GetCircleRadius(edge) * Math.Max(view.ScaleDecimal, 1e-9);
                double coord = verticalExtreme
                    ? (maximize ? c[1] + r : c[1] - r)
                    : (maximize ? c[0] + r : c[0] - r);

                if (maximize ? coord > bestCoord : coord < bestCoord)
                {
                    bestCoord = coord;
                    best = edge;
                }
            }

            return best;
        }

        private static bool TryOutlinePick(
            SmartDimHelper h,
            IView view,
            double minX, double minY, double maxX, double maxY,
            bool horizontalSpan,
            string key,
            double expectedModel,
            Action<string>? log,
            double? pickY = null)
        {
            if (h.DimensionedFeatures.Contains(key))
                return true;

            double midX = (minX + maxX) / 2.0;
            double midY = pickY ?? (minY + maxY) / 2.0;
            h.ClearSelection();
            h.ActivateView(view);

            bool a, b;
            if (horizontalSpan)
            {
                a = SelectEdgeAt(h, view, minX, midY);
                b = SelectEdgeAt(h, view, maxX, midY, append: true);
            }
            else
            {
                a = SelectEdgeAt(h, view, midX, minY);
                b = SelectEdgeAt(h, view, midX, maxY, append: true);
            }

            if (!a || !b)
                return false;

            double tx = horizontalSpan ? midX : maxX + DimOffset;
            double ty = horizontalSpan ? maxY + DimOffset : midY;
            var dim = h.CreateLinearDimension(tx, ty) ?? h.CreateDimension(tx, ty);
            if (dim == null)
                return false;

            ApplyArcEndMax(dim);

            h.DimensionedFeatures.Add(key);
            log?.Invoke($"  [Overall] {(horizontalSpan ? "Width" : "Height")} {expectedModel * 1000:F1} mm (outline pick).");
            return true;
        }

        private static bool SelectEdgeAt(SmartDimHelper h, IView view, double x, double y, bool append = false)
        {
            try
            {
                return h.Ext.SelectByID2(
                    string.Empty,
                    SmartDimConstants.EdgeSelectType,
                    x, y, 0.0,
                    append, 0, null, 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// SetArcEndCondition(Index, Condition): apply Max to entity slots 1 and 2
        /// so arc-to-line spans reach the tangent (not the center).
        /// </summary>
        private static void ApplyArcEndMax(DisplayDimension dim)
        {
            if (dim.GetDimension2(0) is not Dimension modelDim)
                return;

            try
            {
                int max = (int)swArcEndCondition_e.swArcEndConditionMax;
                modelDim.SetArcEndCondition(1, max);
                modelDim.SetArcEndCondition(2, max);
            }
            catch
            {
                // Line-line dims ignore Max safely.
            }
        }
    }
}
