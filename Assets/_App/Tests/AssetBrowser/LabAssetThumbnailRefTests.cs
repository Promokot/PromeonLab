using NUnit.Framework;
using UnityEngine;

public class LabAssetThumbnailRefTests
{
    [Test]
    public void ImportedLabAsset_ThumbnailRef_DefaultsNull_ThenReflectsSetValue()
    {
        var a = new ImportedLabAsset("id1", "Name", AssetType.Object, "asset-libraries/sources/id1.glb");
        Assert.IsTrue(string.IsNullOrEmpty(a.ThumbnailRef), "fresh record has no thumbnail ref");

        a.SetThumbnailRef("asset-libraries/thumbnails/id1.png");
        Assert.AreEqual("asset-libraries/thumbnails/id1.png", a.ThumbnailRef);
    }

    [Test]
    public void ImportedLabAsset_ThumbnailRef_RoundTripsThroughJson()
    {
        var a = new ImportedLabAsset("id2", "Name2", AssetType.Reference, "asset-libraries/sources/id2.png");
        a.SetThumbnailRef("asset-libraries/sources/id2.png");

        var json = JsonUtility.ToJson(a);
        var back = JsonUtility.FromJson<ImportedLabAsset>(json);

        Assert.AreEqual("asset-libraries/sources/id2.png", back.ThumbnailRef);
    }

    [Test]
    public void SavedLabAsset_ThumbnailRef_IsNull()
    {
        var s = new SavedLabAsset("sid", "S", AssetType.Object, "aid");
        Assert.IsNull(s.ThumbnailRef);
    }
}
