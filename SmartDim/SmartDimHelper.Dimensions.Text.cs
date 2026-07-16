using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    /// <summary>Dimension text: parentheses, hole quantity (<c>Nx</c>), linear prefixes.</summary>
    public partial class SmartDimHelper
    {
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
                    var extras = new List<IAnnotation>(bareMatches.Count - 1);
                    for (int i = 1; i < bareMatches.Count; i++)
                        extras.Add(bareMatches[i].Annotation);
                    DeleteAnnotations(Model, extras);
                }
            }

            return modified;
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
    }
}
