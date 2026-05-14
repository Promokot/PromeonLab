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
        // Phase 4: DemoAssetCatalog, AssetImporter
        // Phase 5: SceneGraph, SelectionManager, CommandStack, GizmoController
        // Phase 6: RigRuntime
        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
    }
}
