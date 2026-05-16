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

    public string    Id          => _id;
    public string    DisplayName => _displayName;
    public AssetType Type        => _type;
    public Sprite    Icon        => null;
    public string    FilePath    => _filePath;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string filePath)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _filePath    = filePath;
    }

    public Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        throw new NotImplementedException("ImportedLabAsset.SpawnAsync — drag-drop phase");
    }
}
