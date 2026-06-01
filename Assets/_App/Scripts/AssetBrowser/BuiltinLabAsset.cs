using System;
using UnityEngine;

[Serializable]
public struct BuiltinLabAsset : ILabAsset
{
    [SerializeField] private string     _id;
    [SerializeField] private string     _displayName;
    [SerializeField] private AssetType  _type;
    [SerializeField] private Sprite     _icon;
    [SerializeField] private GameObject      _prefab;
    [SerializeField] private TerminalBoneAxis _terminalBonesAxis;       // leaf-bone axis for Rig entries; ignored otherwise
    [SerializeField] private bool             _invertTerminalBonesAxis; // flip the chosen X/Y/Z axis

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Builtin;
    public string      SourceRef   => null;
    public Sprite      Icon        => _icon;
    public GameObject       Prefab        => _prefab;
    public TerminalBoneAxis TerminalBonesAxis       => _terminalBonesAxis;
    public bool             InvertTerminalBonesAxis => _invertTerminalBonesAxis;
}
