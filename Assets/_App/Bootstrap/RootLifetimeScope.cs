using VContainer;
using VContainer.Unity;
using UnityEngine;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private DemoAssetCatalog    _demoAssetCatalog;
    [SerializeField] private ModeTransitionGraph _transitionGraph;
    [SerializeField] private BuiltinAssetLibrary _builtinLibrary;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        builder.Register<EventBus>(Lifetime.Singleton);
        builder.RegisterInstance(_demoAssetCatalog);
        builder.RegisterInstance(_transitionGraph);
        builder.RegisterInstance(_builtinLibrary);
        builder.Register<ImportedAssetLibrary>(Lifetime.Singleton);
        builder.Register<SavedAssetLibrary>(Lifetime.Singleton);
        builder.Register<ModeOrchestrator>(Lifetime.Singleton);
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)
        // AnimationClock — Phase 7

        var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
        if (userPanel != null)
        {
            builder.RegisterInstance(userPanel);
            builder.RegisterBuildCallback(c => c.Inject(userPanel));
        }

        var spawnApplier = Object.FindAnyObjectByType<PlayerSpawnApplier>(FindObjectsInactive.Include);
        if (spawnApplier != null)
        {
            builder.RegisterInstance(spawnApplier);
            builder.RegisterBuildCallback(c => c.Inject(spawnApplier));
        }
    }
}
