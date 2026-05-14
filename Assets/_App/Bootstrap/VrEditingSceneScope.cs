using VContainer;
using VContainer.Unity;

public class VrEditingSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        // Phase 2: PanelRegistry, UiPanelManager
        // Phase 3: UnsavedChangesGuard
        // Phase 4: DemoAssetCatalog, AssetImporter
        // Phase 5: SceneGraph, SelectionManager, CommandStack, GizmoController
        // Phase 6: RigRuntime
        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
    }
}
