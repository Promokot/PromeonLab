using VContainer;
using VContainer.Unity;

public class MainMenuSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        // Phase 2: ModeTransitionGraph, ModeOrchestrator
        // Phase 3: UnsavedChangesGuard
    }
}
