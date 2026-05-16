using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public struct BuiltinLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private Sprite    _icon;
    [SerializeField] private GameObject _prefab;

    public string    Id          => _id;
    public string    DisplayName => _displayName;
    public AssetType Type        => _type;
    public Sprite    Icon        => _icon;

    public Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var instance = UnityEngine.Object.Instantiate(_prefab, position, rotation);
        return Task.FromResult(instance);
    }
}
