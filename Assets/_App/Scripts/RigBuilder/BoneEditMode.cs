using UnityEngine;

// Scene-scoped owner of "bone edit mode": at most one rig at a time has its bone proxies interactive.
// Centralises the enter/exit transition (toggle interactivity, hand off selection, announce visibility)
// that Inspector + Animator panels previously each tracked. Inspector drives it; Animator observes the
// BonesVisibilityChangedEvent it publishes (a root-scoped panel cannot inject a scene service).
public class BoneEditMode
{
    private readonly ISelectionManager _selection;
    private readonly ISceneGraph       _graph;
    private readonly EventBus          _bus;

    public string ActiveRigId { get; private set; }
    public bool   IsActive => !string.IsNullOrEmpty(ActiveRigId);

    [VContainer.Inject]
    public BoneEditMode(ISelectionManager selection, ISceneGraph graph, EventBus bus)
    {
        _selection = selection;
        _graph     = graph;
        _bus       = bus;
    }

    // Enter (on=true) or leave (on=false) bone mode for the given rig. No-op when the node is not a rig.
    public void SetActive(string rigNodeId, bool on)
    {
        var rigNode = string.IsNullOrEmpty(rigNodeId) ? null : _graph?.GetNode(rigNodeId);
        var rig     = rigNode != null ? rigNode.GetComponentInChildren<ProxyRigRuntime>(true) : null;
        if (rig == null) return;

        rig.SetBonesInteractive(on);
        _bus?.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = on });

        if (on)
        {
            // Enter: remember the rig and drop its object selection so we start clean inside the rig.
            ActiveRigId = rigNodeId;
            _selection?.Select(null);
        }
        else
        {
            // Leave: forget it and re-select the rig object (cast mask returns to SceneObjects).
            ActiveRigId = null;
            _selection?.Select(rigNodeId);
        }
    }

    // Forget the active rig without touching geometry – used when the rig vanished (scene change).
    public void ClearActive() => ActiveRigId = null;
}
