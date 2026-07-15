using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>
    /// Upgrades / recreates hole diameter callouts as quantity prefixes (e.g. 24x Ø50.8).
    /// Imported Mark-for-Drawing dims often accept SetText(Prefix) in API but do not display it —
    /// so the reliable path is delete + recreate a smart diameter.
    /// </summary>
    internal static class FlangeGasketImportedDimUpgrader
    {
        private const double MaxHoleDiameterMeters = 0.25;
        private static readonly Regex VisibleQtyRegex =
            new(@"\b\d+\s*[x×]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void UpgradeDiscFaceDimensions(
            SmartDimHelper h,
            IModelDoc2 model,
            IView? discFaceView,
            Action<string> log)
        {
            if (discFaceView == null)
                return;

            EnsureHoleQuantityCallouts(h, model, discFaceView, log);

            FlangeDiscGeometry? geometry = FlangeGasketPatternGeometry.Analyze(
                h, discFaceView, h.GetViewEdgesCached(discFaceView));
            TryUpgradeBcdPrefix(h, discFaceView, discFaceView.GetName2(), geometry, log);
        }

        public static void EnsureHoleQuantityCallouts(
            SmartDimHelper h,
            IModelDoc2 model,
            IView? discFaceView,
            Action<string> log)
        {
            if (discFaceView == null)
                return;

            string viewName = discFaceView.GetName2();
            // Fresh edges — cache may be stale after import/dedupe.
            h.ClearViewCaches();
            Edge[] edges = h.GetViewEdgesCached(discFaceView);
            FlangeDiscGeometry? geometry = FlangeGasketPatternGeometry.Analyze(h, discFaceView, edges);
            if (geometry == null)
            {
                log($"  [{viewName}] Warning: no flange disc geometry for hole qty.");
                return;
            }

            foreach (var group in BuildHoleGroups(h, edges, geometry).OrderByDescending(g => g.Count))
            {
                if (group.Count <= 1)
                    continue;

                if (HasVisibleQuantityCallout(h, discFaceView, group.Diameter, group.Count))
                {
                    int bare = h.DeleteBareDiameterDimensions(model, discFaceView, group.Diameter);
                    if (bare > 0)
                        log($"  [{viewName}] Removed {bare} bare Ø{group.Diameter * 1000:F1} mm (kept {group.Count}x).");
                    continue;
                }

                // Never trust SetText on imported dims — always recreate.
                int removed = h.DeleteAllDiameterDimensions(model, discFaceView, group.Diameter);
                // Also purge same Ø from sibling orthographic views so dedupe cannot revive a bare Ø.
                RemoveDiameterFromSiblingViews(h, model, discFaceView, group.Diameter);

                Edge? representative = PickHoleEdge(h, edges, geometry, group.Diameter);
                if (representative == null)
                {
                    log($"  [{viewName}] Warning: no hole edge for {group.Count}x Ø{group.Diameter * 1000:F1} mm" +
                        (removed > 0 ? $" (removed {removed})." : "."));
                    continue;
                }

                double[] center = h.GetCircleCenterOnSheet(representative, discFaceView);
                h.ClearSelection();
                DisplayDimension? dim = h.CreateDiameterDimension(
                    representative,
                    discFaceView,
                    center[0] + 0.014,
                    center[1] + 0.014);
                h.ClearSelection();

                if (dim == null)
                {
                    log($"  [{viewName}] Warning: failed to create Ø{group.Diameter * 1000:F1} mm callout.");
                    continue;
                }

                if (!TryApplyVisibleQuantityPrefix(dim, group.Count, model))
                {
                    log($"  [{viewName}] Warning: created Ø{group.Diameter * 1000:F1} mm but quantity prefix did not stick.");
                    continue;
                }

                log($"  [{viewName}] Hole callout {group.Count}x Ø{group.Diameter * 1000:F1} mm" +
                    (removed > 0 ? $" (recreated, removed {removed} old)." : "."));
            }
        }

        private static bool TryApplyVisibleQuantityPrefix(DisplayDimension dim, int quantity, IModelDoc2 model)
        {
            string prefix = $"{quantity}x ";
            try
            {
                dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, prefix);
                model.GraphicsRedraw2();

                if (IsVisibleQuantity(dim, quantity))
                    return true;

                // Alternate times glyph.
                dim.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, $"{quantity}× ");
                model.GraphicsRedraw2();
                return IsVisibleQuantity(dim, quantity);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVisibleQuantity(DisplayDimension dim, int quantity)
        {
            try
            {
                string prefix = dim.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty;
                string all = dim.GetText((int)swDimensionTextParts_e.swDimensionTextAll) ?? string.Empty;
                string expected = quantity.ToString();
                bool prefixOk = prefix.Contains(expected, StringComparison.Ordinal) &&
                                VisibleQtyRegex.IsMatch(prefix);
                // All-text must also reflect qty for dims that hide Prefix in the UI.
                bool allOk = VisibleQtyRegex.IsMatch(all) && all.Contains(expected, StringComparison.Ordinal);
                return prefixOk || allOk;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasVisibleQuantityCallout(
            SmartDimHelper h,
            IView view,
            double diameterMeters,
            int quantity)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    Dimension? modelDim = displayDim?.GetDimension2(0) as Dimension;
                    if (displayDim != null && modelDim != null)
                    {
                        int type = displayDim.Type2;
                        double value = Math.Abs(modelDim.SystemValue);
                        bool diaMatch =
                            (type == (int)swDimensionType_e.swDiameterDimension &&
                             Math.Abs(value - diameterMeters) < 0.00025) ||
                            (type == (int)swDimensionType_e.swRadialDimension &&
                             Math.Abs(value * 2.0 - diameterMeters) < 0.00025);

                        if (diaMatch && IsVisibleQuantity(displayDim, quantity))
                            return true;
                    }
                }

                ann = ann.GetNext3();
            }

            return false;
        }

        private static void RemoveDiameterFromSiblingViews(
            SmartDimHelper h,
            IModelDoc2 model,
            IView discFaceView,
            double diameterMeters)
        {
            IView? view = (h.Drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!ReferenceEquals(view, discFaceView) &&
                    !view.GetName2().Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    h.DeleteAllDiameterDimensions(model, view, diameterMeters);
                }

                view = view.GetNextView() as IView;
            }
        }

        private static Edge? PickHoleEdge(
            SmartDimHelper h,
            Edge[] edges,
            FlangeDiscGeometry geometry,
            double diameter)
        {
            Edge? fromEdges = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e))
                .FirstOrDefault(e => Math.Abs(h.GetCircleRadius(e) * 2.0 - diameter) < 0.00025);

            if (fromEdges != null)
                return fromEdges;

            return geometry.PrimaryBoltCircle?.Holes
                .FirstOrDefault(e => Math.Abs(h.GetCircleRadius(e) * 2.0 - diameter) < 0.00025);
        }

        private static void TryUpgradeBcdPrefix(
            SmartDimHelper h,
            IView view,
            string viewName,
            FlangeDiscGeometry? geometry,
            Action<string> log)
        {
            BoltCircleRing? ring = geometry?.PrimaryBoltCircle;
            if (ring == null)
                return;

            double bcd = Math.Round(ring.BoltCircleDiameterMeters, 4);
            if (h.TrySetLinearDimensionPrefix(view, bcd, "BCD "))
                log($"  [{viewName}] BCD prefix applied to imported {bcd * 1000:F1} mm.");
        }

        private static IEnumerable<HoleDiameterGroup> BuildHoleGroups(
            SmartDimHelper h,
            Edge[] edges,
            FlangeDiscGeometry geometry)
        {
            double excludeOuter = geometry.OuterDiameterMeters;
            double? excludeInner = geometry.InnerDiameterMeters;
            double maxHole = Math.Max(MaxHoleDiameterMeters, geometry.OuterDiameterMeters * 0.92);

            var groups = new Dictionary<double, List<Edge>>();

            foreach (Edge edge in edges.Where(e => h.IsCircular(e) && h.IsFullCircle(e)))
            {
                double d = Math.Round(h.GetCircleRadius(edge) * 2.0, 4);
                if (Math.Abs(d - excludeOuter) < 0.0002)
                    continue;
                if (excludeInner.HasValue && Math.Abs(d - excludeInner.Value) < 0.0002)
                    continue;
                if (d < 0.002 || d > maxHole)
                    continue;

                if (!groups.ContainsKey(d))
                    groups[d] = new List<Edge>();
                groups[d].Add(edge);
            }

            // Prefer bolt-circle count when edge grouping under-counts (duplicate edge refs).
            if (geometry.PrimaryBoltCircle != null)
            {
                double holeDia = Math.Round(geometry.PrimaryBoltCircle.HoleDiameterMeters, 4);
                int boltCount = geometry.PrimaryBoltCircle.Holes.Count;
                if (boltCount >= 3)
                {
                    if (!groups.ContainsKey(holeDia) || groups[holeDia].Count < boltCount)
                        yield return new HoleDiameterGroup(holeDia, boltCount);
                }
            }

            foreach (var kvp in groups)
            {
                if (geometry.PrimaryBoltCircle != null &&
                    Math.Abs(kvp.Key - geometry.PrimaryBoltCircle.HoleDiameterMeters) < 0.00025 &&
                    geometry.PrimaryBoltCircle.Holes.Count >= 3)
                    continue;

                yield return new HoleDiameterGroup(kvp.Key, kvp.Value.Count);
            }
        }

        private sealed record HoleDiameterGroup(double Diameter, int Count);
    }
}
