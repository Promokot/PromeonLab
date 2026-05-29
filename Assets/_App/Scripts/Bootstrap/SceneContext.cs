// Root-scoped façade exposing the currently-loaded scene scope's services, or null when no
// scene scope is live. Persistent UI (UserPanel modules) will read scene services THROUGH this
// (later plan) so it never holds a reference that outlives the scene scope. Populated/cleared
// only by SceneContextBinder.
public class SceneContext
{
    public SceneGraph        Graph     { get; private set; }
    public ISelectionManager Selection { get; private set; }
    public CommandStack      Commands  { get; private set; }
    public GizmoController    Gizmo     { get; private set; }
    public AnimationAuthoring Authoring { get; private set; }
    public AnimationClock     Clock     { get; private set; }
    public IRigRuntime       Rig       { get; private set; }

    public bool HasScene => Graph != null;

    public void Bind(SceneGraph graph, ISelectionManager selection, CommandStack commands,
                     GizmoController gizmo, AnimationAuthoring authoring, AnimationClock clock,
                     IRigRuntime rig)
    {
        Graph = graph; Selection = selection; Commands = commands;
        Gizmo = gizmo; Authoring = authoring; Clock = clock; Rig = rig;
    }

    public void Clear()
    {
        Graph = null; Selection = null; Commands = null;
        Gizmo = null; Authoring = null; Clock = null; Rig = null;
    }
}
