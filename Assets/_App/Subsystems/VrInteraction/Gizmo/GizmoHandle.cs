using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GizmoHandle : XRBaseInteractable
{
    [SerializeField] private HandleKind _kind;
    [SerializeField] private AxisKind   _axis;

    public HandleKind Kind => _kind;
    public AxisKind   Axis => _axis;

    private GizmoActivator    _activator;
    private NearFarInteractor _locked;
    private NearFarInteractor _lastHovering;

    private enum HandleState { Idle, Dragging }
    private HandleState _state;

    public void Bind(GizmoActivator activator) => _activator = activator;

    protected override void Awake()
    {
        base.Awake();
        // base.Awake auto-adds child colliders; keep only same-GO collider for hit-test precision.
        colliders.Clear();
        foreach (var c in GetComponents<Collider>())
            if (c != null && !colliders.Contains(c)) colliders.Add(c);
        Debug.Log($"[GizmoHandle:{name}] Awake. kind={_kind}, axis={_axis}, colliders={colliders.Count}");
    }

    private int _hoverFrames;
    private bool _hoverLoggedThisSession;
    private bool _gripWasDownLastFrame;

    public override bool IsSelectableBy(IXRSelectInteractor _) => false;

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        if (_state == HandleState.Dragging && (_locked == null || !_locked.isActiveAndEnabled))
        {
            Debug.LogWarning($"[GizmoHandle:{name}] DEFENSIVE ABORT — _locked={(_locked == null ? "null" : "exists,enabled=" + _locked.isActiveAndEnabled)}");
            _activator?.OnHandleAborted();
            _state  = HandleState.Idle;
            _locked = null;
            return;
        }

        switch (_state)
        {
            case HandleState.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null)
                {
                    if (_hoverLoggedThisSession) { Debug.Log($"[GizmoHandle:{name}] hover ENDED"); _hoverLoggedThisSession = false; }
                    break;
                }
                if (!_hoverLoggedThisSession)
                {
                    Debug.Log($"[GizmoHandle:{name}] hover BEGAN. activator={(_activator != null ? "OK" : "NULL")}, primary={IsPrimaryFor(ni)}");
                    _hoverLoggedThisSession = true;
                }
                if (!IsPrimaryFor(ni)) break;
                // Raw-state polling instead of WasPerformed/WasCompleted edge detection.
                // XRI's edge events misfire on interactables with IsSelectableBy=false (select-flow
                // rejection cancels the InputAction same frame, causing WasCompleted next frame).
                bool gripDownNow = ni.selectInput.ReadIsPerformed();
                if (gripDownNow && !_gripWasDownLastFrame)
                {
                    Debug.Log($"[GizmoHandle:{name}] GRIP DOWN — entering Dragging");
                    _locked = ni;
                    _state  = HandleState.Dragging;
                    var attach = ni.GetAttachTransform(this);
                    _activator?.OnHandleGrabbed(this, attach.position, attach.rotation);
                }
                _gripWasDownLastFrame = gripDownNow;
                break;

            case HandleState.Dragging:
                bool gripStillDown = _locked.selectInput.ReadIsPerformed();
                if (!gripStillDown)
                {
                    Debug.Log($"[GizmoHandle:{name}] GRIP UP — releasing");
                    _activator?.OnHandleReleased();
                    _locked = null;
                    _state  = HandleState.Idle;
                    _gripWasDownLastFrame = false;
                    break;
                }
                var a = _locked.GetAttachTransform(this);
                _activator?.OnHandleDragged(a.position, a.rotation);
                break;
        }
    }

    private void UpdateLastHovering()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) { _lastHovering = ni; return; }
        }
    }

    private NearFarInteractor CurrentHoverer()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) return ni;
        }
        return _lastHovering != null && _lastHovering.isActiveAndEnabled ? _lastHovering : null;
    }

    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false;
        }
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);
        return false;
    }
}
