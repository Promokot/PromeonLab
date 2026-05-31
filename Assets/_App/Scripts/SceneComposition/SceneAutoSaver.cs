using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class SceneAutoSaver : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;
    private readonly AppStorage _storage;

    public SceneAutoSaver(EventBus bus, SceneGraph graph, AppStorage storage)
    {
        _bus     = bus;
        _graph   = graph;
        _storage = storage;
    }

    public void Start()   => _bus.Subscribe<ModeExitingEvent>(OnModeExiting);
    public void Dispose() => _bus.Unsubscribe<ModeExitingEvent>(OnModeExiting);

    // Hook the PRE-unload event, not ModeChangedEvent. This saver is scene-scoped, so it is disposed
    // during the Single scene load — before ModeChangedEvent fires. ModeExitingEvent is published
    // while the outgoing VrEditing scene is still loaded, so CaptureSnapshot (synchronous, before the
    // first await in SaveCurrentAsync) reads live nodes/transforms; the async write then runs on the
    // detached snapshot.
    private void OnModeExiting(ModeExitingEvent e)
    {
        if (e.From == AppMode.VrEditing && e.To != AppMode.VrEditing)
            _ = SaveCurrentAsync();
    }

    private async Task SaveCurrentAsync()
    {
        try
        {
            var activeId = _storage.ActiveSceneId;
            if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;

            // Capture before any await — scene may unload after the first yield.
            var cached = _storage.GetCachedScene(activeId);
            if (cached == null) return;
            var snap = _graph.CaptureSnapshot(activeId, cached.DisplayName, cached.CreatedAt);

            await _storage.SaveSceneAsync(snap, CancellationToken.None);
            _bus.Publish(new SceneClosedEvent());
        }
        catch (Exception ex)
        {
            Debug.LogError($"SceneAutoSaver failed: {ex}");
        }
    }
}
