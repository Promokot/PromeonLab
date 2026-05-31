using System;
using UnityEngine;

[Serializable]
public struct BuiltinLabAsset : ILabAsset
{
    [SerializeField] private string     _id;
    [SerializeField] private string     _displayName;
    [SerializeField] private AssetType  _type;
    [SerializeField] private Sprite     _icon;
    [SerializeField] private GameObject _prefab;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Builtin;
    public string      SourceRef   => null;
    public Sprite      Icon        => _icon;
    public GameObject  Prefab      => _prefab;
}
