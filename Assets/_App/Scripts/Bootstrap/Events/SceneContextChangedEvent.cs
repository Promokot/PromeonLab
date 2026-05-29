// DI-lifecycle signal: published when SceneContext is bound (HasScene = true) or cleared
// (HasScene = false). Distinct from SceneOpenedEvent (scene data) and ModeChangedEvent
// (panel visibility). Consumers subscribe to rebuild/clear their UI when scene services
// appear or disappear.
public struct SceneContextChangedEvent
{
    public bool HasScene;
}
