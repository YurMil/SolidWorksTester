using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Lofted-bend shell dimensions matched to EST reference drawings:
    /// side (View1) — height, OD (+ prefix), id (+ prefix), axis centerline;
    /// end (View2) — wall thickness, weld-gap centerline + linear gap at outer tips;
    /// flat pattern — length + width + bend notes.
    /// </summary>
    internal static partial class LoftedBendsDimensions
    {
        private static void EnsureFlatPatternLengthAndWidth(
            SmartDimHelper h,
            IView view,
            ShellSizeHints hints,
            Action<string> log)
        {
            Edge[] edges = h.GetViewEdges(view);
            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length == 0)
            {
                log("  Flat pattern: no edges.");
                return;
            }

            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double outlineW = (maxX - minX) / scale;
            double outlineH = (maxY - minY) / scale;

            var horiz = linear.Where(e => h.IsHorizontalInView(e, view)).ToArray();
            var vert = linear.Where(e => h.IsVerticalInView(e, view)).ToArray();

            // LENGTH = long axis on sheet.
            bool lengthIsHorizontal = outlineW >= outlineH;
            bool lengthOk = false;
            bool widthOk = false;

            if (lengthIsHorizontal)
            {
                // Length: longest horizontal edge OR left↔right vertical pair.
                lengthOk = TryDimLongestEdge(h, view, horiz, maxY + DimOffset, "LoftFpLength",
                    hints.FlatLengthMeters, log, "length")
                    || TryDimParallelPair(h, view, vert, verticalPair: true,
                        minX - DimOffset, (minY + maxY) / 2.0,
                        "LoftFpLength", hints.FlatLengthMeters, log, "length");

                // Width (= shell height): top↔bottom horizontal pair.
                widthOk = TryDimParallelPair(h, view, horiz, verticalPair: false,
                    maxX + DimOffset, (minY + maxY) / 2.0,
                    "LoftFpWidth", hints.HeightMeters, log, "width");
            }
            else
            {
                lengthOk = TryDimLongestEdge(h, view, vert, maxX + DimOffset, "LoftFpLength",
                    hints.FlatLengthMeters, log, "length")
                    || TryDimParallelPair(h, view, horiz, verticalPair: false,
                        maxX + DimOffset, (minY + maxY) / 2.0,
                        "LoftFpLength", hints.FlatLengthMeters, log, "length");

                widthOk = TryDimParallelPair(h, view, vert, verticalPair: true,
                    minX - DimOffset, (minY + maxY) / 2.0,
                    "LoftFpWidth", hints.HeightMeters, log, "width");
            }

            if (!lengthOk || !widthOk)
            {
                log($"  Flat pattern: length={(lengthOk ? "ok" : "MISSING")}, " +
                    $"width={(widthOk ? "ok" : "MISSING")} — SmartDimOverall fallback.");
                SmartDimOverall.Add(h, view, log);
            }
        }

        private static bool TryDimLongestEdge(
            SmartDimHelper h,
            IView view,
            Edge[] candidates,
            double textAlong,
            string key,
            double? expected,
            Action<string> log,
            string label)
        {
            if (candidates.Length == 0 || h.DimensionedFeatures.Contains(key))
                return h.DimensionedFeatures.Contains(key);
            // View-local only: FP width (== shell height) must not skip because View1 already has 140.
            if (expected.HasValue && h.HasDimensionWithValue(view, expected.Value))
            {
                h.DimensionedFeatures.Add(key);
                return true;
            }

            Edge edge = candidates.OrderByDescending(e => h.GetProjectedLength(e, view)).First();
            h.ClearSelection();
            if (!h.SelectEdge(edge, view, false))
                return false;

            double[] mid = h.GetEdgeMidpointOnSheet(edge, view);
            // textAlong used as the free axis placement — pick X or Y by orientation.
            bool horiz = h.IsHorizontalInView(edge, view);
            double tx = horiz ? mid[0] : textAlong;
            double ty = horiz ? textAlong : mid[1];
            if (h.CreateDimension(tx, ty) == null)
                return false;

            h.DimensionedFeatures.Add(key);
            log($"  Flat pattern {label} on {view.GetName2()}.");
            return true;
        }

        private static bool TryDimParallelPair(
            SmartDimHelper h,
            IView view,
            Edge[] candidates,
            bool verticalPair,
            double textX,
            double textY,
            string key,
            double? expected,
            Action<string> log,
            string label)
        {
            if (candidates.Length < 2 || h.DimensionedFeatures.Contains(key))
                return h.DimensionedFeatures.Contains(key);
            // View-local only — do not skip FP width when shell height already exists on View1.
            if (expected.HasValue && h.HasDimensionWithValue(view, expected.Value))
            {
                h.DimensionedFeatures.Add(key);
                return true;
            }

            Edge a, b;
            if (verticalPair)
            {
                a = candidates.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[0]).First();
                b = candidates.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[0]).Last();
            }
            else
            {
                a = candidates.OrderByDescending(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
                b = candidates.OrderBy(e => h.GetEdgeMidpointOnSheet(e, view)[1]).First();
            }

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double span = verticalPair
                ? Math.Abs(h.GetEdgeMidpointOnSheet(a, view)[0] - h.GetEdgeMidpointOnSheet(b, view)[0]) / scale
                : Math.Abs(h.GetEdgeMidpointOnSheet(a, view)[1] - h.GetEdgeMidpointOnSheet(b, view)[1]) / scale;

            if (span < MinSize)
                return false;

            // If expected known, reject wildly wrong pairs (e.g. length mistaken for width).
            if (expected.HasValue && Math.Abs(span - expected.Value) / expected.Value > 0.35)
            {
                log($"  Flat pattern {label}: pair span {span * 1000:F0} ≠ expected {expected.Value * 1000:F0} — skip.");
                return false;
            }

            h.ClearSelection();
            if (!h.SelectEdge(a, view, false) || !h.SelectEdge(b, view, true))
                return false;
            if (h.CreateDimension(textX, textY) == null)
                return false;

            h.DimensionedFeatures.Add(key);
            log($"  {label} {span * 1000:F1} mm on {view.GetName2()}.");
            return true;
        }

        private static void TryEnableBendNotes(IView view, Action<string> log)
        {
            try
            {
                view.ShowSheetMetalBendNotes = true;
                log($"  Bend notes enabled on {view.GetName2()}.");
            }
            catch (Exception ex)
            {
                log($"  Bend notes warning: {ex.Message}");
            }
        }

        private static double NormalizeAngle(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
