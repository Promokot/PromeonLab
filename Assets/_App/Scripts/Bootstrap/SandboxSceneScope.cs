using VContainer;
using VContainer.Unity;
using UnityEngine;

public class SandboxSceneScope : LifetimeScope
{
    [SerializeField] private PanelRegistry _panelRegistry;
    [SerializeField] private GizmoConfig   _gizmoConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_panelRegistry);
        // Scene-scoped: fills/clears the root SceneContext for this scene's lifetime.
        builder.RegisterEntryPoint<SceneContextBinder>();
        if (_gizmoConfig != null) builder.RegisterInstance(_gizmoConfig);
        builder.RegisterInstance(Camera.main);
        builder.Register<UiPanelOrchestrator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionVisualSync>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

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

        // AssetBrowserPanel + FileBrowserSurface are persistent (XR rig / UserPanel) with
        // root-only deps → injected in RootLifetimeScope so they work in every mode.

        var gizmoActivator = Object.FindAnyObjectByType<GizmoActivator>(FindObjectsInactive.Include);
        if (gizmoActivator != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoActivator));

        var gizmoToolsPanel = Object.FindAnyObjectByType<GizmoToolsPanel>(FindObjectsInactive.Include);
        if (gizmoToolsPanel != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoToolsPanel));

        // --- Region model: router + nav buttons live at Root (persistent panel). ---
        // Here we only wire SCENE-bound surfaces (file browser) against the root router.
        builder.RegisterBuildCallback(c =>
        {
            var router = c.Resolve<PanelRegionRouter>();

            // Re-register any scene-resident region members (persistent ones are already
            // registered by RootLifetimeScope; RegisterModule is idempotent).
            foreach (var rm in Object.FindObjectsByType<RegionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.Inject(rm);
                router.RegisterModule(rm.ModuleId, rm);
            }

            router.ApplyMode(c.Resolve<ModeOrchestrator>().CurrentMode);
        });
    }
}
