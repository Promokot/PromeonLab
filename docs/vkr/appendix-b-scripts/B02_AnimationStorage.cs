using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Owns animation.json load/save + the debounced write. Non-destructive on unsupported versions:
// an old or too-new file is left untouched and an empty document is opened in memory (B4).
public class AnimationStorage : IDisposable
{
    private readonly PathProvider _paths;
    private CancellationTokenSource _saveCts;
    private const int SAVE_DEBOUNCE_MS = 200;

    [VContainer.Inject]
    public AnimationStorage(PathProvider paths) => _paths = paths;

    public async Task<SceneAnimationData> LoadAsync(string sceneId, CancellationToken ct)
    {
        var path = _paths.AnimationJson(sceneId);
        if (!File.Exists(path)) return new SceneAnimationData();

        try
        {
            var json   = await File.ReadAllTextAsync(path, ct);
            var loaded = JsonUtility.FromJson<SceneAnimationData>(json);

            if (loaded == null || loaded.schemaVersion < 2 || loaded.schemaVersion > 2)
            {
                Debug.LogWarning($"AnimationStorage: '{path}' has unsupported schemaVersion="
                    + $"{loaded?.schemaVersion ?? 0}. Opening empty; file left untouched.");
                return new SceneAnimationData();
            }

            if (loaded.Fps <= 0)
                loaded.Fps = loaded.Containers.Count > 0 ? Mathf.Max(1, loaded.Containers[0].Fps) : 24;

            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogError($"AnimationStorage: load failed '{path}': {ex.Message}");
            return new SceneAnimationData();
        }
    }

    public void RequestSave(SceneAnimationData data, string sceneId)
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = DebouncedSave(data, sceneId, _saveCts.Token);
    }

    private async Task DebouncedSave(SceneAnimationData data, string sceneId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(SAVE_DEBOUNCE_MS, ct);
            if (!ct.IsCancellationRequested) await SaveAsync(data, sceneId, ct);
        }
        catch (TaskCanceledException) { }
    }

    private async Task SaveAsync(SceneAnimationData data, string sceneId, CancellationToken ct)
    {
        if (data == null || string.IsNullOrEmpty(sceneId)) return;
        try
        {
            var path = _paths.AnimationJson(sceneId);
            await File.WriteAllTextAsync(path, JsonUtility.ToJson(data, prettyPrint: true), ct);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AnimationStorage: save failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
    }
}
