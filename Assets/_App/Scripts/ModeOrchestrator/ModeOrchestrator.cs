using UnityEngine;

public class ModeOrchestrator
{
    private readonly EventBus            _bus;
    private readonly ModeTransitionGraph _graph;
    private readonly ISceneTransition    _transition;

    private AppMode _current = AppMode.MainMenu;
    public AppMode CurrentMode => _current;

    public ModeOrchestrator(EventBus bus, ModeTransitionGraph graph, ISceneTransition transition)
    {
        _bus        = bus;
        _graph      = graph;
        _transition = transition;
    }

    public void TransitionTo(AppMode target)
    {
        if (_current == target) return;
        if (_transition.IsTransitioning) return;
        if (!_graph.IsAllowed(_current, target))
        {
            Debug.LogWarning($"Transition {_current} → {target} not allowed");
            return;
        }

        var prev = _current;
        _current = target;

        _transition.Load(SceneNameFor(target), () =>
            _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target }));
    }

    private static string SceneNameFor(AppMode mode) => mode switch
    {
        AppMode.MainMenu  => "MainMenu",
        AppMode.VrEditing => "VrEditing",
        AppMode.Sandbox   => "Sandbox",
        _                 => null,
    };
}
