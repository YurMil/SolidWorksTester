using SolidWorksTester.FlangeGasket;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Tests;

public class FlangeGasketImportSkipTests
{
    [Fact]
    public void Skip_PureImportedBody_WithoutSketchDims()
    {
        var analysis = new PartAnalysisResult
        {
            IsImportedGeometry = true,
            ImportFeatureCount = 1,
            CanImportSketchDimensions = false
        };

        Assert.True(FlangeGasketDimensionPipeline.ShouldSkipModelDimensionImport(analysis));
    }

    [Fact]
    public void DoNotSkip_SheetMetal_WithStrayImportFeature()
    {
        var analysis = new PartAnalysisResult
        {
            IsImportedGeometry = false,
            ImportFeatureCount = 1,
            ImportFeatureName = "Imported1",
            CanImportSketchDimensions = false
        };

        Assert.False(FlangeGasketDimensionPipeline.ShouldSkipModelDimensionImport(analysis));
    }

    [Fact]
    public void DoNotSkip_WhenSketchDimensionsAreEligible()
    {
        var analysis = new PartAnalysisResult
        {
            IsImportedGeometry = true,
            ImportFeatureCount = 1,
            CanImportSketchDimensions = true
        };

        Assert.False(FlangeGasketDimensionPipeline.ShouldSkipModelDimensionImport(analysis));
    }
}
