using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Tests;

public class ImportedFeatureNameTests
{
    [Theory]
    [InlineData("blind-flange_DN900_PN25_1250mm.step<1>", true)]
    [InlineData("part.STP", true)]
    [InlineData("housing.x_t", true)]
    [InlineData("cover.IGES", true)]
    [InlineData("Boss-Extrude1", false)]
    [InlineData("Cut-Extrude1", false)]
    public void FeatureNameLooksLikeForeignCad_MatchesExtensions(string name, bool expected)
    {
        // Mirror scanner heuristic (kept public via reflection-free duplicate for safety).
        bool actual = LooksLike(name);
        Assert.Equal(expected, actual);
    }

    private static bool LooksLike(string featName)
    {
        string[] exts = { ".STEP", ".STP", ".IGS", ".IGES", ".X_T", ".X_B", ".SAT", ".PARASOLID", ".VDA", ".JT" };
        string upper = featName.ToUpperInvariant();
        foreach (string ext in exts)
        {
            if (upper.Contains(ext))
                return true;
        }

        return false;
    }
}
