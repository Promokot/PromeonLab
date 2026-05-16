using NUnit.Framework;
using UnityEngine;

public class AssetRegistryTests
{
    [Test]
    public void Find_UnknownSource_ReturnsNull()
    {
        var builtin = ScriptableObject.CreateInstance<BuiltinAssetLibrary>();
        var sut = new AssetRegistry(builtin, null, null);

        var result = sut.Find(new AssetRef { Source = (AssetSource)999, AssetId = "x" });

        Assert.IsNull(result);
    }

    [Test]
    public void Find_BuiltinByMissingId_ReturnsNull()
    {
        var builtin = ScriptableObject.CreateInstance<BuiltinAssetLibrary>();
        var sut = new AssetRegistry(builtin, null, null);

        var result = sut.Find(new AssetRef { Source = AssetSource.Builtin, AssetId = "no-such" });

        Assert.IsNull(result);
    }
}
