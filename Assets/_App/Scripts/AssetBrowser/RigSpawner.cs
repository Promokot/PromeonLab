using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RigSpawner : IAssetSpawner
{
    public AssetType HandledType => AssetType.Rig;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => BuiltinAssetSpawnCore.SpawnBuiltin(asset, position, rotation);
}
