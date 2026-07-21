using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Geometric dimensions for cylindrical parts: OD, wall thickness, inner diameter in parentheses, length.
    /// </summary>
    public static class CylindricalDimSizes
    {
        private const double MinSizeMeters = 0.0001;
        private const double MinWallMeters = 0.0005;
        private const double MaxWallMeters = 0.080;
        private const double DimOffset = 0.012;

        /// <summary>End-face view below front view (top view in standard 3-view layout).</summary>
        public const string EndFaceViewName = "Drawing View2";

        public static void Add(
            SmartDimHelper h,
            IView view,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            string viewName = view.GetName2();
            log($"  [CylSizes] start {viewName}");
            Edge[] edges = h.GetViewEdgesCached(view);
            if (edges.Length == 0)
            {
                log($"  [CylSizes] no edges in {viewName}.");
                return;
            }

            // Full circles OR arcs (lengthwise-cut pipes show semi-circular profiles).
            Edge[] profileCircles = CylindricalViewAnalyzer.GetEndFaceCircles(h, view, edges);

            bool isEndFaceView =
                CylindricalViewAnalyzer.IsEndFaceView(h, view, edges) ||
                viewName.Equals(EndFaceViewName, StringComparison.OrdinalIgnoreCase);

            // Named end view with arcs but weak classifier — still treat as end if ≥1 profile arc.
            if (!isEndFaceView &&
                viewName.Equals(EndFaceViewName, StringComparison.OrdinalIgnoreCase) &&
                profileCircles.Length > 0)
            {
                isEndFaceView = true;
            }

            if (profileCircles.Length > 0 && isEndFaceView)
            {
                log($"  [CylSizes] end-face path ({profileCircles.Length} arcs/circles).");
                AddEndViewDimensions(h, view, viewName, profileCircles, analysis, log);
            }
            else
            {
                log($"  [CylSizes] side-view length path ({edges.Length} edges).");
                AddSideViewLength(h, view, viewName, edges, log);
            }

            log($"  [CylSizes] done {viewName}");
        }

        /// <summary>Backward-compatible overload.</summary>
        public static void Add(
            SmartDimHelper h,
            IView view,
            bool isHollow,
            Action<string> log)
        {
            Add(h, view, new PartAnalysisResult
            {
                IsHollow = isHollow,
                HasHoles = isHollow,
                Kind = PartModelKind.Cylindrical,
                EstProperties = new EstPartProperties()
            }, log);
        }

        private static void AddEndViewDimensions(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] profileCircles,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            Edge outer = profileCircles[0];
            double outerRadius = h.GetCircleRadius(outer);
            bool cutPipe = profileCircles.Any(e => !h.IsFullCircle(e));
            if (cutPipe)
                log($"  End view {viewName}: cut/half pipe profile (arcs).");

            if (outerRadius > MinSizeMeters)
                TryDiameterDimension(h, view, viewName, outer, outerRadius * 2.0, "OD", log);

            double? expectedWall = analysis.EstProperties.Dim3Mm is double w && w > 0.4 && w <= 80
                ? w / 1000.0
                : null;

            if (!TryPickInnerCircle(h, view, profileCircles, outer, expectedWall, out Edge inner, out double wall))
            {
                if (analysis.IsHollow || analysis.HasHoles || cutPipe || IsPipeEstName(analysis))
                    log($"  Wall/ID: end view {viewName} has no concentric inner arc/circle.");
                return;
            }

            bool treatAsTube =
                analysis.IsHollow ||
                analysis.HasHoles ||
                cutPipe ||
                expectedWall.HasValue ||
                IsPipeEstName(analysis) ||
                (wall >= MinWallMeters && wall <= MaxWallMeters);

            if (!treatAsTube)
                return;

            TryWallThickness(h, view, viewName, inner, outer, wall, log);
            TryInnerDiameterInParentheses(
                h, view, viewName, inner, h.GetCircleRadius(inner) * 2.0, log);
        }

        private static bool IsPipeEstName(PartAnalysisResult analysis)
        {
            string? name = analysis.EstProperties.Name;
            string? desc = analysis.EstProperties.Description;
            return (!string.IsNullOrEmpty(name) &&
                    name.Contains("PIPE", StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(desc) &&
                    desc.Contains("PIPE", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryPickInnerCircle(
            SmartDimHelper h,
            IView view,
            Edge[] profileCircles,
            Edge outer,
            double? expectedWall,
            out Edge inner,
            out double wall)
        {
            inner = null!;
            wall = 0;
            if (profileCircles.Length < 2)
                return false;

            double outerR = h.GetCircleRadius(outer);
            double[] cOuter = h.GetCircleCenterOnSheet(outer, view);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double concentricTolSheet = Math.Max(outerR * scale * 0.20, 0.002);

            Edge? best = null;
            double bestErr = double.MaxValue;
            double bestWall = 0;

            foreach (Edge candidate in profileCircles.Skip(1))
            {
                double innerR = h.GetCircleRadius(candidate);
                double w = outerR - innerR;
                if (w < MinWallMeters || w > MaxWallMeters)
                    continue;

                // Same axis (concentric) — rejects unrelated arcs on the cut face.
                double[] cInner = h.GetCircleCenterOnSheet(candidate, view);
                double dx = cInner[0] - cOuter[0];
                double dy = cInner[1] - cOuter[1];
                if (Math.Sqrt(dx * dx + dy * dy) > concentricTolSheet)
                    continue;

                double err = expectedWall.HasValue
                    ? Math.Abs(w - expectedWall.Value)
                    : w;
                if (err < bestErr)
                {
                    bestErr = err;
                    best = candidate;
                    bestWall = w;
                }
            }

            if (best == null)
                return false;

            if (expectedWall.HasValue && bestErr > expectedWall.Value * 0.5 + 0.0005)
                return false;

            inner = best;
            wall = bestWall;
            return true;
        }

        private static void AddSideViewLength(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge[] edges,
            Action<string> log)
        {
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length == 0)
                return;

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double bboxWidth = maxX - minX;
            double bboxHeight = maxY - minY;
            double minSpan = Math.Max(bboxWidth, bboxHeight) * 0.55;

            var vertical = linear.Where(e => h.IsVerticalInView(e, view)).ToArray();
            if (vertical.Length >= 2 &&
                TryLongestParallelPair(h, view, vertical, vertical: true, minSpan, out Edge vA, out Edge vB))
            {
                TryLinearDimension(h, view, viewName, vA, vB, "Length", log);
                return;
            }

            var horizontal = linear.Where(e => h.IsHorizontalInView(e, view)).ToArray();
            if (horizontal.Length >= 2 &&
                TryLongestParallelPair(h, view, horizontal, vertical: false, minSpan, out Edge hA, out Edge hB))
            {
                TryLinearDimension(h, view, viewName, hA, hB, "Length", log);
            }
        }

        private static bool TryLongestParallelPair(
            SmartDimHelper h,
            IView view,
            Edge[] edges,
            bool vertical,
            double minSpan,
            out Edge edgeA,
            out Edge edgeB)
        {
            edgeA = null!;
            edgeB = null!;
            double bestSpan = 0;

            Func<Edge, double> coord = e => vertical
                ? h.GetEdgeMidpointOnSheet(e, view)[1]
                : h.GetEdgeMidpointOnSheet(e, view)[0];

            var longEdges = edges
                .Where(e => h.GetProjectedLength(e, view) >= minSpan * 0.5)
                .ToArray();

            if (longEdges.Length < 2)
                return false;

            for (int i = 0; i < longEdges.Length; i++)
            {
                for (int j = i + 1; j < longEdges.Length; j++)
                {
                    double span = Math.Abs(coord(longEdges[j]) - coord(longEdges[i]));
                    if (span >= minSpan && span > bestSpan)
                    {
                        bestSpan = span;
                        edgeA = longEdges[i];
                        edgeB = longEdges[j];
                    }
                }
            }

            return bestSpan > 0;
        }

        private static void TryDiameterDimension(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge circle,
            double diameter,
            string label,
            Action<string> log)
        {
            string key = $"Cyl_{label}_{diameter:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(circle, view, false))
                    return;

                double[] center = h.GetCircleCenterOnSheet(circle, view);
                DisplayDimension? dim = h.CreateDimension(center[0], center[1] + DimOffset);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  {label} Ø{diameter * 1000:F1} mm in {viewName}.");
                }
            }
            catch (InvalidCastException ex)
            {
                log($"  Warning: {label} dimension cast failed in {viewName}: {ex.Message}");
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryWallThickness(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge inner,
            Edge outer,
            double wall,
            Action<string> log)
        {
            if (wall < MinSizeMeters)
                return;

            string key = $"Cyl_Wall_{wall:F4}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValue(view, wall))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(inner, view, false))
                    return;
                if (!h.SelectEdge(outer, view, true))
                    return;

                double[] center = h.GetCircleCenterOnSheet(outer, view);
                DisplayDimension? dim = h.CreateLinearDimension(
                    center[0] + DimOffset,
                    center[1] - DimOffset * 2.0);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  Wall thickness {wall * 1000:F1} mm in {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryInnerDiameterInParentheses(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge innerCircle,
            double innerDiameter,
            Action<string> log)
        {
            string key = $"Cyl_ID_{innerDiameter:F4}";
            if (h.DimensionedFeatures.Contains(key))
                return;

            if (h.TrySetParenthesesOnDiameter(view, innerDiameter))
            {
                h.DimensionedFeatures.Add(key);
                log($"  ID (Ø{innerDiameter * 1000:F1} mm) marked with parentheses on {viewName}.");
                return;
            }

            if (h.HasDimensionWithValueInDrawing(innerDiameter))
                return;

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(innerCircle, view, false))
                    return;

                double[] center = h.GetCircleCenterOnSheet(innerCircle, view);
                DisplayDimension? dim = h.CreateDiameterDimension(
                    innerCircle, view, center[0], center[1] - DimOffset * 3.2)
                    ?? h.CreateDimension(center[0], center[1] - DimOffset * 3.2);

                if (dim != null)
                {
                    dim.ShowParenthesis = true;
                    h.DimensionedFeatures.Add(key);
                    log($"  ID (Ø{innerDiameter * 1000:F1} mm) in parentheses on {viewName}.");
                }
            }
            finally
            {
                h.ClearSelection();
            }
        }

        private static void TryLinearDimension(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge edgeA,
            Edge edgeB,
            string label,
            Action<string> log)
        {
            var (sA, eA) = h.GetEdgeEndpointsOnSheet(edgeA, view);
            var (sB, eB) = h.GetEdgeEndpointsOnSheet(edgeB, view);
            double length = Math.Max(
                Math.Abs(sA[1] - sB[1]),
                Math.Abs(sA[0] - sB[0]));

            if (length < MinSizeMeters)
                return;

            string key = $"Cyl_{label}_{length:F4}";
            try
            {
                if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(length))
                    return;
            }
            catch (InvalidCastException)
            {
                // Continue — duplicate probe failed due to interop cast; still try to place dim.
            }

            try
            {
                h.ClearSelection();
                if (!h.SelectEdge(edgeA, view, false))
                    return;
                if (!h.SelectEdge(edgeB, view, true))
                    return;

                double dimX = (sA[0] + sB[0]) / 2.0 + DimOffset;
                double dimY = (sA[1] + sB[1]) / 2.0;
                DisplayDimension? dim = h.CreateDimension(dimX, dimY);
                if (dim != null)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  {label} {length * 1000:F1} mm in {viewName}.");
                }
            }
            catch (InvalidCastException ex)
            {
                log($"  Warning: {label} dimension cast failed in {viewName}: {ex.Message}");
            }
            finally
            {
                h.ClearSelection();
            }
        }
    }
}
