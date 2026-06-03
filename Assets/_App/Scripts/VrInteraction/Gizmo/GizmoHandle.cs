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

    private GizmoDriver    _activator;
    private NearFarInteractor _locked;
    private NearFarInteractor _lastHovering;

    private enum HandleState { Idle, Dragging }
    private HandleState _state;

    public void Bind(GizmoDriver activator) => _activator = activator;

    protected override void Awake()
    {
        base.Awake();
        // base.Awake auto-adds child colliders; keep only same-GO collider for hit-test precision.
        colliders.Clear();
        foreach (var c in GetComponents<Collider>())
            if (c != null && !colliders.Contains(c)) colliders.Add(c);
        // Self-tag onto the GizmoHandles layer so the interactor mask sees handles in gizmo context.
        gameObject.SetInteractionLayer(InteractionLayer.GizmoHandles);
        //Debug.Log($"[GizmoHandle:{name}] Awake. kind={_kind}, axis={_axis}, colliders={colliders.Count}");
    }

    private int _hoverFrames;
    private bool _hoverActive;
    private bool _gripWasDownLastFrame;
    // Расстояние controller→handle в момент grip-down. Используется чтобы вычислять виртуальную
    // hand-позицию как точку перед контроллером (controllerPos + forward*dist). Так поворот
    // контроллера двигает виртуальную точку по сфере — как у XRI regular grab.
    private float _grabRayDistance;

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
                bool primaryHover = ni != null && IsPrimaryFor(ni);
                SetHover(primaryHover);
                if (!primaryHover) break;
                bool gripDownNow = ni.selectInput.ReadValue() > 0.5f;
                // DIAGNOSTIC (закомментировано — спам подтвердил что hold-input работает корректно):
                // Debug.Log($"[GizmoHandle:{name}] f={Time.frameCount} HOVER ReadValue={ni.selectInput.ReadValue():F2} IsPerformed={ni.selectInput.ReadIsPerformed()} WasPerformed={ni.selectInput.ReadWasPerformedThisFrame()} WasCompleted={ni.selectInput.ReadWasCompletedThisFrame()}");
                if (gripDownNow && !_gripWasDownLastFrame)
                {
                    _locked = ni;
                    _state  = HandleState.Dragging;
                    // Drop hover state so the grab color owns the handle; re-evaluated on release.
                    SetHover(false);
                    var ctrl = ni.transform;
                    // Виртуальная hand-точка = controllerPos + forward*dist. dist — расстояние
                    // от контроллера до этой ручки в момент grip. Так поворот контроллера
                    // двигает точку по сфере (как у regular grab — wrist twist = motion).
                    _grabRayDistance = Vector3.Distance(ctrl.position, transform.position);
                    if (_grabRayDistance < 0.05f) _grabRayDistance = 0.05f;
                    var virtualPos = ctrl.position + ctrl.forward * _grabRayDistance;
                    _activator?.OnHandleGrabbed(this, virtualPos, ctrl.rotation);
                }
                _gripWasDownLastFrame = gripDownNow;
                break;

            case HandleState.Dragging:
                bool gripStillDown = _locked.selectInput.ReadValue() > 0.5f;
                // DIAGNOSTIC (закомментировано — спам подтвердил что hold-input сохраняется во время DRAG):
                // Debug.Log($"[GizmoHandle:{name}] f={Time.frameCount} DRAG ReadValue={_locked.selectInput.ReadValue():F2} IsPerformed={_locked.selectInput.ReadIsPerformed()} WasCompleted={_locked.selectInput.ReadWasCompletedThisFrame()}");
                if (!gripStillDown)
                {
                    _activator?.OnHandleReleased();
                    _locked = null;
                    _state  = HandleState.Idle;
                    _gripWasDownLastFrame = false;
                    break;
                }
                var ctrlNow = _locked.transform;
                var virtualPosNow = ctrlNow.position + ctrlNow.forward * _grabRayDistance;
                _activator?.OnHandleDragged(virtualPosNow, ctrlNow.rotation);
                break;
        }
    }

    private void SetHover(bool on)
    {
        if (on == _hoverActive) return;
        _hoverActive = on;
        _activator?.OnHandleHoverChanged(this, on);
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
