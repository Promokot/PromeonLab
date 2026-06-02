using System.IO;
using NUnit.Framework;

public class PathProviderThumbnailTests
{
    [Test]
    public void ThumbnailPath_IsUnderAssetLibrariesThumbnails()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_pp");
        var pp = new PathProvider(root);

        var expected = Path.Combine(root, "asset-libraries", "thumbnails", "abc.png");
        Assert.AreEqual(expected, pp.ThumbnailPath("abc"));
        Assert.AreEqual(Path.Combine(root, "asset-libraries", "thumbnails"), pp.ThumbnailsDir);
    }

    [Test]
    public void ThumbnailRelativeRef_IsRootIndependentRelativePath()
    {
        var expected = Path.Combine("asset-libraries", "thumbnails", "abc.png");
        Assert.AreEqual(expected, PathProvider.ThumbnailRelativeRef("abc"));
    }
}
