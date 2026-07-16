using System;

namespace SolidWorksTester.ArcSector
{
    /// <summary>Shared constants / sheet placement helpers for ArcSector algorithms.</summary>
    internal static class ArcSectorDimHelpers
    {
        public const double DimOffset = 0.014;
        public const double MinHoleDiameterMeters = 0.001;
        public const double MaxHoleDiameterMeters = 0.120;

        /// <summary>Place annotation text outside the sector, away from the arc center.</summary>
        public static void OffsetAwayFromCenter(
            double midX,
            double midY,
            double centerX,
            double centerY,
            out double textX,
            out double textY)
        {
            double vx = midX - centerX;
            double vy = midY - centerY;
            double len = Math.Sqrt(vx * vx + vy * vy);
            if (len < 1e-9)
            {
                vx = 1;
                vy = 0;
                len = 1;
            }

            textX = midX + vx / len * DimOffset;
            textY = midY + vy / len * DimOffset;
        }

        public static bool SelectAt(SmartDimHelper h, double x, double y, bool append = false)
        {
            try
            {
                return h.Ext.SelectByID2(
                    string.Empty,
                    SmartDim.SmartDimConstants.EdgeSelectType,
                    x, y, 0.0,
                    append, 0, null, 0);
            }
            catch
            {
                return false;
            }
        }
    }
}
