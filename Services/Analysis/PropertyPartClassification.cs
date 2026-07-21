using System;
using System.Collections.Generic;

namespace SolidWorksTester.Services.Analysis
{
    public enum PropertyClassificationOrigin
    {
        None = 0,
        CadAsExplicit = 1,
        EstConfigurationName = 2
    }

    public sealed class PropertyPartClassification
    {
        public bool HasPartKind { get; set; }
        public PartModelKind? PartKind { get; set; }
        public bool HasFlatPlateSubKind { get; set; }
        public FlatPlateSubKind? FlatPlateSubKind { get; set; }
        public string? DrawingProfile { get; set; }
        public bool IsTrusted { get; set; }
        public string? TrustFailureReason { get; set; }
        public PropertyClassificationOrigin Origin { get; set; }
        public EstPartProperties Est { get; set; } = new();
        public EstNameIdentification? EstNameMatch { get; set; }
        public IReadOnlyDictionary<string, string> FileProperties { get; set; }
            = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> ConfigurationProperties { get; set; }
            = new Dictionary<string, string>();
        public string ActiveConfiguration { get; set; } = string.Empty;
    }

    internal static class PropertyPartClassifier
    {
        public static PropertyPartClassification Read(CustomPropertySnapshot snapshot)
        {
            EstPartProperties est = EstPartPropertiesParser.Parse(snapshot);
            EstNameRegistry.TryIdentify(est.Name, out EstNameIdentification? estNameMatch);

            string? partKindRaw = CustomPropertyReader.ResolveFirst(snapshot, PartClassificationPropertyNames.PartKindKeys);
            string? subKindRaw = CustomPropertyReader.ResolveFirst(snapshot, PartClassificationPropertyNames.FlatPlateSubKindKeys);
            string? profileRaw = CustomPropertyReader.ResolveFirst(snapshot, PartClassificationPropertyNames.DrawingProfileKeys);

            bool hasPartKind = TryParsePartKind(partKindRaw, out PartModelKind partKind);
            PropertyClassificationOrigin origin = PropertyClassificationOrigin.None;
            bool hasSubKind = TryParseFlatPlateSubKind(subKindRaw, out FlatPlateSubKind subKind);

            if (!hasPartKind && estNameMatch != null)
            {
                hasPartKind = true;
                partKind = estNameMatch.PartKind;
                origin = PropertyClassificationOrigin.EstConfigurationName;

                if (!hasSubKind && estNameMatch.FlatPlateSubKind != FlatPlateSubKind.Unknown)
                {
                    hasSubKind = true;
                    subKind = estNameMatch.FlatPlateSubKind;
                }
            }
            else if (!hasPartKind && est.HasName &&
                     EstPartPropertiesParser.TryMapNameToPartKind(est.Name, out PartModelKind estKind))
            {
                hasPartKind = true;
                partKind = estKind;
                origin = PropertyClassificationOrigin.EstConfigurationName;
            }

            if (hasPartKind && origin == PropertyClassificationOrigin.None && !string.IsNullOrWhiteSpace(partKindRaw))
                origin = PropertyClassificationOrigin.CadAsExplicit;

            if (hasPartKind && origin == PropertyClassificationOrigin.EstConfigurationName && !hasSubKind &&
                EstPartPropertiesParser.TryMapNameToFlatPlateSubKind(est.Name, out FlatPlateSubKind estSub))
            {
                hasSubKind = true;
                subKind = estSub;
            }

            string? drawingProfile = profileRaw;
            if (string.IsNullOrWhiteSpace(drawingProfile) && estNameMatch != null)
                drawingProfile = estNameMatch.CatalogId;

            // Baffle before flange: Description "BAFFLE PLATE…" must not become FlangeGasket.
            if (EstPartPropertiesParser.DescriptionIndicatesBafflePlate(est.Description))
            {
                hasPartKind = true;
                partKind = PartModelKind.FlatPlate;
                hasSubKind = true;
                subKind = FlatPlateSubKind.BafflePlate;
                if (origin == PropertyClassificationOrigin.None)
                {
                    // Description-only hints are not EST Name catalog matches.
                    origin = string.IsNullOrWhiteSpace(est.Name)
                        ? PropertyClassificationOrigin.CadAsExplicit
                        : PropertyClassificationOrigin.EstConfigurationName;
                }
                if (string.IsNullOrWhiteSpace(drawingProfile) ||
                    drawingProfile.Equals("plate", StringComparison.OrdinalIgnoreCase))
                    drawingProfile = "baffle_plate";
            }
            else if (EstPartPropertiesParser.DescriptionIndicatesFlangeOrGasket(est.Description))
            {
                hasPartKind = true;
                partKind = PartModelKind.FlatPlate;
                hasSubKind = true;
                subKind = FlatPlateSubKind.FlangeGasket;
                if (origin == PropertyClassificationOrigin.None)
                {
                    origin = string.IsNullOrWhiteSpace(est.Name)
                        ? PropertyClassificationOrigin.CadAsExplicit
                        : PropertyClassificationOrigin.EstConfigurationName;
                }
                if (string.IsNullOrWhiteSpace(drawingProfile) ||
                    drawingProfile.Equals("plate", StringComparison.OrdinalIgnoreCase))
                    drawingProfile = "flange";
            }

            return new PropertyPartClassification
            {
                HasPartKind = hasPartKind,
                PartKind = hasPartKind ? partKind : null,
                HasFlatPlateSubKind = hasSubKind,
                FlatPlateSubKind = hasSubKind ? subKind : null,
                DrawingProfile = drawingProfile,
                Origin = origin,
                Est = est,
                EstNameMatch = estNameMatch,
                FileProperties = snapshot.FileProperties,
                ConfigurationProperties = snapshot.ConfigurationProperties,
                ActiveConfiguration = snapshot.ActiveConfiguration
            };
        }

