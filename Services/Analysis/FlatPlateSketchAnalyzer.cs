using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Detects flat sheet-metal parts whose sketch dimensions can be imported onto a drawing
    /// (no move-face / boss additions after the base profile).
    /// </summary>
    internal static class FlatPlateSketchAnalyzer
    {
        private const double MinDimensionMeters = 0.0001;

        private static readonly HashSet<string> DisqualifyingFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MoveFace", "MoveFace2", "MoveBody", "Indent", "BossExtrude", "ExtrudeBoss",
            "Boss", "SweepBoss", "LoftBoss", "RevolveBoss", "BoundaryBoss",
            "Thicken", "CombineBodies", "SplitBody", "Split", "MirrorSolid",
            "ReplaceFace", "DeleteFace", "ScaleFeature", "Freeform", "Dome",
            "RuledSurfaceFromEdge", "LocalChainPattern", "MoveCopyBody", "Cavity",
            "DerivedPartSolid", "InsertPart", "VarFillet", "Wrap", "Flex"
        };

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

        public static bool HasDisqualifyingFeatures(IModelDoc2 partDoc)
        {
            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                string typeName = feat.GetTypeName2();
                if (DisqualifyingFeatureTypes.Contains(typeName))
                    return true;

                feat = feat.GetNextFeature() as Feature;
            }

            return false;
        }

        public static bool CanImportSketchDimensions(
            IModelDoc2 partDoc,
            bool hasSheetMetal,
            int bendCount,
            int displayDimensionCount)
        {
            if (!hasSheetMetal || bendCount > 0)
                return false;

            if (HasDisqualifyingFeatures(partDoc))
                return false;

            // At least two driving dimensions visible on the 3D model (overall + feature).
            return displayDimensionCount >= 2;
        }

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
