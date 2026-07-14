using System;
using System.Collections.Generic;
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
