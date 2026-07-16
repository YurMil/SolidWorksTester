using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Switches EST sheet formats (EST_A1L…EST_A4L) when files exist next to the
    /// active .slddrt / under C:\EST\91_SW Setup\Sheet Formats. Fast path = File.Exists only.
    /// </summary>
    internal static class DrawingSheetProfile
    {
        private const string DefaultFormatsDir = @"C:\EST\91_SW Setup\Sheet Formats";
        private static readonly int[] StandardScaleDenominators = { 1, 2, 5, 10, 20, 25, 50, 100 };

        private readonly struct EstFormat
        {
            public EstFormat(string fileStem, swDwgPaperSizes_e paper, double widthM, double heightM)
            {
                FileStem = fileStem;
                Paper = paper;
                WidthM = widthM;
                HeightM = heightM;
            }

            public string FileStem { get; }
            public swDwgPaperSizes_e Paper { get; }
            public double WidthM { get; }
            public double HeightM { get; }
            public string FileName => FileStem + ".slddrt";
        }

        // Landscape EST borders (same names as in Sheet Properties list).
        private static readonly EstFormat FormatA1 = new("EST_A1L", swDwgPaperSizes_e.swDwgPaperA1size, 0.841, 0.594);
        private static readonly EstFormat FormatA2 = new("EST_A2L", swDwgPaperSizes_e.swDwgPaperA2size, 0.594, 0.420);
        private static readonly EstFormat FormatA3 = new("EST_A3L", swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297);
        private static readonly EstFormat FormatA4 = new("EST_A4L", swDwgPaperSizes_e.swDwgPaperA4size, 0.297, 0.210);

        public static bool IsBaffleFamily(PartAnalysisResult analysis, DrawingRouteDecision route) =>
            route.ForcedFlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
            analysis.FlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
            analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
            string.Equals(analysis.EstNameCatalogId, "baffle_plate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(analysis.EstNameCatalogId, "baffle_plate_fuzzy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(analysis.DrawingProfile, "baffle_plate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(analysis.DrawingProfile, "baffle_plate_fuzzy", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Tries EST_A1L/A2L for large baffles. Returns true when the .slddrt was actually switched.
        /// If the target file is missing, keeps the active template (no paper/border mismatch).
        /// </summary>
        public static bool ApplyForBaffleIfNeeded(
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            double spanMeters = Math.Max(analysis.BboxLongMeters, analysis.BboxMidMeters);

            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet == null)
                return false;

            string currentSlddrt = sheet.GetTemplateName() ?? string.Empty;
            string? formatsDir = ResolveFormatsDirectory(currentSlddrt, log);
            if (formatsDir == null)
            {
                log("  Sheet format: no EST Sheet Formats folder — keep active template.");
                return false;
            }

            EstFormat target = PickTargetFormat(spanMeters);
            if (!TryResolveFormatFile(formatsDir, target, out string targetPath))
            {
                // Prefer next-smaller EST frame if A1 missing.
                if (string.Equals(target.FileStem, FormatA1.FileStem, StringComparison.OrdinalIgnoreCase) &&
                    TryResolveFormatFile(formatsDir, FormatA2, out targetPath))
                {
                    target = FormatA2;
                    log($"  Sheet format: EST_A1L missing — fallback {target.FileName}.");
                }
                else
                {
                    LogAvailability(formatsDir, log);
                    log($"  Sheet format: {target.FileName} not found in {formatsDir} — keep active template.");
                    return false;
                }
            }

            // Already on the desired border — only nudge scale if needed.
            if (!string.IsNullOrWhiteSpace(currentSlddrt) && PathsEqual(currentSlddrt, targetPath))
            {
                log($"  Sheet format: already {target.FileStem} — keep border, adjust scale only.");
                return TrySetScaleOnly(model, drawing, sheet, spanMeters, target, log);
            }

            double[] sheetProps = (double[])sheet.GetProperties2();
            string sheetName = sheet.GetName();
            bool firstAngle = sheetProps.Length > 4 && sheetProps[4] == 1;

            // Always load custom .slddrt by path (list name EST_A1L ↔ file EST_A1L.slddrt).
            int templateIn = (int)swDwgTemplates_e.swDwgTemplateCustom;

            double usable = Math.Min(target.WidthM, target.HeightM) * 0.50;
            int scaleDen = PickScaleDenominator(spanMeters, usable);

            log($"  Sheet format: switch → {target.FileStem} ({target.WidthM * 1000:F0}×{target.HeightM * 1000:F0} mm), " +
                $"scale 1:{scaleDen} (span {spanMeters * 1000:F0} mm).");
            log($"    slddrt: {targetPath}");

            bool ok = drawing.SetupSheet6(
                sheetName,
                (int)target.Paper,
                templateIn,
                1,
                scaleDen,
                firstAngle,
                targetPath,
                target.WidthM,
                target.HeightM,
                "Default",
                true, // drop notes from previous (smaller) border
                0, 0, 0, 0, 0, 0);

            if (!ok)
            {
                log("  Warning: SetupSheet6 returned false — active template unchanged.");
                return false;
            }

            model.ForceRebuild3(true);
            return true;
        }

        /// <summary>
        /// Places Drawing View1/2/3 into the content area of the current sheet
        /// (above title block, left of right-hand stamp). Call after views exist.
        /// </summary>
        public static void RelayoutOrthographicViews(IDrawingDoc drawing, Action<string> log)
        {
            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet == null)
                return;

            double[] props = (double[])sheet.GetProperties2();
            if (props.Length < 7)
                return;

            double w = props[5];
            double h = props[6];
            var layout = DrawingViewLayout.ForSheet(w, h);

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            int moved = 0;
            while (view != null)
            {
                string name = view.GetName2();
                double x, y;
                if (name.Equals("Drawing View1", StringComparison.OrdinalIgnoreCase))
                {
                    x = layout.FrontX;
                    y = layout.FrontY;
                }
                else if (name.Equals("Drawing View2", StringComparison.OrdinalIgnoreCase))
                {
                    x = layout.TopX;
                    y = layout.TopY;
                }
                else if (name.Equals("Drawing View3", StringComparison.OrdinalIgnoreCase))
                {
                    x = layout.RightX;
                    y = layout.RightY;
                }
                else
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                try
                {
                    view.Position = new[] { x, y };
                    moved++;
                    log($"    reposition {name} → ({x:F3}, {y:F3})");
                }
                catch (Exception ex)
                {
                    log($"    Warning: could not move {name}: {ex.Message}");
                }

                view = view.GetNextView() as IView;
            }

            if (moved > 0)
                log($"  View layout: {moved} orthographic view(s) on {w * 1000:F0}×{h * 1000:F0} mm sheet.");
        }

        public static int PickScaleDenominator(double bboxLongMeters, double usableSheetMeters)
        {
            double longMm = bboxLongMeters * 1000.0;
            double usableMm = Math.Max(usableSheetMeters * 1000.0, 1.0);

            foreach (int d in StandardScaleDenominators)
            {
                if (longMm / d <= usableMm)
                    return d;
            }

            return StandardScaleDenominators[^1];
        }

        private static EstFormat PickTargetFormat(double spanMeters)
        {
            if (spanMeters >= 1.50)
                return FormatA1;
            if (spanMeters >= 0.90)
                return FormatA2;
            return FormatA3;
        }

        private static string? ResolveFormatsDirectory(string currentSlddrt, Action<string> log)
        {
            if (!string.IsNullOrWhiteSpace(currentSlddrt))
            {
                string? dir = Path.GetDirectoryName(currentSlddrt);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    // Prefer folder that actually contains EST_A*.slddrt (fast Exists checks).
                    if (File.Exists(Path.Combine(dir, FormatA3.FileName)) ||
                        File.Exists(Path.Combine(dir, FormatA1.FileName)))
                        return dir;
                }
            }

            if (Directory.Exists(DefaultFormatsDir))
                return DefaultFormatsDir;

            log($"  Sheet format: default folder missing ({DefaultFormatsDir}).");
            return null;
        }

        private static bool TryResolveFormatFile(string formatsDir, EstFormat format, out string fullPath)
        {
            fullPath = Path.Combine(formatsDir, format.FileName);
            return File.Exists(fullPath);
        }

        private static void LogAvailability(string formatsDir, Action<string> log)
        {
            bool a1 = File.Exists(Path.Combine(formatsDir, FormatA1.FileName));
            bool a2 = File.Exists(Path.Combine(formatsDir, FormatA2.FileName));
            bool a3 = File.Exists(Path.Combine(formatsDir, FormatA3.FileName));
            bool a4 = File.Exists(Path.Combine(formatsDir, FormatA4.FileName));
            log($"  Sheet format availability: A1={(a1 ? "yes" : "no")}, A2={(a2 ? "yes" : "no")}, " +
                $"A3={(a3 ? "yes" : "no")}, A4={(a4 ? "yes" : "no")}");
        }

        private static bool TrySetScaleOnly(
            IModelDoc2 model,
            IDrawingDoc drawing,
            ISheet sheet,
            double spanMeters,
            EstFormat format,
            Action<string> log)
        {
            double[] sheetProps = (double[])sheet.GetProperties2();
            double usable = Math.Min(format.WidthM, format.HeightM) * 0.50;
            int scaleDen = PickScaleDenominator(spanMeters, usable);
            int currentDen = sheetProps.Length > 3 ? (int)sheetProps[3] : 0;
            if (currentDen == scaleDen)
            {
                log($"  Sheet scale: already 1:{scaleDen}.");
                return false;
            }

            string slddrt = sheet.GetTemplateName() ?? string.Empty;
            bool ok = drawing.SetupSheet6(
                sheet.GetName(),
                (int)format.Paper,
                (int)swDwgTemplates_e.swDwgTemplateCustom,
                1,
                scaleDen,
                sheetProps.Length > 4 && sheetProps[4] == 1,
                slddrt,
                format.WidthM,
                format.HeightM,
                "Default",
                false,
                0, 0, 0, 0, 0, 0);

            if (ok)
            {
                log($"  Sheet scale: 1:{currentDen} → 1:{scaleDen}.");
                model.ForceRebuild3(true);
            }

            return ok;
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(
                Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
    }
}
