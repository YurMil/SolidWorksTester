using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── Display dimensions ───────────────────────────────────────────

        /// <summary>
        /// Creates a display dimension at sheet coordinates; entities must be pre-selected.
        /// </summary>
        public DisplayDimension? CreateDimension(double textX, double textY) =>
            Model.AddDimension2(textX, textY, 0.0) as DisplayDimension;

        /// <summary>
        /// Angular dimension; two lines/edges must be pre-selected.
        /// Does not fall back to <see cref="IModelDoc2.AddDimension2"/> (that creates a linear chord).
        /// </summary>
        public DisplayDimension? CreateAngularDimension(double textX, double textY)
        {
            int err = 0;
            return Ext.AddSpecificDimension(
                textX, textY, 0.0,
                (int)swDimensionType_e.swAngularDimension,
                ref err) as DisplayDimension;
        }

        /// <summary>
        /// Diameter dimension on a circular edge (full circle or arc). Prefers diameter display over radius.
        /// </summary>
        public DisplayDimension? CreateDiameterDimension(
            Edge circularEdge,
            IView view,
            double textX,
            double textY)
        {
            ClearSelection();
            if (!SelectEdge(circularEdge, view, false))
                return null;

            DisplayDimension? dim = null;

            if (IsFullCircle(circularEdge))
            {
                dim = Model.AddDimension2(textX, textY, 0.0) as DisplayDimension;
                if (dim != null)
                    return dim;
            }

            int err = 0;
            dim = Ext.AddSpecificDimension(
                textX, textY, 0.0,
                (int)swDimensionType_e.swDiameterDimension,
                ref err) as DisplayDimension;

            if (dim != null)
                return dim;

            dim = Model.AddRadialDimension2(textX, textY, 0.0) as DisplayDimension;
            return dim;
        }

        /// <summary>Inserts a center mark on a circular edge (hole, arc, or full circle).</summary>
        public bool TryInsertCenterMark(IDrawingDoc drawing, IView view, Edge circularEdge)
        {
            try
            {
                ClearSelection();
                if (!SelectEdge(circularEdge, view, false))
                    return false;

                return drawing.InsertCenterMark2(1, true) != null;
            }
            catch
            {
                return false;
            }
            finally
            {
                ClearSelection();
            }
        }

        /// <summary>True when the view already contains a display dimension with the given value.</summary>
        public bool HasDimensionWithValue(
            IView view,
            double valueMeters,
            int? dimType = null,
            double tol = SmartDimConstants.DimensionValueToleranceMeters)
        {
            double target = Math.Abs(valueMeters);

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (entry.ModelDimension == null)
                    continue;

                if (Math.Abs(Math.Abs(entry.ModelDimension.SystemValue) - target) < tol &&
                    (dimType == null || entry.DisplayDimension.Type2 == dimType.Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when any orthographic view (except isometric) already has the given dimension value.
        /// </summary>
        public bool HasDimensionWithValueInDrawing(
            double valueMeters,
            int? dimType = null,
            double tol = SmartDimConstants.DimensionValueToleranceMeters)
        {
            foreach (IView view in EnumerateDrawingViews(skipIsometric: true))
            {
                if (HasDimensionWithValue(view, valueMeters, dimType, tol))
                    return true;
            }

            return false;
        }

        /// <summary>Applies parentheses to an existing diameter dimension in the view, if found.</summary>
        public bool TrySetParenthesesOnDiameter(
            IView view,
            double valueMeters,
            double tol = SmartDimConstants.DimensionValueToleranceMeters)
        {
            double target = Math.Abs(valueMeters);

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (entry.ModelDimension == null)
                    continue;

                if (Math.Abs(Math.Abs(entry.ModelDimension.SystemValue) - target) < tol)
                {
                    entry.DisplayDimension.ShowParenthesis = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when a diameter display dimension already has an "Nx " quantity prefix.</summary>
        public bool HasQuantityPrefix(DisplayDimension displayDim)
        {
            try
            {
                string prefix = displayDim.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty;
                return QuantityPrefixRegex.IsMatch(prefix.Trim());
            }
            catch
            {
                return false;
            }
        }

        public bool HasQuantityPrefixForDiameter(IView view, double diameterMeters, int quantity)
        {
            string expected = $"{quantity}x ";
            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (!MatchesDiameterValue(entry, diameterMeters))
                    continue;

                string prefix = entry.DisplayDimension.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty;
                if (prefix.TrimStart().StartsWith(expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Sets "Nx " prefix on imported hole diameter dims; removes duplicate bare dims.</summary>
        public int TryUpgradeHoleQuantityPrefix(IView view, double diameterMeters, int quantity)
        {
            if (quantity <= 1)
                return 0;

            string prefix = $"{quantity}x ";
            var bareMatches = new List<DisplayDimensionEntry>();
            var prefixedMatches = new List<DisplayDimensionEntry>();

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (!MatchesDiameterValue(entry, diameterMeters))
                    continue;

                if (HasQuantityPrefix(entry.DisplayDimension))
                    prefixedMatches.Add(entry);
                else
                    bareMatches.Add(entry);
            }

            if (bareMatches.Count == 0 && prefixedMatches.Count > 0)
                return 0;

            int modified = 0;

            if (bareMatches.Count > 0)
            {
                bareMatches[0].DisplayDimension.SetText(
                    (int)swDimensionTextParts_e.swDimensionTextPrefix, prefix);
                modified++;

                if (bareMatches.Count > 1)
                {
                    Model.ClearSelection2(true);
                    for (int i = 1; i < bareMatches.Count; i++)
                        bareMatches[i].Annotation.Select3(true, null);

                    Model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                    Model.ClearSelection2(true);
                }
            }

            return modified;
        }

        /// <summary>Deletes bare (no quantity prefix) diameter dimensions matching a hole size.</summary>
        public int DeleteBareDiameterDimensions(IModelDoc2 model, IView view, double diameterMeters)
        {
            var toDelete = new List<IAnnotation>();

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (!MatchesDiameterValue(entry, diameterMeters))
                    continue;

                if (!HasQuantityPrefix(entry.DisplayDimension))
                    toDelete.Add(entry.Annotation);
            }

            if (toDelete.Count == 0)
                return 0;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            return toDelete.Count;
        }

        /// <summary>Deletes all diameter/radial dimensions matching a size (bare or prefixed).</summary>
        public int DeleteAllDiameterDimensions(IModelDoc2 model, IView view, double diameterMeters)
        {
            var toDelete = new List<IAnnotation>();

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (!MatchesDiameterValue(entry, diameterMeters))
                    continue;

                toDelete.Add(entry.Annotation);
            }

            if (toDelete.Count == 0)
                return 0;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            return toDelete.Count;
        }

        /// <summary>Adds a text prefix to a linear dimension with the given value, if not already prefixed.</summary>
        public bool TrySetLinearDimensionPrefix(IView view, double valueMeters, string prefix)
        {
            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (entry.ModelDimension == null)
                    continue;

                if (Math.Abs(Math.Abs(entry.ModelDimension.SystemValue) - Math.Abs(valueMeters)) >=
                    SmartDimConstants.DimensionValueToleranceMeters)
                    continue;

                if (entry.DisplayDimension.Type2 != (int)swDimensionType_e.swLinearDimension)
                    continue;

                string existing = entry.DisplayDimension.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? string.Empty;
                if (existing.TrimStart().StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;

                entry.DisplayDimension.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, prefix);
                return true;
            }

            return false;
        }

        /// <summary>Deletes linear dimensions whose value is near the given size (e.g. half-thickness).</summary>
        public int DeleteLinearDimensionsNearValue(
            IModelDoc2 model,
            IView view,
            double valueMeters,
            double toleranceMeters)
        {
            var toDelete = new List<IAnnotation>();

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (entry.ModelDimension == null)
                    continue;

                if (entry.DisplayDimension.Type2 != (int)swDimensionType_e.swLinearDimension)
                    continue;

                double value = Math.Abs(entry.ModelDimension.SystemValue);
                if (Math.Abs(value - Math.Abs(valueMeters)) >= toleranceMeters)
                    continue;

                toDelete.Add(entry.Annotation);
            }

            if (toDelete.Count == 0)
                return 0;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            return toDelete.Count;
        }

        /// <summary>
        /// Deletes angular dimensions in a view that are not near the expected pitch (degrees).
        /// Useful to clear centerline-to-hole angles (e.g. 165°) before placing hole-to-hole pitch.
        /// </summary>
        public int DeleteAngularDimensionsNotNearDegrees(
            IModelDoc2 model,
            IView view,
            double expectedDegrees,
            double toleranceDegrees)
        {
            var toDelete = new List<IAnnotation>();

            foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
            {
                if (entry.ModelDimension == null)
                    continue;

                if (entry.DisplayDimension.Type2 != (int)swDimensionType_e.swAngularDimension)
                    continue;

                double deg = Math.Abs(entry.ModelDimension.SystemValue) * 180.0 / Math.PI;
                while (deg > 180.0)
                    deg = 360.0 - deg;

                if (Math.Abs(deg - expectedDegrees) <= toleranceDegrees)
                    continue;

                toDelete.Add(entry.Annotation);
            }

            if (toDelete.Count == 0)
                return 0;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            return toDelete.Count;
        }

        private static bool MatchesDiameterValue(DisplayDimensionEntry entry, double diameterMeters)
        {
            if (entry.ModelDimension == null)
                return false;

            double value = Math.Abs(entry.ModelDimension.SystemValue);
            if (Math.Abs(value - Math.Abs(diameterMeters)) >= SmartDimConstants.DimensionValueToleranceMeters)
                return false;

            int type = entry.DisplayDimension.Type2;
            return type == (int)swDimensionType_e.swDiameterDimension ||
                   type == (int)swDimensionType_e.swRadialDimension;
        }

        private static readonly Regex QuantityPrefixRegex =
            new(@"^\d+\s*[x×]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private IEnumerable<IView> EnumerateDrawingViews(bool skipIsometric)
        {
            IView? view = (Drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!skipIsometric ||
                    !view.GetName2().Equals(
                        SmartDimConstants.IsometricViewName,
                        StringComparison.OrdinalIgnoreCase))
                    yield return view;

                view = view.GetNextView() as IView;
            }
        }

        private IEnumerable<DisplayDimensionEntry> EnumerateDisplayDimensions(IView view)
        {
            Annotation? ann = view.GetFirstAnnotation3();
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    Dimension? modelDim = displayDim?.GetDimension2(0) as Dimension;

                    if (displayDim != null)
                        yield return new DisplayDimensionEntry(ann, displayDim, modelDim);
                }

                ann = ann.GetNext3();
            }
        }

        private readonly record struct DisplayDimensionEntry(
            IAnnotation Annotation,
            DisplayDimension DisplayDimension,
            Dimension? ModelDimension);
    }
}
