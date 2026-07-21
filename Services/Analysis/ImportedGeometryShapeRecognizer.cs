using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Classifies imported solid geometry from bounding box and face statistics.</summary>
    internal static class ImportedGeometryShapeRecognizer
    {
        public sealed class ShapeRecognitionResult
        {
            public ImportedGeometryShapeKind Shape { get; init; }
            public bool IsTrueCylindricalTube { get; init; }
            public double BboxShortMeters { get; init; }
            public double BboxMidMeters { get; init; }
            public double BboxLongMeters { get; init; }
        }

        public static ShapeRecognitionResult Recognize(
            IModelDoc2 partDoc,
            SolidBodyAnalysisResult bodyAnalysis)
        {
            var (s, m, l) = GetSortedBboxDimensions(partDoc);
            return RecognizeFromBbox(bodyAnalysis, s, m, l);
        }

        /// <summary>Pure recognition entry for unit tests (no COM).</summary>
        public static ShapeRecognitionResult RecognizeFromBbox(
            SolidBodyAnalysisResult bodyAnalysis,
            double s,
            double m,
            double l)
        {
            // Flat discs / flanges first — OD cylinders must not win as "tube".
            if (IsFlatPlateLike(s, m, l))
            {
                return new ShapeRecognitionResult
                {
                    Shape = ImportedGeometryShapeKind.FlatPlateLike,
                    BboxShortMeters = s,
                    BboxMidMeters = m,
                    BboxLongMeters = l
                };
            }

            bool trueTube = IsTrueCylindricalTube(bodyAnalysis, s, m, l);
            if (trueTube)
            {
                return new ShapeRecognitionResult
                {
                    Shape = ImportedGeometryShapeKind.CylindricalLike,
                    IsTrueCylindricalTube = true,
                    BboxShortMeters = s,
                    BboxMidMeters = m,
                    BboxLongMeters = l
                };
            }

            if (IsElongatedThinProfile(s, m, l))
            {
                return new ShapeRecognitionResult
                {
                    Shape = ImportedGeometryShapeKind.ElongatedThinProfile,
                    BboxShortMeters = s,
                    BboxMidMeters = m,
                    BboxLongMeters = l
                };
            }

            if (IsComplexBracket(bodyAnalysis, s, m, l))
            {
                return new ShapeRecognitionResult
                {
                    Shape = ImportedGeometryShapeKind.ComplexBracket,
                    BboxShortMeters = s,
                    BboxMidMeters = m,
                    BboxLongMeters = l
                };
            }

            if (l / Math.Max(m, 0.0001) < 2.5 && m / Math.Max(s, 0.0001) < 2.5)
            {
                return new ShapeRecognitionResult
                {
                    Shape = ImportedGeometryShapeKind.BlockyPrismatic,
                    BboxShortMeters = s,
                    BboxMidMeters = m,
                    BboxLongMeters = l
                };
            }

            return new ShapeRecognitionResult
            {
                Shape = ImportedGeometryShapeKind.Unknown,
                BboxShortMeters = s,
                BboxMidMeters = m,
                BboxLongMeters = l
            };
        }

        /// <summary>
        /// Strict tube/rod test — fillets, bolt holes, and flat discs must not pass.
        /// </summary>
        internal static bool IsTrueCylindricalTube(
            SolidBodyAnalysisResult body,
            double s,
            double m,
            double l)
        {
            if (!body.IsCylindricalGeometry)
                return false;

            // Round flat stock (flange / blank) — never a tube.
            if (IsFlatPlateLike(s, m, l))
                return false;

            if (body.SolidBodyCount >= 2 && l / Math.Max(m, 0.0001) < 3.5)
                return false;

            if (body.PlanarFaces > body.CylindricalFaces &&
                body.SmallCylinderFaces >= body.LargeCylinderFaces)
                return false;

            // Short stub with plate-like aspect (L≈M) but slightly below FlatPlateLike threshold.
            if (Math.Abs(l - m) / Math.Max(m, 1e-12) <= 0.25 &&
                m / Math.Max(s, 1e-12) >= 3.0)
                return false;

            if (l / Math.Max(m, 0.0001) < 2.0 && l / Math.Max(s, 0.0001) < 3.0)
                return false;

            return body.IsHollow || body.LargeCylinderFaces >= 2;
        }

        private static bool IsComplexBracket(
            SolidBodyAnalysisResult body,
            double s,
            double m,
            double l)
        {
            if (body.SolidBodyCount >= 2)
                return true;

            if (body.ImportFeatureCount >= 2)
                return true;

            if (body.PlanarFaces >= 8 && body.SmallCylinderFaces >= 4)
                return true;

            double aspect = l / Math.Max(m, 0.0001);
            return aspect >= 1.5 && aspect < 3.5 &&
                   body.PlanarFaces > body.CylindricalFaces &&
                   body.SmallCylinderFaces > 0;
        }

        private static (double s, double m, double l) GetSortedBboxDimensions(IModelDoc2 partDoc)
        {
            if (partDoc is not PartDoc part)
                return (0, 0, 0);

            double[] box = (double[])part.GetPartBox(true);
            double[] dims =
            {
                Math.Abs(box[3] - box[0]),
                Math.Abs(box[4] - box[1]),
                Math.Abs(box[5] - box[2])
            };
            Array.Sort(dims);
            return (dims[0], dims[1], dims[2]);
        }

        internal static bool IsFlatPlateLike(double s, double m, double l)
        {
            const double minGauge = 0.0005;
            const double minFlatRatio = 4.0;
            const double similarLargeTolerance = 0.20;

            if (s < minGauge)
                return false;

            if (m / s < minFlatRatio)
                return false;

            return Math.Abs(l - m) / m <= similarLargeTolerance;
        }

        private static bool IsElongatedThinProfile(double s, double m, double l)
        {
            const double minGauge = 0.0003;
            const double minLengthToMid = 3.0;
            const double minLengthToShort = 6.0;

            if (s < minGauge)
                return false;

            if (l / Math.Max(m, 0.0001) >= minLengthToMid)
                return true;

            return l / Math.Max(s, 0.0001) >= minLengthToShort;
        }
    }
}
