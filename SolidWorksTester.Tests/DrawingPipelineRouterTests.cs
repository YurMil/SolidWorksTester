using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;

namespace SolidWorksTester.Tests;

public class DrawingPipelineRouterTests
{
    [Fact]
    public void Resolve_GeometryFlatPlate_MapsToFlatPlatePipeline()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.FlatPlate,
            FlatPlateSubKind = FlatPlateSubKind.Generic,
            ClassificationSource = ClassificationSource.Geometry,
            KindSource = ClassificationSource.Geometry
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);
        Assert.Equal(DrawingPipelineId.FlatPlate, route.PipelineId);
        Assert.Equal(DrawingRouteSource.Geometry, route.Source);
    }

    [Fact]
    public void Resolve_LoftedBends_MapsToLoftedPipeline()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.LoftedBends,
            ClassificationSource = ClassificationSource.Geometry,
            KindSource = ClassificationSource.Geometry
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);
        Assert.Equal(DrawingPipelineId.LoftedBends, route.PipelineId);
    }

    [Fact]
    public void Resolve_ShellCatalog_OverridesToLoftedBends()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.Cylindrical,
            EstNameCatalogId = "shell",
            EstNameHasDedicatedPipeline = true,
            ClassificationSource = ClassificationSource.CustomProperty,
            KindSource = ClassificationSource.CustomProperty,
            PropertyOrigin = PropertyClassificationOrigin.EstConfigurationName
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);
        Assert.Equal(DrawingPipelineId.LoftedBends, route.PipelineId);
        Assert.Equal(DrawingRouteSource.EstNameCatalog, route.Source);
        Assert.True(route.HasDedicatedPipeline);
    }

    [Fact]
    public void Resolve_BaffleCatalog_ForcesFlatPlateSubKind()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.FlatPlate,
            FlatPlateSubKind = FlatPlateSubKind.Generic,
            FlatPlateSubKindSource = ClassificationSource.CustomProperty,
            EstNameCatalogId = "baffle_plate",
            EstNameHasDedicatedPipeline = true,
            ClassificationSource = ClassificationSource.CustomProperty,
            KindSource = ClassificationSource.CustomProperty,
            PropertyOrigin = PropertyClassificationOrigin.EstConfigurationName
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);
        Assert.Equal(DrawingPipelineId.FlatPlate, route.PipelineId);
        Assert.Equal(FlatPlateSubKind.BafflePlate, route.ForcedFlatPlateSubKind);
    }

    [Fact]
    public void Resolve_UnknownKind_FallsBackToGeneric()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = (PartModelKind)999,
            ClassificationSource = ClassificationSource.Geometry,
            KindSource = ClassificationSource.Geometry
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);
        Assert.Equal(DrawingPipelineId.GenericFallback, route.PipelineId);
    }
}
