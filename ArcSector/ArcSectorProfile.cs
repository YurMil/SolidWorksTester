using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.ArcSector
{
    /// <summary>
    /// Resolved annular-sector geometry in a drawing view (shared by all ArcSector algorithms).
    /// </summary>
    internal readonly struct ArcSectorProfile
    {
        public ArcSectorProfile(
            Edge innerArc,
            Edge outerArc,
            double innerRadius,
            double outerRadius,
            double centerX,
            double centerY,
            Edge[] radialEdges)
        {
            InnerArc = innerArc;
            OuterArc = outerArc;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            CenterX = centerX;
            CenterY = centerY;
            RadialEdges = radialEdges;
        }

        public Edge InnerArc { get; }
        public Edge OuterArc { get; }
        public double InnerRadius { get; }
        public double OuterRadius { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public Edge[] RadialEdges { get; }

        /// <summary>Strip width R_out − R_in (model meters).</summary>
        public double StripWidth => OuterRadius - InnerRadius;
    }
}
