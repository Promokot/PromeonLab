using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using VContainer;

[AddComponentMenu("PromeonLab/Interaction Mask Binder")]
public class InteractionMaskBinder : MonoBehaviour
{
    private EventBus _bus;
    private readonly List<SphereInteractionCaster> _nearCasters = new List<SphereInteractionCaster>();
    private readonly List<CurveInteractionCaster>  _farCasters  = new List<CurveInteractionCaster>();

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

        _uiMask = LayerMask.GetMask("UI"); 
    }

    private void Start() => Apply(); 

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
        int mask = (1 << unity) | _uiMask; 

        foreach (var c in _nearCasters) if (c != null) c.physicsLayerMask = mask;
        foreach (var c in _farCasters)  if (c != null) c.raycastMask      = mask;
    }
}
