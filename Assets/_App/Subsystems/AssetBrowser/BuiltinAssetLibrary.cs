using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "BuiltinAssetLibrary", menuName = "PromeonLab/BuiltinAssetLibrary")]
public class BuiltinAssetLibrary : ScriptableObject, IAssetLibrary
{
    [SerializeField] private List<BuiltinLabAsset> _entries = new();

    private List<ILabAsset> _assets;

    public IReadOnlyList<ILabAsset> Assets
    {
        get
        {
            if (_assets == null)
            {
                _assets = new List<ILabAsset>(_entries.Count);
                foreach (var e in _entries) _assets.Add(e);
            }
            return _assets;
        }
    }

    public Task LoadAsync(CancellationToken ct) => Task.CompletedTask;
    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    public void Add(ILabAsset asset) =>
        throw new InvalidOperationException("BuiltinAssetLibrary is read-only");

    public void Remove(string id) =>
        throw new InvalidOperationException("BuiltinAssetLibrary is read-only");
}
