using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class AssetSourceStoreTests
{
    [Test]
    public async Task Copy_PlacesFileUnderSourcesAndReturnsRelativeRef()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_src_" + Path.GetRandomFileName());
        var srcFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".glb");
        File.WriteAllBytes(srcFile, new byte[] { 1, 2, 3 });

        var store = new AssetSourceStore(new PathProvider(root));
        var rel = await store.CopyAsync("asset9", srcFile, CancellationToken.None);

        var abs = Path.Combine(root, rel);
        Assert.IsTrue(File.Exists(abs), "copied file must exist under sources/");
        Assert.AreEqual(3, new FileInfo(abs).Length);
        StringAssert.Contains("asset9.glb", rel);

        Directory.Delete(root, true);
        File.Delete(srcFile);
    }
}
