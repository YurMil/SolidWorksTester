using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Compares EST configuration dimension hints (DIM1–DIM3, Length) with dimensions on a drawing.
    /// </summary>
    public static class EstDrawingQualityValidator
    {
        private const double MinToleranceMm = 0.5;
        private const double RelativeTolerance = 0.015;

        public static EstDrawingQualityReport Validate(
            PartAnalysisResult analysis,
            IReadOnlyList<DrawingDimensionSample> drawingDimensions)
        {
            EstPartProperties est = analysis.EstProperties;
            if (!est.HasAnyDimension)
            {
                return new EstDrawingQualityReport { HasExpectedDimensions = false };
            }

            var report = new EstDrawingQualityReport { HasExpectedDimensions = true };
            string family = est.Name?.Trim().ToUpperInvariant() ?? string.Empty;
            bool flangeFamily = analysis.FlatPlateSubKind == FlatPlateSubKind.FlangeGasket ||
                analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.FlangeGasket ||
                family.Contains("FLANGE") || family.Contains("GASKET") ||
                EstPartPropertiesParser.DescriptionIndicatesFlangeOrGasket(est.Description);

            if (flangeFamily)
            {
                ValidateFlange(est, drawingDimensions, report);
                return report;
            }

            if (analysis.Kind == PartModelKind.LoftedBends ||
                family == "SHELL" ||
                family.Contains("SHELL"))
            {
                ValidateShell(est, drawingDimensions, report);
                return report;
            }

            if (analysis.Kind == PartModelKind.Cylindrical ||
                family.Contains("PIPE") || family.Contains("TUBE"))
            {
                ValidatePipe(est, drawingDimensions, report);
                return report;
            }

            if (analysis.FlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
                analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
                EstPartPropertiesParser.DescriptionIndicatesBafflePlate(est.Description) ||
                family.Contains("BAFFLE"))
            {
                // DIM1 on baffles is often a series code (e.g. 6), not thickness.
                ValidateBaffle(est, drawingDimensions, report);
                return report;
            }

            if (analysis.Kind == PartModelKind.FlatPlate &&
                (family.Contains("PLATE") || family.Contains("SHEET") ||
                 analysis.FlatPlateSubKind == FlatPlateSubKind.Generic ||
                 analysis.FlatPlateSubKind == FlatPlateSubKind.RoundedEnd))
            {
                ValidatePlate(est, drawingDimensions, report);
                return report;
            }

            ValidateGeneric(est, drawingDimensions, report);
            return report;
        }

        public static void ValidateAndLog(
            PartAnalysisResult analysis,
            IDrawingDoc drawing,
            Action<string> log)
        {
            var dims = DrawingDimensionCollector.Collect(drawing);
            ValidateAndLog(analysis, dims, log);
        }

        public static void ValidateAndLog(
            PartAnalysisResult analysis,
            IReadOnlyList<DrawingDimensionSample> drawingDimensions,
            Action<string> log)
        {
            EstDrawingQualityReport report = Validate(analysis, drawingDimensions);
            LogReport(report, log);
        }

        public static void LogReport(EstDrawingQualityReport report, Action<string> log)
        {
            if (!report.HasExpectedDimensions)
                return;

            if (report.IsPass)
            {
                log("  EST dimension quality: OK (all expected values found on drawing).");
                return;
            }

            log($"  EST dimension quality: {report.Flags.Count} issue(s).");
            foreach (string flag in report.Flags)
                log($"    [{flag}]");
            foreach (string note in report.Notes)
                log($"    {note}");
        }

        private static void ValidateShell(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var linears = ExtractLinearsMm(dims).ToList();
            // SHELL EST: DIM1=thickness, DIM2=height, DIM3=developed length; OD from Description.
            if (est.Dim1Mm is > 0 and <= 40)
                CheckValue(report, "thickness", est.Dim1Mm, linears);
            CheckValue(report, "height", est.Dim2Mm, linears);
            CheckValue(report, "flat_length", est.Dim3Mm, linears);
        }

        private static void ValidatePipe(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var diameters = ExtractDiametersMm(dims).ToList();
            var linears = ExtractLinearsMm(dims).ToList();

            CheckValue(report, "od", est.Dim1Mm, diameters);

            if (est.Dim3Mm.HasValue)
            {
                var wallCandidates = linears
                    .Where(v => v <= Math.Max(80, (est.Dim1Mm ?? 500) * 0.5))
                    .ToList();
                CheckValue(report, "wall_thickness", est.Dim3Mm, wallCandidates);
            }

            if (est.LengthMm.HasValue)
            {
                var lengthCandidates = linears
                    .Where(v => !est.Dim3Mm.HasValue || Math.Abs(v - est.Dim3Mm.Value) > MinToleranceMm)
                    .ToList();
                CheckValue(report, "length", est.LengthMm, lengthCandidates);
            }
        }

        private static void ValidatePlate(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var linears = ExtractLinearsMm(dims).ToList();
            var diameters = ExtractDiametersMm(dims).ToList();
            var all = linears.Concat(diameters).Distinct().ToList();

            CheckValue(report, "thickness", est.Dim1Mm, all);
            CheckValue(report, "width", est.Dim2Mm, all);
            CheckValue(report, "plate_length", est.Dim3Mm, all);
        }

        private static void ValidateBaffle(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var linears = ExtractLinearsMm(dims)
                .Where(v => v > 0 && v <= 80)
                .ToList();

            // Only Dim3 is treated as gauge; Dim1 is often a baffle series index.
            if (est.Dim3Mm is > 0 and <= 80)
                CheckValue(report, "thickness", est.Dim3Mm, linears);
        }

        private static void ValidateFlange(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var diameters = ExtractDiametersMm(dims).ToList();
            var linears = ExtractLinearsMm(dims).ToList();

            // EST blind flanges often store overall thickness in DIM1 (e.g. 108.6 mm gabarit).
            if (est.Dim1Mm.HasValue && !est.Dim3Mm.HasValue && est.Dim1Mm.Value <= 500)
            {
                CheckValue(report, "thickness", est.Dim1Mm, linears);
                return;
            }

            CheckValue(report, "od", est.Dim1Mm, diameters);

            if (est.Dim3Mm.HasValue)
                CheckValue(report, "thickness", est.Dim3Mm, linears);
            else if (est.Dim1Mm.HasValue)
                CheckValue(report, "thickness", est.Dim1Mm, linears);
        }

        private static void ValidateGeneric(
            EstPartProperties est,
            IReadOnlyList<DrawingDimensionSample> dims,
            EstDrawingQualityReport report)
        {
            var all = ExtractLinearsMm(dims).Concat(ExtractDiametersMm(dims)).Distinct().ToList();
            CheckValue(report, "dim1", est.Dim1Mm, all);
            CheckValue(report, "dim2", est.Dim2Mm, all);
            CheckValue(report, "dim3", est.Dim3Mm, all);
            CheckValue(report, "length", est.LengthMm, all);
        }

        private static void CheckValue(
            EstDrawingQualityReport report,
            string role,
            double? expectedMm,
            IReadOnlyList<double> candidatesMm)
        {
            if (!expectedMm.HasValue || expectedMm.Value <= 0)
                return;

            double expected = expectedMm.Value;
            double tolerance = ToleranceMm(expected);

            if (candidatesMm.Any(v => Math.Abs(v - expected) <= tolerance))
                return;

            string missingFlag = $"missing_{role}";
            string wrongFlag = $"wrong_{role}";

            if (candidatesMm.Count == 0)
            {
                report.Flags.Add(missingFlag);
                report.Notes.Add($"{role}: expected {expected:F1} mm — no matching dimension on drawing.");
                return;
            }

            double closest = candidatesMm.OrderBy(v => Math.Abs(v - expected)).First();
            report.Flags.Add(wrongFlag);
            report.Notes.Add(
                $"{role}: expected {expected:F1} mm, closest on drawing {closest:F1} mm (Δ {Math.Abs(closest - expected):F1} mm).");
        }

        private static IEnumerable<double> ExtractDiametersMm(IReadOnlyList<DrawingDimensionSample> dims)
        {
            foreach (DrawingDimensionSample dim in dims)
            {
                if (dim.Type == (int)swDimensionType_e.swDiameterDimension)
                    yield return dim.ValueMm;
                else if (dim.Type == (int)swDimensionType_e.swRadialDimension)
                    yield return dim.ValueMm * 2.0;
            }
        }

        private static IEnumerable<double> ExtractLinearsMm(IReadOnlyList<DrawingDimensionSample> dims)
        {
            foreach (DrawingDimensionSample dim in dims)
            {
                if (dim.Type == (int)swDimensionType_e.swLinearDimension)
                    yield return dim.ValueMm;
            }
        }

        private static double ToleranceMm(double expectedMm) =>
            Math.Max(MinToleranceMm, expectedMm * RelativeTolerance);
    }
}
