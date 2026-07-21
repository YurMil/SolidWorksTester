using System.Collections.Generic;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Tests;

public class FastenerPropertyDetectorTests
{
    [Fact]
    public void Detects_DocumentType_Fastener()
    {
        var snapshot = new CustomPropertySnapshot(
            new Dictionary<string, string> { ["DocumentType"] = "Fastener" },
            new Dictionary<string, string>(),
            "Default");

        Assert.True(FastenerPropertyDetector.TryDetect(snapshot, out string reason));
        Assert.Equal("DocumentType=Fastener", reason);
    }

    [Fact]
    public void Detects_IsFastener_One_On_Configuration()
    {
        var snapshot = new CustomPropertySnapshot(
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["IsFastener"] = "1" },
            "Default");

        Assert.True(FastenerPropertyDetector.TryDetect(snapshot, out string reason));
        Assert.Contains("IsFastener", reason);
    }

    [Fact]
    public void Ignores_NonFastener_DocumentType()
    {
        var snapshot = new CustomPropertySnapshot(
            new Dictionary<string, string>
            {
                ["DocumentType"] = "Part",
                ["Type"] = "PURCHASED"
            },
            new Dictionary<string, string>(),
            "Default");

        Assert.False(FastenerPropertyDetector.TryDetect(snapshot, out _));
    }
}
