using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    /// <summary>Delete display dimensions that match diameter / linear / angular filters.</summary>
    public partial class SmartDimHelper
    {
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

            return DeleteAnnotations(model, toDelete);
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

            return DeleteAnnotations(model, toDelete);
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

            return DeleteAnnotations(model, toDelete);
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

            return DeleteAnnotations(model, toDelete);
        }

        private static int DeleteAnnotations(IModelDoc2 model, List<IAnnotation> toDelete)
        {
            if (toDelete.Count == 0)
                return 0;

            model.ClearSelection2(true);
            foreach (IAnnotation annotation in toDelete)
                annotation.Select3(true, null);

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            return toDelete.Count;
        }
    }
}
