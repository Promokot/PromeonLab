using VContainer;
using VContainer.Unity;

public class ArPreviewSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        // ArPreview registrations — Phase N
    }
}
