using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    public sealed class PartAnalysisResult
    {
        public PartModelKind Kind { get; set; }
        public int BendFeatureCount { get; init; }
        public bool HasSheetMetalFeature { get; init; }
        public bool IsHollow { get; init; }
        public bool HasHoles { get; init; }
        public bool HasChamfers { get; init; }
        public int CylindricalFaceCount { get; init; }
        /// <summary>Flat disc-like sheet (two similar large dims, one thin).</summary>
        public bool IsRoundFlatProfile { get; init; }
        /// <summary>Flat plate with straight edge and rounded end (not a full disc).</summary>
        public bool IsRoundedEndFlatProfile { get; init; }
        /// <summary>Sketch dimensions on the 3D model can be imported (clean sheet-metal, no extra bosses).</summary>
        public bool CanImportSketchDimensions { get; init; }
        public int ModelSketchDimensionCount { get; init; }
        /// <summary>Nested flat-plate sub-router kind (disc, rounded end, flange/gasket, generic).</summary>
        public FlatPlateSubKind FlatPlateSubKind { get; set; }
        /// <summary>Sub-kind from geometry before property/EST merge (never overwritten).</summary>
        public FlatPlateSubKind GeometryFlatPlateSubKind { get; set; } = FlatPlateSubKind.Unknown;
        public string ActiveConfiguration { get; init; } = string.Empty;

        /// <summary>Final classification source for <see cref="Kind"/>.</summary>
        public ClassificationSource KindSource { get; set; } = ClassificationSource.Geometry;
        public ClassificationSource FlatPlateSubKindSource { get; set; } = ClassificationSource.Geometry;
        public ClassificationSource ClassificationSource { get; set; } = ClassificationSource.Geometry;
        public bool PropertyClassificationTrusted { get; set; }
        public string? PropertyTrustFailureReason { get; set; }
        public PartModelKind? DeclaredPartKind { get; set; }
        public FlatPlateSubKind? DeclaredFlatPlateSubKind { get; set; }
        public string? DrawingProfile { get; set; }
        public EstPartProperties EstProperties { get; set; } = new();
        public PropertyClassificationOrigin PropertyOrigin { get; set; }
        /// <summary>EST Name catalog slug when identified from configuration <c>Name</c>.</summary>
        public string? EstNameCatalogId { get; set; }
        public bool EstNameHasDedicatedPipeline { get; set; }

        /// <summary>True when the part is a dumb solid from STEP/IGES/3D Interconnect import.</summary>
        public bool IsImportedGeometry { get; init; }
        public ImportedGeometryShapeKind ImportedShape { get; init; }
        public int ImportFeatureCount { get; init; }
        public string ImportFeatureName { get; init; } = string.Empty;
        public double BboxLongMeters { get; init; }
        public double BboxMidMeters { get; init; }
        public double BboxShortMeters { get; init; }
        public int SolidBodyCount { get; init; }
        /// <summary>True only for genuine tube/rod imports — enables cylindrical dim modules.</summary>
        public bool IsTrueCylindricalTube { get; init; }
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

                var importInfo = ImportedGeometryDetector.Analyze(partDoc);
                int solidBodyCount = CountSolidBodies(partDoc);
                bool isCylindricalGeometry = IsCylindricalGeometry(
                    hasCylindricalFeature, cylindricalFaces, planarFaces);

                var bodyAnalysis = BuildBodyAnalysis(
                    partDoc, solidBodyCount, cylindricalFaces, planarFaces,
                    isHollow, hasHoles, isCylindricalGeometry, importInfo.ImportFeatureCount);

                string configName = partDoc.ConfigurationManager.ActiveConfiguration.Name;

                PartModelKind kind;
                ImportedGeometryShapeKind importedShape = ImportedGeometryShapeKind.Unknown;
                bool isTrueCylindricalTube = false;
                double bboxS = 0, bboxM = 0, bboxL = 0;

                if (hasSheetMetal)
                {
                    kind = bendCount > 0 ? PartModelKind.BentSheetMetal : PartModelKind.FlatPlate;
                }
                else if (importInfo.IsImported)
                {
                    kind = PartModelKind.ImportedGeometry;
                    var shapeInfo = ImportedGeometryShapeRecognizer.Recognize(partDoc, bodyAnalysis);
                    importedShape = shapeInfo.Shape;
                    isTrueCylindricalTube = shapeInfo.IsTrueCylindricalTube;
                    bboxS = shapeInfo.BboxShortMeters;
                    bboxM = shapeInfo.BboxMidMeters;
                    bboxL = shapeInfo.BboxLongMeters;
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
                bool isRoundedEndFlatProfile = kind == PartModelKind.FlatPlate &&
                    !isRoundFlatProfile &&
                    IsRoundedEndFlatPlate(partDoc);
                int modelSketchDimCount = kind == PartModelKind.FlatPlate
                    ? FlatPlateSketchAnalyzer.CountPartDisplayDimensions(partDoc)
                    : 0;
                bool canImportSketchDimensions = FlatPlateSketchAnalyzer.CanImportSketchDimensions(
                    partDoc, hasSheetMetal, bendCount, modelSketchDimCount);
                FlatPlateSubKind flatPlateSubKind = kind == PartModelKind.FlatPlate
                    ? FlatPlateClassifier.Classify(
                        partDoc, isRoundFlatProfile, isRoundedEndFlatProfile, hasHoles)
                    : FlatPlateSubKind.Unknown;

                LogPartType(kind, hasSheetMetal, bendCount, bendNames, isHollow, hasHoles, hasChamfers,
                    isRoundFlatProfile, isRoundedEndFlatProfile, flatPlateSubKind,
                    canImportSketchDimensions, modelSketchDimCount,
                    importInfo, importedShape, solidBodyCount, bboxL, bboxM, bboxS, log);

                var geometryResult = new PartAnalysisResult
                {
                    Kind = kind,
                    BendFeatureCount = bendCount,
                    HasSheetMetalFeature = hasSheetMetal,
                    IsHollow = isHollow,
                    HasHoles = hasHoles,
                    HasChamfers = hasChamfers,
                    CylindricalFaceCount = cylindricalFaces,
                    IsRoundFlatProfile = isRoundFlatProfile,
                    IsRoundedEndFlatProfile = isRoundedEndFlatProfile,
                    CanImportSketchDimensions = canImportSketchDimensions,
                    ModelSketchDimensionCount = modelSketchDimCount,
                    FlatPlateSubKind = flatPlateSubKind,
                    GeometryFlatPlateSubKind = flatPlateSubKind,
                    ActiveConfiguration = configName,
                    IsImportedGeometry = importInfo.IsImported,
                    ImportedShape = importedShape,
                    ImportFeatureCount = importInfo.ImportFeatureCount,
                    ImportFeatureName = importInfo.PrimaryImportFeatureName,
                    BboxLongMeters = bboxL,
                    BboxMidMeters = bboxM,
                    BboxShortMeters = bboxS,
                    SolidBodyCount = solidBodyCount,
                    IsTrueCylindricalTube = isTrueCylindricalTube
                };

                CustomPropertySnapshot propertySnapshot = CustomPropertyReader.Read(partDoc);
                PropertyPartClassification propertyClassification =
                    PropertyPartClassifier.Read(propertySnapshot);

                if (propertyClassification.HasPartKind || propertyClassification.HasFlatPlateSubKind)
                {
                    log("  Custom property classification:");
                    if (propertyClassification.EstNameMatch != null)
                    {
                        log($"    EST Name: {propertyClassification.EstNameMatch.RawName} " +
                            $"→ {propertyClassification.EstNameMatch.CatalogId} " +
                            $"(pipeline: {propertyClassification.EstNameMatch.PartKind}" +
                            (propertyClassification.EstNameMatch.FlatPlateSubKind != FlatPlateSubKind.Unknown
                                ? $", sub: {propertyClassification.EstNameMatch.FlatPlateSubKind}"
                                : string.Empty) +
                            $", dedicated: {propertyClassification.EstNameMatch.HasDedicatedPipeline})");
                    }
                    else if (propertyClassification.Origin == PropertyClassificationOrigin.EstConfigurationName)
                        log($"    Source: EST configuration Name = {propertyClassification.Est.Name}");
                    if (propertyClassification.HasPartKind)
                        log($"    PartKind: {propertyClassification.PartKind}");
                    if (propertyClassification.HasFlatPlateSubKind)
                        log($"    FlatPlateSubKind: {propertyClassification.FlatPlateSubKind}");
                    if (!string.IsNullOrWhiteSpace(propertyClassification.DrawingProfile))
                        log($"    DrawingProfile: {propertyClassification.DrawingProfile}");
                }

                if (propertyClassification.Est.HasAnyDimension || propertyClassification.Est.HasName)
                {
                    log("  EST dimension hints from configuration:");
                    if (!string.IsNullOrWhiteSpace(propertyClassification.Est.Description))
                        log($"    Description: {propertyClassification.Est.Description}");
                    if (propertyClassification.Est.Dim1Mm.HasValue)
                        log($"    DIM1: {propertyClassification.Est.Dim1Mm:F1} mm");
                    if (propertyClassification.Est.Dim2Mm.HasValue)
                        log($"    DIM2: {propertyClassification.Est.Dim2Mm:F1} mm");
                    if (propertyClassification.Est.Dim3Mm.HasValue)
                        log($"    DIM3: {propertyClassification.Est.Dim3Mm:F1} mm");
                    if (propertyClassification.Est.LengthMm.HasValue)
                        log($"    Length: {propertyClassification.Est.LengthMm:F1} mm");
                }

                PartAnalysisResult merged = PartClassificationRouter.Apply(geometryResult, propertyClassification, log);
                merged.EstProperties = propertyClassification.Est;
                merged.PropertyOrigin = propertyClassification.Origin;
                merged.EstNameCatalogId = propertyClassification.EstNameMatch?.CatalogId;
                merged.EstNameHasDedicatedPipeline =
                    merged.FlatPlateSubKind == FlatPlateSubKind.FlangeGasket ||
                    propertyClassification.EstNameMatch?.HasDedicatedPipeline == true;
                if (string.IsNullOrWhiteSpace(merged.DrawingProfile))
                    merged.DrawingProfile = propertyClassification.DrawingProfile;
                else if (merged.FlatPlateSubKind == FlatPlateSubKind.FlangeGasket &&
                         merged.DrawingProfile.Equals("plate", StringComparison.OrdinalIgnoreCase))
                    merged.DrawingProfile = propertyClassification.DrawingProfile ?? "flange";
                return merged;
            }
            finally
            {
                swApp.CloseDoc(partPath);
            }
        }

        private static int CountSolidBodies(IModelDoc2 partDoc)
        {
            if (partDoc is not PartDoc part)
                return 0;

            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            return bodies?.Length ?? 0;
        }

        private static SolidBodyAnalysisResult BuildBodyAnalysis(
            IModelDoc2 partDoc,
            int solidBodyCount,
            int cylindricalFaces,
            int planarFaces,
            bool isHollow,
            bool hasHoles,
            bool isCylindricalGeometry,
            int importFeatureCount)
        {
            int smallCyl = 0;
            int largeCyl = 0;

            if (partDoc is PartDoc part)
            {
                object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                if (bodies != null)
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
                            if (surf == null || !surf.IsCylinder())
                                continue;

                            try
                            {
                                double[] param = (double[])surf.CylinderParams;
                                double radius = param[6];
                                if (radius >= 0.015)
                                    largeCyl++;
                                else if (radius >= 0.001)
                                    smallCyl++;
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                    }
                }
            }

            return new SolidBodyAnalysisResult
            {
                SolidBodyCount = solidBodyCount,
                CylindricalFaces = cylindricalFaces,
                PlanarFaces = planarFaces,
                SmallCylinderFaces = smallCyl,
                LargeCylinderFaces = largeCyl,
                IsHollow = isHollow,
                HasHoles = hasHoles,
                IsCylindricalGeometry = isCylindricalGeometry,
                ImportFeatureCount = importFeatureCount
            };
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
                                // Hole-sized cylinders (1–50 mm) on parts with fillets/tubes.
                                if (param[6] >= 0.001 && param[6] < 0.05)
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

        /// <summary>
        /// Flat rectangular-ish plate with a large outer cylindrical face (rounded end profile).
        /// </summary>
        private static bool IsRoundedEndFlatPlate(IModelDoc2 partDoc)
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
            const double minFlatRatio = 3.0;
            const double minAspect = 1.08;

            if (dims[0] < minThickness || dims[1] / dims[0] < minFlatRatio)
                return false;

            double aspect = dims[2] / dims[1];
            if (aspect < minAspect)
                return false;

            return HasLargeOuterCylinderFace(partDoc, minRadiusMeters: 0.025);
        }

        private static bool HasLargeOuterCylinderFace(IModelDoc2 partDoc, double minRadiusMeters)
        {
            if (partDoc is not PartDoc part)
                return false;

            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return false;

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
                        if (param[6] >= minRadiusMeters)
                            return true;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            return false;
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
            bool isRoundedEndFlatProfile,
            FlatPlateSubKind flatPlateSubKind,
            bool canImportSketchDimensions,
            int modelSketchDimCount,
            ImportedGeometryDetector.ImportDetectionResult importInfo,
            ImportedGeometryShapeKind importedShape,
            int solidBodyCount,
            double bboxL,
            double bboxM,
            double bboxS,
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
                    if (isRoundedEndFlatProfile)
                        log("  Rounded-end flat plate profile detected.");
                    if (flatPlateSubKind == FlatPlateSubKind.FlangeGasket)
                        log("  Flange/gasket profile detected (circular hole pattern).");
                    if (canImportSketchDimensions)
                        log($"  Sketch dimensions on model ({modelSketchDimCount}) — eligible for drawing import.");
                    break;

                case PartModelKind.Cylindrical:
                    log($"Part type: Cylindrical ({(isHollow ? "hollow tube/pipe" : "solid cylinder")}).");
                    if (hasHoles)
                        log("  Detected hole features.");
                    if (hasChamfers)
                        log("  Detected chamfer/fillet features.");
                    break;

                case PartModelKind.ImportedGeometry:
                    log($"Part type: Imported geometry ({importInfo.ImportFeatureCount} import feature(s), " +
                        $"{solidBodyCount} solid body/bodies, primary: {importInfo.PrimaryImportFeatureName}).");
                    log($"  Recognized shape: {importedShape}.");
                    log($"  Bounding box (L×M×S): {bboxL * 1000:F1} × {bboxM * 1000:F1} × {bboxS * 1000:F1} mm.");
                    if (hasHoles)
                        log("  Detected cylindrical cutouts / holes in geometry.");
                    break;
            }
        }
    }
}
