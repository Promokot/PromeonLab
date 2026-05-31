using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Shared model-spawn logic for Object + Rig (both load a mesh and place it). Slice 2 will extend the
// Rig path with runtime proxy-rig building.
public static class ModelSpawnCore
{
    public static Task<GameObject> SpawnAsync(
        ILabAsset asset, Vector3 position, Quaternion rotation,
        AssetSourceStore store, GltfModelLoader loader, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported/Saved asset '{asset.Id}' has no SourceRef");

        var abs = store.AbsolutePath(asset.SourceRef);
        return loader.LoadAsync(abs, position, rotation, ct);
    }
}
