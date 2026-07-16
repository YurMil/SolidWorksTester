using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Finds the orthographic view where a flat plate lies face-on (largest projected area).
    /// </summary>
    internal static class FlatPlateViewAnalyzer
    {
        /// <summary>
        /// Fast primary-view pick using view outline only (no GetVisibleEntities edge walk).
        /// Critical for dense hole plates where edge enumeration costs minutes.
        /// </summary>
        public static IView? FindPrimaryFlatLyingViewByOutline(IDrawingDoc drawing)
        {
            IView? bestView = null;
            double bestArea = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (TryGetOutlineSize(view, out double width, out double height))
                {
                    double area = width * height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestView = view;
                    }
                }

                view = view.GetNextView() as IView;
            }

            return bestView;
        }

        /// <summary>
        /// Edge-based primary view (accurate but slow on dense views). Prefer
        /// <see cref="FindPrimaryFlatLyingViewByOutline"/> for baffle / perforated plates.
        /// </summary>
        public static IView? FindPrimaryFlatLyingView(SmartDimHelper h, IDrawingDoc drawing)
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

                // Prefer cached edges when PreCache already ran.
                Edge[] edges = h.GetViewEdgesCached(view);
                if (edges.Length == 0)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
                double width = maxX - minX;
                double height = maxY - minY;
                if (width <= 0 || height <= 0)
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                int linearCount = 0;
                foreach (Edge edge in edges)
                {
                    if (h.IsLinear(edge))
                        linearCount++;
                }

                double area = width * height;
                double score = area * (1.0 + linearCount * 0.02);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestView = view;
                }

                view = view.GetNextView() as IView;
            }

            return bestView;
        }

        public static bool TryGetOutlineSize(IView view, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (view.GetOutline() is not double[] outline || outline.Length < 4)
                return false;

            width = Math.Abs(outline[2] - outline[0]);
            height = Math.Abs(outline[3] - outline[1]);
            return width > 0 && height > 0;
        }
    }
}
