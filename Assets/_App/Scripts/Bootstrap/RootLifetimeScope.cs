using VContainer;
using VContainer.Unity;
using UnityEngine;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private DemoAssetCatalog    _demoAssetCatalog;
    [SerializeField] private ModeTransitionGraph _transitionGraph;
    [SerializeField] private BuiltinAssetLibrary _builtinLibrary;
    [SerializeField] private NavBarConfig        _navBarConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        builder.Register<EventBus>(Lifetime.Singleton);
        builder.Register<AnimationClipboard>(Lifetime.Singleton);
        builder.RegisterInstance(_demoAssetCatalog);
        builder.RegisterInstance(_transitionGraph);
        if (_builtinLibrary != null)
            builder.RegisterInstance(_builtinLibrary);
        else
            Debug.LogError("RootLifetimeScope: _builtinLibrary not assigned!");
        builder.Register<ImportedAssetLibrary>(Lifetime.Singleton);
        builder.Register<SavedAssetLibrary>(Lifetime.Singleton);
        builder.Register<AssetRegistry>(Lifetime.Singleton).As<IAssetRegistry>();
        builder.Register<ModeOrchestrator>(Lifetime.Singleton);
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)

        var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
        if (userPanel != null)
        {
            builder.RegisterInstance(userPanel);
            builder.RegisterBuildCallback(c => c.Inject(userPanel));
        }

        var spawnApplier = Object.FindAnyObjectByType<PlayerSpawnApplier>(FindObjectsInactive.Include);
        if (spawnApplier != null)
            builder.RegisterInstance(spawnApplier);

        var keyboard = Object.FindAnyObjectByType<VrKeyboard>(FindObjectsInactive.Include);
        if (keyboard != null)
            builder.RegisterBuildCallback(c => c.Inject(keyboard));

        // --- Region model (app-lifetime) ---
        // The UserPanel + its nav buttons live on the persistent XR rig, so the router that
        // drives them must share that lifetime. Scene-bound module CONTENT controllers
        // (OutlinerPanel, AssetBrowserPanel, …) stay scene-scoped; scene-bound surfaces like
        // the file browser register themselves against this root router from their scene scope.
        if (_navBarConfig != null)
        {
            builder.RegisterInstance(_navBarConfig).As<IRegionConfig>().AsSelf();
            builder.Register<PanelRegionRouter>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterBuildCallback(c =>
            {
                var router = c.Resolve<PanelRegionRouter>();

                foreach (var nav in Object.FindObjectsByType<RegionNavButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    c.Inject(nav);
                    router.RegisterButton(nav);
                }

                foreach (var rm in Object.FindObjectsByType<RegionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    c.Inject(rm);
                    router.RegisterModule(rm.ModuleId, rm);
                }

                router.ApplyMode(c.Resolve<ModeOrchestrator>().CurrentMode);
            });
        }
        else
        {
            Debug.LogError("RootLifetimeScope: _navBarConfig not assigned — nav buttons will be inert!");
        }
    }
}
