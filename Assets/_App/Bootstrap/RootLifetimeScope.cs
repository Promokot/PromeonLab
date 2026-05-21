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

        var assetBrowser = Object.FindAnyObjectByType<AssetBrowserModule>(FindObjectsInactive.Include);
        if (assetBrowser != null)
            builder.RegisterBuildCallback(c => c.Inject(assetBrowser));

        var spawnApplier = Object.FindAnyObjectByType<PlayerSpawnApplier>(FindObjectsInactive.Include);
        if (spawnApplier != null)
            builder.RegisterInstance(spawnApplier);

        var keyboard = Object.FindAnyObjectByType<VrKeyboard>(FindObjectsInactive.Include);
        if (keyboard != null)
            builder.RegisterBuildCallback(c => c.Inject(keyboard));
    }
}
