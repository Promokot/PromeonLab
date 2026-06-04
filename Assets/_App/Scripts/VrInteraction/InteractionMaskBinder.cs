using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using VContainer;

// Place this on the XR rig root (an ancestor of both hands' NearFarInteractors).

/// Switches the XR interactors' physics cast masks to match the current interaction context, so the
/// ray sees only the relevant interactive layer and passes through everything else (the floor, the
/// selected object behind its gizmo, the body in front of a bone). Persistent (lives with the XR rig);
/// listens to the single root EventBus.
///
/// Context → mask:
///   gizmo up (panel open + selection) → GizmoHandles   (modal: target behind can't be mis-hit)
///   bones visible                     → BoneProxies     (body excluded → ray passes through to bone)
///   otherwise                         → SceneObjects    (normal object selection)
[AddComponentMenu("PromeonLab/Interaction Mask Binder")]
public class InteractionMaskBinder : MonoBehaviour
{
    private EventBus _bus;
    private readonly List<SphereInteractionCaster> _nearCasters = new List<SphereInteractionCaster>();
    private readonly List<CurveInteractionCaster>  _farCasters  = new List<CurveInteractionCaster>();

    // UI is always interactable: the uGUI graphic raycast (TrackedDeviceGraphicRaycaster) reads its
    // mask from the same caster raycastMask (CurveInteractionCaster sets uiModel.raycastLayerMask =
    // m_RaycastMask), so the UI layer MUST stay in every context mask or buttons go dead.
    private int _uiMask;

    private bool _bonesMode;
    private bool _panelOpen;
    private bool _hasSelection;

    [Inject]
    public void Construct(EventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibility);
        _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnGizmoPanelOpened);
        _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnGizmoPanelClosed);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void Awake()
    {
        // Auto-discover both hands' interactors from the rig hierarchy this component sits on
        // (avoids fragile inspector references; the binder is placed on the XR rig root).
        foreach (var ix in GetComponentsInChildren<NearFarInteractor>(includeInactive: true))
        {
            var near = ix.GetComponent<SphereInteractionCaster>();
            var far  = ix.GetComponent<CurveInteractionCaster>();
            if (near != null) _nearCasters.Add(near);
            if (far  != null) _farCasters.Add(far);
        }
        if (_nearCasters.Count == 0 && _farCasters.Count == 0)
            Debug.LogError("InteractionMaskBinder: found no NearFarInteractor casters in children – " +
                           "place this component on the XR rig root.");

        _uiMask = LayerMask.GetMask("UI"); // always-on UI channel (uGUI shares the caster mask)
    }

    private void Start() => Apply(); // establish the default (Object) context at startup

    private void OnDestroy()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibility);
        _bus.Unsubscribe<GizmoToolsPanelOpenedEvent>(OnGizmoPanelOpened);
        _bus.Unsubscribe<GizmoToolsPanelClosedEvent>(OnGizmoPanelClosed);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void OnBonesVisibility(BonesVisibilityChangedEvent e) { _bonesMode    = e.Visible;               Apply(); }
    private void OnGizmoPanelOpened(GizmoToolsPanelOpenedEvent _) { _panelOpen    = true;                   Apply(); }
    private void OnGizmoPanelClosed(GizmoToolsPanelClosedEvent _) { _panelOpen    = false;                  Apply(); }
    private void OnSelectionChanged(SelectionChangedEvent e)      { _hasSelection = e.SelectedNodeId != null; Apply(); }

    // A scene/mode transition reuses this persistent binder. Scene-scoped publishers (selection,
    // gizmo panel, bones toggle) do NOT re-emit their "off" state for the new scene, so without an
    // explicit reset the caster mask can stay stuck on GizmoHandles/BoneProxies from the previous
    // session – the ray then can't hit SceneObjects and nothing is clickable. Reset to the default
    // object-selection context. ModeChangedEvent fires after the new scene/scope are live.
    private void OnModeChanged(ModeChangedEvent _)
    {
        _bonesMode    = false;
        _panelOpen    = false;
        _hasSelection = false;
        Apply();
    }

    private void Apply()
    {
        InteractionLayer context =
            (_panelOpen && _hasSelection) ? InteractionLayer.GizmoHandles
            : _bonesMode                  ? InteractionLayer.BoneProxies
            :                               InteractionLayer.SceneObjects;

        int unity = InteractionLayers.UnityLayer(context);
        if (unity < 0) return;
        int mask = (1 << unity) | _uiMask; // context layer + always-on UI

        foreach (var c in _nearCasters) if (c != null) c.physicsLayerMask = mask;
        foreach (var c in _farCasters)  if (c != null) c.raycastMask      = mask;
    }
}
