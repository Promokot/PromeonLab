using System.IO;
using NUnit.Framework;

public class PathProviderSourceTests
{
    [Test]
    public void SourcePath_CombinesAssetLibrarySourcesIdAndExt()
    {
        var p = new PathProvider("/root");
        var expected = Path.Combine("/root", "asset-libraries", "sources", "abc123.glb");
        Assert.AreEqual(expected, p.SourcePath("abc123", ".glb"));
    }

    [Test]
    public void SourcePath_NormalizesExtensionWithoutDot()
    {
        var p = new PathProvider("/root");
        Assert.AreEqual(p.SourcePath("id", ".png"), p.SourcePath("id", "png"));
    }
}
