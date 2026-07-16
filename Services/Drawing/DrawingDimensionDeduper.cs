using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Removes duplicate display dimensions across drawing views.
    /// Keeps the first occurrence (Drawing View1 first, then View2, View3, ...).
    /// Returns a snapshot of kept dimensions (for EST validate without a second COM walk).
    /// </summary>
    internal static class DrawingDimensionDeduper
    {
        private const double ValueTolerance = 0.00005;

        private const double SubMmNoiseMeters = 0.001; // 1 mm

        /// <summary>
        /// Dedupes all model views except those in <paramref name="skipViewNames"/>.
        /// Returns samples for every kept dimension (value already read during key build).
        /// </summary>
        public static IReadOnlyList<DrawingDimensionSample> RemoveDuplicateDimensions(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            params string[] skipViewNames) =>
            RemoveDuplicateDimensions(model, drawing, log, sheetMetalThicknessMeters: null, skipViewNames);

        public static IReadOnlyList<DrawingDimensionSample> RemoveDuplicateDimensions(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            double? sheetMetalThicknessMeters,
            params string[] skipViewNames)
        {
            var skipViews = new HashSet<string>(skipViewNames, StringComparer.OrdinalIgnoreCase);
            return RemoveDuplicateDimensionsCore(
                model,
                drawing,
                log,
                viewName => !skipViews.Contains(viewName),
                sheetMetalThicknessMeters);
        }

        /// <summary>
        /// Dedupes only the named views (e.g. baffle primary after single-view import).
        /// Empty / untouched views are never opened for annotation walks.
        /// </summary>
        public static IReadOnlyList<DrawingDimensionSample> RemoveDuplicateDimensionsOnlyIn(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            params string[] onlyViewNames) =>
            RemoveDuplicateDimensionsOnlyIn(model, drawing, log, sheetMetalThicknessMeters: null, onlyViewNames);

        public static IReadOnlyList<DrawingDimensionSample> RemoveDuplicateDimensionsOnlyIn(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            double? sheetMetalThicknessMeters,
            params string[] onlyViewNames)
        {
            var onlyViews = new HashSet<string>(onlyViewNames, StringComparer.OrdinalIgnoreCase);
            return RemoveDuplicateDimensionsCore(
                model,
                drawing,
                log,
                viewName => onlyViews.Contains(viewName),
                sheetMetalThicknessMeters);
        }

        private static IReadOnlyList<DrawingDimensionSample> RemoveDuplicateDimensionsCore(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            Func<string, bool> includeView,
            double? sheetMetalThicknessMeters)
        {
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toDelete = new List<IAnnotation>();
            var keptSamples = new List<DrawingDimensionSample>();
            int noiseCount = 0;
            int duplicateCount = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (includeView(viewName))
                {
                    CollectDuplicatesInView(
                        view,
                        viewName,
                        seenKeys,
                        toDelete,
                        keptSamples,
                        sheetMetalThicknessMeters,
                        ref noiseCount,
                        ref duplicateCount);
                }

                view = view.GetNextView() as IView;
            }

            if (toDelete.Count > 0)
            {
                model.ClearSelection2(true);
                foreach (IAnnotation annotation in toDelete)
                    annotation.Select3(true, null);

                model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                model.ClearSelection2(true);
            }

            if (noiseCount > 0)
                log($"  Removed {noiseCount} noise dimension(s) (Move Face / sub-mm).");

            if (duplicateCount > 0)
                log($"  Duplicate check: removed {duplicateCount} duplicate dimension(s), kept {keptSamples.Count} unique.");
            else
                log($"  Duplicate check: {keptSamples.Count} unique dimensions, no duplicates.");

            return keptSamples;
        }

        private static void CollectDuplicatesInView(
            IView view,
            string viewName,
            HashSet<string> seenKeys,
            List<IAnnotation> toDelete,
            List<DrawingDimensionSample> keptSamples,
            double? sheetMetalThicknessMeters,
            ref int noiseCount,
            ref int duplicateCount)
        {
            var seenInView = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;

                    if (IsNoiseDimension(displayDim, sheetMetalThicknessMeters))
                    {
                        toDelete.Add(ann);
                        noiseCount++;
                    }
                    else
                    {
                        List<string> keys = BuildDimensionKeys(displayDim, ann);

                        bool duplicate = keys.Any(seenKeys.Contains) || keys.Any(seenInView.Contains);
                        if (duplicate)
                        {
                            toDelete.Add(ann);
                            duplicateCount++;
                        }
                        else
                        {
                            foreach (string key in keys)
                            {
                                seenKeys.Add(key);
                                seenInView.Add(key);
                            }

                            if (TryCreateSample(displayDim, viewName, out DrawingDimensionSample sample))
                                keptSamples.Add(sample);
                        }
                    }
                }

                ann = ann.GetNext3();
            }
        }

        /// <summary>
        /// Drops Move Face tech offsets and sub-mm linears that are not sheet-metal thickness.
        /// </summary>
        private static bool IsNoiseDimension(
            DisplayDimension? displayDim,
            double? sheetMetalThicknessMeters)
        {
            if (displayDim == null)
                return false;

            try
            {
                Dimension? dim = displayDim.GetDimension2(0) as Dimension;
                if (dim == null)
                    return false;

                string fullName = dim.FullName ?? string.Empty;
                if (IsMoveFaceFeatureName(fullName))
                    return true;

                if (displayDim.Type2 != (int)swDimensionType_e.swLinearDimension)
                    return false;

                double value = Math.Abs(dim.SystemValue);
                if (value >= SubMmNoiseMeters)
                    return false;

                // Keep genuine SM thickness even if < 1 mm.
                if (sheetMetalThicknessMeters.HasValue)
                {
                    double tol = Math.Max(0.00005, sheetMetalThicknessMeters.Value * 0.02);
                    if (Math.Abs(value - sheetMetalThicknessMeters.Value) <= tol)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMoveFaceFeatureName(string fullName)
        {
            // e.g. "D1@Move Face Hole M18@PartConfig"
            int firstAt = fullName.IndexOf('@');
            if (firstAt < 0 || firstAt + 1 >= fullName.Length)
                return false;

            string after = fullName[(firstAt + 1)..];
            int secondAt = after.IndexOf('@');
            string feature = secondAt >= 0 ? after[..secondAt] : after;
            feature = feature.Trim();

            return feature.StartsWith("Move Face", StringComparison.OrdinalIgnoreCase) ||
                   feature.StartsWith("MoveFace", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateSample(
            DisplayDimension? displayDim,
            string viewName,
            out DrawingDimensionSample sample)
        {
            sample = null!;
            if (displayDim == null)
                return false;

            try
            {
                Dimension? modelDim = displayDim.GetDimension2(0) as Dimension;
                if (modelDim == null)
                    return false;

                double valueMeters = Math.Abs(modelDim.SystemValue);
                sample = new DrawingDimensionSample
                {
                    Type = displayDim.Type2,
                    ValueMm = Math.Round(valueMeters * 1000.0, 3),
                    Prefix = displayDim.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty,
                    ViewName = viewName
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves conflict between bare Ød and "Nx Ød" callouts.
        /// <paramref name="preferQuantityPrefix"/> true (flanges): keep Nx, delete bare.
        /// false (pipes): keep bare, delete Nx false-positives.
        /// </summary>
        public static void RemoveQuantityPrefixedDiameterDuplicates(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            bool preferQuantityPrefix,
            params string[] skipViewNames)
        {
            var skipViews = new HashSet<string>(skipViewNames, StringComparer.OrdinalIgnoreCase);
            var plainDiameterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var quantityDiameterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toDelete = new List<IAnnotation>();

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (!skipViews.Contains(viewName))
                {
                    CollectPlainDiameterKeys(view, plainDiameterKeys);
                    CollectQuantityDiameterKeys(view, quantityDiameterKeys);
                }

                view = view.GetNextView() as IView;
            }

            view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (!skipViews.Contains(viewName))
                {
                    if (preferQuantityPrefix)
                        CollectBareDuplicatesWhenQuantityExists(view, quantityDiameterKeys, toDelete);
                    else
                        CollectQuantityPrefixedDuplicates(view, plainDiameterKeys, toDelete);
                }

                view = view.GetNextView() as IView;
            }

            if (toDelete.Count == 0)
            {
                log("  Quantity/bare Ø conflict: none.");
                return;
            }

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);

            log(preferQuantityPrefix
                ? $"  Removed {toDelete.Count} bare diameter(s) kept quantity callout(s)."
                : $"  Removed {toDelete.Count} quantity-prefixed duplicate diameter(s) (e.g. 2x Ø).");
        }

        /// <summary>Pipe-safe default: drop false "Nx Ø" when a bare Ø already exists.</summary>
        public static void RemoveQuantityPrefixedDiameterDuplicates(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            params string[] skipViewNames) =>
            RemoveQuantityPrefixedDiameterDuplicates(
                model, drawing, log, preferQuantityPrefix: false, skipViewNames);

        private static void CollectPlainDiameterKeys(IView view, HashSet<string> keys)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null &&
                        !HasQuantityPrefix(displayDim) &&
                        TryGetDiameterKey(displayDim, out string key))
                        keys.Add(key);
                }

                ann = ann.GetNext3();
            }
        }

        private static void CollectQuantityDiameterKeys(IView view, HashSet<string> keys)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null &&
                        HasQuantityPrefix(displayDim) &&
                        TryGetDiameterKey(displayDim, out string key))
                        keys.Add(key);
                }

                ann = ann.GetNext3();
            }
        }

        private static void CollectBareDuplicatesWhenQuantityExists(
            IView view,
            HashSet<string> quantityDiameterKeys,
            List<IAnnotation> toDelete)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null &&
                        !HasQuantityPrefix(displayDim) &&
                        TryGetDiameterKey(displayDim, out string key) &&
                        quantityDiameterKeys.Contains(key))
                        toDelete.Add(ann);
                }

                ann = ann.GetNext3();
            }
        }

        private static void CollectQuantityPrefixedDuplicates(
            IView view,
            HashSet<string> plainDiameterKeys,
            List<IAnnotation> toDelete)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null &&
                        HasQuantityPrefix(displayDim) &&
                        TryGetDiameterKey(displayDim, out string key) &&
                        plainDiameterKeys.Contains(key))
                        toDelete.Add(ann);
                }

                ann = ann.GetNext3();
            }
        }

        private static bool HasQuantityPrefix(DisplayDimension displayDim)
        {
            try
            {
                string prefix = displayDim.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty;
                return Regex.IsMatch(prefix.Trim(), @"^\d+\s*[x×]\s*", RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDiameterKey(DisplayDimension displayDim, out string key)
        {
            key = string.Empty;
            try
            {
                Dimension? dim = displayDim.GetDimension2(0) as Dimension;
                if (dim == null)
                    return false;

                double value = Math.Abs(dim.SystemValue);
                if (value < ValueTolerance)
                    return false;

                string text = displayDim.GetText((int)swDimensionTextParts_e.swDimensionTextAll) ?? string.Empty;
                string normalized = NormalizeDisplayText(text);
                if (normalized.Contains('D') || normalized.Contains("RAD", StringComparison.OrdinalIgnoreCase))
                {
                    key = $"DIA:{FormatValue(value)}";
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Builds semantic keys for duplicate detection. Value + normalized text are preferred
        /// over FullName so repeated Ø60.3 imports from different model features dedupe correctly.
        /// </summary>
        private static List<string> BuildDimensionKeys(DisplayDimension? displayDim, IAnnotation annotation)
        {
            var keys = new List<string>();

            if (displayDim != null)
            {
                try
                {
                    Dimension? dim = displayDim.GetDimension2(0) as Dimension;
                    if (dim != null)
                    {
                        double value = Math.Abs(dim.SystemValue);
                        if (value >= ValueTolerance)
                        {
                            int dimType = displayDim.Type2;
                            keys.Add($"V:{dimType}:{FormatValue(value)}");

                            if (TryGetDiameterKey(displayDim, out string diaKey))
                                keys.Add(diaKey);
                        }
                    }
                }
                catch
                {
                    // fall through to text key
                }

                try
                {
                    string text = displayDim.GetText((int)swDimensionTextParts_e.swDimensionTextAll);
                    string normalized = NormalizeDisplayText(text);
                    if (!string.IsNullOrWhiteSpace(normalized))
                        keys.Add("TX:" + normalized);
                }
                catch
                {
                    // ignore
                }
            }

            if (keys.Count == 0)
                keys.Add("ID:" + annotation.GetName());

            return keys;
        }

        private static string FormatValue(double value) =>
            value.ToString("F6", CultureInfo.InvariantCulture);

        private static string NormalizeDisplayText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();
            text = Regex.Replace(text, @"^\d+\s*[x×]\s*", string.Empty, RegexOptions.IgnoreCase);
            text = text.Replace("Ø", "D", StringComparison.OrdinalIgnoreCase);
            text = text.Replace("φ", "D", StringComparison.OrdinalIgnoreCase);
            text = Regex.Replace(text, @"\s+", string.Empty);
            return text.ToUpperInvariant();
        }
    }
}
