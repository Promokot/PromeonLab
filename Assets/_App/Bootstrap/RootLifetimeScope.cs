using VContainer;
using VContainer.Unity;

public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Singleton);
        // Phase 3: PathProvider, AppStorage
        // Phase 4: DemoAssetCatalog, AssetImporter (moved to VrEditingSceneScope)
        // Phase 7: AnimationClock
    }
}
