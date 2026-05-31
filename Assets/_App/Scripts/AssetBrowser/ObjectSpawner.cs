using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ObjectSpawner : IAssetSpawner
{
    public AssetType HandledType => AssetType.Object;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => BuiltinAssetSpawnCore.SpawnBuiltin(asset, position, rotation);
}
