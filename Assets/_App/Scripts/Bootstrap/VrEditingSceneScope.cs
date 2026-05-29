using VContainer;
using VContainer.Unity;
using UnityEngine;

public class VrEditingSceneScope : LifetimeScope
{
    [SerializeField] private PanelRegistry _panelRegistry;
    [SerializeField] private GizmoConfig   _gizmoConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_panelRegistry);
        if (_gizmoConfig != null) builder.RegisterInstance(_gizmoConfig);
        builder.RegisterInstance(Camera.main);
        builder.Register<UiPanelOrchestrator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneAutoSaver>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionVisualSync>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<AssetImporter>(Lifetime.Scoped);

        var catcher = Object.FindAnyObjectByType<WorldClickCatcher>(FindObjectsInactive.Include);
        if (catcher != null)
            builder.RegisterBuildCallback(c => c.Inject(catcher));

        var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
        if (undo != null)
            builder.RegisterBuildCallback(c => c.Inject(undo));

        var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
        if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

        var ikWizard = Object.FindAnyObjectByType<IkWizardPanel>(FindObjectsInactive.Include);
        if (ikWizard != null) builder.RegisterInstance(ikWizard);

        var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
        if (bonePanel != null) builder.RegisterInstance(bonePanel);

        var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
        if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();

        builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        var outliner = Object.FindAnyObjectByType<OutlinerPanel>(FindObjectsInactive.Include);
        if (outliner != null)
            builder.RegisterBuildCallback(c => c.Inject(outliner));

        var inspector = Object.FindAnyObjectByType<InspectorPanel>(FindObjectsInactive.Include);
        if (inspector != null)
            builder.RegisterBuildCallback(c => c.Inject(inspector));

        var assetBrowser = Object.FindAnyObjectByType<AssetBrowserPanel>(FindObjectsInactive.Include);
        if (assetBrowser != null)
            builder.RegisterBuildCallback(c => c.Inject(assetBrowser));

        builder.RegisterEntryPoint<AnimationClock>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<AnimationAuthoring>(Lifetime.Scoped).AsSelf();

        var animPanel = Object.FindAnyObjectByType<AnimatorPanel>(FindObjectsInactive.Include);
        if (animPanel != null)
            builder.RegisterBuildCallback(c => c.Inject(animPanel));

        var gizmoActivator = Object.FindAnyObjectByType<GizmoActivator>(FindObjectsInactive.Include);
        if (gizmoActivator != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoActivator));

        var gizmoToolsPanel = Object.FindAnyObjectByType<GizmoToolsPanel>(FindObjectsInactive.Include);
        if (gizmoToolsPanel != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoToolsPanel));
    }
}
