using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;

namespace SolidWorksTester.Tests;

public class ImportedBlindFlangeRoutingTests
{
    [Fact]
    public void ShapeRecognizer_Dn900BlindFlangeDisc_IsFlatPlateLikeNotTube()
    {
        // 1250 × 1250 × 91.6 mm — OD cylinders must not classify as tube.
        var body = new SolidBodyAnalysisResult
        {
            SolidBodyCount = 1,
            CylindricalFaces = 34,
            PlanarFaces = 2,
            SmallCylinderFaces = 32,
            LargeCylinderFaces = 2,
            IsHollow = false,
            HasHoles = true,
            IsCylindricalGeometry = true,
            ImportFeatureCount = 1
        };

        var result = ImportedGeometryShapeRecognizer.RecognizeFromBbox(
            body, s: 0.0916, m: 1.250, l: 1.250);

        Assert.Equal(ImportedGeometryShapeKind.FlatPlateLike, result.Shape);
        Assert.False(result.IsTrueCylindricalTube);
    }

    [Fact]
    public void ShapeRecognizer_LongHollowPipe_IsTrueTube()
    {
        var body = new SolidBodyAnalysisResult
        {
            SolidBodyCount = 1,
            CylindricalFaces = 2,
            PlanarFaces = 2,
            SmallCylinderFaces = 0,
            LargeCylinderFaces = 2,
            IsHollow = true,
            HasHoles = false,
            IsCylindricalGeometry = true,
            ImportFeatureCount = 1
        };

        var result = ImportedGeometryShapeRecognizer.RecognizeFromBbox(
            body, s: 0.090, m: 0.100, l: 2.000);

        Assert.Equal(ImportedGeometryShapeKind.CylindricalLike, result.Shape);
        Assert.True(result.IsTrueCylindricalTube);
    }

    [Fact]
    public void Router_DescriptionOnlyFlangeProfile_IsExplicitNotEstCatalog()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.FlatPlate,
            FlatPlateSubKind = FlatPlateSubKind.FlangeGasket,
            FlatPlateSubKindSource = ClassificationSource.CustomProperty,
            ClassificationSource = ClassificationSource.CustomProperty,
            KindSource = ClassificationSource.CustomProperty,
            PropertyOrigin = PropertyClassificationOrigin.CadAsExplicit,
            DrawingProfile = "flange",
            EstProperties = new EstPartProperties { Name = "", Description = "BLIND FLANGE DN900" },
            EstNameHasDedicatedPipeline = true
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);

        Assert.Equal(DrawingPipelineId.FlatPlate, route.PipelineId);
        Assert.Equal(FlatPlateSubKind.FlangeGasket, route.ForcedFlatPlateSubKind);
        Assert.Equal(DrawingRouteSource.ExplicitProfile, route.Source);
        Assert.True(route.HasDedicatedPipeline);
    }

    [Fact]
    public void Router_GeometryFlange_MapsToFlatPlatePipeline()
    {
        var analysis = new PartAnalysisResult
        {
            Kind = PartModelKind.FlatPlate,
            FlatPlateSubKind = FlatPlateSubKind.FlangeGasket,
            GeometryFlatPlateSubKind = FlatPlateSubKind.FlangeGasket,
            FlatPlateSubKindSource = ClassificationSource.Geometry,
            ClassificationSource = ClassificationSource.Geometry,
            KindSource = ClassificationSource.Geometry,
            IsImportedGeometry = true,
            ImportedShape = ImportedGeometryShapeKind.FlatPlateLike
        };

        DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis);

        Assert.Equal(DrawingPipelineId.FlatPlate, route.PipelineId);
        Assert.Equal(DrawingRouteSource.Geometry, route.Source);
        // Geometry-sourced sub-kind is not forced; drawing resolver still uses FlatPlateSubKind.
        Assert.Null(route.ForcedFlatPlateSubKind);
    }

    [Fact]
    public void Catalog_Flange_ForcesFlangeGasketPipeline()
    {
        Assert.True(EstCatalogRouteTable.TryGet("flange", out var route));
        Assert.Equal(DrawingPipelineId.FlatPlate, route.OverridePipelineId);
        Assert.Equal(FlatPlateSubKind.FlangeGasket, route.ForceFlatPlateSubKind);
        Assert.True(route.HasDedicatedPipeline);
    }
}
