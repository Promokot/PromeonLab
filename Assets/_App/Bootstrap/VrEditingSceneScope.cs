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
        builder.Register<SceneAutoSaver>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
        builder.Register<SelectionVisualSync>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<AssetImporter>(Lifetime.Scoped);

        var catcher = Object.FindAnyObjectByType<WorldClickCatcher>(FindObjectsInactive.Include);
        if (catcher != null)
            builder.RegisterBuildCallback(c => c.Inject(catcher));

        var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
        if (undo != null)
            builder.RegisterInstance(undo);

        var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
        if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

        var ikWizard = Object.FindAnyObjectByType<IkSetupWizard>(FindObjectsInactive.Include);
        if (ikWizard != null) builder.RegisterInstance(ikWizard);

        var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
        if (bonePanel != null) builder.RegisterInstance(bonePanel);

        var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
        if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();

        builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        var outliner = Object.FindAnyObjectByType<SceneOutlinerView>(FindObjectsInactive.Include);
        if (outliner != null)
            builder.RegisterBuildCallback(c => c.Inject(outliner));

        var inspector = Object.FindAnyObjectByType<SceneInspectorView>(FindObjectsInactive.Include);
        if (inspector != null)
            builder.RegisterBuildCallback(c => c.Inject(inspector));

        var assetBrowser = Object.FindAnyObjectByType<AssetBrowserModule>(FindObjectsInactive.Include);
        if (assetBrowser != null)
            builder.RegisterBuildCallback(c => c.Inject(assetBrowser));

        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
    }
}
