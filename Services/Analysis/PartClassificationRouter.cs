using System;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Merges geometry-based classification with optional custom-property overrides.
    /// Falls back to geometry when properties are missing or not trusted.
    /// </summary>
    internal static class PartClassificationRouter
    {
        public static PartAnalysisResult Apply(
            PartAnalysisResult geometry,
            PropertyPartClassification property,
            Action<string> log)
        {
            PropertyPartClassification trusted = PropertyPartClassifier.ValidateTrust(property, geometry);

            if (!trusted.IsTrusted)
            {
                if (property.HasPartKind)
                    log($"  Property classification not trusted: {trusted.TrustFailureReason} Using geometry.");

                return Clone(geometry, overrides =>
                {
                    overrides.KindSource = ClassificationSource.Geometry;
                    overrides.FlatPlateSubKindSource = ClassificationSource.Geometry;
                    overrides.ClassificationSource = ClassificationSource.Geometry;
                    overrides.PropertyClassificationTrusted = false;
                    overrides.PropertyTrustFailureReason = trusted.TrustFailureReason;
                    overrides.DeclaredPartKind = property.PartKind;
                    overrides.DeclaredFlatPlateSubKind = property.FlatPlateSubKind;
                    overrides.DrawingProfile = property.DrawingProfile;
                    overrides.EstProperties = property.Est;
                    overrides.PropertyOrigin = property.Origin;
                });
            }

            PartModelKind finalKind = trusted.PartKind!.Value;
            FlatPlateSubKind finalSubKind = geometry.FlatPlateSubKind;
            ClassificationSource kindSource = ClassificationSource.CustomProperty;
            ClassificationSource subKindSource = ClassificationSource.Geometry;

            if (finalKind != geometry.Kind)
            {
                string sourceLabel = trusted.Origin == PropertyClassificationOrigin.EstConfigurationName
                    ? $"EST Name ({trusted.Est.Name})"
                    : "custom property";
                log($"  Part kind from {sourceLabel}: {finalKind} (geometry was {geometry.Kind}).");
            }

            if (finalKind == PartModelKind.FlatPlate)
            {
                finalSubKind = ResolveFlatPlateSubKind(
                    geometry, property, trusted, finalSubKind, ref subKindSource, log);
            }
            else
            {
                finalSubKind = FlatPlateSubKind.Unknown;
            }

            ClassificationSource overallSource =
                kindSource == ClassificationSource.CustomProperty &&
                subKindSource == ClassificationSource.CustomProperty
                    ? ClassificationSource.CustomProperty
                    : subKindSource == ClassificationSource.Geometry
                        ? kindSource
                        : ClassificationSource.Hybrid;

            return Clone(geometry, overrides =>
            {
                overrides.Kind = finalKind;
                overrides.FlatPlateSubKind = finalSubKind;
                overrides.KindSource = kindSource;
                overrides.FlatPlateSubKindSource = subKindSource;
                overrides.ClassificationSource = overallSource;
                overrides.PropertyClassificationTrusted = true;
                overrides.PropertyTrustFailureReason = null;
                overrides.DeclaredPartKind = trusted.PartKind;
                overrides.DeclaredFlatPlateSubKind = trusted.FlatPlateSubKind;
                overrides.DrawingProfile = trusted.DrawingProfile;
                overrides.EstProperties = property.Est;
                overrides.PropertyOrigin = trusted.Origin;
            });
        }

        private static FlatPlateSubKind ResolveFlatPlateSubKind(
            PartAnalysisResult geometry,
            PropertyPartClassification property,
            PropertyPartClassification trusted,
            FlatPlateSubKind current,
            ref ClassificationSource subKindSource,
            Action<string> log)
        {
            FlatPlateSubKind geometrySub = geometry.GeometryFlatPlateSubKind != FlatPlateSubKind.Unknown
                ? geometry.GeometryFlatPlateSubKind
                : geometry.FlatPlateSubKind;

            if (trusted.HasFlatPlateSubKind && trusted.FlatPlateSubKind.HasValue)
            {
                FlatPlateSubKind declared = trusted.FlatPlateSubKind.Value;

                if (declared == FlatPlateSubKind.FlangeGasket)
                {
                    subKindSource = ClassificationSource.CustomProperty;
                    if (declared != geometrySub)
                    {
                        log($"  Flat-plate sub-kind from EST/property: {declared} " +
                            $"(geometry was {geometrySub}).");
                    }

                    return declared;
                }

                if (declared == FlatPlateSubKind.Generic &&
                    geometrySub != FlatPlateSubKind.Unknown &&
                    geometrySub != FlatPlateSubKind.Generic)
                {
                    subKindSource = ClassificationSource.Hybrid;
                    log($"  Flat-plate sub-kind from geometry: {geometrySub} " +
                        $"(EST/property was Generic).");
                    return geometrySub;
                }

                subKindSource = ClassificationSource.CustomProperty;
                if (declared != geometrySub)
                {
                    log($"  Flat-plate sub-kind from custom property: {declared} " +
                        $"(geometry was {geometrySub}).");
                }

                return declared;
            }

            if (TryInferSubKindFromAlias(property, out FlatPlateSubKind inferred))
            {
                subKindSource = ClassificationSource.Hybrid;
                log($"  Flat-plate sub-kind inferred from PartKind alias: {inferred}.");
                return inferred;
            }

            return current;
        }

        private static PartAnalysisResult Clone(
            PartAnalysisResult source,
            Action<PartAnalysisResult> mutate)
        {
            var copy = new PartAnalysisResult
            {
                Kind = source.Kind,
                BendFeatureCount = source.BendFeatureCount,
                HasSheetMetalFeature = source.HasSheetMetalFeature,
                IsHollow = source.IsHollow,
                HasHoles = source.HasHoles,
                HasChamfers = source.HasChamfers,
                CylindricalFaceCount = source.CylindricalFaceCount,
                IsRoundFlatProfile = source.IsRoundFlatProfile,
                IsRoundedEndFlatProfile = source.IsRoundedEndFlatProfile,
                CanImportSketchDimensions = source.CanImportSketchDimensions,
                ModelSketchDimensionCount = source.ModelSketchDimensionCount,
                FlatPlateSubKind = source.FlatPlateSubKind,
                GeometryFlatPlateSubKind = source.GeometryFlatPlateSubKind,
                ActiveConfiguration = source.ActiveConfiguration,
                KindSource = source.KindSource,
                FlatPlateSubKindSource = source.FlatPlateSubKindSource,
                ClassificationSource = source.ClassificationSource,
                PropertyClassificationTrusted = source.PropertyClassificationTrusted,
                PropertyTrustFailureReason = source.PropertyTrustFailureReason,
                DeclaredPartKind = source.DeclaredPartKind,
                DeclaredFlatPlateSubKind = source.DeclaredFlatPlateSubKind,
                DrawingProfile = source.DrawingProfile,
                EstProperties = source.EstProperties,
                PropertyOrigin = source.PropertyOrigin,
                IsImportedGeometry = source.IsImportedGeometry,
                ImportedShape = source.ImportedShape,
                ImportFeatureCount = source.ImportFeatureCount,
                ImportFeatureName = source.ImportFeatureName,
                BboxLongMeters = source.BboxLongMeters,
                BboxMidMeters = source.BboxMidMeters,
                BboxShortMeters = source.BboxShortMeters,
                SolidBodyCount = source.SolidBodyCount,
                IsTrueCylindricalTube = source.IsTrueCylindricalTube
            };

            mutate(copy);
            return copy;
        }

        private static bool TryInferSubKindFromAlias(
            PropertyPartClassification property,
            out FlatPlateSubKind subKind)
        {
            subKind = FlatPlateSubKind.Unknown;
            if (!property.HasPartKind || property.PartKind != PartModelKind.FlatPlate)
                return false;

            var snapshot = new CustomPropertySnapshot(
                property.FileProperties,
                property.ConfigurationProperties,
                property.ActiveConfiguration);

            string? raw = CustomPropertyReader.ResolveFirst(snapshot, PartClassificationPropertyNames.PartKindKeys);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string upper = raw.Trim().ToUpperInvariant();
                if (upper.Contains("FLANGE") || upper.Contains("GASKET"))
                {
                    subKind = FlatPlateSubKind.FlangeGasket;
                    return true;
                }
            }

            if (EstPartPropertiesParser.TryMapNameToFlatPlateSubKind(property.Est.Name, out FlatPlateSubKind fromEst))
            {
                subKind = fromEst;
                return true;
            }

            return false;
        }
    }
}
