using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    /// <summary>Look up existing display dimensions by value / type; walk view annotations.</summary>
    public partial class SmartDimHelper
    {
        private static readonly Regex QuantityPrefixRegex =
            new(@"^\s*\d+\s*[x×]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private HashSet<long>? _drawingDimValueCache;
        private const int MaxAnnotationsPerView = 400;

        /// <summary>Drop cached dimension values (call after import / create / delete).</summary>
        public void InvalidateDimensionValueCache() => _drawingDimValueCache = null;

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
        /// Uses a one-shot value cache so repeated probes after model-import stay fast.
        /// </summary>
        public bool HasDimensionWithValueInDrawing(
            double valueMeters,
            int? dimType = null,
            double tol = SmartDimConstants.DimensionValueToleranceMeters)
        {
            // Typed lookups still need a walk; untyped value checks use the cache.
            if (dimType != null)
            {
                foreach (IView view in EnumerateDrawingViews(skipIsometric: true))
                {
                    if (HasDimensionWithValue(view, valueMeters, dimType, tol))
                        return true;
                }

                return false;
            }

            EnsureDrawingDimValueCache();
            long key = QuantizeDimMeters(valueMeters);
            // Also probe neighbors within ~tol (0.01 mm steps around target).
            long tolSteps = Math.Max(1, (long)Math.Ceiling(tol / 1e-5));
            for (long d = -tolSteps; d <= tolSteps; d++)
            {
                if (_drawingDimValueCache!.Contains(key + d))
                    return true;
            }

            return false;
        }

        private void EnsureDrawingDimValueCache()
        {
            if (_drawingDimValueCache != null)
                return;

            var cache = new HashSet<long>();
            foreach (IView view in EnumerateDrawingViews(skipIsometric: true))
            {
                foreach (DisplayDimensionEntry entry in EnumerateDisplayDimensions(view))
                {
                    if (entry.ModelDimension == null)
                        continue;

                    try
                    {
                        cache.Add(QuantizeDimMeters(entry.ModelDimension.SystemValue));
                    }
                    catch
                    {
                        // skip broken COM dim
                    }
                }
            }

            _drawingDimValueCache = cache;
        }

        /// <summary>Quantize meters to 0.01 mm units for set membership.</summary>
        private static long QuantizeDimMeters(double meters) =>
            (long)Math.Round(Math.Abs(meters) * 1e5);

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
            var visited = new HashSet<int>();
            Annotation? ann = DrawingAnnotationWalk.GetFirst(view);
            int count = 0;

            while (ann != null && count < MaxAnnotationsPerView)
            {
                count++;
                int id = RuntimeHelpers.GetHashCode(ann);
                if (!visited.Add(id))
                    yield break; // cyclic COM walk — abort

                DisplayDimension? displayDim = DrawingAnnotationWalk.AsDisplayDimension(ann);
                if (displayDim != null)
                {
                    Dimension? modelDim = DrawingAnnotationWalk.GetModelDimension(displayDim);
                    yield return new DisplayDimensionEntry(ann, displayDim, modelDim);
                }

                ann = DrawingAnnotationWalk.GetNext(ann);
            }
        }

        private readonly record struct DisplayDimensionEntry(
            IAnnotation Annotation,
            DisplayDimension DisplayDimension,
            Dimension? ModelDimension);
    }
}
