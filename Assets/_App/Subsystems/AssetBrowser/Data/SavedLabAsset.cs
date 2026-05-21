using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class SavedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _assetId;

    public string    Id          => _id;
    public string    DisplayName => _displayName;
    public AssetType Type        => _type;
    public Sprite    Icon        => null;
    public string    AssetId     => _assetId;

    public SavedLabAsset() { }

    public SavedLabAsset(string id, string displayName, AssetType type, string assetId)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _assetId     = assetId;
    }

    public Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        throw new NotImplementedException("SavedLabAsset.SpawnAsync — drag-drop phase");
    }
}
