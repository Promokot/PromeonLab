using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class XRPromeonInteractable : XRBaseInteractable
{
    [SerializeField] private float _tapWindow = 0.5f;

    [Tooltip("If true, the interactable auto-registers colliders found in this GO and its children. " +
             "Default false: only colliders on the same GameObject are used (the right choice for " +
             "rig proxies and most prefabs where the collider sits on the root).")]
    [SerializeField] private bool _includeChildColliders = false;

    [Tooltip("Interaction layer this object's colliders sit on. The XR caster mask must include it " +
             "for the ray to hover this object. SceneObjects for spawned assets; bone proxies are set " +
             "to BoneProxies by the rig builder.")]
    [SerializeField] private InteractionLayer _interactionLayer = InteractionLayer.SceneObjects;

    private ISelectionManager _selectionManager;
    private GizmoController   _gizmoController;
    private IDragStrategy     _dragStrategy = new SingleDragStrategy();
    private SceneNode         _node;

    private enum State { Idle, TriggerPressed, TriggerRotate, GripMove }
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

        // base.Awake auto-discovers GetComponentsInChildren<Collider>(true), which for nested
        // rig proxies would grab every descendant proxy collider — leading to "collider already
        // registered" warnings from XRInteractionManager and root-vs-bone selection ambiguity.
        // Take ownership: clear and re-populate per our policy.
        colliders.Clear();
        var found = _includeChildColliders
            ? GetComponentsInChildren<Collider>(includeInactive: true)
            : GetComponents<Collider>();
        foreach (var c in found)
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);

        // Self-tag: put every registered collider's GameObject on the interaction layer so the XR
        // caster mask (set per context by InteractionMaskBinder) can hover this object. Handles
        // multi-part prefabs whose colliders sit on child meshes.
        ApplyInteractionLayer();
    }

    /// Sets which interaction layer this interactable's colliders live on, and re-applies it.
    /// Used by runtime builders that create the interactable themselves (bone proxies → BoneProxies).
    public void SetInteractionLayer(InteractionLayer layer)
    {
        _interactionLayer = layer;
        ApplyInteractionLayer();
    }

    private void ApplyInteractionLayer()
    {
        foreach (var c in colliders)
            if (c != null) c.gameObject.SetInteractionLayer(_interactionLayer);
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
                if (!IsPrimaryFor(ni)) break;   // ray pierces multiple objects: only the closest hit processes input

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
                    CapturePositionOffset();
                    _state = State.GripMove;
                }
                break;

            case State.TriggerPressed:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    var node = _node;
                    EndInteraction();
                    if (node != null) _selectionManager.Select(node.NodeId);
                    break;
                }
                if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
                {
                    CaptureRotationOffset();
                    _state = State.TriggerRotate;
                }
                break;

            case State.TriggerRotate:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    var pos = transform.position;
                    var rot = transform.rotation;
                    var scl = transform.localScale;
                    EndInteraction();
                    _gizmoController.CommitTransform(transform, pos, rot, scl);
                    break;
                }
                ApplyRotate();
                break;

            case State.GripMove:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                {
                    var pos = transform.position;
                    var rot = transform.rotation;
                    var scl = transform.localScale;
                    EndInteraction();
                    _gizmoController.CommitTransform(transform, pos, rot, scl);
                    break;
                }
                ApplyMove();
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

    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        // Ray (Far) path: primary = whoever owns the current ray hit collider.
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false; // ray exists but hits nothing — not primary
        }

        // True Near path (no ray interactor — physical hand interaction only).
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);

        return false;
    }

    private bool IsObjectSelected()
    {
        if (_node == null || _selectionManager == null) return false;
        return _selectionManager.SelectedNodeId == _node.NodeId;
    }

    private void Lock(NearFarInteractor interactor) => _locked = interactor;

    private void EndInteraction()
    {
        _locked = null;
        _state  = State.Idle;
        // _lastHovering preserved — UpdateLastHovering() refreshes it next Idle frame
    }

    private void CapturePositionOffset()
    {
        var attach = _locked.GetAttachTransform(this);
        // Local-space offset: position swings with attach rotation (ray sweep)
        _grabPosOffset = attach.InverseTransformPoint(transform.position);
    }

    private void CaptureRotationOffset()
    {
        var attach = _locked.GetAttachTransform(this);
        _grabRotOffset = Quaternion.Inverse(attach.rotation) * transform.rotation;
    }

    private void ApplyMove()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetPos = attach.TransformPoint(_grabPosOffset);
        _dragStrategy.Apply(transform, targetPos, transform.rotation, DragMode.PositionOnly);
    }

    private void ApplyRotate()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetRot = attach.rotation * _grabRotOffset;
        _dragStrategy.Apply(transform, transform.position, targetRot, DragMode.RotationOnly);
    }
}
