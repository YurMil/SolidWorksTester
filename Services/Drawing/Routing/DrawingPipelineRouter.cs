using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.Routing
{
    /// <summary>
    /// Resolves a drawing route from merged <see cref="PartAnalysisResult"/>
    /// (geometry + EST/custom properties).
    /// </summary>
    public static class DrawingPipelineRouter
    {
        public static DrawingRouteDecision Resolve(PartAnalysisResult analysis, Action<string>? log = null)
        {
            log?.Invoke("Resolving drawing pipeline route...");

            DrawingRouteSource source = InferRouteSource(analysis);
            DrawingPipelineId pipelineId = MapKindToPipeline(analysis.Kind);
            bool dedicated = analysis.EstNameHasDedicatedPipeline;
            FlatPlateSubKind? forcedSubKind = InferForcedFlatPlateSubKind(analysis);

            ApplyCatalogOverrides(
                analysis.EstNameCatalogId,
                ref pipelineId,
                ref forcedSubKind,
                ref dedicated,
                ref source,
                isProfile: false);

            if (!string.IsNullOrWhiteSpace(analysis.DrawingProfile) &&
                !analysis.DrawingProfile.Equals(analysis.EstNameCatalogId, StringComparison.OrdinalIgnoreCase))
            {
                ApplyCatalogOverrides(
                    analysis.DrawingProfile,
                    ref pipelineId,
                    ref forcedSubKind,
                    ref dedicated,
                    ref source,
                    isProfile: true);
            }

            if (!dedicated && !string.IsNullOrWhiteSpace(analysis.EstNameCatalogId))
            {
                log?.Invoke(
                    $"  EST catalog '{analysis.EstNameCatalogId}' has no dedicated dimension pipeline — " +
                    $"using {pipelineId} with generic modules.");
            }

            var decision = new DrawingRouteDecision
            {
                PipelineId = pipelineId,
                PartKind = analysis.Kind,
                ForcedFlatPlateSubKind = forcedSubKind,
                CatalogId = analysis.EstNameCatalogId,
                DrawingProfile = analysis.DrawingProfile,
                HasDedicatedPipeline = dedicated,
                Source = source,
                Summary = BuildSummary(pipelineId, analysis, forcedSubKind, dedicated, source)
            };

            log?.Invoke($"  Route: {decision}");
            return decision;
        }

        private static void ApplyCatalogOverrides(
            string? key,
            ref DrawingPipelineId pipelineId,
            ref FlatPlateSubKind? forcedSubKind,
            ref bool dedicated,
            ref DrawingRouteSource source,
            bool isProfile)
        {
            if (!EstCatalogRouteTable.TryGet(key, out EstCatalogRouteTable.CatalogRoute catalogRoute))
                return;

            if (catalogRoute.OverridePipelineId.HasValue)
                pipelineId = catalogRoute.OverridePipelineId.Value;

            if (catalogRoute.ForceFlatPlateSubKind.HasValue)
                forcedSubKind = catalogRoute.ForceFlatPlateSubKind;

            if (catalogRoute.HasDedicatedPipeline.HasValue)
                dedicated = catalogRoute.HasDedicatedPipeline.Value;

            source = isProfile ? DrawingRouteSource.ExplicitProfile : DrawingRouteSource.EstNameCatalog;
        }

        private static DrawingPipelineId MapKindToPipeline(PartModelKind kind) => kind switch
        {
            PartModelKind.FlatPlate => DrawingPipelineId.FlatPlate,
            PartModelKind.BentSheetMetal => DrawingPipelineId.BentSheetMetal,
            PartModelKind.Cylindrical => DrawingPipelineId.Cylindrical,
            PartModelKind.ImportedGeometry => DrawingPipelineId.ImportedGeometry,
            _ => DrawingPipelineId.GenericFallback
        };

        private static FlatPlateSubKind? InferForcedFlatPlateSubKind(PartAnalysisResult analysis)
        {
            if (analysis.Kind != PartModelKind.FlatPlate)
                return null;

            if (analysis.FlatPlateSubKind == FlatPlateSubKind.Unknown)
                return null;

            if (analysis.FlatPlateSubKindSource == ClassificationSource.Geometry)
                return null;

            if (analysis.FlatPlateSubKind == FlatPlateSubKind.Generic &&
                analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.FlangeGasket)
                return FlatPlateSubKind.FlangeGasket;

            return analysis.FlatPlateSubKind;
        }

        private static DrawingRouteSource InferRouteSource(PartAnalysisResult analysis)
        {
            if (analysis.ClassificationSource == ClassificationSource.Hybrid)
                return DrawingRouteSource.Hybrid;

            if (analysis.KindSource == ClassificationSource.CustomProperty)
            {
                return analysis.PropertyOrigin == PropertyClassificationOrigin.EstConfigurationName
                    ? DrawingRouteSource.EstNameCatalog
                    : DrawingRouteSource.ExplicitProfile;
            }

            if (!string.IsNullOrWhiteSpace(analysis.EstNameCatalogId))
                return DrawingRouteSource.EstNameCatalog;

            return DrawingRouteSource.Geometry;
        }

        private static string BuildSummary(
            DrawingPipelineId pipelineId,
            PartAnalysisResult analysis,
            FlatPlateSubKind? forcedSubKind,
            bool dedicated,
            DrawingRouteSource source)
        {
            string sub = forcedSubKind is FlatPlateSubKind sk && sk != FlatPlateSubKind.Unknown
                ? $", sub={sk}"
                : analysis.Kind == PartModelKind.FlatPlate
                    ? $", sub={analysis.FlatPlateSubKind}"
                    : "";

            string catalog = string.IsNullOrWhiteSpace(analysis.EstNameCatalogId)
                ? ""
                : $", catalog={analysis.EstNameCatalogId}";

            return $"{pipelineId}{sub}{catalog}, dedicated={dedicated}, source={source}";
        }
    }
}
