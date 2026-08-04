using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;

namespace SolidWorksTester.Tests;

public class EstCatalogRouteTableTests
{
    [Theory]
    [InlineData("ipe", DrawingPipelineId.ImportedGeometry)]
    [InlineData("shell", DrawingPipelineId.LoftedBends)]
    [InlineData("baffle_plate", DrawingPipelineId.FlatPlate)]
    [InlineData("flange", DrawingPipelineId.FlatPlate)]
    [InlineData("cone", DrawingPipelineId.Cylindrical)]
    public void TryGet_KnownCatalog_OverridesPipeline(string catalogId, DrawingPipelineId expected)
    {
        Assert.True(EstCatalogRouteTable.TryGet(catalogId, out var route));
        Assert.Equal(expected, route.OverridePipelineId);
    }

    [Fact]
    public void TryGet_Baffle_ForcesSubKind()
    {
        Assert.True(EstCatalogRouteTable.TryGet("baffle_plate", out var route));
        Assert.Equal(FlatPlateSubKind.BafflePlate, route.ForceFlatPlateSubKind);
        Assert.True(route.HasDedicatedPipeline);
    }

    [Fact]
    public void TryGet_Unknown_ReturnsFalse()
    {
        Assert.False(EstCatalogRouteTable.TryGet("no_such_catalog", out _));
        Assert.False(EstCatalogRouteTable.TryGet(null, out _));
        Assert.False(EstCatalogRouteTable.TryGet("  ", out _));
    }
}
