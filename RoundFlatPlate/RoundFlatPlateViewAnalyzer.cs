using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.RoundFlatPlate
{
    /// <summary>Detects round flat plate geometry from drawing views.</summary>
    internal static class RoundFlatPlateViewAnalyzer
    {
        private const double MinRadiusMeters = 0.005;

        public static bool IsCircularFaceView(SmartDimHelper h, IView view)
        {
            Edge[] edges = h.GetViewEdges(view);
            var profileCircles = edges
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinRadiusMeters)
                .ToArray();

            if (profileCircles.Length == 0)
                return false;

            var linear = edges.Where(h.IsLinear).ToArray();
            if (linear.Length == 0)
                return true;

            Edge outer = profileCircles.OrderByDescending(h.GetCircleRadius).First();
            double outerRadius = h.GetCircleRadius(outer);
            double longestLinear = linear.Max(e => h.GetProjectedLength(e, view));

            return outerRadius * 2.0 > longestLinear * 2.0;
        }

        public static Edge? GetOuterProfileCircle(SmartDimHelper h, IView view)
        {
            if (!IsCircularFaceView(h, view))
                return null;

            return h.GetViewEdges(view)
                .Where(e => h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > MinRadiusMeters)
                .OrderByDescending(h.GetCircleRadius)
                .FirstOrDefault();
        }

        public static bool DetectFromDrawing(SmartDimHelper h, IDrawingDoc drawing)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (IsCircularFaceView(h, view))
                    return true;

                view = view.GetNextView() as IView;
            }

            return false;
        }
    }
}
