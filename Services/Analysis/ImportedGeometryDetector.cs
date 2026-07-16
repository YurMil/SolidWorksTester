namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Import / dumb-solid detection result.
    /// Populated by <see cref="PartGeometryScanner"/> during the single feature-tree walk.
    /// </summary>
    internal static class ImportedGeometryDetector
    {
        public sealed class ImportDetectionResult
        {
            public bool IsImported { get; init; }
            public int ImportFeatureCount { get; init; }
            public int NativeSolidFeatureCount { get; init; }
            public string PrimaryImportFeatureName { get; init; } = string.Empty;
        }
    }
}
