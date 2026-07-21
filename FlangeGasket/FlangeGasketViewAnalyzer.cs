using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.RoundFlatPlate;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.FlangeGasket
{
    /// <summary>Detects flange/gasket disc face views with bolt-circle hole patterns.</summary>
    internal static class FlangeGasketViewAnalyzer
    {
        private const double MinProfileAspectRatio = 1.15;
        private const double MaxProfileThicknessMeters = 0.30;

        public static bool IsFlangeFaceView(SmartDimHelper h, IView view)
        {
            if (!IsDominantDiscFaceView(h, view))
                return false;

            Edge[] edges = h.GetViewEdgesCached(view);
            FlangeDiscGeometry? geometry = FlangeGasketPatternGeometry.Analyze(h, view, edges);
            return geometry?.PrimaryBoltCircle != null;
        }

        public static bool DetectFromDrawing(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (IsFlangeFaceView(h, view))
                    return true;

                view = view.GetNextView() as IView;
            }

            return false;
        }

        public static IView? FindPrimaryDiscView(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? bestFlange = null;
            double bestFlangeScore = 0;
            IView? bestDisc = null;
            double bestDiscOd = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (!IsDominantDiscFaceView(h, view))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                Edge[] edges = h.GetViewEdgesCached(view);
                FlangeDiscGeometry? geometry = FlangeGasketPatternGeometry.Analyze(h, view, edges);
                if (geometry == null)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (geometry.PrimaryBoltCircle != null)
                {
                    double score = geometry.OuterDiameterMeters * geometry.PrimaryBoltCircle.Holes.Count;
                    if (score > bestFlangeScore)
                    {
                        bestFlangeScore = score;
                        bestFlange = view;
                    }
                }

                if (geometry.OuterDiameterMeters > bestDiscOd)
                {
                    bestDiscOd = geometry.OuterDiameterMeters;
                    bestDisc = view;
                }

                view = view.GetNextView() as IView;
            }

            return bestFlange ?? bestDisc;
        }

        public static IView? FindProfileSideView(SmartDimHelper h, IDrawingDoc drawing, IView? discFaceView)
        {
            IView? bestView = null;
            double bestScore = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (discFaceView != null && ReferenceEquals(view, discFaceView))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                // True disc faces only — do not skip side views that merely expose circular hole edges.
                if (IsDominantDiscFaceView(h, view))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (!TryGetProfileExtentsModelMeters(h, view, out double thinModel, out double wideModel))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (thinModel < 0.0005 || thinModel > MaxProfileThicknessMeters)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                double aspect = wideModel / Math.Max(thinModel, 1e-9);
                if (aspect < MinProfileAspectRatio)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                // Prefer elongated side views (typical flange profile).
                if (aspect > bestScore)
                {
                    bestScore = aspect;
                    bestView = view;
                }

                view = view.GetNextView() as IView;
            }

            return bestView;
        }

        public static bool IsDominantDiscFaceView(SmartDimHelper h, IView view) =>
            RoundFlatPlateViewAnalyzer.IsCircularFaceView(h, view);

        /// <summary>View extents in model meters (sheet size / view scale).</summary>
        private static bool TryGetProfileExtentsModelMeters(
            SmartDimHelper h,
            IView view,
            out double thinModel,
            out double wideModel)
        {
            thinModel = 0;
            wideModel = 0;
            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            var set = new HashSet<Edge>();
            foreach (Edge edge in h.GetViewEdgesCached(view))
                set.Add(edge);

            // Silhouette intentionally skipped (SW2025 HLV cost / instability).

            Edge[] linear = set.Where(h.IsLinear).ToArray();
            if (linear.Length >= 2)
            {
                var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(linear, view);
                double width = maxX - minX;
                double height = maxY - minY;
                if (width > 0 && height > 0)
                {
                    thinModel = Math.Min(width, height) / scale;
                    wideModel = Math.Max(width, height) / scale;
                    return true;
                }
            }

            if (set.Count > 0)
            {
                var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(set.ToArray(), view);
                double width = maxX - minX;
                double height = maxY - minY;
                if (width > 0 && height > 0)
                {
                    thinModel = Math.Min(width, height) / scale;
                    wideModel = Math.Max(width, height) / scale;
                    return true;
                }
            }

            if (view.GetOutline() is not double[] outline || outline.Length < 4)
                return false;

            double ow = Math.Abs(outline[2] - outline[0]);
            double oh = Math.Abs(outline[3] - outline[1]);
            if (ow <= 0 || oh <= 0)
                return false;

            thinModel = Math.Min(ow, oh) / scale;
            wideModel = Math.Max(ow, oh) / scale;
            return true;
        }
    }
}
