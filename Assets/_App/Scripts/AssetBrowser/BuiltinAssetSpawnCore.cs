using System;
using System.Threading.Tasks;
using UnityEngine;

public static class BuiltinAssetSpawnCore
{
    public static Task<GameObject> SpawnBuiltin(ILabAsset asset, Vector3 position, Quaternion rotation)
    {
        if (asset.Source != AssetSource.Builtin || asset is not BuiltinLabAsset b)
            throw new NotSupportedException(
                $"Spawning source '{asset.Source}' requires the glTF/image loader (Slice 1B). " +
                $"Slice 1A supports only Builtin assets.");
        var go = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
        return Task.FromResult(go);
    }
}
