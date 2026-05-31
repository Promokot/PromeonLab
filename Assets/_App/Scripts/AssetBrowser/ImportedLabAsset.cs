using System;
using UnityEngine;

[Serializable]
public class ImportedLabAsset : ILabAsset
{
    [SerializeField] private string            _id;
    [SerializeField] private string            _displayName;
    [SerializeField] private AssetType         _type;
    [SerializeField] private string            _sourceRef;
    [SerializeField] private AssetEntityRecipe _recipe;

    public string            Id          => _id;
    public string            DisplayName => _displayName;
    public AssetType         Type        => _type;
    public AssetSource       Source      => AssetSource.Imported;
    public string            SourceRef   => _sourceRef;
    public Sprite            Icon        => null;
    public AssetEntityRecipe Recipe      => _recipe;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string sourceRef, AssetEntityRecipe recipe = null)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _sourceRef   = sourceRef;
        _recipe      = recipe;
    }

    public void SetRecipe(AssetEntityRecipe recipe) => _recipe = recipe;
}
