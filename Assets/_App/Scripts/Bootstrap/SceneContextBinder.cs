using System;
using VContainer;
using VContainer.Unity;

// Scene-scoped entry point: the SINGLE place that fills SceneContext from the live scene scope
// and clears it when the scope is torn down (scene unload). Service sets differ per scope
// (Sandbox registers no AnimationAuthoring/AnimationClock/RigRuntime), so each is resolved
// defensively — an unregistered service resolves to null, which SceneContext exposes as null.
public class SceneContextBinder : IStartable, IDisposable
{
    private readonly IObjectResolver _resolver;
    private readonly SceneContext    _ctx;
    private readonly EventBus        _bus;

    public SceneContextBinder(IObjectResolver resolver, SceneContext ctx, EventBus bus)
    {
        _resolver = resolver;
        _ctx      = ctx;
        _bus      = bus;
    }

    public void Start()
    {
        _ctx.Bind(
            Resolve<ISceneGraph>(),
            Resolve<ISelectionManager>(),
            Resolve<CommandStack>(),
            Resolve<GizmoController>(),
            Resolve<AnimationAuthoring>(),
            Resolve<AnimationClock>(),
            Resolve<IRigRuntime>());

        // TEMP smoke log (removed in a later task): confirms bind on scene load.
        UnityEngine.Debug.Log($"[SCTXDBG] SceneContext bound HasScene={_ctx.HasScene}");
        _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
    }

    public void Dispose()
    {
        _ctx.Clear();
        UnityEngine.Debug.Log("[SCTXDBG] SceneContext cleared");
        _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
    }

    private T Resolve<T>() where T : class
    {
        try { return _resolver.Resolve<T>(); }
        catch (VContainerException) { return null; } // service not registered in this scope
    }
}
