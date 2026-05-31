using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AssetSpawnerRegistry
{
    private readonly Dictionary<AssetType, IAssetSpawner> _byType = new();

    public AssetSpawnerRegistry(IReadOnlyList<IAssetSpawner> spawners)
    {
        foreach (var s in spawners) _byType[s.HandledType] = s;
    }

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (!_byType.TryGetValue(asset.Type, out var spawner))
            throw new NotSupportedException($"No spawner registered for asset type {asset.Type}");
        return spawner.SpawnAsync(asset, position, rotation, ct);
    }
}
