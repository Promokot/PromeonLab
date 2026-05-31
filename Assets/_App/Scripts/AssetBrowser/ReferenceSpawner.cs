using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceSpawner : IAssetSpawner
{
    private readonly AssetSourceStore     _store;
    private readonly ReferenceQuadFactory _quads;

    public ReferenceSpawner(AssetSourceStore store, ReferenceQuadFactory quads)
    {
        _store = store;
        _quads = quads;
    }

    public AssetType HandledType => AssetType.Reference;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");
        return _quads.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
