using SolidWorksTester.UI;

namespace SolidWorksTester.Tests;

public class UiPathCacheTests
{
    [Fact]
    public void MissingFile_ReturnsEmptyPaths()
    {
        string previous = UiPathCache.CacheFilePath;
        string file = Path.Combine(Path.GetTempPath(), "SolidWorksTesterTests", Guid.NewGuid().ToString("N"), "paths.json");
        UiPathCache.CacheFilePath = file;
        try
        {
            (string template, string format) = UiPathCache.Load();
            Assert.Equal("", template);
            Assert.Equal("", format);
        }
        finally
        {
            UiPathCache.CacheFilePath = previous;
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsBothPaths()
    {
        string previous = UiPathCache.CacheFilePath;
        string directory = Path.Combine(Path.GetTempPath(), "SolidWorksTesterTests", Guid.NewGuid().ToString("N"));
        UiPathCache.CacheFilePath = Path.Combine(directory, "paths.json");
        try
        {
            UiPathCache.Save(@"C:\templates\drawing.drwdot", @"C:\formats\frame.slddrt");
            (string template, string format) = UiPathCache.Load();
            Assert.Equal(@"C:\templates\drawing.drwdot", template);
            Assert.Equal(@"C:\formats\frame.slddrt", format);
        }
        finally
        {
            UiPathCache.CacheFilePath = previous;
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
