using VContainer;
using VContainer.Unity;
using UnityEngine;

public class MainMenuSceneScope : LifetimeScope
{
    [SerializeField] private ModeTransitionGraph _transitionGraph;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        builder.RegisterInstance(_transitionGraph);
        builder.Register<ModeOrchestrator>(Lifetime.Scoped);
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<ScenePickerPanel>();
        builder.RegisterComponentInHierarchy<MainMenuPanel>();
    }
}
