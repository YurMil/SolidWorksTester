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
    /// </summary>
    internal static class DrawingDimensionDeduper
    {
        private const double ValueTolerance = 0.00005;

        public static void RemoveDuplicateDimensions(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            params string[] skipViewNames)
        {
            var skipViews = new HashSet<string>(skipViewNames, StringComparer.OrdinalIgnoreCase);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toDelete = new List<IAnnotation>();
            int kept = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (!skipViews.Contains(viewName))
                    CollectDuplicatesInView(view, seenKeys, toDelete, ref kept);

                view = view.GetNextView() as IView;
            }

            if (toDelete.Count == 0)
            {
                log($"  Duplicate check: {kept} unique dimensions, no duplicates.");
                return;
            }

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);

            log($"  Duplicate check: removed {toDelete.Count} duplicate dimension(s), kept {kept} unique.");
        }

        private static void CollectDuplicatesInView(
            IView view,
            HashSet<string> seenKeys,
            List<IAnnotation> toDelete,
            ref int kept)
        {
            var seenInView = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    List<string> keys = BuildDimensionKeys(displayDim, ann);

                    bool duplicate = keys.Any(seenKeys.Contains) || keys.Any(seenInView.Contains);
                    if (duplicate)
                        toDelete.Add(ann);
                    else
                    {
                        foreach (string key in keys)
                        {
                            seenKeys.Add(key);
                            seenInView.Add(key);
                        }

                        kept++;
                    }
                }

                ann = ann.GetNext3();
            }
        }

        /// <summary>
        /// Removes "2x Ø..." style dimensions when the same diameter already exists
        /// without a quantity prefix (typical false positive on pipe end faces).
        /// </summary>
        public static void RemoveQuantityPrefixedDiameterDuplicates(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log,
            params string[] skipViewNames)
        {
            var skipViews = new HashSet<string>(skipViewNames, StringComparer.OrdinalIgnoreCase);
            var plainDiameterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toDelete = new List<IAnnotation>();

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (!skipViews.Contains(viewName))
                    CollectPlainDiameterKeys(view, plainDiameterKeys);

                view = view.GetNextView() as IView;
            }

            view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2();
                if (!skipViews.Contains(viewName))
                    CollectQuantityPrefixedDuplicates(view, plainDiameterKeys, toDelete);

                view = view.GetNextView() as IView;
            }

            if (toDelete.Count == 0)
                return;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);

            log($"  Removed {toDelete.Count} quantity-prefixed duplicate diameter(s) (e.g. 2x Ø).");
        }

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
