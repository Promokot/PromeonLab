using VContainer;
using VContainer.Unity;

public class MainMenuSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<SceneDirtyTracker>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<ScenePickerPanel>();
        builder.RegisterComponentInHierarchy<MainMenuPanel>();
    }
}
