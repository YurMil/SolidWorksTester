using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis.FlangeGasket
{
    /// <summary>
    /// Detects disc-like flanges/gaskets with circular hole patterns from the 3D model.
    /// </summary>
    internal static class FlangeGasketModelAnalyzer
    {
        private const double MinHoleRadiusMeters = 0.001;
        private const double MaxHoleRadiusMeters = 0.25;
        private const double MinBoltCircleHoleCount = 3;
        private const double BoltCircleRadiusTolerance = 0.003;

        private static readonly HashSet<string> CircularPatternFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CirPattern", "CircularPattern", "LPattern", "Pattern", "DerivedPattern",
            "SketchPattern", "TablePattern"
        };

        public static bool IsFlangeOrGasket(IModelDoc2 partDoc, bool hasHoles)
        {
            if (!hasHoles || !IsDiscLikeBbox(partDoc))
                return false;

            if (HasCircularPatternFeature(partDoc))
                return true;

            return CountBoltCircleHoles(partDoc) >= MinBoltCircleHoleCount;
        }

        private static bool IsDiscLikeBbox(IModelDoc2 partDoc)
        {
            if (partDoc is not PartDoc part)
                return false;

            double[] box = (double[])part.GetPartBox(true);
            double[] dims =
            {
                Math.Abs(box[3] - box[0]),
                Math.Abs(box[4] - box[1]),
                Math.Abs(box[5] - box[2])
            };
            Array.Sort(dims);

            const double minThickness = 0.0005;
            const double minFlatRatio = 5.0;
            const double roundTolerance = 0.08;

            if (dims[0] < minThickness || dims[1] / dims[0] < minFlatRatio)
                return false;

            return Math.Abs(dims[2] - dims[1]) / dims[1] <= roundTolerance;
        }

        private static bool HasCircularPatternFeature(IModelDoc2 partDoc)
        {
            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                if (CircularPatternFeatureTypes.Contains(feat.GetTypeName2()))
                    return true;

                feat = feat.GetNextFeature() as Feature;
            }

            return false;
        }

        private static int CountBoltCircleHoles(IModelDoc2 partDoc)
        {
            if (partDoc is not PartDoc part)
                return 0;

            double[] box = (double[])part.GetPartBox(true);
            double cx = (box[0] + box[3]) / 2.0;
            double cy = (box[1] + box[4]) / 2.0;
            double cz = (box[2] + box[5]) / 2.0;
            double outerRadius = Math.Max(
                Math.Max(box[3] - box[0], box[4] - box[1]),
                box[5] - box[2]) / 2.0;

            var polarRadii = new List<double>();

            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return 0;

            foreach (object bodyObj in bodies)
            {
                if (bodyObj is not Body2 body)
                    continue;

                object[]? faces = body.GetFaces() as object[];
                if (faces == null)
                    continue;

                foreach (object faceObj in faces)
                {
                    if (faceObj is not Face2 face)
                        continue;

                    Surface? surf = face.GetSurface() as Surface;
                    if (surf == null || !surf.IsCylinder())
                        continue;

                    try
                    {
                        double[] param = (double[])surf.CylinderParams;
                        double radius = param[6];
                        if (radius < MinHoleRadiusMeters || radius > MaxHoleRadiusMeters)
                            continue;

                        double dx = param[0] - cx;
                        double dy = param[1] - cy;
                        double dz = param[2] - cz;
                        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                        if (dist < outerRadius * 0.08 || dist > outerRadius * 0.98)
                            continue;

                        polarRadii.Add(Math.Round(dist, 3));
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            if (polarRadii.Count < MinBoltCircleHoleCount)
                return 0;

            return polarRadii
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .First()
                .Count();
        }
    }
}
