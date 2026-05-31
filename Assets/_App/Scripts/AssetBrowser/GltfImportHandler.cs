using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class GltfImportHandler : IAssetImportHandler
{
    private readonly AssetSourceStore _store;

    public GltfImportHandler(AssetSourceStore store) => _store = store;

    public bool CanHandle(string ext) => ext == ".glb" || ext == ".gltf";

    // Default selection. The wizard lets the user switch to Rig for skinned characters; runtime
    // skeleton auto-detection is deferred (it requires a full load) — see plan notes.
    public AssetType SuggestedType => AssetType.Object;

    public async Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct)
    {
        if (Path.GetExtension(sourceFilePath).ToLowerInvariant() == ".gltf")
            Debug.LogWarning("GltfImportHandler: .gltf with external buffers/textures may not load at runtime; prefer self-contained .glb.");

        var id  = Guid.NewGuid().ToString("N")[..8];
        var rel = await _store.CopyAsync(id, sourceFilePath, ct);
        return new ImportedLabAsset(id, displayName, chosenType, rel);
    }
}
