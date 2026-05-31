using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ObjectSpawner : IAssetSpawner
{
    private readonly AssetSourceStore _store;
    private readonly GltfModelLoader  _loader;

    public ObjectSpawner(AssetSourceStore store, GltfModelLoader loader)
    {
        _store  = store;
        _loader = loader;
    }

    public AssetType HandledType => AssetType.Object;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => ModelSpawnCore.SpawnAsync(asset, position, rotation, _store, _loader, ct);
}
