using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class XRPromeonInteractable : XRBaseInteractable
{
    [SerializeField] private float _tapWindow = 0.5f;

    private ISelectionManager _selectionManager;
    private GizmoController   _gizmoController;
    private IDragStrategy     _dragStrategy = new SingleDragStrategy();
    private SceneNode         _node;

    private enum State { Idle, TriggerPressed, TriggerMove, GripRotate }
    private State              _state;
    private NearFarInteractor  _locked;
    private NearFarInteractor  _lastHovering;
    private float              _pressTime;
    private Vector3            _grabPosOffset;
    private Quaternion         _grabRotOffset;

    public void RegisterColliders(IEnumerable<Collider> source)
    {
        if (source == null) return;
        foreach (var c in source)
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);
    }

    protected override void Awake()
    {
        base.Awake();
        _node = GetComponentInParent<SceneNode>();
    }

    [Inject]
    public void Construct(ISelectionManager selectionManager, GizmoController gizmoController)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
    }

    // Disable XRI standard select-flow. We read inputs directly. Hover still works
    // (IsHoverableBy stays default true).
    public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        // Defensive: dangling lock from destroyed/disabled interactor.
        if (_state != State.Idle && (_locked == null || !_locked.isActiveAndEnabled))
        { EndInteraction(); return; }

        if (_selectionManager == null || _gizmoController == null) return;

        switch (_state)
        {
            case State.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null) break;

                // Order matters: trigger checked first → wins same-frame ties.
                if (ni.activateInput.ReadWasPerformedThisFrame())
                {
                    Lock(ni);
                    _pressTime = Time.time;
                    _state = State.TriggerPressed;
                    break;
                }

                if (ni.selectInput.ReadWasPerformedThisFrame() && IsObjectSelected())
                {
                    Lock(ni);
                    CaptureRotationOffset();
                    _state = State.GripRotate;
                }
                break;

            case State.TriggerPressed:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    if (_node != null) _selectionManager.Toggle(_node.NodeId);
                    EndInteraction();
                    break;
                }
                if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
                {
                    CapturePositionOffset();
                    _state = State.TriggerMove;
                }
                break;

            case State.TriggerMove:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    _gizmoController.CommitTransform(transform,
                        transform.position, transform.rotation, transform.localScale);
                    EndInteraction();
                    break;
                }
                ApplyMove();
                break;

            case State.GripRotate:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                {
                    _gizmoController.CommitTransform(transform,
                        transform.position, transform.rotation, transform.localScale);
                    EndInteraction();
                    break;
                }
                ApplyRotate();
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
        // 1-frame jitter fallback.
        return _lastHovering != null && _lastHovering.isActiveAndEnabled ? _lastHovering : null;
    }

    private bool IsObjectSelected()
    {
        if (_node == null || _selectionManager == null) return false;
        var ids = _selectionManager.SelectedIds;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == _node.NodeId) return true;
        return false;
    }

    private void Lock(NearFarInteractor interactor) => _locked = interactor;

    private void EndInteraction()
    {
        _locked       = null;
        _lastHovering = null;
        _state        = State.Idle;
    }

    private void CapturePositionOffset()
    {
        var attach = _locked.GetAttachTransform(this);
        _grabPosOffset = transform.position - attach.position;
    }

    private void CaptureRotationOffset()
    {
        var attach = _locked.GetAttachTransform(this);
        _grabRotOffset = Quaternion.Inverse(attach.rotation) * transform.rotation;
    }

    private void ApplyMove()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetPos = attach.position + _grabPosOffset;
        _dragStrategy.Apply(transform, targetPos, transform.rotation, DragMode.PositionOnly);
    }

    private void ApplyRotate()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetRot = attach.rotation * _grabRotOffset;
        _dragStrategy.Apply(transform, transform.position, targetRot, DragMode.RotationOnly);
    }
}
