using UnityEngine;
using UnityEngine.SceneManagement;

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

        UnloadCurrentScene(prev);
        LoadScene(target);

        _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target });
    }

    private void LoadScene(AppMode mode)
    {
        var sceneName = mode switch
        {
            AppMode.MainMenu  => "MainMenu",
            AppMode.VrEditing => "VrEditing",
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
            _                 => null
        };
        if (sceneName != null && SceneManager.GetSceneByName(sceneName).isLoaded)
            SceneManager.UnloadSceneAsync(sceneName);
    }
}
