using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class ImportHandlerTests
{
    [Test]
    public void Gltf_HandlesGlbAndGltf_NotPng()
    {
        var h = new GltfAssetImporter(null);
        Assert.IsTrue(h.CanHandle(".glb"));
        Assert.IsTrue(h.CanHandle(".gltf"));
        Assert.IsFalse(h.CanHandle(".png"));
        Assert.AreEqual(AssetType.Object, h.SuggestedType);
    }

    [Test]
    public void Image_HandlesPngJpg_NotGlb()
    {
        var h = new ImageAssetImporter(null);
        Assert.IsTrue(h.CanHandle(".png"));
        Assert.IsTrue(h.CanHandle(".jpg"));
        Assert.IsTrue(h.CanHandle(".jpeg"));
        Assert.IsFalse(h.CanHandle(".glb"));
        Assert.AreEqual(AssetType.Reference, h.SuggestedType);
    }

    [Test]
    public async Task Import_CopiesSource_AndStampsChosenType()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_imp_" + Path.GetRandomFileName());
        var src  = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".glb");
        File.WriteAllBytes(src, new byte[] { 9, 9 });

        var store = new ImportedSourceProvider(new PathProvider(root));
        var h      = new GltfAssetImporter(store);

        var record = await h.ImportAsync(src, AssetType.Rig, "Hero", CancellationToken.None);

        Assert.AreEqual(AssetType.Rig, record.Type);
        Assert.AreEqual("Hero", record.DisplayName);
        Assert.AreEqual(AssetSource.Imported, record.Source);
        Assert.IsFalse(string.IsNullOrEmpty(record.SourceRef));
        Assert.IsTrue(File.Exists(Path.Combine(root, record.SourceRef)));

        Directory.Delete(root, true);
        File.Delete(src);
    }
}
