using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class ImportedAssetLibrary : IAssetLibrary, IStartable
{
    private readonly PathProvider _paths;
    private readonly List<ImportedLabAsset> _entries = new();
    private List<ILabAsset> _assets;

    public ImportedAssetLibrary(PathProvider paths) => _paths = paths;

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

    public void Start() => _ = LoadAsync(CancellationToken.None);

    public async Task LoadAsync(CancellationToken ct)
    {
        _entries.Clear();
        _assets = null;

        var path = _paths.ImportedLibraryPath;
        if (!File.Exists(path)) return;

        var json = await File.ReadAllTextAsync(path, ct);
        var data = JsonUtility.FromJson<LibraryJson>(json);
        if (data?.entries != null)
            _entries.AddRange(data.entries);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        var path = _paths.ImportedLibraryPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var data = new LibraryJson { entries = _entries };
        var json = JsonUtility.ToJson(data, prettyPrint: true);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public void Add(ILabAsset asset)
    {
        if (asset is not ImportedLabAsset imported) return;
        _entries.Add(imported);
        _assets = null;
    }

    public void Remove(string id)
    {
        _entries.RemoveAll(e => e.Id == id);
        _assets = null;
    }

    [Serializable]
    private class LibraryJson
    {
        public List<ImportedLabAsset> entries = new();
    }
}
