using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.Tests;

public class SheetFormatPaperTests
{
    [Fact]
    public void Parse_DefaultA3Landscape_Is420By297()
    {
        const string path = @"C:\SX\91_SW Setup\Sheet Formats\SINGLE ROW REV TABLE\SL_A3L_SR_rev.slddrt";

        Assert.True(DrawingSheetProfile.TryParsePaperSize(path, out DrawingSheetProfile.SheetFormatSize size));
        Assert.Equal(swDwgPaperSizes_e.swDwgPaperA3size, size.Paper);
        Assert.Equal(0.420, size.WidthMeters, 3);
        Assert.Equal(0.297, size.HeightMeters, 3);
    }

    [Fact]
    public void Parse_A4Portrait_SwapsSides()
    {
        Assert.True(DrawingSheetProfile.TryParsePaperSize(
            @"C:\formats\SCX_A4P_SR_rev.slddrt",
            out DrawingSheetProfile.SheetFormatSize size));
        Assert.Equal(swDwgPaperSizes_e.swDwgPaperA4sizeVertical, size.Paper);
        Assert.Equal(0.210, size.WidthMeters, 3);
        Assert.Equal(0.297, size.HeightMeters, 3);
    }

    [Fact]
    public void Parse_BareA1_IsLandscape()
    {
        Assert.True(DrawingSheetProfile.TryParsePaperSize(
            @"C:\formats\GEA_30020_A1.slddrt",
            out DrawingSheetProfile.SheetFormatSize size));
        Assert.Equal(swDwgPaperSizes_e.swDwgPaperA1size, size.Paper);
        Assert.Equal(0.841, size.WidthMeters, 3);
        Assert.Equal(0.594, size.HeightMeters, 3);
    }

    [Fact]
    public void Parse_WithoutPaperToken_ReturnsFalse()
    {
        Assert.False(DrawingSheetProfile.TryParsePaperSize(
            @"C:\formats\TitleBlock.slddrt",
            out _));
    }
}
