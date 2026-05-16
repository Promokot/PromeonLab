using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface ILabAsset
{
    string    Id          { get; }
    string    DisplayName { get; }
    AssetType Type        { get; }
    Sprite    Icon        { get; }

    Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct);
}
