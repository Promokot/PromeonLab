using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class AppStorage
{
    private readonly PathProvider _paths;
    private readonly Dictionary<string, SceneData> _cache = new();

    private string _activeSceneId;
    public string ActiveSceneId => _activeSceneId;

    public AppStorage(PathProvider paths) => _paths = paths;

    public async Task<SceneData> CreateSceneAsync(string displayName, CancellationToken ct = default)
    {
        var sceneId = Guid.NewGuid().ToString("N")[..8];
        var data = new SceneData
        {
            SceneId     = sceneId,
            DisplayName = displayName,
            CreatedAt   = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };

        Directory.CreateDirectory(_paths.SceneRoot(sceneId));
        await SaveSceneAsync(data, ct);
        _cache[sceneId] = data;
        return data;
    }

    public async Task<SceneData> LoadSceneAsync(string sceneId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(sceneId, out var cached)) return cached;

        var path = _paths.SceneJson(sceneId);
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, ct);
        var data = SceneSerializer.Deserialize(json);
        _cache[sceneId] = data;
        return data;
    }

    public async Task SaveSceneAsync(SceneData data, CancellationToken ct = default)
    {
        var path = _paths.SceneJson(data.SceneId);
        var json = SceneSerializer.Serialize(data);
        await File.WriteAllTextAsync(path, json, ct);
        _cache[data.SceneId] = data;
    }

    public void SetActiveScene(SceneData data) => _activeSceneId = data.SceneId;

    public IEnumerable<string> GetAllSceneIds()
    {
        var root = _paths.ScenesRoot();
        if (!Directory.Exists(root)) yield break;
        foreach (var dir in Directory.GetDirectories(root))
            yield return Path.GetFileName(dir);
    }

    public void DeleteScene(string sceneId)
    {
        var root = _paths.SceneRoot(sceneId);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
        _cache.Remove(sceneId);
    }
}
