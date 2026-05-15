using VContainer;
using VContainer.Unity;

public class ArMappingSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        // EnvironmentMapping registrations — Phase N
    }
}
