using VContainer;
using VContainer.Unity;

public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        // AnimationClock — Phase 7
    }
}
