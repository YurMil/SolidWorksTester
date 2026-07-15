using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Detects imported / dumb-solid parts from the FeatureManager tree.</summary>
    internal static class ImportedGeometryDetector
    {
        private static readonly HashSet<string> ImportFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MBimport",
            "BodyFeature",
            "SolidBody",
            "ImportSolid",
            "ImpOrphan"
        };

        private static readonly HashSet<string> NativeSolidFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Boss", "BossExtrude", "BossThin", "Cut", "CutExtrude", "CutThin",
            "Extrude", "Revolve", "RevolveBoss", "RevolveCut", "RevolveThin",
            "Sweep", "SweepBoss", "SweepCut", "SweepThin",
            "Loft", "LoftBoss", "LoftCut",
            "BaseBody", "BaseFlange", "EdgeFlange", "SheetMetal"
        };

        private static readonly HashSet<string> IgnoredFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "OriginProfileFeature", "Reference", "RefPlane", "RefAxis", "CoordSys",
            "CommentsFolder", "FavoriteFolder", "HistoryFolder", "SelectionSetFolder",
            "SensorFolder", "SolidBodyFolder", "SurfaceBodyFolder", "MateReference",
            "MaterialFolder", "DetailCabinet", "EnvFolder", "InkMarkupFolder",
            "NotesAreaFolder", "DocsFolder", "Attribute", "TitleBlock", "Frame",
            "FlatPatternFolder", "SubWeldFolder", "Weldment", "MeshBodyFeature",
            "MagneticGround", "AmbientLight", "DirectionLight", "SpotLight"
        };

        public sealed class ImportDetectionResult
        {
            public bool IsImported { get; init; }
            public int ImportFeatureCount { get; init; }
            public int NativeSolidFeatureCount { get; init; }
            public string PrimaryImportFeatureName { get; init; } = string.Empty;
        }

        public static ImportDetectionResult Analyze(IModelDoc2 partDoc)
        {
            int importCount = 0;
            int nativeCount = 0;
            string primaryImportName = string.Empty;

            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                string typeName = feat.GetTypeName2() ?? string.Empty;
                string featName = feat.Name ?? string.Empty;

                if (!IgnoredFeatureTypes.Contains(typeName))
                {
                    if (IsImportFeature(feat, typeName, featName))
                    {
                        importCount++;
                        if (string.IsNullOrEmpty(primaryImportName))
                            primaryImportName = featName;
                    }
                    else if (NativeSolidFeatureTypes.Contains(typeName))
                    {
                        nativeCount++;
                    }
                }

                feat = feat.GetNextFeature() as Feature;
            }

            bool isImported = importCount > 0 && nativeCount == 0;

            return new ImportDetectionResult
            {
                IsImported = isImported,
                ImportFeatureCount = importCount,
                NativeSolidFeatureCount = nativeCount,
                PrimaryImportFeatureName = primaryImportName
            };
        }

        private static bool IsImportFeature(Feature feat, string typeName, string featName)
        {
            if (ImportFeatureTypes.Contains(typeName))
                return true;

            if (featName.StartsWith("Imported", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                if (feat.Is3DInterconnectFeature)
                    return true;
            }
            catch
            {
                // ignore COM failures on older features
            }

            return false;
        }
    }
}
