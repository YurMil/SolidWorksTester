using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.Routing
{
    /// <summary>Resolved drawing generation route after classification merge.</summary>
    public sealed class DrawingRouteDecision
    {
        public DrawingPipelineId PipelineId { get; init; }
        public PartModelKind PartKind { get; init; }
        /// <summary>When set, flat-plate sub-router skips geometry rediscovery.</summary>
        public FlatPlateSubKind? ForcedFlatPlateSubKind { get; init; }
        public string? CatalogId { get; init; }
        public string? DrawingProfile { get; init; }
        public bool HasDedicatedPipeline { get; init; }
        public DrawingRouteSource Source { get; init; }
        public string Summary { get; init; } = string.Empty;

        public string PipelineLabel => PipelineId switch
        {
            DrawingPipelineId.FlatPlate => "P-01 FlatPlate",
            DrawingPipelineId.BentSheetMetal => "P-02 BentSheetMetal",
            DrawingPipelineId.Cylindrical => "P-03 Cylindrical",
            DrawingPipelineId.ImportedGeometry => "P-04 ImportedGeometry",
            DrawingPipelineId.LoftedBends => "P-05 LoftedBends",
            DrawingPipelineId.GenericFallback => "P-00 GenericFallback",
            _ => PipelineId.ToString()
        };

        public override string ToString() =>
            $"{PipelineLabel} | kind={PartKind}" +
            (ForcedFlatPlateSubKind is FlatPlateSubKind sk && sk != FlatPlateSubKind.Unknown
                ? $", sub={sk}"
                : "") +
            (string.IsNullOrWhiteSpace(CatalogId) ? "" : $", catalog={CatalogId}") +
            (HasDedicatedPipeline ? "" : ", generic-dims") +
            $" ({Source})";
    }
}