        public static PropertyPartClassification ValidateTrust(
            PropertyPartClassification property,
            PartAnalysisResult geometry)
        {
            if (!property.HasPartKind || property.PartKind == null)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "Part kind not declared (no CADAS_* or EST Name).";
                });
            }

            PartModelKind declared = property.PartKind.Value;

            if (declared == PartModelKind.BentSheetMetal && !geometry.HasSheetMetalFeature &&
                property.Origin != PropertyClassificationOrigin.EstConfigurationName)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "Property BentSheetMetal but model has no sheet-metal feature.";
                });
            }

            if (declared == PartModelKind.FlatPlate && geometry.Kind == PartModelKind.Cylindrical &&
                !geometry.HasSheetMetalFeature && !geometry.IsImportedGeometry &&
                !IsEstPlateName(property.Est.Name))
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "Property FlatPlate conflicts with cylindrical geometry.";
                });
            }

            if (declared == PartModelKind.Cylindrical &&
                geometry.HasLoftedBendFeature &&
                geometry.HasSheetMetalFeature)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason =
                        "Property Cylindrical/SHELL but model has Lofted Bends — use unfold pipeline.";
                });
            }

            if (declared == PartModelKind.Cylindrical &&
                geometry.Kind == PartModelKind.FlatPlate &&
                geometry.HasSheetMetalFeature &&
                IsEstPlateName(property.Est.Name))
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "EST Name PLATE conflicts with cylindrical geometry.";
                });
            }

            if (declared == PartModelKind.Cylindrical &&
                geometry.Kind == PartModelKind.FlatPlate &&
                geometry.HasSheetMetalFeature &&
                property.Origin != PropertyClassificationOrigin.EstConfigurationName)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "Property Cylindrical conflicts with flat sheet-metal geometry.";
                });
            }

            if (property.HasFlatPlateSubKind && declared != PartModelKind.FlatPlate)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "FlatPlateSubKind property set but PartKind is not FlatPlate.";
                });
            }

            if (property.HasFlatPlateSubKind && property.FlatPlateSubKind == FlatPlateSubKind.Unknown)
            {
                return Clone(property, p =>
                {
                    p.IsTrusted = false;
                    p.TrustFailureReason = "FlatPlateSubKind property is invalid.";
                });
            }

            return Clone(property, p =>
            {
                p.IsTrusted = true;
                p.TrustFailureReason = null;
            });
        }

        private static bool IsEstPlateName(string? name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.Trim().ToUpperInvariant().Contains("PLATE");

        private static PropertyPartClassification Clone(
            PropertyPartClassification source,
            Action<PropertyPartClassification> mutate)
        {
            var copy = new PropertyPartClassification
            {
                HasPartKind = source.HasPartKind,
                PartKind = source.PartKind,
                HasFlatPlateSubKind = source.HasFlatPlateSubKind,
                FlatPlateSubKind = source.FlatPlateSubKind,
                DrawingProfile = source.DrawingProfile,
                IsTrusted = source.IsTrusted,
                TrustFailureReason = source.TrustFailureReason,
                Origin = source.Origin,
                Est = source.Est,
                EstNameMatch = source.EstNameMatch,
                FileProperties = source.FileProperties,
                ConfigurationProperties = source.ConfigurationProperties,
                ActiveConfiguration = source.ActiveConfiguration
            };

            mutate(copy);
            return copy;
        }

        private static bool TryParsePartKind(string? raw, out PartModelKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim().Replace('-', '_').Replace(' ', '_');

            if (Enum.TryParse(raw, ignoreCase: true, out kind))
                return true;

            return raw.ToUpperInvariant() switch
            {
                "BENT" or "BENT_SHEET_METAL" or "SHEET_METAL_BENT" => Assign(PartModelKind.BentSheetMetal, out kind),
                "LOFTED" or "LOFTED_BENDS" or "SHELL_LOFTED" => Assign(PartModelKind.LoftedBends, out kind),
                "FLAT" or "FLAT_PLATE" or "SHEET_METAL_FLAT" => Assign(PartModelKind.FlatPlate, out kind),
                "CYLINDER" or "CYLINDRICAL" or "TUBE" or "PIPE" => Assign(PartModelKind.Cylindrical, out kind),
                "IMPORT" or "IMPORTED" or "IMPORTED_GEOMETRY" or "STEP" => Assign(PartModelKind.ImportedGeometry, out kind),
                "FLANGE" or "GASKET" or "FLANGE_GASKET" => Assign(PartModelKind.FlatPlate, out kind),
                _ => false
            };
        }

        private static bool TryParseFlatPlateSubKind(string? raw, out FlatPlateSubKind subKind)
        {
            subKind = FlatPlateSubKind.Unknown;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim().Replace('-', '_').Replace(' ', '_');

            if (Enum.TryParse(raw, ignoreCase: true, out subKind) && subKind != FlatPlateSubKind.Unknown)
                return true;

            return raw.ToUpperInvariant() switch
            {
                "DISC" or "ROUND" or "ROUND_DISC" => Assign(FlatPlateSubKind.RoundDisc, out subKind),
                "ROUNDED" or "ROUNDED_END" => Assign(FlatPlateSubKind.RoundedEnd, out subKind),
                "FLANGE" or "GASKET" or "FLANGE_GASKET" => Assign(FlatPlateSubKind.FlangeGasket, out subKind),
                "BAFFLE" or "BAFFLE_PLATE" or "PERFORATED" or "TUBE_SHEET" or "TUBESHEET"
                    => Assign(FlatPlateSubKind.BafflePlate, out subKind),
                "ARC" or "SECTOR" or "ARC_SECTOR" or "ANNULAR" or "RING_SEGMENT"
                    => Assign(FlatPlateSubKind.ArcSector, out subKind),
                "GENERIC" or "PLATE" => Assign(FlatPlateSubKind.Generic, out subKind),
                _ => false
            };
        }

        private static bool Assign<T>(T value, out T target)
        {
            target = value;
            return true;
        }
    }
}
