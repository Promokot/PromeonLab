using VContainer;
using VContainer.Unity;
using UnityEngine;

public class VrEditingSceneScope : LifetimeScope
{
    [SerializeField] private PanelRegistry _panelRegistry;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_panelRegistry);
        builder.RegisterInstance(Camera.main);
        builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
        builder.Register<AssetImporter>(Lifetime.Scoped);

        var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
        if (undo != null)
            builder.RegisterInstance(undo);

        var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
        if (userPanel != null)
            builder.RegisterInstance(userPanel);

        builder.RegisterComponentInHierarchy<RigRuntime>().AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<IkSetupWizard>();
        builder.RegisterComponentInHierarchy<BoneInspectorPanel>();
        builder.RegisterComponentInHierarchy<PropertyPanel>().AsImplementedInterfaces().AsSelf();
        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
    }
}
