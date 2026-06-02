using System;
using UnityEngine;

[Serializable]
public class SavedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _assetId;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Saved;
    public string      SourceRef   => null;
    public Sprite      Icon        => null;
    public string      ThumbnailRef => null;   // Saved-library spawn flow is Slice 3 (not implemented)
    public AssetEntityRecipe Recipe => null;   // Saved-library spawn flow is Slice 3 (not implemented)
    public string      AssetId     => _assetId;

    public SavedLabAsset() { }

    public SavedLabAsset(string id, string displayName, AssetType type, string assetId)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _assetId     = assetId;
    }
}
