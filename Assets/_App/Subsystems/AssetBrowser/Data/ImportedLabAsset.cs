using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class ImportedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _filePath;
    [SerializeField] private AssetCapabilities _capabilities = AssetCapabilities.Selectable;

    public string    Id          => _id;
    public string    DisplayName => _displayName;
    public AssetType Type        => _type;
    public Sprite    Icon        => null;
    public string    FilePath    => _filePath;
    public AssetCapabilities Capabilities => _capabilities;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string filePath,
        AssetCapabilities capabilities = AssetCapabilities.Selectable | AssetCapabilities.Movable)
    {
        _id           = id;
        _displayName  = displayName;
        _type         = type;
        _filePath     = filePath;
        _capabilities = capabilities;
    }

    public Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        throw new NotImplementedException("ImportedLabAsset.SpawnAsync — drag-drop phase");
    }
}
