using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services;
using SolidWorksTester.Services.Analysis.FlangeGasket;

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
        /// <summary>Sheet-metal Lofted Bends feature present — dedicated unfold pipeline.</summary>
        public bool HasLoftedBendFeature { get; init; }
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

        /// <summary>
        /// Purchased fastener (<c>DocumentType=Fastener</c> / <c>IsFastener=1</c>) — skip drawing.
        /// </summary>
        public bool IsFastener { get; set; }

        /// <summary>Human-readable reason when <see cref="IsFastener"/> is true.</summary>
        public string? FastenerSkipReason { get; set; }

        /// <summary>
        /// Cylinder face samples collected by the analysis scan (radius + axis origin).
        /// Lets drawing-stage pattern math (baffle pitch/angle/seed) run without another face walk.
        /// May be a partial sample when the scan early-exited, but always covers ≥ the dominant
        /// hole group needed for dense-pattern classification.
        /// </summary>
        public IReadOnlyList<CylinderFaceSample> CylinderSamples { get; set; } =
            Array.Empty<CylinderFaceSample>();

        /// <summary>Shallow copy for classification merge without mutating the geometry snapshot.</summary>
        public PartAnalysisResult Clone() => new()
        {
            Kind = Kind,
            BendFeatureCount = BendFeatureCount,
            HasSheetMetalFeature = HasSheetMetalFeature,
            IsHollow = IsHollow,
            HasHoles = HasHoles,
            HasChamfers = HasChamfers,
            HasLoftedBendFeature = HasLoftedBendFeature,
            CylindricalFaceCount = CylindricalFaceCount,
            IsRoundFlatProfile = IsRoundFlatProfile,
            IsRoundedEndFlatProfile = IsRoundedEndFlatProfile,
            CanImportSketchDimensions = CanImportSketchDimensions,
            ModelSketchDimensionCount = ModelSketchDimensionCount,
            FlatPlateSubKind = FlatPlateSubKind,
            GeometryFlatPlateSubKind = GeometryFlatPlateSubKind,
            ActiveConfiguration = ActiveConfiguration,
            KindSource = KindSource,
            FlatPlateSubKindSource = FlatPlateSubKindSource,
            ClassificationSource = ClassificationSource,
            PropertyClassificationTrusted = PropertyClassificationTrusted,
            PropertyTrustFailureReason = PropertyTrustFailureReason,
            DeclaredPartKind = DeclaredPartKind,
            DeclaredFlatPlateSubKind = DeclaredFlatPlateSubKind,
            DrawingProfile = DrawingProfile,
            EstProperties = EstProperties,
            PropertyOrigin = PropertyOrigin,
            EstNameCatalogId = EstNameCatalogId,
            EstNameHasDedicatedPipeline = EstNameHasDedicatedPipeline,
            IsImportedGeometry = IsImportedGeometry,
            ImportedShape = ImportedShape,
            ImportFeatureCount = ImportFeatureCount,
            ImportFeatureName = ImportFeatureName,
            BboxLongMeters = BboxLongMeters,
            BboxMidMeters = BboxMidMeters,
            BboxShortMeters = BboxShortMeters,
            SolidBodyCount = SolidBodyCount,
            IsTrueCylindricalTube = IsTrueCylindricalTube,
            IsFastener = IsFastener,
            FastenerSkipReason = FastenerSkipReason,
            CylinderSamples = CylinderSamples
        };
    }

    public static class PartModelAnalyzer
    {
        public static PartAnalysisResult Analyze(ISldWorks swApp, string partPath, Action<string> log)
        {
            using var timer = new Services.Drawing.PipelineStopwatch(log, "part analysis");
            int errors = 0;
            int warnings = 0;

            timer.Step("OpenDoc6");
            IModelDoc2? partDoc = swApp.OpenDoc6(
                partPath,
                (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as IModelDoc2;
            timer.FinishStep();

            if (partDoc == null)
                throw new InvalidOperationException($"Failed to open part for analysis. Error code: {errors}.");

            try
            {
                PartGeometrySnapshot snap = timer.Measure("geometry scan", () =>
                    PartGeometryScanner.Scan(partDoc));

                bool hasHoles = snap.HasHoles;
                bool isHollow = snap.IsHollow;
                bool isCylindricalGeometry = IsCylindricalGeometry(
                    snap.HasCylindricalFeature, snap.CylindricalFaces, snap.PlanarFaces, snap.BboxSortedDims);

                // Hollow OD/ID only when large concentric-like cylinders exist (not slot+fillet radii).
                if (isHollow && snap.LargeCylinderFaces < 2)
                    isHollow = false;

                ImportedGeometryDetector.ImportDetectionResult importInfo = snap.ImportDetection;
                string configName = partDoc.ConfigurationManager.ActiveConfiguration.Name;

                PartModelKind kind;
                ImportedGeometryShapeKind importedShape = ImportedGeometryShapeKind.Unknown;
                bool isTrueCylindricalTube = false;
                double bboxS = snap.BboxSortedDims.Length >= 3 ? snap.BboxSortedDims[0] : 0;
                double bboxM = snap.BboxSortedDims.Length >= 3 ? snap.BboxSortedDims[1] : 0;
                double bboxL = snap.BboxSortedDims.Length >= 3 ? snap.BboxSortedDims[2] : 0;

                if (snap.HasSheetMetal)
                {
                    // Lofted-bend shells need unfold + OD/height — not generic bent flanges.
                    if (snap.HasLoftedBendFeature)
                        kind = PartModelKind.LoftedBends;
                    else
                        kind = snap.BendCount > 0 ? PartModelKind.BentSheetMetal : PartModelKind.FlatPlate;
                }
                else if (importInfo.IsImported)
                {
                    kind = PartModelKind.ImportedGeometry;
                    SolidBodyAnalysisResult bodyAnalysis = BuildBodyAnalysisFromSnapshot(
                        snap, isHollow, hasHoles, isCylindricalGeometry, importInfo.ImportFeatureCount);
                    var shapeInfo = ImportedGeometryShapeRecognizer.RecognizeFromBbox(
                        bodyAnalysis, bboxS, bboxM, bboxL);
                    importedShape = shapeInfo.Shape;
                    isTrueCylindricalTube = shapeInfo.IsTrueCylindricalTube;
                    bboxS = shapeInfo.BboxShortMeters;
                    bboxM = shapeInfo.BboxMidMeters;
                    bboxL = shapeInfo.BboxLongMeters;

                    // STEP blind flanges / discs: promote to FlatPlate so P-01 + flange dims run.
                    if (ShouldPromoteImportedToFlatPlate(snap, importedShape, partPath))
                    {
                        kind = PartModelKind.FlatPlate;
                        isTrueCylindricalTube = false;
                    }
                }
                else if (isCylindricalGeometry)
                {
                    kind = PartModelKind.Cylindrical;
                    isTrueCylindricalTube = isHollow || snap.LargeCylinderFaces >= 2;
                }
                else
                {
                    kind = PartModelKind.FlatPlate;
                }

                bool isRoundFlatProfile = kind == PartModelKind.FlatPlate &&
                    snap.IsRoundFlatDisc(minThickness: 0.0005, minFlatRatio: 4.0, roundTolerance: 0.10);

                bool isRoundedEndFlatProfile = kind == PartModelKind.FlatPlate &&
                    !isRoundFlatProfile &&
                    IsRoundedEndFlatPlate(snap);

                int modelSketchDimCount = 0;
                bool canImportSketchDimensions = false;
                FlatPlateSubKind flatPlateSubKind = FlatPlateSubKind.Unknown;

                timer.Measure("classify + sketch dims", () =>
                {
                    if (kind == PartModelKind.FlatPlate &&
                        FlatPlateSketchAnalyzer.IsEligibleForSketchImport(
                            snap.HasSheetMetal,
                            snap.BendCount,
                            snap.HasDisqualifyingSketchImportFeatures))
                    {
                        modelSketchDimCount = FlatPlateSketchAnalyzer.CountPartDisplayDimensions(partDoc);
                        canImportSketchDimensions = modelSketchDimCount >= 2;
                    }

                    flatPlateSubKind = kind == PartModelKind.FlatPlate
                        ? FlatPlateClassifier.Classify(snap, isRoundFlatProfile, isRoundedEndFlatProfile)
                        : FlatPlateSubKind.Unknown;

                    // Early-exit scan can set large-outer cylinder → rounded-end flag; baffle wins.
                    if (flatPlateSubKind == FlatPlateSubKind.BafflePlate)
                        isRoundedEndFlatProfile = false;
                });

                LogPartType(kind, snap.HasSheetMetal, snap.BendCount, snap.BendNames,
                    isHollow, hasHoles, snap.HasChamferFeature, snap.HasLoftedBendFeature,
                    isRoundFlatProfile, isRoundedEndFlatProfile, flatPlateSubKind,
                    canImportSketchDimensions, modelSketchDimCount,
                    importInfo, importedShape, snap.SolidBodyCount, bboxL, bboxM, bboxS, log);

                if (kind == PartModelKind.FlatPlate && importInfo.IsImported)
                {
                    log($"  Imported body promoted to flat plate (shape: {importedShape}, " +
                        $"L×M×S: {bboxL * 1000:F1} × {bboxM * 1000:F1} × {bboxS * 1000:F1} mm).");
                }

                var geometryResult = new PartAnalysisResult
                {
                    Kind = kind,
                    BendFeatureCount = snap.BendCount,
                    HasSheetMetalFeature = snap.HasSheetMetal,
                    IsHollow = isHollow,
                    HasHoles = hasHoles,
                    HasChamfers = snap.HasChamferFeature,
                    HasLoftedBendFeature = snap.HasLoftedBendFeature,
                    CylindricalFaceCount = snap.CylindricalFaces,
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
                    SolidBodyCount = snap.SolidBodyCount,
                    IsTrueCylindricalTube = isTrueCylindricalTube
                };

                CustomPropertySnapshot propertySnapshot = timer.Measure("custom properties", () =>
                    CustomPropertyReader.Read(partDoc));
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
                merged.CylinderSamples = snap.Cylinders;
                merged.EstProperties = propertyClassification.Est;
                merged.PropertyOrigin = propertyClassification.Origin;
                merged.EstNameCatalogId = propertyClassification.EstNameMatch?.CatalogId;
                merged.EstNameHasDedicatedPipeline =
                    merged.FlatPlateSubKind is FlatPlateSubKind.FlangeGasket or FlatPlateSubKind.BafflePlate ||
                    propertyClassification.EstNameMatch?.HasDedicatedPipeline == true;
                if (string.IsNullOrWhiteSpace(merged.DrawingProfile))
                    merged.DrawingProfile = propertyClassification.DrawingProfile;
                else if (merged.FlatPlateSubKind == FlatPlateSubKind.FlangeGasket &&
                         merged.DrawingProfile.Equals("plate", StringComparison.OrdinalIgnoreCase))
                    merged.DrawingProfile = propertyClassification.DrawingProfile ?? "flange";
                else if (merged.FlatPlateSubKind == FlatPlateSubKind.BafflePlate &&
                         merged.DrawingProfile.Equals("plate", StringComparison.OrdinalIgnoreCase))
                    merged.DrawingProfile = propertyClassification.DrawingProfile ?? "baffle_plate";

                if (FastenerPropertyDetector.TryDetect(propertySnapshot, out string fastenerReason))
                {
                    merged.IsFastener = true;
                    merged.FastenerSkipReason = fastenerReason;
                    log($"  Fastener detected ({fastenerReason}) — drawing will be skipped.");
                }

                return merged;
            }
            catch
            {
                // Keep the part open for drawing creation; only close if analysis itself failed.
                SolidWorksConnection.SafeCloseDocumentByPath(swApp, partPath, log);
                throw;
            }
        }

        private static bool ShouldPromoteImportedToFlatPlate(
            PartGeometrySnapshot snap,
            ImportedGeometryShapeKind importedShape,
            string partPath)
        {
            if (importedShape == ImportedGeometryShapeKind.FlatPlateLike)
                return true;

            if (FlangeGasketModelAnalyzer.IsFlangeOrGasket(snap))
                return true;

            // Filename fallback when STEP has no EST Name (e.g. blind-flange_DN900....step).
            return FileNameLooksLikeFlangeOrGasket(partPath) &&
                   snap.IsDiscLikeBbox(minThickness: 0.0005, minFlatRatio: 4.0, roundTolerance: 0.12);
        }

        private static bool FileNameLooksLikeFlangeOrGasket(string partPath)
        {
            string name = Path.GetFileNameWithoutExtension(partPath);
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Drop trailing .step from "*.step.SLDPRT" style names.
            if (name.EndsWith(".step", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".stp", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);

            string upper = name.ToUpperInvariant();
            return upper.Contains("FLANGE") || upper.Contains("GASKET");
        }

        private static SolidBodyAnalysisResult BuildBodyAnalysisFromSnapshot(
            PartGeometrySnapshot snap,
            bool isHollow,
            bool hasHoles,
            bool isCylindricalGeometry,
            int importFeatureCount) =>
            new()
            {
                SolidBodyCount = snap.SolidBodyCount,
                CylindricalFaces = snap.CylindricalFaces,
                PlanarFaces = snap.PlanarFaces,
                SmallCylinderFaces = snap.SmallCylinderFaces,
                LargeCylinderFaces = snap.LargeCylinderFaces,
                IsHollow = isHollow,
                HasHoles = hasHoles,
                IsCylindricalGeometry = isCylindricalGeometry,
                ImportFeatureCount = importFeatureCount
            };

        private static bool IsCylindricalGeometry(
            bool hasCylindricalFeature,
            int cylindricalFaces,
            int planarFaces,
            double[] bboxSorted)
        {
            // Thin flat stock: short gauge + large face aspect → plate/bracket, not tube.
            if (LooksLikeThinFlatPlate(bboxSorted, planarFaces, cylindricalFaces))
                return false;

            if (hasCylindricalFeature && cylindricalFaces >= 1)
                return true;

            if (cylindricalFaces >= 2 && cylindricalFaces >= planarFaces)
                return true;

            return cylindricalFaces >= 3;
        }

        /// <summary>
        /// U-lugs, brackets, slotted plates: thin bbox + planar-dominant (or few large OD faces).
        /// </summary>
        private static bool LooksLikeThinFlatPlate(PartGeometrySnapshot snap) =>
            LooksLikeThinFlatPlate(snap.BboxSortedDims, snap.PlanarFaces, snap.CylindricalFaces);

        private static bool LooksLikeThinFlatPlate(
            double[] bboxSorted,
            int planarFaces,
            int cylindricalFaces)
        {
            if (bboxSorted.Length < 3)
                return false;

            double s = bboxSorted[0];
            double m = bboxSorted[1];
            const double maxGaugeMeters = 0.050; // 50 mm
            const double minFlatRatio = 3.0;

            if (s < 0.0005 || s > maxGaugeMeters)
                return false;

            if (m / Math.Max(s, 1e-12) < minFlatRatio)
                return false;

            if (planarFaces > cylindricalFaces)
                return true;

            // Plate with many small hole/fillet cylinders but not a pipe OD/ID pair.
            return planarFaces >= 4 && cylindricalFaces > 0;
        }

        private static bool IsRoundedEndFlatPlate(PartGeometrySnapshot snap)
        {
            if (snap.BboxSortedDims.Length < 3)
                return false;

            double s = snap.BboxSortedDims[0];
            double m = snap.BboxSortedDims[1];
            double l = snap.BboxSortedDims[2];

            const double minThickness = 0.0005;
            const double minFlatRatio = 3.0;
            const double minAspect = 1.08;

            if (s < minThickness || m / Math.Max(s, 1e-12) < minFlatRatio)
                return false;

            if (l / Math.Max(m, 1e-12) < minAspect)
                return false;

            // Dense perforated plates are baffles, not rounded-end.
            if (snap.HasLinearOrFillPattern || snap.CylindricalFaces >= 80)
                return false;

            return snap.HasLargeOuterCylinderFace;
        }

        private static void LogPartType(
            PartModelKind kind,
            bool hasSheetMetal,
            int bendCount,
            IReadOnlyList<string> bendNames,
            bool isHollow,
            bool hasHoles,
            bool hasChamfers,
            bool hasLoftedBend,
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
                case PartModelKind.LoftedBends:
                    log($"Part type: Lofted Bends sheet-metal shell " +
                        $"({bendCount} bend feature(s): {string.Join(", ", bendNames)}).");
                    break;

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
                    if (flatPlateSubKind == FlatPlateSubKind.BafflePlate)
                        log("  Baffle / perforated plate detected (dense hole array).");
                    if (canImportSketchDimensions)
                        log($"  Sketch dimensions on model ({modelSketchDimCount}) — eligible for drawing import.");
                    break;

                case PartModelKind.Cylindrical:
                    log($"Part type: Cylindrical ({(isHollow ? "hollow tube/pipe" : "solid cylinder")}).");
                    if (hasHoles)
                        log("  Detected hole features.");
                    if (hasChamfers)
                        log("  Detected chamfer/fillet features.");
                    if (hasLoftedBend)
                        log("  Note: LoftedBend also present — prefer Lofted Bends pipeline if SM.");
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
