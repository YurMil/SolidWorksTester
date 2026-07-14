using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    public sealed class PartAnalysisResult
    {
        public PartModelKind Kind { get; init; }
        public int BendFeatureCount { get; init; }
        public bool HasSheetMetalFeature { get; init; }
        public bool IsHollow { get; init; }
        public bool HasHoles { get; init; }
        public bool HasChamfers { get; init; }
        public int CylindricalFaceCount { get; init; }
        /// <summary>Flat disc-like sheet (two similar large dims, one thin).</summary>
        public bool IsRoundFlatProfile { get; init; }
        public string ActiveConfiguration { get; init; } = string.Empty;
    }

    public static class PartModelAnalyzer
    {
        private static readonly HashSet<string> BendFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "EdgeFlange", "Hem", "Jog", "SketchBend", "SM3dBend",
            "SMMiteredFlange", "MiterFlange", "LoftedBend", "OneBend", "Fold"
        };

        private static readonly HashSet<string> SheetMetalRootTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "SheetMetal", "BaseFlange", "FlatPattern"
        };

        private static readonly HashSet<string> CylindricalFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Revolve", "RevolveBoss", "RevolveCut", "RevolveThin",
            "Sweep", "SweepBoss", "SweepCut", "SweepThin",
            "ThinRevolve", "ThinSweep", "Loft", "LoftBoss", "LoftCut"
        };

        private static readonly HashSet<string> HoleFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "HoleWiz", "AdvHoleWiz", "HoleSeries", "SimpleHole", "HoleSeriesWizard"
        };

        private static readonly HashSet<string> ChamferFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Chamfer", "Fillet"
        };

        public static PartAnalysisResult Analyze(ISldWorks swApp, string partPath, Action<string> log)
        {
            int errors = 0;
            int warnings = 0;

            IModelDoc2? partDoc = swApp.OpenDoc6(
                partPath,
                (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as IModelDoc2;

            if (partDoc == null)
                throw new InvalidOperationException($"Failed to open part for analysis. Error code: {errors}.");

            try
            {
                bool hasSheetMetal = false;
                int bendCount = 0;
                var bendNames = new List<string>();
                bool hasCylindricalFeature = false;
                bool hasHoles = false;
                bool hasChamfers = false;

                Feature? feat = partDoc.FirstFeature() as Feature;
                while (feat != null)
                {
                    string typeName = feat.GetTypeName2();
                    if (SheetMetalRootTypes.Contains(typeName))
                        hasSheetMetal = true;

                    if (BendFeatureTypes.Contains(typeName))
                    {
                        bendCount++;
                        bendNames.Add(feat.Name);
                    }

                    if (CylindricalFeatureTypes.Contains(typeName))
                        hasCylindricalFeature = true;

                    if (HoleFeatureTypes.Contains(typeName))
                        hasHoles = true;

                    if (ChamferFeatureTypes.Contains(typeName))
                        hasChamfers = true;

                    feat = feat.GetNextFeature() as Feature;
                }

                int cylindricalFaces = 0;
                int planarFaces = 0;
                bool isHollow = false;
                AnalyzeSolidBodies(partDoc, ref cylindricalFaces, ref planarFaces, ref isHollow, ref hasHoles);

                string configName = partDoc.ConfigurationManager.ActiveConfiguration.Name;
                bool isCylindricalGeometry = IsCylindricalGeometry(
                    hasCylindricalFeature, cylindricalFaces, planarFaces);

                PartModelKind kind;
                if (hasSheetMetal)
                {
                    kind = bendCount > 0 ? PartModelKind.BentSheetMetal : PartModelKind.FlatPlate;
                }
                else if (isCylindricalGeometry)
                {
                    kind = PartModelKind.Cylindrical;
                }
                else
                {
                    kind = PartModelKind.FlatPlate;
                }

                bool isRoundFlatProfile = kind == PartModelKind.FlatPlate &&
                    IsRoundFlatDisc(partDoc);

                LogPartType(kind, hasSheetMetal, bendCount, bendNames, isHollow, hasHoles, hasChamfers,
                    isRoundFlatProfile, log);

                return new PartAnalysisResult
                {
                    Kind = kind,
                    BendFeatureCount = bendCount,
                    HasSheetMetalFeature = hasSheetMetal,
                    IsHollow = isHollow,
                    HasHoles = hasHoles,
                    HasChamfers = hasChamfers,
                    CylindricalFaceCount = cylindricalFaces,
                    IsRoundFlatProfile = isRoundFlatProfile,
                    ActiveConfiguration = configName
                };
            }
            finally
            {
                swApp.CloseDoc(partPath);
            }
        }

        private static bool IsCylindricalGeometry(
            bool hasCylindricalFeature,
            int cylindricalFaces,
            int planarFaces)
        {
            if (hasCylindricalFeature && cylindricalFaces >= 1)
                return true;

            if (cylindricalFaces >= 2 && cylindricalFaces >= planarFaces)
                return true;

            return cylindricalFaces >= 3;
        }

        private static void AnalyzeSolidBodies(
            IModelDoc2 partDoc,
            ref int cylindricalFaces,
            ref int planarFaces,
            ref bool isHollow,
            ref bool hasHoles)
        {
            PartDoc part = (PartDoc)partDoc;
            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null || bodies.Length == 0)
                return;

            var cylinderRadii = new List<double>();

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
                    if (surf == null)
                        continue;

                    if (surf.IsCylinder())
                    {
                        cylindricalFaces++;
                        try
                        {
                            double[] param = (double[])surf.CylinderParams;
                            cylinderRadii.Add(param[6]);
                        }
                        catch
                        {
                            // ignore invalid cylinder params
                        }
                    }
                    else if (surf.IsPlane())
                    {
                        planarFaces++;
                    }
                }
            }

            if (cylinderRadii.Count >= 2)
            {
                cylinderRadii.Sort();
                double minR = cylinderRadii[0];
                double maxR = cylinderRadii[^1];
                isHollow = maxR - minR > 0.0005;
            }

            if (!hasHoles)
            {
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
                        if (surf != null && surf.IsCylinder())
                        {
                            try
                            {
                                double[] param = (double[])surf.CylinderParams;
                                // Small-radius cylinders are cut holes on non-tubular parts only.
                                // Hollow pipe/tube bores must not be treated as hole features.
                                if (param[6] < 0.05 && cylinderRadii.Count < 2)
                                    hasHoles = true;
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Detects round flat discs from bounding box: two similar large dimensions and one thin gauge.
        /// </summary>
        private static bool IsRoundFlatDisc(IModelDoc2 partDoc)
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

            const double minThickness = 0.0005;  // 0.5 mm
            const double minFlatRatio = 5.0;     // diameter / thickness
            const double roundTolerance = 0.06;  // 6 % difference between large dims

            if (dims[0] < minThickness)
                return false;

            if (dims[1] / dims[0] < minFlatRatio)
                return false;

            double roundDelta = Math.Abs(dims[2] - dims[1]) / dims[1];
            return roundDelta <= roundTolerance;
        }

        private static void LogPartType(
            PartModelKind kind,
            bool hasSheetMetal,
            int bendCount,
            List<string> bendNames,
            bool isHollow,
            bool hasHoles,
            bool hasChamfers,
            bool isRoundFlatProfile,
            Action<string> log)
        {
            switch (kind)
            {
                case PartModelKind.BentSheetMetal:
                    log($"Part type: Bent sheet metal ({bendCount} bend feature(s): {string.Join(", ", bendNames)}).");
                    break;

                case PartModelKind.FlatPlate:
                    log(hasSheetMetal
                        ? "Part type: Flat plate (sheet metal, no bends)."
                        : "Part type: Flat plate (no bend features detected).");
                    if (isRoundFlatProfile)
                        log("  Round / disc-like flat profile detected.");
                    break;

                case PartModelKind.Cylindrical:
                    log($"Part type: Cylindrical ({(isHollow ? "hollow tube/pipe" : "solid cylinder")}).");
                    if (hasHoles)
                        log("  Detected hole features.");
                    if (hasChamfers)
                        log("  Detected chamfer/fillet features.");
                    break;
            }
        }
    }
}
