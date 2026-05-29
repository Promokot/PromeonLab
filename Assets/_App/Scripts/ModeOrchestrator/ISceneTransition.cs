using System;

// Mechanism behind ModeOrchestrator: load a scene single-mode behind a fade, then invoke onLoaded
// once the new scene (and its LifetimeScope) is live. Implemented by the persistent
// SceneTransitionRunner. Kept as an interface so ModeOrchestrator stays unit-testable.
public interface ISceneTransition
{
    bool IsTransitioning { get; }
    void Load(string sceneName, Action onLoaded);
}
