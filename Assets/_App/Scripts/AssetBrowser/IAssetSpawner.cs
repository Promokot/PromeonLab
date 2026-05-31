using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IAssetSpawner
{
    AssetType HandledType { get; }
    Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct);
}
