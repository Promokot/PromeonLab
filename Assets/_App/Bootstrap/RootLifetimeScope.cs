using VContainer;
using VContainer.Unity;
using UnityEngine;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private DemoAssetCatalog _demoAssetCatalog;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        builder.RegisterInstance(_demoAssetCatalog);
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)
        // AnimationClock — Phase 7
    }
}
