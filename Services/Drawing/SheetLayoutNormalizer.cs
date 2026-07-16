using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Conservative final layout: keep the proven sheet anchors, then only
    /// push projected views along their free axis so outline gaps are ≥ 25 mm,
    /// and park the isometric in the free top-right corner.
    /// </summary>
    internal static class SheetLayoutNormalizer
    {
        private const double GapMeters = 0.025;
        private const double MarginMeters = 0.015;
        private const double StampWidthMeters = 0.185;
        private const double StampHeightMeters = 0.090;
        private const int MaxScaleSteps = 5;

        private static readonly int[] StandardDenominators = { 1, 2, 5, 10, 20, 25, 50, 100 };

        private enum ViewRole { Front, Top, Right, Iso, Free }

        private sealed class ViewBox
        {
            public required IView View { get; init; }
            public required string Name { get; init; }
            public ViewRole Role { get; set; }
            public double MinX { get; set; }
            public double MinY { get; set; }
            public double MaxX { get; set; }
            public double MaxY { get; set; }
            public double Cx { get; set; }
            public double Cy { get; set; }
            /// <summary>True when top view sits above front (first-angle style).</summary>
            public bool TopIsAboveFront { get; set; }

            public double W => Math.Max(MaxX - MinX, 1e-6);
            public double H => Math.Max(MaxY - MinY, 1e-6);
        }

        public static void Arrange(IModelDoc2 model, IDrawingDoc drawing, Action<string> log)
        {
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet == null)
            {
                log("  Sheet layout: no active sheet.");
                return;
            }

            if (!TryGetSheetSize(sheet, out double sheetW, out double sheetH))
            {
                log("  Sheet layout: cannot read sheet size.");
                return;
            }

            log("  Sheet layout: outline gaps + stable anchors...");

            BindViewsToSheetScale(drawing);

            for (int step = 0; step <= MaxScaleSteps; step++)
            {
                List<ViewBox> views = CollectViews(drawing);
                if (views.Count == 0)
                {
                    log("  Sheet layout: no model views.");
                    return;
                }

                AssignRoles(views);
                RememberTopSide(views);

                // 1) Park front on the same anchor family as CreateStandardThreeViews.
                PlaceFrontOnStableAnchor(views, sheetW, sheetH, log);

                // 2) Children: only the free projection axis, outline-based gap.
                SpaceProjectedChildren(views, log);

                // 3) Iso into remaining top-right (never over the ortho pack / stamp).
                PlaceIsoTopRight(views, sheetW, sheetH, log);

                // 4) Detail / section / flat — only if clearly free; else leave alone.
                NudgeFreeViewsIfOverlapping(views, sheetW, sheetH, log);

                RefreshOutlines(views);

                if (PackOverflows(views, sheetW, sheetH) &&
                    TryStepScaleDown(model, drawing, sheet, log))
                {
                    model.ForceRebuild3(true);
                    continue;
                }

                // One gentle gap pass — axis-only for ortho, corner park for iso.
                EnsureOrthoGaps(views, log);
                EnsureIsoClear(views, sheetW, sheetH, log);

                LogSummary(views, sheetW, sheetH, log);
                return;
            }

            log("  Sheet layout: finished (scale steps exhausted).");
        }

        private static bool TryGetSheetSize(ISheet sheet, out double w, out double h)
        {
            w = 0.420;
            h = 0.297;
            try
            {
                sheet.GetSize(ref w, ref h);
                if (w > 0.05 && h > 0.05)
                    return true;
            }
            catch
            {
                // fall through
            }

            if (sheet.GetProperties2() is double[] props && props.Length >= 7)
            {
                w = props[5];
                h = props[6];
                return w > 0.05 && h > 0.05;
            }

            return false;
        }

        private static List<ViewBox> CollectViews(IDrawingDoc drawing)
        {
            var list = new List<ViewBox>();
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (TryReadBox(view, out ViewBox box))
                    list.Add(box);
                view = view.GetNextView() as IView;
            }

            return list;
        }

        private static bool TryReadBox(IView view, out ViewBox box)
        {
            box = null!;
            if (view.GetOutline() is not double[] o || o.Length < 4)
                return false;

            double[]? pos = view.Position as double[];
            box = new ViewBox
            {
                View = view,
                Name = view.GetName2() ?? string.Empty,
                Role = ViewRole.Free,
                MinX = o[0],
                MinY = o[1],
                MaxX = o[2],
                MaxY = o[3],
                Cx = pos is { Length: >= 2 } ? pos[0] : (o[0] + o[2]) * 0.5,
                Cy = pos is { Length: >= 2 } ? pos[1] : (o[1] + o[3]) * 0.5
            };
            return true;
        }

        private static void RefreshOutlines(List<ViewBox> views)
        {
            foreach (ViewBox box in views)
                RefreshOne(box);
        }

        private static void RefreshOne(ViewBox box)
        {
            if (box.View.GetOutline() is not double[] o || o.Length < 4)
                return;

            box.MinX = o[0];
            box.MinY = o[1];
            box.MaxX = o[2];
            box.MaxY = o[3];
            double[]? pos = box.View.Position as double[];
            if (pos is { Length: >= 2 })
            {
                box.Cx = pos[0];
                box.Cy = pos[1];
            }
        }

        private static void AssignRoles(List<ViewBox> views)
        {
            foreach (ViewBox box in views)
            {
                int type = 0;
                try { type = box.View.Type; } catch { /* ignore */ }

                if (type == (int)swDrawingViewTypes_e.swDrawingDetailView ||
                    type == (int)swDrawingViewTypes_e.swDrawingSectionView)
                {
                    box.Role = ViewRole.Free;
                    continue;
                }

                if (box.Name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase) ||
                    box.Name.Contains("Isometric", StringComparison.OrdinalIgnoreCase))
                    box.Role = ViewRole.Iso;
                else if (box.Name.Equals("Drawing View1", StringComparison.OrdinalIgnoreCase))
                    box.Role = ViewRole.Front;
                else if (box.Name.Equals("Drawing View2", StringComparison.OrdinalIgnoreCase))
                    box.Role = ViewRole.Top;
                else if (box.Name.Equals("Drawing View3", StringComparison.OrdinalIgnoreCase))
                    box.Role = ViewRole.Right;
                else
                    box.Role = ViewRole.Free;
            }

            if (views.All(v => v.Role != ViewRole.Front))
            {
                ViewBox? pick = views.FirstOrDefault(v =>
                    v.Role is ViewRole.Top or ViewRole.Right) ??
                    views.OrderByDescending(v => v.W * v.H).FirstOrDefault();
                if (pick != null)
                    pick.Role = ViewRole.Front;
            }
        }

        private static void RememberTopSide(List<ViewBox> views)
        {
            ViewBox? front = views.FirstOrDefault(v => v.Role == ViewRole.Front);
            ViewBox? top = views.FirstOrDefault(v => v.Role == ViewRole.Top);
            if (front == null || top == null)
                return;

            // Preserve whatever CreateUnfoldedViewAt3 already established.
            top.TopIsAboveFront = top.Cy >= front.Cy;
        }

        private static void PlaceFrontOnStableAnchor(
            List<ViewBox> views,
            double sheetW,
            double sheetH,
            Action<string> log)
        {
            ViewBox? front = views.FirstOrDefault(v => v.Role == ViewRole.Front);
            if (front == null)
                return;

            var layout = DrawingViewLayout.ForSheet(sheetW, sheetH);
            SetPosition(front, layout.FrontX, layout.FrontY);
            RefreshOne(front);
            log($"    front anchor → ({front.Cx:F3}, {front.Cy:F3})");
        }

        private static void SpaceProjectedChildren(List<ViewBox> views, Action<string> log)
        {
            ViewBox? front = views.FirstOrDefault(v => v.Role == ViewRole.Front);
            if (front == null)
                return;

            ViewBox? right = views.FirstOrDefault(v => v.Role == ViewRole.Right);
            ViewBox? top = views.FirstOrDefault(v => v.Role == ViewRole.Top);

            if (right != null)
            {
                RefreshOne(front);
                RefreshOne(right);
                // Desired: gap between outline edges on X. SW keeps Y aligned with parent.
                double neededCx = front.MaxX + GapMeters + right.W * 0.5;
                // Position is view origin, not always outline center — convert via current delta.
                double outlineCx = (right.MinX + right.MaxX) * 0.5;
                double delta = right.Cx - outlineCx;
                SetPosition(right, neededCx + delta, right.Cy);
                RefreshOne(right);
                log($"    right gap → x={right.Cx:F3} (outline gap ≥ {GapMeters * 1000:F0} mm)");
            }

            if (top != null)
            {
                RefreshOne(front);
                RefreshOne(top);
                bool above = top.TopIsAboveFront;
                double outlineCy = (top.MinY + top.MaxY) * 0.5;
                double delta = top.Cy - outlineCy;
                double neededCy = above
                    ? front.MaxY + GapMeters + top.H * 0.5
                    : front.MinY - GapMeters - top.H * 0.5;
                SetPosition(top, top.Cx, neededCy + delta);
                RefreshOne(top);
                log($"    top gap → y={top.Cy:F3} ({(above ? "above" : "below")} front)");
            }
        }

        private static void PlaceIsoTopRight(
            List<ViewBox> views,
            double sheetW,
            double sheetH,
            Action<string> log)
        {
            ViewBox? iso = views.FirstOrDefault(v => v.Role == ViewRole.Iso);
            if (iso == null)
                return;

            RefreshOutlines(views);
            RefreshOne(iso);

            double stampLeft = sheetW - StampWidthMeters;
            double stampTop = StampHeightMeters;

            // Right of the orthographic pack, below the top margin.
            double orthoMaxX = views
                .Where(v => v.Role is ViewRole.Front or ViewRole.Top or ViewRole.Right)
                .Select(v => v.MaxX)
                .DefaultIfEmpty(sheetW * 0.5)
                .Max();
            double orthoMaxY = views
                .Where(v => v.Role is ViewRole.Front or ViewRole.Top or ViewRole.Right)
                .Select(v => v.MaxY)
                .DefaultIfEmpty(sheetH * 0.5)
                .Max();

            double outlineCx = (iso.MinX + iso.MaxX) * 0.5;
            double outlineCy = (iso.MinY + iso.MaxY) * 0.5;
            double dx = iso.Cx - outlineCx;
            double dy = iso.Cy - outlineCy;

            double targetOutlineCx = Math.Max(
                orthoMaxX + GapMeters + iso.W * 0.5,
                sheetW - MarginMeters - iso.W * 0.5);
            targetOutlineCx = Math.Min(targetOutlineCx, sheetW - MarginMeters - iso.W * 0.5);

            double targetOutlineCy = sheetH - MarginMeters - iso.H * 0.5;
            // Keep above stamp and preferably above the ortho pack mid-line.
            targetOutlineCy = Math.Max(targetOutlineCy, Math.Max(stampTop + GapMeters + iso.H * 0.5, orthoMaxY * 0.55));
            targetOutlineCy = Math.Min(targetOutlineCy, sheetH - MarginMeters - iso.H * 0.5);

            SetPosition(iso, targetOutlineCx + dx, targetOutlineCy + dy);
            RefreshOne(iso);

            // If still over stamp, nudge up/left.
            if (IntersectsStamp(iso, stampLeft, stampTop))
            {
                targetOutlineCy = stampTop + GapMeters + iso.H * 0.5;
                SetPosition(iso, targetOutlineCx + dx, targetOutlineCy + dy);
                RefreshOne(iso);
            }

            log($"    iso → ({iso.Cx:F3}, {iso.Cy:F3})");
        }

        private static void NudgeFreeViewsIfOverlapping(
            List<ViewBox> views,
            double sheetW,
            double sheetH,
            Action<string> log)
        {
            var free = views.Where(v => v.Role == ViewRole.Free).ToList();
            if (free.Count == 0)
                return;

            var blockers = views.Where(v => v.Role != ViewRole.Free).ToList();
            foreach (ViewBox box in free)
            {
                RefreshOne(box);

                // Wider/taller than the usable sheet — no position can help; leave for review.
                if (box.W > sheetW - 2 * MarginMeters || box.H > sheetH - 2 * MarginMeters)
                {
                    log($"    free {box.Name} exceeds sheet ({box.W * 1000:F0}×{box.H * 1000:F0} mm) — left in place.");
                    continue;
                }

                bool hits = blockers.Any(b => Intersects(box, b, GapMeters * 0.5));
                if (!hits)
                    continue;

                // Park mid-right above stamp — one try only (no corner roulette), clamped to sheet.
                double outlineCx = (box.MinX + box.MaxX) * 0.5;
                double outlineCy = (box.MinY + box.MaxY) * 0.5;
                double dx = box.Cx - outlineCx;
                double dy = box.Cy - outlineCy;
                double tx = Math.Clamp(
                    sheetW - MarginMeters - box.W * 0.5,
                    MarginMeters + box.W * 0.5,
                    sheetW - MarginMeters - box.W * 0.5);
                double ty = Math.Clamp(
                    Math.Max(StampHeightMeters + GapMeters + box.H * 0.5, sheetH * 0.45),
                    MarginMeters + box.H * 0.5,
                    sheetH - MarginMeters - box.H * 0.5);
                SetPosition(box, tx + dx, ty + dy);
                RefreshOne(box);
                log($"    free {box.Name} nudged → ({box.Cx:F3}, {box.Cy:F3})");
            }
        }

        private static void EnsureOrthoGaps(List<ViewBox> views, Action<string> log)
        {
            ViewBox? front = views.FirstOrDefault(v => v.Role == ViewRole.Front);
            ViewBox? right = views.FirstOrDefault(v => v.Role == ViewRole.Right);
            ViewBox? top = views.FirstOrDefault(v => v.Role == ViewRole.Top);
            if (front == null)
                return;

            RefreshOne(front);

            if (right != null)
            {
                RefreshOne(right);
                double gap = right.MinX - front.MaxX;
                if (gap < GapMeters)
                {
                    double push = GapMeters - gap;
                    SetPosition(right, right.Cx + push, right.Cy);
                    RefreshOne(right);
                    log($"    right push +{push * 1000:F0} mm");
                }
            }

            if (top != null)
            {
                RefreshOne(top);
                if (top.TopIsAboveFront)
                {
                    double gap = top.MinY - front.MaxY;
                    if (gap < GapMeters)
                    {
                        double push = GapMeters - gap;
                        SetPosition(top, top.Cx, top.Cy + push);
                        RefreshOne(top);
                        log($"    top push +{push * 1000:F0} mm");
                    }
                }
                else
                {
                    double gap = front.MinY - top.MaxY;
                    if (gap < GapMeters)
                    {
                        double push = GapMeters - gap;
                        SetPosition(top, top.Cx, top.Cy - push);
                        RefreshOne(top);
                        log($"    top push -{push * 1000:F0} mm");
                    }
                }
            }
        }

        private static void EnsureIsoClear(
            List<ViewBox> views,
            double sheetW,
            double sheetH,
            Action<string> log)
        {
            ViewBox? iso = views.FirstOrDefault(v => v.Role == ViewRole.Iso);
            if (iso == null)
                return;

            RefreshOne(iso);
            var ortho = views.Where(v =>
                v.Role is ViewRole.Front or ViewRole.Top or ViewRole.Right).ToList();

            bool hit = ortho.Any(o => Intersects(iso, o, GapMeters * 0.5));
            if (!hit)
                return;

            double maxX = ortho.Max(o => o.MaxX);
            double push = (maxX + GapMeters + iso.W * 0.5) - (iso.MinX + iso.MaxX) * 0.5;
            if (push > 0)
            {
                SetPosition(iso, iso.Cx + push, iso.Cy);
                RefreshOne(iso);
                log($"    iso clear +{push * 1000:F0} mm X");
            }

            // If still colliding, push up.
            hit = ortho.Any(o => Intersects(iso, o, GapMeters * 0.5));
            if (hit)
            {
                double maxY = ortho.Max(o => o.MaxY);
                double pushY = (maxY + GapMeters + iso.H * 0.5) - (iso.MinY + iso.MaxY) * 0.5;
                if (pushY > 0)
                {
                    double cy = Math.Min(iso.Cy + pushY, sheetH - MarginMeters - iso.H * 0.5);
                    SetPosition(iso, iso.Cx, cy);
                    RefreshOne(iso);
                    log($"    iso clear +{pushY * 1000:F0} mm Y");
                }
            }
        }

        private static bool PackOverflows(List<ViewBox> views, double sheetW, double sheetH)
        {
            double left = MarginMeters;
            double bottom = MarginMeters;
            double right = sheetW - MarginMeters;
            double top = sheetH - MarginMeters;
            double stampLeft = sheetW - StampWidthMeters;

            foreach (ViewBox v in views)
            {
                // Free views (detail 1:1 / section 2:1) have their own scale — shrinking the
                // sheet scale can never fit them, so they must not drive the densify loop.
                if (v.Role == ViewRole.Free)
                    continue;

                if (v.MinX < left - 0.01 || v.MaxX > right + 0.01 ||
                    v.MinY < bottom - 0.01 || v.MaxY > top + 0.01)
                    return true;

                // Ortho/iso deep inside stamp zone → need smaller scale.
                if (v.MaxX > stampLeft + 0.01 &&
                    v.MinY < StampHeightMeters - 0.01)
                    return true;
            }

            ViewBox? front = views.FirstOrDefault(v => v.Role == ViewRole.Front);
            ViewBox? rightView = views.FirstOrDefault(v => v.Role == ViewRole.Right);
            ViewBox? topView = views.FirstOrDefault(v => v.Role == ViewRole.Top);
            if (front == null)
                return false;

            double needW = front.W + (rightView != null ? GapMeters + rightView.W : 0);
            double needH = front.H + (topView != null ? GapMeters + topView.H : 0);
            ViewBox? iso = views.FirstOrDefault(v => v.Role == ViewRole.Iso);
            if (iso != null)
                needW += GapMeters + iso.W * 0.5;

            return needW > (sheetW - 2 * MarginMeters) || needH > (sheetH - 2 * MarginMeters);
        }

        private static void BindViewsToSheetScale(IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                view.UseSheetScale = 1;
                view = view.GetNextView() as IView;
            }
        }

        private static bool TryStepScaleDown(
            IModelDoc2 model,
            IDrawingDoc drawing,
            ISheet sheet,
            Action<string> log)
        {
            double[] props = (double[])sheet.GetProperties2();
            if (props.Length < 7)
                return false;

            int num = Math.Max(1, (int)props[2]);
            int den = Math.Max(1, (int)props[3]);
            int idx = IndexOfDenom(den);
            if (idx < 0 || idx + 1 >= StandardDenominators.Length)
            {
                log($"  Sheet layout: cannot densify further (1:{den}).");
                return false;
            }

            int newDen = StandardDenominators[idx + 1];
            bool ok = drawing.SetupSheet6(
                sheet.GetName(),
                (int)props[0],
                (int)props[1],
                num,
                newDen,
                props.Length > 4 && props[4] == 1,
                sheet.GetTemplateName() ?? string.Empty,
                props[5],
                props[6],
                "Default",
                false,
                0, 0, 0, 0, 0, 0);

            if (!ok)
                return false;

            log($"  Sheet layout: scale 1:{den} → 1:{newDen} (fit).");
            return true;
        }

        private static int IndexOfDenom(int den)
        {
            for (int i = 0; i < StandardDenominators.Length; i++)
            {
                if (StandardDenominators[i] == den)
                    return i;
            }

            for (int i = 0; i < StandardDenominators.Length; i++)
            {
                if (StandardDenominators[i] >= den)
                    return i;
            }

            return StandardDenominators.Length - 1;
        }

        private static void SetPosition(ViewBox box, double cx, double cy)
        {
            try
            {
                box.View.Position = new[] { cx, cy };
                box.Cx = cx;
                box.Cy = cy;
            }
            catch
            {
                // projected views may reject one axis
            }
        }

        private static bool Intersects(ViewBox a, ViewBox b, double gap) =>
            a.MinX < b.MaxX + gap &&
            a.MaxX + gap > b.MinX &&
            a.MinY < b.MaxY + gap &&
            a.MaxY + gap > b.MinY;

        private static bool IntersectsStamp(ViewBox box, double stampLeft, double stampTop) =>
            box.MaxX > stampLeft && box.MinY < stampTop;

        private static void LogSummary(
            List<ViewBox> views,
            double sheetW,
            double sheetH,
            Action<string> log)
        {
            log($"  Sheet layout: {views.Count} view(s) on {sheetW * 1000:F0}×{sheetH * 1000:F0} mm " +
                $"(gap {GapMeters * 1000:F0} mm, anchors=ForSheet).");
        }
    }
}
