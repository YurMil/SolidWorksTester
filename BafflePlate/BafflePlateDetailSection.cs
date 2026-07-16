using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// P2 baffle engineering views: Detail A (1:1 pattern) + Section B-B (2:1 through a hole row).
    /// Sketch circle/line must be pre-selected via <see cref="ISketchSegment.Select4"/> (not IEntity).
    /// </summary>
    internal static class BafflePlateDetailSection
    {
        private const double DetailCirclePitches = 2.6;
        /// <summary>Short local cut — through the seed hole and its direct neighbors only.</summary>
        private const double SectionHalfLengthPitches = 1.5;
        private const double NoteCharHeightMeters = 0.003;

        public static void TryCreate(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView? primaryFlatView,
            Services.Analysis.PartAnalysisResult? analysis,
            Action<string> log)
        {
            if (primaryFlatView == null)
            {
                log("  Detail/section: no primary view.");
                return;
            }

            var samples = analysis?.CylinderSamples;
            if (samples == null || samples.Count < 3)
            {
                log($"  Detail/section: no cylinder samples from analysis ({samples?.Count ?? 0}).");
                return;
            }

            // Part doc is optional — only used for exact pcs from pattern-feature totals.
            TryGetPartDoc(primaryFlatView, out PartDoc? part);

            if (!BafflePlateHolePattern.TryFromAnalysisSamples(
                    samples, part, log, out BaffleHolePatternInfo pattern))
                return;

            IView? detail = TryCreateDetailA(h, model, drawing, primaryFlatView, pattern, log);

            // Section from Detail A (EST reference style): the cut spans the detail bubble only.
            // A section of the full primary view would be span × 2 wide (meters of sheet).
            IView? section = detail != null
                ? TryCreateSectionBB(h, model, drawing, detail, pattern, log)
                : null;
            if (detail == null)
                log("  Section B-B: skipped (no Detail A parent — full-view cut would not fit any sheet).");

            if (detail != null)
                AnnotateDetail(model, drawing, detail, pattern, log);

            if (section != null)
                AnnotateSection(model, drawing, section, pattern, log);
        }

        private static IView? TryCreateDetailA(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView parent,
            BaffleHolePatternInfo pattern,
            Action<string> log)
        {
            string parentName = parent.GetName2();
            double[] seedSheet = h.TransformToSheet(
                new[] { pattern.SeedX, pattern.SeedY, pattern.SeedZ }, parent);

            // Sketches in an activated drawing view live in the VIEW'S MODEL space
            // (verified live: sheet pt → inverse ModelToViewTransform; radius in model meters).
            double[] seedSketch = SheetToViewSketch(h, parent, seedSheet[0], seedSheet[1]);
            double circleRadiusModel = Math.Max(pattern.PitchMeters * DetailCirclePitches, 0.040);

            ResolveDetailPlacement(drawing, parent, out double placeX, out double placeY);

            model.ClearSelection2(true);
            drawing.ActivateView(parentName);

            ISketchManager sketchMgr = model.SketchManager;
            sketchMgr.AddToDB = true;
            ISketchSegment? circle = sketchMgr.CreateCircleByRadius(
                seedSketch[0], seedSketch[1], 0.0, circleRadiusModel) as ISketchSegment;
            sketchMgr.AddToDB = false;

            if (circle == null)
            {
                log("  Detail A: CreateCircleByRadius failed.");
                ExitSketchIfNeeded(model);
                return null;
            }

            log($"  Detail A: region circle at view-model ({seedSketch[0]:F4}, {seedSketch[1]:F4}), " +
                $"r={circleRadiusModel * 1000:F0} mm model (sheet seed {seedSheet[0]:F4},{seedSheet[1]:F4}).");

            model.ClearSelection2(true);
            // SketchSegment is NOT IEntity — use ISketchSegment.Select4 (see 5_5 bend lines).
            if (!circle.Select4(false, null))
            {
                log("  Detail A: SketchSegment.Select4 failed — CreateDetailViewAt4 skipped.");
                ExitSketchIfNeeded(model);
                return null;
            }

            IView? detail = null;
            try
            {
                object? created = drawing.CreateDetailViewAt4(
                    placeX,
                    placeY,
                    0.0,
                    0,
                    1.0,
                    1.0,
                    "A",
                    0,
                    true,
                    false,
                    false,
                    0); // swDetViewFontSource_Document (enum missing in this interop)

                detail = created as IView;
            }
            catch (Exception ex)
            {
                log($"  Detail A: CreateDetailViewAt4 threw ({ex.Message}).");
                model.ClearSelection2(true);
                ExitSketchIfNeeded(model);
                return null;
            }

            model.ClearSelection2(true);
            ExitSketchIfNeeded(model);

            if (detail == null)
            {
                log("  Detail A: CreateDetailViewAt4 returned null.");
                return null;
            }

            log($"  Detail A created at ({placeX:F3}, {placeY:F3}), scale 1:1.");
            return detail;
        }

        private static IView? TryCreateSectionBB(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView parent,
            BaffleHolePatternInfo pattern,
            Action<string> log)
        {
            string parentName = parent.GetName2();

            double[] seedSheet = h.TransformToSheet(
                new[] { pattern.SeedX, pattern.SeedY, pattern.SeedZ }, parent);
            double[] neighSheet = h.TransformToSheet(
                new[] { pattern.NeighborX, pattern.NeighborY, pattern.NeighborZ }, parent);

            // View-model sketch space (same convention as the detail circle).
            double[] seedSk = SheetToViewSketch(h, parent, seedSheet[0], seedSheet[1]);
            double[] neighSk = SheetToViewSketch(h, parent, neighSheet[0], neighSheet[1]);

            double dx = neighSk[0] - seedSk[0];
            double dy = neighSk[1] - seedSk[1];
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9)
            {
                log("  Section B-B: degenerate row direction.");
                return null;
            }

            dx /= len;
            dy /= len;
            // Model meters — the cut spans seed hole ± 1.5 pitches.
            double halfLen = Math.Max(pattern.PitchMeters * SectionHalfLengthPitches, 0.040);

            double x1 = seedSk[0] - dx * halfLen;
            double y1 = seedSk[1] - dy * halfLen;
            double x2 = seedSk[0] + dx * halfLen;
            double y2 = seedSk[1] + dy * halfLen;

            ResolveSectionPlacement(drawing, parent, out double placeX, out double placeY);

            model.ClearSelection2(true);
            drawing.ActivateView(parentName);

            ISketchManager sketchMgr = model.SketchManager;
            sketchMgr.AddToDB = true;
            ISketchLine? line = sketchMgr.CreateLine(x1, y1, 0.0, x2, y2, 0.0) as ISketchLine;
            sketchMgr.AddToDB = false;

            if (line == null)
            {
                log("  Section B-B: CreateLine failed.");
                ExitSketchIfNeeded(model);
                return null;
            }

            log($"  Section B-B: sketch line ({x1:F4},{y1:F4})→({x2:F4},{y2:F4}).");

            model.ClearSelection2(true);
            if (line is not ISketchSegment lineSeg || !lineSeg.Select4(false, null))
            {
                log("  Section B-B: SketchSegment.Select4 failed — CreateSectionViewAt5 skipped.");
                ExitSketchIfNeeded(model);
                return null;
            }

            IView? section = null;
            try
            {
                section = drawing.CreateSectionViewAt5(
                    placeX,
                    placeY,
                    0.0,
                    "B",
                    0,
                    null,
                    0.0) as IView;
            }
            catch (Exception ex)
            {
                log($"  Section B-B: CreateSectionViewAt5 threw ({ex.Message}).");
                model.ClearSelection2(true);
                ExitSketchIfNeeded(model);
                return null;
            }

            model.ClearSelection2(true);
            ExitSketchIfNeeded(model);

            if (section == null)
            {
                log("  Section B-B: CreateSectionViewAt5 returned null.");
                return null;
            }

            // Scale immediately after create (before annotations) — one HLR pass.
            TrySetViewScale(section, 2, 1);
            log($"  Section B-B created at ({placeX:F3}, {placeY:F3}), scale 2:1.");
            return section;
        }

        private static void AnnotateDetail(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView detail,
            BaffleHolePatternInfo pattern,
            Action<string> log)
        {
            PlacePatternNote(
                model,
                drawing,
                detail,
                $"PITCH {pattern.PitchMeters * 1000:F0}\r\n" +
                $"{pattern.AngleDegrees:F0}°\r\n" +
                $"Ø{pattern.RadiusMeters * 2000:F1}",
                log,
                "Detail A");
        }

        private static void AnnotateSection(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView section,
            BaffleHolePatternInfo pattern,
            Action<string> log)
        {
            // Pcs only when exact (pattern feature); a sampled lower bound would mislead.
            string text = pattern.HoleCountExact
                ? $"Ø{pattern.RadiusMeters * 2000:F1}\r\n{pattern.HoleCount} pcs"
                : $"Ø{pattern.RadiusMeters * 2000:F1}";
            PlacePatternNote(model, drawing, section, text, log, "Section B-B");

            TryPlaceRa32(model, drawing, section, log);
        }

        private static void PlacePatternNote(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            string text,
            Action<string> log,
            string label)
        {
            try
            {
                ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
                if (sheet != null)
                    drawing.ActivateSheet(sheet.GetName());

                model.ClearSelection2(true);
                Note? note = model.InsertNote(text) as Note;
                if (note == null)
                {
                    log($"  {label}: InsertNote failed.");
                    return;
                }

                double[] pos = view.Position as double[] ?? new[] { 0.5, 0.4 };
                if (!FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double w, out double h))
                {
                    w = 0.06;
                    h = 0.06;
                }

                note.SetTextPoint(pos[0] + w * 0.55, pos[1] - h * 0.05, 0.0);
                TrySetNoteCharHeight(note);

                log($"  {label}: annotation note placed.");
            }
            catch (Exception ex)
            {
                log($"  {label}: note failed ({ex.Message}).");
            }
        }

        private static void TrySetNoteCharHeight(Note note)
        {
            try
            {
                if (note.GetTextFormat() is not TextFormat tf)
                    return;

                tf.CharHeight = NoteCharHeightMeters;
                note.SetTextFormat(0, tf);
            }
            catch
            {
                // optional cosmetics
            }
        }

        private static void TryPlaceRa32(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView section,
            Action<string> log)
        {
            try
            {
                ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
                if (sheet != null)
                    drawing.ActivateSheet(sheet.GetName());

                model.ClearSelection2(true);

                double[] pos = section.Position as double[] ?? new[] { 0.5, 0.3 };
                if (!FlatPlateViewAnalyzer.TryGetOutlineSize(section, out double w, out double h))
                {
                    w = 0.05;
                    h = 0.04;
                }

                double x = pos[0] - w * 0.2;
                double y = pos[1] + h * 0.55;

                SFSymbol? sym = model.Extension.InsertSurfaceFinishSymbol3(
                    1,
                    (int)swLeaderStyle_e.swNO_LEADER,
                    x,
                    y,
                    0.0,
                    0,
                    (int)swArrowStyle_e.swCLOSED_ARROWHEAD,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "3.2",
                    string.Empty,
                    string.Empty) as SFSymbol;

                if (sym == null)
                {
                    log("  Section B-B: InsertSurfaceFinishSymbol3 returned null.");
                    return;
                }

                log("  Section B-B: Ra 3.2 surface finish placed.");
            }
            catch (Exception ex)
            {
                log($"  Section B-B: Ra symbol failed ({ex.Message}).");
            }
        }

        private static void ExitSketchIfNeeded(IModelDoc2 model)
        {
            try
            {
                // Leaving Edit Sketch open blocks subsequent drawing COM calls.
                if (model.GetActiveSketch() != null)
                    model.SketchManager.InsertSketch(true);
            }
            catch
            {
                // Best-effort — view creation may already have closed the sketch.
            }
        }

        private static void TrySetViewScale(IView view, double num, double den)
        {
            try
            {
                view.ScaleRatio = new[] { num, den };
            }
            catch
            {
                try
                {
                    view.ScaleDecimal = num / den;
                }
                catch
                {
                    // optional
                }
            }
        }

        private static void ResolveDetailPlacement(
            IDrawingDoc drawing,
            IView parent,
            out double x,
            out double y)
        {
            GetSheetSize(drawing, out double sheetW, out double sheetH);
            x = sheetW * 0.72;
            y = sheetH * 0.72;

            double[]? p = parent.Position as double[];
            if (p != null && p.Length >= 2)
            {
                x = Math.Max(x, p[0] + 0.28);
                y = Math.Max(y, p[1] + 0.08);
            }

            ClampToSheet(sheetW, sheetH, ref x, ref y);
        }

        private static void ResolveSectionPlacement(
            IDrawingDoc drawing,
            IView parent,
            out double x,
            out double y)
        {
            GetSheetSize(drawing, out double sheetW, out double sheetH);

            // Parent is Detail A — park the section just below it.
            double[]? p = parent.Position as double[];
            if (p != null && p.Length >= 2)
            {
                x = p[0];
                y = p[1] - 0.14;
            }
            else
            {
                x = sheetW * 0.72;
                y = sheetH * 0.38;
            }

            ClampToSheet(sheetW, sheetH, ref x, ref y);
        }

        /// <summary>
        /// Sheet point → the view's sketch space. Verified live (SW 2025): sketch entities
        /// created after ActivateView are stored in the view's MODEL coordinates; the mapping
        /// from sheet space is the inverse of <see cref="IView.ModelToViewTransform"/>.
        /// </summary>
        private static double[] SheetToViewSketch(SmartDimHelper h, IView view, double x, double y)
        {
            IMathUtility mathUtil = (IMathUtility)h.SwApp.GetMathUtility();
            var inverse = (IMathTransform)((IMathTransform)view.ModelToViewTransform).Inverse();
            var pt = (IMathPoint)mathUtil.CreatePoint(new[] { x, y, 0.0 });
            return (double[])((IMathPoint)pt.MultiplyTransform(inverse)).ArrayData;
        }

        private static void GetSheetSize(IDrawingDoc drawing, out double w, out double h)
        {
            w = 0.841;
            h = 0.594;
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet?.GetProperties2() is double[] props && props.Length >= 7 &&
                props[5] > 0.05 && props[6] > 0.05)
            {
                w = props[5];
                h = props[6];
            }
        }

        private static void ClampToSheet(double sheetW, double sheetH, ref double x, ref double y)
        {
            x = Math.Clamp(x, 0.06, sheetW - 0.06);
            y = Math.Clamp(y, 0.12, sheetH - 0.06);
        }

        private static bool TryGetPartDoc(IView view, out PartDoc? part)
        {
            part = null;
            try
            {
                object[]? comps = view.GetVisibleComponents() as object[];
                if (comps != null && comps.Length > 0 &&
                    comps[0] is Component2 comp &&
                    comp.GetModelDoc2() is PartDoc fromComp)
                {
                    part = fromComp;
                    return true;
                }

                if (view.ReferencedDocument is PartDoc fromRef)
                {
                    part = fromRef;
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            return false;
        }
    }
}
