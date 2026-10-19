namespace SolidWorksTester.Tests;

public class OverallWidthTests
{
    [Fact]
    public void FlatBetweenCorners_DoesNotCountAsOverallWidth()
    {
        // 167.6 mm top flat on a wider outline (chamfer + corner radius).
        Assert.False(SmartDimOverall.WidthEdgeCoversOutline(0.1676, 0.200, scale: 1));
    }

    [Fact]
    public void EdgeMatchingOutline_CountsAsOverallWidth()
    {
        Assert.True(SmartDimOverall.WidthEdgeCoversOutline(0.1676, 0.1676, scale: 1));
    }

    [Fact]
    public void SheetOutline_IsConvertedWithViewScale()
    {
        // 20 mm on the sheet at 1:10 is a 200 mm model width.
        Assert.True(SmartDimOverall.WidthEdgeCoversOutline(0.200, 0.020, scale: 0.1));
        Assert.False(SmartDimOverall.WidthEdgeCoversOutline(0.1676, 0.020, scale: 0.1));
    }
}
