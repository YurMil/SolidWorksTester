using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Detects flat sheet-metal parts whose sketch dimensions can be imported onto a drawing
    /// (no move-face / boss additions after the base profile).
    /// Disqualifying feature flags come from <see cref="PartGeometrySnapshot"/> — no extra tree walk.
    /// </summary>
    internal static class FlatPlateSketchAnalyzer
    {
        private const double MinDimensionMeters = 0.0001;

        public static int CountPartDisplayDimensions(IModelDoc2 partDoc)
        {
            int count = 0;
            Annotation? ann = partDoc.GetFirstAnnotation2() as Annotation;
            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension? displayDim = ann.GetSpecificAnnotation() as DisplayDimension;
                    if (displayDim != null && TryGetDimensionValue(displayDim, out double value) &&
                        value >= MinDimensionMeters)
                    {
                        count++;
                    }
                }

                ann = ann.GetNext2() as Annotation;
            }

            return count;
        }

        /// <summary>
        /// True when sheet-metal / bend / disqualifier gates allow sketch import
        /// (before counting display dimensions).
        /// </summary>
        public static bool IsEligibleForSketchImport(
            bool hasSheetMetal,
            int bendCount,
            bool hasDisqualifyingFeatures) =>
            hasSheetMetal && bendCount == 0 && !hasDisqualifyingFeatures;

        public static bool CanImportSketchDimensions(
            bool hasSheetMetal,
            int bendCount,
            bool hasDisqualifyingFeatures,
            int displayDimensionCount) =>
            IsEligibleForSketchImport(hasSheetMetal, bendCount, hasDisqualifyingFeatures) &&
            displayDimensionCount >= 2;

        private static bool TryGetDimensionValue(DisplayDimension displayDim, out double value)
        {
            value = 0;
            try
            {
                Dimension? dim = displayDim.GetDimension2(0) as Dimension;
                if (dim == null)
                    return false;

                value = dim.SystemValue;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
