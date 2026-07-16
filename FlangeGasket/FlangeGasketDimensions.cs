using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Cylindrical;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>
    /// Dimensions for flanges and gaskets: OD, ID, BCD, pattern angle, hole qty, thickness.
    /// </summary>
    internal static class FlangeGasketDimensions
    {
        private const double DimOffset = 0.012;
        private const double MaxHoleDiameterMeters = 0.25;

        public static void AddForView(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            IView? discFaceView,
            Action<string> log)
        {
            string viewName = view.GetName2();
            bool isDiscFace = discFaceView != null
                ? ReferenceEquals(view, discFaceView)
                : FlangeGasketViewAnalyzer.IsDominantDiscFaceView(h, view);

            if (!isDiscFace)
                return;

            Edge[] edges = h.GetViewEdgesCached(view);
            FlangeDiscGeometry? geometry = FlangeGasketPatternGeometry.Analyze(h, view, edges);
            if (geometry == null)
                return;

            CylindricalDimCenterlines.Add(h, model, drawing, view, log);
            AddDiscFaceDimensions(h, drawing, view, viewName, geometry, edges, model, log);
        }

        public static void AddThicknessOnce(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView? discFaceView,
            Action<string> log) =>
            FlangeGasketProfileDimensions.TryAddOnce(h, model, drawing, discFaceView, null, log);

        private static void AddDiscFaceDimensions(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            string viewName,
            FlangeDiscGeometry geometry,
            Edge[] edges,
            IModelDoc2 model,
            Action<string> log)
        {
            TryOuterDiameter(h, view, viewName, geometry.OuterCircle, log);

            if (geometry.InnerBoreCircle != null && geometry.InnerDiameterMeters.HasValue)
                TryInnerDiameter(h, view, viewName, geometry.InnerBoreCircle, geometry.InnerDiameterMeters.Value, log);

            AddBoltHoleDiameters(h, drawing, view, viewName, geometry, edges, model, log);

            if (geometry.PrimaryBoltCircle != null)
            {
                InsertBoltCircleCenterMarks(h, drawing, view, geometry.PrimaryBoltCircle, log);
                TryBoltCircleDiameter(h, view, viewName, geometry, log);
                TryPatternAngle(h, view, viewName, geometry, log);
            }
        }

        private static void InsertBoltCircleCenterMarks(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            BoltCircleRing ring,
            Action<string> log)
        {
            int placed = 0;
            foreach (Edge hole in ring.Holes)
            {
                if (h.TryInsertCenterMark(drawing, view, hole))
                    placed++;
            }

            if (placed > 0)
                log($"  [{view.GetName2()}] Center marks on {placed} bolt-hole(s).");
        }

        private static void TryOuterDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge outerCircle,
            Action<string> log)
        {
            double diameter = Math.Round(h.GetCircleRadius(outerCircle) * 2.0, 4);
            string key = $"Flange_OD_{diameter:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            double[] center = h.GetCircleCenterOnSheet(outerCircle, view);
            DisplayDimension? dim = h.CreateDiameterDimension(
                outerCircle, view, center[0], center[1] + DimOffset * 2.5);

            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] OD Ø{diameter * 1000:F1} mm.");
            }
        }

        private static void TryInnerDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            Edge innerCircle,
            double diameter,
            Action<string> log)
        {
            string key = $"Flange_ID_{diameter:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(diameter))
                return;

            double[] center = h.GetCircleCenterOnSheet(innerCircle, view);
            DisplayDimension? dim = h.CreateDiameterDimension(
                innerCircle, view, center[0] - DimOffset, center[1] - DimOffset);

            if (dim != null)
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] ID Ø{diameter * 1000:F1} mm.");
            }
        }

        private static void AddBoltHoleDiameters(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            string viewName,
            FlangeDiscGeometry geometry,
            Edge[] edges,
            IModelDoc2 model,
            Action<string> log)
        {
            double excludeOuter = geometry.OuterDiameterMeters;
            double? excludeInner = geometry.InnerDiameterMeters;
            double maxHole = Math.Max(MaxHoleDiameterMeters, geometry.OuterDiameterMeters * 0.92);

            var holeEdges = edges
                .Where(h.IsCircular)
                .Where(e => h.IsFullCircle(e))
                .Where(e =>
                {
                    double d = Math.Round(h.GetCircleRadius(e) * 2.0, 4);
                    if (Math.Abs(d - excludeOuter) < 0.0002)
                        return false;
                    if (excludeInner.HasValue && Math.Abs(d - excludeInner.Value) < 0.0002)
                        return false;
                    return d >= 0.002 && d <= maxHole;
                })
                .ToArray();

            var groups = new Dictionary<double, List<Edge>>();
            foreach (Edge edge in holeEdges)
            {
                double d = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (!groups.ContainsKey(d))
                    groups[d] = new List<Edge>();
                groups[d].Add(edge);
            }

            int groupIndex = 0;

            foreach (var kvp in groups.OrderBy(g => g.Key))
            {
                double diameter = kvp.Key;
                int qty = kvp.Value.Count;
                string key = $"FlangeHole_{diameter:F4}_{viewName}";

                if (h.DimensionedFeatures.Contains(key))
                    continue;

                if (qty > 1 && h.HasQuantityPrefixForDiameter(view, diameter, qty))
                {
                    h.DimensionedFeatures.Add(key);
                    continue;
                }

                if (qty > 1 && h.TryUpgradeHoleQuantityPrefix(view, diameter, qty) > 0)
                {
                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Hole Ø{diameter * 1000:F1} mm × {qty} (upgraded imported).");
                    continue;
                }

                if (h.HasDimensionWithValueInDrawing(diameter,
                        (int)swDimensionType_e.swDiameterDimension) ||
                    h.HasDimensionWithValueInDrawing(diameter,
                        (int)swDimensionType_e.swRadialDimension))
                {
                    if (qty > 1)
                        h.DeleteBareDiameterDimensions(model, view, diameter);
                    else
                        continue;
                }

                Edge representative = kvp.Value[0];
                double[] center = h.GetCircleCenterOnSheet(representative, view);

                h.ClearSelection();
                DisplayDimension? dim = h.CreateDiameterDimension(
                    representative,
                    view,
                    center[0] + DimOffset + groupIndex * 0.004,
                    center[1] + DimOffset);

                if (dim != null)
                {
                    if (qty > 1)
                        dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, $"{qty}x ");

                    h.DimensionedFeatures.Add(key);
                    log($"  [{viewName}] Hole Ø{diameter * 1000:F1} mm × {qty}.");
                    groupIndex++;
                }

                h.ClearSelection();
            }
        }

        private static void TryBoltCircleDiameter(
            SmartDimHelper h,
            IView view,
            string viewName,
            FlangeDiscGeometry geometry,
            Action<string> log)
        {
            BoltCircleRing? ring = geometry.PrimaryBoltCircle;
            if (ring == null)
                return;

            double bcd = Math.Round(ring.BoltCircleDiameterMeters, 4);
            string key = $"Flange_BCD_{bcd:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) || h.HasDimensionWithValueInDrawing(bcd))
                return;

            (Edge first, Edge second)? opposite =
                FlangeGasketPatternGeometry.FindOppositeHoles(ring, h, view);

            DisplayDimension? dim = null;

            if (opposite != null)
            {
                h.ClearSelection();
                h.SelectEdge(opposite.Value.first, view, false);
                h.SelectEdge(opposite.Value.second, view, true);

                double[] c1 = h.GetCircleCenterOnSheet(opposite.Value.first, view);
                double[] c2 = h.GetCircleCenterOnSheet(opposite.Value.second, view);
                double dimX = (c1[0] + c2[0]) / 2.0;
                double dimY = (c1[1] + c2[1]) / 2.0 + DimOffset;

                dim = h.CreateDimension(dimX, dimY);
            }

            if (dim == null)
            {
                Edge hole = ring.Holes[0];
                h.ClearSelection();
                h.SelectEdge(hole, view, false);
                double[] c = h.GetCircleCenterOnSheet(hole, view);
                dim = h.CreateDimension(c[0] + DimOffset, c[1] + DimOffset * 2.0);
            }

            if (dim != null)
            {
                dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, "BCD ");
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] BCD {bcd * 1000:F1} mm ({ring.Holes.Count} holes).");
            }

            h.ClearSelection();
        }

        private static void TryPatternAngle(
            SmartDimHelper h,
            IView view,
            string viewName,
            FlangeDiscGeometry geometry,
            Action<string> log)
        {
            BoltCircleRing? ring = geometry.PrimaryBoltCircle;
            if (ring == null || ring.Holes.Count < 2)
                return;

            (Edge first, Edge second, double angleDegrees)? adjacent =
                FlangeGasketPatternGeometry.FindAdjacentHolePair(ring, h, view);

            if (adjacent == null)
                return;

            double expectedDegrees = adjacent.Value.angleDegrees;
            if (ring.Holes.Count >= 3)
            {
                double equalSpacing = 360.0 / ring.Holes.Count;
                if (Math.Abs(expectedDegrees - equalSpacing) < 2.0)
                    expectedDegrees = equalSpacing;
            }

            double angleRad = expectedDegrees * Math.PI / 180.0;
            string key = $"Flange_Angle_{angleRad:F4}_{viewName}";
            if (h.DimensionedFeatures.Contains(key) ||
                h.HasDimensionWithValueInDrawing(angleRad, (int)swDimensionType_e.swAngularDimension))
                return;

            // Remove stray angular dims (e.g. 165° to a centerline) before placing the pitch.
            int removed = h.DeleteAngularDimensionsNotNearDegrees(h.Model, view, expectedDegrees, toleranceDegrees: 2.5);
            if (removed > 0)
                log($"  [{viewName}] Removed {removed} incorrect angular dim(s).");

            DisplayDimension? dim =
                TryAngularViaCenterRays(h, view, adjacent.Value.first, adjacent.Value.second, geometry, expectedDegrees) ??
                TryAngularViaHoleEdges(h, view, adjacent.Value.first, adjacent.Value.second, geometry, expectedDegrees);

            if (dim != null && IsAngularNearDegrees(dim, expectedDegrees, 2.5))
            {
                h.DimensionedFeatures.Add(key);
                log($"  [{viewName}] Pattern angle {expectedDegrees:F1}° (between adjacent holes).");
            }
            else
            {
                if (dim != null)
                    TryDeleteDisplayDimension(h, dim);
                log($"  [{viewName}] Warning: could not place pattern angle {expectedDegrees:F1}° between holes.");
            }

            h.ClearSelection();
        }

        private static DisplayDimension? TryAngularViaHoleEdges(
            SmartDimHelper h,
            IView view,
            Edge first,
            Edge second,
            FlangeDiscGeometry geometry,
            double expectedDegrees)
        {
            (double dimX, double dimY) = MinorArcTextPoint(
                geometry.DiscCenterOnSheet,
                h.GetCircleCenterOnSheet(first, view),
                h.GetCircleCenterOnSheet(second, view));

            h.ClearSelection();
            if (!h.SelectEdge(first, view, false) || !h.SelectEdge(second, view, true))
            {
                h.ClearSelection();
                return null;
            }

            DisplayDimension? dim = h.CreateAngularDimension(dimX, dimY);
            h.ClearSelection();

            if (dim == null || dim.Type2 != (int)swDimensionType_e.swAngularDimension)
                return null;

            if (!IsAngularNearDegrees(dim, expectedDegrees, 2.5))
            {
                TryDeleteDisplayDimension(h, dim);
                return null;
            }

            return dim;
        }

        private static void ExitSketchIfNeeded(IModelDoc2 model)
        {
            try
            {
                if (model.GetActiveSketch() != null)
                    model.SketchManager.InsertSketch(true);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Temporary construction rays from disc center to two adjacent hole centers,
        /// then angular dim on the minor arc between those rays.
        /// </summary>
        private static DisplayDimension? TryAngularViaCenterRays(
            SmartDimHelper h,
            IView view,
            Edge firstHole,
            Edge secondHole,
            FlangeDiscGeometry geometry,
            double expectedDegrees)
        {
            IModelDoc2 model = h.Model;
            IDrawingDoc drawing = h.Drawing;
            string viewName = view.GetName2();

            double[] c = h.GetCircleCenter(geometry.OuterCircle);
            double[] a = h.GetCircleCenter(firstHole);
            double[] b = h.GetCircleCenter(secondHole);

            (double dimX, double dimY) = MinorArcTextPoint(
                geometry.DiscCenterOnSheet,
                h.GetCircleCenterOnSheet(firstHole, view),
                h.GetCircleCenterOnSheet(secondHole, view));

            drawing.ActivateView(viewName);
            model.ClearSelection2(true);

            try
            {
                model.SketchManager.InsertSketch(true);

                var ray1 = model.SketchManager.CreateLine(c[0], c[1], c[2], a[0], a[1], a[2]) as SketchSegment;
                var ray2 = model.SketchManager.CreateLine(c[0], c[1], c[2], b[0], b[1], b[2]) as SketchSegment;
                if (ray1 == null || ray2 == null)
                {
                    ExitSketchIfNeeded(model);
                    return null;
                }

                ray1.ConstructionGeometry = true;
                ray2.ConstructionGeometry = true;

                ExitSketchIfNeeded(model);

                h.ClearSelection();
                if (!h.SelectSketchSegment(ray1, view, false) ||
                    !h.SelectSketchSegment(ray2, view, true))
                    return null;

                DisplayDimension? dim = h.CreateAngularDimension(dimX, dimY);
                if (dim == null || dim.Type2 != (int)swDimensionType_e.swAngularDimension)
                    return null;

                if (!IsAngularNearDegrees(dim, expectedDegrees, 2.5))
                {
                    TryDeleteDisplayDimension(h, dim);
                    return null;
                }

                return dim;
            }
            catch
            {
                ExitSketchIfNeeded(model);
                return null;
            }
            finally
            {
                h.ClearSelection();
            }
        }

        /// <summary>Sheet text point on the minor-arc bisector between two hole centers.</summary>
        private static (double x, double y) MinorArcTextPoint(double[] center, double[] holeA, double[] holeB)
        {
            double angA = Math.Atan2(holeA[1] - center[1], holeA[0] - center[0]);
            double angB = Math.Atan2(holeB[1] - center[1], holeB[0] - center[0]);

            double diff = angB - angA;
            while (diff > Math.PI) diff -= 2.0 * Math.PI;
            while (diff < -Math.PI) diff += 2.0 * Math.PI;

            // Walk the short way from A to B.
            double mid = angA + diff / 2.0;
            double radius = (
                Math.Sqrt(Math.Pow(holeA[0] - center[0], 2) + Math.Pow(holeA[1] - center[1], 2)) +
                Math.Sqrt(Math.Pow(holeB[0] - center[0], 2) + Math.Pow(holeB[1] - center[1], 2))) / 2.0;

            double textRadius = Math.Max(radius * 0.72, DimOffset * 3);
            return (
                center[0] + Math.Cos(mid) * textRadius,
                center[1] + Math.Sin(mid) * textRadius);
        }

        private static bool IsAngularNearDegrees(DisplayDimension dim, double expectedDegrees, double tolDegrees)
        {
            try
            {
                Dimension? modelDim = dim.GetDimension2(0) as Dimension;
                if (modelDim == null)
                    return false;

                double deg = Math.Abs(modelDim.SystemValue) * 180.0 / Math.PI;
                // Normalize into [0, 180] — SW may return supplementary.
                while (deg > 180.0) deg = 360.0 - deg;
                return Math.Abs(deg - expectedDegrees) <= tolDegrees;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteDisplayDimension(SmartDimHelper h, DisplayDimension dim)
        {
            try
            {
                Annotation? ann = dim.GetAnnotation() as Annotation;
                if (ann == null)
                    return;

                h.Model.ClearSelection2(true);
                ann.Select3(false, null);
                h.Model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                h.Model.ClearSelection2(true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
