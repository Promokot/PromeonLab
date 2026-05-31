using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class AssetSpawnerRegistryTests
{
    private class FakeAsset : ILabAsset
    {
        public FakeAsset(AssetType t) => Type = t;
        public string Id => "fake";
        public string DisplayName => "fake";
        public AssetType Type { get; }
        public AssetSource Source => AssetSource.Builtin;
        public string SourceRef => null;
        public Sprite Icon => null;
    }

    private class FakeSpawner : IAssetSpawner
    {
        public FakeSpawner(AssetType t) => HandledType = t;
        public AssetType HandledType { get; }
        public bool Called;
        public Task<GameObject> SpawnAsync(ILabAsset a, Vector3 p, Quaternion r, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult<GameObject>(null);
        }
    }

    [Test]
    public async Task SpawnAsync_DispatchesToSpawnerForAssetType()
    {
        var obj = new FakeSpawner(AssetType.Object);
        var rig = new FakeSpawner(AssetType.Rig);
        var registry = new AssetSpawnerRegistry(new IAssetSpawner[] { obj, rig });

        await registry.SpawnAsync(new FakeAsset(AssetType.Rig), Vector3.zero, Quaternion.identity, CancellationToken.None);

        Assert.IsTrue(rig.Called);
        Assert.IsFalse(obj.Called);
    }

    [Test]
    public void SpawnAsync_UnknownType_Throws()
    {
        var registry = new AssetSpawnerRegistry(new IAssetSpawner[] { new FakeSpawner(AssetType.Object) });
        Assert.ThrowsAsync<System.NotSupportedException>(async () =>
            await registry.SpawnAsync(new FakeAsset(AssetType.Reference), Vector3.zero, Quaternion.identity, CancellationToken.None));
    }
}
