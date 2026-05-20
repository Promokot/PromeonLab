using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

public class ModeOrchestrator
{
    private readonly EventBus _bus;
    private readonly ModeTransitionGraph _graph;

    private AppMode _current = AppMode.MainMenu;
    public AppMode CurrentMode => _current;

    public ModeOrchestrator(EventBus bus, ModeTransitionGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void TransitionTo(AppMode target)
    {
        if (_current == target) return;
        if (!_graph.IsAllowed(_current, target))
        {
            Debug.LogWarning($"Transition {_current} → {target} not allowed");
            return;
        }

        var prev = _current;
        _current = target;

        SceneManager.sceneLoaded += OnSceneLoadedForSpawn;
        UnloadCurrentScene(prev);
        LoadScene(target);

        _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target });
    }

    private void OnSceneLoadedForSpawn(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForSpawn;
        SceneManager.SetActiveScene(scene);
        foreach (var root in scene.GetRootGameObjects())
        {
            var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
            if (anchor == null) continue;
            _bus.Publish(new PlayerSpawnRequestedEvent
            {
                Position = anchor.transform.position,
                Rotation = anchor.transform.rotation
            });
            return;
        }
    }

    private void LoadScene(AppMode mode)
    {
        var sceneName = mode switch
        {
            AppMode.MainMenu  => "MainMenu",
            AppMode.VrEditing => "VrEditing",
            AppMode.ArMapping => "ArMapping",
            AppMode.ArPreview => "ArPreview",
            AppMode.Sandbox   => "Sandbox",
            _                 => null
        };
        if (sceneName != null)
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }

    private void UnloadCurrentScene(AppMode mode)
    {
        var sceneName = mode switch
        {
            AppMode.MainMenu  => "MainMenu",
            AppMode.VrEditing => "VrEditing",
            AppMode.ArMapping => "ArMapping",
            AppMode.ArPreview => "ArPreview",
            AppMode.Sandbox   => "Sandbox",
            _                 => null
        };
        if (sceneName != null && SceneManager.GetSceneByName(sceneName).isLoaded)
            SceneManager.UnloadSceneAsync(sceneName);
    }
}
