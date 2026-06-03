using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

public class GltfModelImporter
{
    /// Loads a .glb from an absolute file path and instantiates it under a fresh root GameObject
    /// placed at pose. Returns the root (or null on failure).
    public async Task<GameObject> LoadAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(absolutePath, ct);

        var gltf = new GltfImport();
        var ok   = await gltf.LoadGltfBinary(bytes, new Uri(absolutePath));
        if (!ok)
        {
            Debug.LogError($"GltfModelImporter: failed to parse '{absolutePath}'");
            return null;
        }

        var root = new GameObject("ImportedModel");
        root.transform.SetPositionAndRotation(position, rotation);
        var instantiated = await gltf.InstantiateMainSceneAsync(root.transform);
        if (!instantiated)
        {
            Debug.LogError($"GltfModelImporter: failed to instantiate '{absolutePath}'");
            UnityEngine.Object.Destroy(root);
            return null;
        }
        return root;
    }
}
