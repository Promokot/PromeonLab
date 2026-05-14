using VContainer;
using VContainer.Unity;
using UnityEngine;

public class VrEditingSceneScope : LifetimeScope
{
    [SerializeField] private PanelRegistry _panelRegistry;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        builder.RegisterInstance(_panelRegistry);
        builder.RegisterInstance(Camera.main);
        builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped);
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
        builder.Register<AssetImporter>(Lifetime.Scoped);
        builder.RegisterComponentInHierarchy<UndoKeyHandler>();
        // Phase 6: RigRuntime
        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
    }
}
