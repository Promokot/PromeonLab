// Root-scoped façade exposing the currently-loaded scene scope's services, or null when no
// scene scope is live. Persistent UI (UserPanel modules) will read scene services THROUGH this
// (later plan) so it never holds a reference that outlives the scene scope. Populated/cleared
// only by SceneContextBinder.
public class SceneContext
{
    public SceneGraph        Graph     { get; private set; }
    public ISelectionManager Selection { get; private set; }
    public AnimationAuthoring Authoring { get; private set; }
    public AnimationClock     Clock     { get; private set; }

    public bool HasScene => Graph != null;

    public void Bind(SceneGraph graph, ISelectionManager selection,
                     AnimationAuthoring authoring, AnimationClock clock)
    {
        Graph = graph; Selection = selection;
        Authoring = authoring; Clock = clock;
    }

    public void Clear()
    {
        Graph = null; Selection = null;
        Authoring = null; Clock = null;
    }
}
