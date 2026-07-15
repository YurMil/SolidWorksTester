namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Aggregated solid-body statistics used for import shape recognition.</summary>
    public sealed class SolidBodyAnalysisResult
    {
        public int SolidBodyCount { get; init; }
        public int CylindricalFaces { get; init; }
        public int PlanarFaces { get; init; }
        public int SmallCylinderFaces { get; init; }
        public int LargeCylinderFaces { get; init; }
        public bool IsHollow { get; init; }
        public bool HasHoles { get; init; }
        public bool IsCylindricalGeometry { get; init; }
        public int ImportFeatureCount { get; init; }
    }
}
