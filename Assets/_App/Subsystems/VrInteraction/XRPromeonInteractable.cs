using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class XRPromeonInteractable : XRBaseInteractable
{
    public void RegisterColliders(System.Collections.Generic.IEnumerable<Collider> source)
    {
        if (source == null) return;
        foreach (var c in source)
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);
    }

    [SerializeField] private float _tapWindow     = 0.5f;
    [SerializeField] private float _moveThreshold = 0.5f;   // effectively disabled; tap decided by time
    [SerializeField] private float _nearDistance  = 0.30f;

    private ISelectionManager   _selectionManager;
    private GizmoController     _gizmoController;
    private IDragStrategy       _dragStrategy = new SingleDragStrategy();

    private enum State { Idle, Pressed, Dragging }
    private State               _state;
    private float               _pressTime;
    private Vector3             _attachStartPos;
    private Vector3             _grabPosOffset;
    private Quaternion          _grabRotOffset;
    private DragMode            _dragMode;
    private IXRSelectInteractor _selectingInteractor;

    [Inject]
    public void Construct(ISelectionManager selectionManager, GizmoController gizmoController)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        _selectingInteractor = args.interactorObject;
        _state               = State.Pressed;
        _pressTime           = Time.time;

        var attach = _selectingInteractor.GetAttachTransform(this);
        _attachStartPos = attach.position;
        _grabPosOffset  = transform.position - attach.position;
        _grabRotOffset  = Quaternion.Inverse(attach.rotation) * transform.rotation;

        var interactorTransform = (args.interactorObject as MonoBehaviour)?.transform;
        var distance            = interactorTransform != null
            ? Vector3.Distance(interactorTransform.position, transform.position)
            : float.MaxValue;
        _dragMode = distance <= _nearDistance ? DragMode.RotationOnly : DragMode.PositionOnly;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
        if (_selectingInteractor == null) return;

        var attach = _selectingInteractor.GetAttachTransform(this);

        if (_state == State.Pressed)
        {
            var moved = Vector3.Distance(attach.position, _attachStartPos);
            var held  = Time.time - _pressTime;
            if (held > _tapWindow || moved > _moveThreshold)
                _state = State.Dragging;
        }

        if (_state == State.Dragging)
        {
            var targetPos = attach.position + _grabPosOffset;
            var targetRot = attach.rotation * _grabRotOffset;
            _dragStrategy.Apply(transform, targetPos, targetRot, _dragMode);
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (_state == State.Pressed)
        {
            var sel = GetComponentInParent<Selectable>();
            if (sel != null && _selectionManager != null)
                _selectionManager.Toggle(sel.NodeId);
        }
        else if (_state == State.Dragging && _gizmoController != null)
        {
            _gizmoController.CommitTransform(transform,
                transform.position, transform.rotation, transform.localScale);
        }

        _state               = State.Idle;
        _selectingInteractor = null;
    }
}
