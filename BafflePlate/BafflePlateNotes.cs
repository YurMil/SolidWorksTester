using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Sheet-level general notes for baffle / tube-sheet family (EST reference style).
    /// </summary>
    internal static class BafflePlateNotes
    {
        /// <summary>
        /// Places a multi-line general note above the title block.
        /// Related hole-pattern drawing id comes from EST props when present.
        /// </summary>
        public static void InsertFamilyNotes(
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult? analysis,
            Action<string> log)
        {
            string patternRef = ResolveHolePatternDrawingRef(analysis);
            var lines = new List<string>
            {
                "No sharp edge",
                "Mark 0° and 90° location to edge and back face",
                string.IsNullOrWhiteSpace(patternRef)
                    ? "Full hole pattern in separate drawing"
                    : $"Full hole pattern in drawing {patternRef}"
            };

            string text = string.Join("\n", lines);

            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet != null)
                drawing.ActivateSheet(sheet.GetName());

            model.ClearSelection2(true);

            Note? note = model.InsertNote(text) as Note;
            if (note == null)
            {
                log("  Warning: InsertNote failed for baffle family notes.");
                return;
            }

            double[] props = sheet != null ? (double[])sheet.GetProperties2() : Array.Empty<double>();
            double sheetW = props.Length > 5 ? props[5] : 0.841;
            double sheetH = props.Length > 6 ? props[6] : 0.594;
            double x = 0.025;
            double y = Math.Max(0.055, sheetH * 0.10);

            note.SetTextPoint(x, y, 0.0);
            try
            {
                note.SetHeight(0.0035);
            }
            catch
            {
                // optional
            }

            log($"  Baffle notes placed ({lines.Count} lines) at ({x:F3}, {y:F3}) " +
                $"on sheet {sheetW * 1000:F0}×{sheetH * 1000:F0} mm.");
        }

        private static string ResolveHolePatternDrawingRef(PartAnalysisResult? analysis)
        {
            if (analysis?.EstProperties == null)
                return "EST-P91559";

            string? id = analysis.EstProperties.IdNumber;
            if (!string.IsNullOrWhiteSpace(id) &&
                id.Contains("P91559", StringComparison.OrdinalIgnoreCase))
                return id.Trim();

            return "EST-P91559";
        }
    }
}
