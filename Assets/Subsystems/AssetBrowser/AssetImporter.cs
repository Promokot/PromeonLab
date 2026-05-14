using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AssetImporter
{
    private readonly DemoAssetCatalog _catalog;
    private readonly AppStorage _storage;

    public AssetImporter(DemoAssetCatalog catalog, AppStorage storage)
    {
        _catalog = catalog;
        _storage = storage;
    }

    public async Task<(GameObject Instance, AssetEntry Entry)> ImportAsync(
        string filePath, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(filePath);

        if (!_catalog.TryFind(fileName, out var demoEntry))
        {
            Debug.LogWarning($"AssetImporter: '{fileName}' not in DemoAssetCatalog");
            return (null, default);
        }

        await Task.Yield();

        var instance = UnityEngine.Object.Instantiate(
            demoEntry.Prefab, Vector3.zero, Quaternion.identity);
        instance.name = Path.GetFileNameWithoutExtension(fileName);

        var assetEntry = new AssetEntry
        {
            AssetId      = Guid.NewGuid().ToString("N")[..8],
            Type         = demoEntry.Type,
            DisplayName  = instance.name,
            RelativePath = $"Models/{fileName}",
            Icon         = demoEntry.Icon
        };

        return (instance, assetEntry);
    }
}
