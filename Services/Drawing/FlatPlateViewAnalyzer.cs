using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Finds the orthographic view where a flat plate lies face-on (largest projected area).
    /// </summary>
    internal static class FlatPlateViewAnalyzer
    {
        public static IView? FindPrimaryFlatLyingView(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? bestView = null;
            double bestScore = 0;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, System.StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                Edge[] edges = h.GetViewEdges(view);
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

                int linearCount = edges.Count(h.IsLinear);
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
    }
}
