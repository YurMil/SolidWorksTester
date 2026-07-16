using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Tests;

public class PartAnalysisResultCloneTests
{
    [Fact]
    public void Clone_CopiesFields_AndDoesNotShareMutation()
    {
        var original = new PartAnalysisResult
        {
            Kind = PartModelKind.FlatPlate,
            FlatPlateSubKind = FlatPlateSubKind.RoundDisc,
            EstNameCatalogId = "plate_round",
            HasHoles = true,
            BendFeatureCount = 0,
            BboxLongMeters = 1.2
        };

        PartAnalysisResult copy = original.Clone();
        copy.Kind = PartModelKind.Cylindrical;
        copy.EstNameCatalogId = "pipe";

        Assert.Equal(PartModelKind.FlatPlate, original.Kind);
        Assert.Equal("plate_round", original.EstNameCatalogId);
        Assert.Equal(PartModelKind.Cylindrical, copy.Kind);
        Assert.Equal(FlatPlateSubKind.RoundDisc, copy.FlatPlateSubKind);
        Assert.True(copy.HasHoles);
        Assert.Equal(1.2, copy.BboxLongMeters);
    }
}
