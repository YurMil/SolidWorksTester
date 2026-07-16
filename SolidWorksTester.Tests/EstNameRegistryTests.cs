using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Tests;

public class EstNameRegistryTests
{
    [Theory]
    [InlineData("PIPE", "pipe", PartModelKind.Cylindrical)]
    [InlineData("PLATE", "plate", PartModelKind.FlatPlate)]
    [InlineData("SHELL", "shell", PartModelKind.LoftedBends)]
    [InlineData("BAFFLE PLATE", "baffle_plate", PartModelKind.FlatPlate)]
    [InlineData("FLANGE (BLIND, PLATE)", "flange_blind_plate", PartModelKind.FlatPlate)]
    [InlineData("IPE", "ipe", PartModelKind.ImportedGeometry)]
    [InlineData("  plate  ", "plate", PartModelKind.FlatPlate)]
    public void TryIdentify_KnownNames_MatchExpected(
        string rawName,
        string catalogId,
        PartModelKind kind)
    {
        Assert.True(EstNameRegistry.TryIdentify(rawName, out EstNameIdentification id));
        Assert.Equal(catalogId, id.CatalogId);
        Assert.Equal(kind, id.PartKind);
    }

    [Fact]
    public void TryIdentify_BaffleSetsFlatPlateSubKind()
    {
        Assert.True(EstNameRegistry.TryIdentify("BAFFLE PLATE", out EstNameIdentification id));
        Assert.Equal(FlatPlateSubKind.BafflePlate, id.FlatPlateSubKind);
        Assert.True(id.HasDedicatedPipeline);
    }

    [Fact]
    public void TryIdentify_Unknown_ReturnsFalse()
    {
        Assert.False(EstNameRegistry.TryIdentify("TOTALLY UNKNOWN PART XYZ", out _));
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        Assert.Equal("PLATE ROUND", EstNameRegistry.Normalize("  plate   round  "));
    }

    [Fact]
    public void ListCatalogIds_ContainsCoreFamilies()
    {
        IReadOnlyList<string> ids = EstNameRegistry.ListCatalogIds();
        Assert.Contains("pipe", ids);
        Assert.Contains("shell", ids);
        Assert.Contains("baffle_plate", ids);
    }
}
