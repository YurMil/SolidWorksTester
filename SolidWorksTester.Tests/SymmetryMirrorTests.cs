using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Tests;

public class SymmetryMirrorTests
{
    [Fact]
    public void FourHoleRectangle_IsBiSymmetric()
    {
        // Centers like EST-P61356 layout (sheet units arbitrary).
        var pts = new (double X, double Y)[]
        {
            (10, 10),
            (90, 10),
            (10, 90),
            (90, 90),
        };
        double cx = 50;
        double cy = 50;
        double tol = 1.0;

        Assert.True(SmartDimSymmetryCenterlines.SymmetryMirror.IsMirrored(pts, cx, cy, aboutVertical: true, tol));
        Assert.True(SmartDimSymmetryCenterlines.SymmetryMirror.IsMirrored(pts, cx, cy, aboutVertical: false, tol));
    }

    [Fact]
    public void OffsetHole_BreaksVerticalSymmetry()
    {
        var pts = new (double X, double Y)[]
        {
            (10, 10),
            (85, 10), // not mirrored of 10 about cx=50
            (10, 90),
            (85, 90),
        };

        Assert.False(SmartDimSymmetryCenterlines.SymmetryMirror.IsMirrored(pts, 50, 50, aboutVertical: true, 1.0));
        Assert.True(SmartDimSymmetryCenterlines.SymmetryMirror.IsMirrored(pts, 50, 50, aboutVertical: false, 1.0));
    }

    [Fact]
    public void EmptySet_IsNotSymmetric()
    {
        Assert.False(SmartDimSymmetryCenterlines.SymmetryMirror.IsMirrored(
            Array.Empty<(double, double)>(), 0, 0, aboutVertical: true, 1.0));
    }
}
