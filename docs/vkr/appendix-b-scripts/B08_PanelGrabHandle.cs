using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Grip-based grab for the UserPanel. Hover the handle bar with a NearFarInteractor, hold grip
// (selectInput in this project's mapping) to move the panel in world space (position only),
// release to drop. Mirrors XRPromeonInteractable's direct-input model; XRI standard select-flow
// stays disabled (IsSelectableBy => false) so we read input ourselves and never fight the gizmo.
public class PanelGrabHandle : XRBaseInteractable
{
    [SerializeField] private UserPanel _panel;

    [Header("Visual")]
    [SerializeField] private Graphic _handleGraphic;                                  // tinted on hover/grab
    [SerializeField] private Color   _normalColor = new Color(0f,    0f,    0f,    0.71f);
    [SerializeField] private Color   _hoverColor  = new Color(0.80f, 0.80f, 0.85f, 0.45f);
    [SerializeField] private Color   _grabColor   = new Color(0.45f, 0.50f, 0.55f, 0.70f);

    private enum State { Idle, Grabbing }
    private State              _state;
    private NearFarInteractor  _locked;
    private NearFarInteractor  _lastHovering;
    private Vector3            _grabOffset;   // panel world position expressed in attach-local space

    // Pure helpers – unit-testable grab-offset math. The offset is captured in attach-LOCAL
    // space; this assumes the interactor's attach transform has unit scale (always true for XRI
    // attach points), so InverseTransformPoint/TransformPoint round-trip exactly.
    public static Vector3 CaptureOffset(Transform attach, Vector3 worldPos)
        => attach.InverseTransformPoint(worldPos);
    public static Vector3 ApplyOffset(Transform attach, Vector3 localOffset)
        => attach.TransformPoint(localOffset);

    protected override void Awake()
    {
        base.Awake();
        // base.Awake auto-discovers GetComponentsInChildren<Collider>(true). Take ownership:
        // use only colliders on this GameObject (the handle bar's own BoxCollider).
        colliders.Clear();
        foreach (var c in GetComponents<Collider>())
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);

        if (colliders.Count == 0)
            Debug.LogError($"[PanelGrabHandle] No Collider on '{name}'. The handle can never be hovered/grabbed – add a BoxCollider.", this);

        ApplyColor(_normalColor);
    }

    // Read inputs directly; hover stays enabled (IsHoverableBy default true).
    public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        if (_state != State.Grabbing) ApplyColor(_hoverColor);
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        base.OnHoverExited(args);
        if (_state != State.Grabbing) ApplyColor(_normalColor);
    }

    private void ApplyColor(Color c)
    {
        if (_handleGraphic != null) _handleGraphic.color = c;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
        if (_panel == null) return;

        // Defensive: dangling lock from a destroyed/disabled interactor.
        if (_state == State.Grabbing && (_locked == null || !_locked.isActiveAndEnabled))
        { EndGrab(); return; }

        switch (_state)
        {
            case State.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null) break;
                if (!IsPrimaryFor(ni)) break;            // only the closest ray hit processes input
                if (ni.selectInput.ReadWasPerformedThisFrame())
                {
                    _locked     = ni;
                    var attach  = _locked.GetAttachTransform(this);
                    _grabOffset = CaptureOffset(attach, _panel.transform.position);
                    _panel.SetDragging(true);
                    _state = State.Grabbing;
                    ApplyColor(_grabColor);
                }
                break;

            case State.Grabbing:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                { EndGrab(); break; }
                var a = _locked.GetAttachTransform(this);
                _panel.MoveTo(ApplyOffset(a, _grabOffset));
                break;
        }
    }

    private void EndGrab()
    {
        if (_panel != null) _panel.SetDragging(false);
        _locked = null;
        _state  = State.Idle;
        ApplyColor(isHovered ? _hoverColor : _normalColor);
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
        // 1-frame jitter fallback.
        return _lastHovering != null && _lastHovering.isActiveAndEnabled ? _lastHovering : null;
    }

    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false; // ray exists but hits nothing – not primary
        }

        // True near path (physical hand, no ray).
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);

        return false;
    }
}
