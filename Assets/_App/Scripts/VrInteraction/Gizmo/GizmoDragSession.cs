using UnityEngine;

// One in-flight gizmo drag: snapshots the target pose, resolves the per-handle strategy, drives the
// gizmo instance as the primary source-of-truth, and syncs the target back per strategy. Extracted
// from GizmoActivator (A2) so the activator only owns spawn/visibility. Pure helper (no MonoBehaviour).
//
// The gizmo instance is primary: a strategy mutates _instance directly each frame and the target is
// pulled to follow it (position for move, rotation for rotate, proportional scale factor for scale).
// Mode is frozen for the duration of a drag (the activator's OnModeChanged bails while dragging), so
// the mode captured at Begin is the mode used on release/abort.
public class GizmoDragSession
{
    private readonly GizmoConfig           _config;
    private readonly EventBus              _bus;
    private readonly GizmoHighlightPainter _painter;

    private bool               _dragActive;
    private IGizmoDragStrategy  _activeStrategy;
    private GizmoHandle         _grabbedHandle;
    private Vector3             _originalPos;
    private Quaternion          _originalRot;
    private Vector3             _originalScale;
    // Scale-inversion baselines: при scale-drag гизмо мутирует свою localScale, а target
    // получает пропорциональный фактор (instance.scale / instanceAtGrab → target = targetAtGrab * factor).
    private Vector3             _instanceScaleAtGrab;
    private Vector3             _targetScaleAtGrab;

    // Per-grab context, captured at Begin (all frozen for the drag's duration).
    private Transform      _instance;
    private Transform      _target;
    private string         _targetNodeId;
    private GizmoHierarchy _hierarchy;
    private GizmoMode      _modeAtGrab;
    private float          _resetSize;

    public GizmoDragSession(GizmoConfig config, EventBus bus, GizmoHighlightPainter painter)
    {
        _config  = config;
        _bus     = bus;
        _painter = painter;
    }

    public bool IsActive => _dragActive;
    public bool IsGrabbing(GizmoHandle handle) => _dragActive && _grabbedHandle == handle;

    public void Begin(Transform instance, Transform target, string targetNodeId, GizmoHierarchy hierarchy,
                      GizmoMode mode, float resetSize, GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
        if (_dragActive || target == null || handle == null || instance == null) return;
        _instance     = instance;
        _target       = target;
        _targetNodeId = targetNodeId;
        _hierarchy    = hierarchy;
        _modeAtGrab   = mode;
        _resetSize    = resetSize;

        _dragActive          = true;
        _originalPos         = target.position;
        _originalRot         = target.rotation;
        _originalScale       = target.localScale;
        _instanceScaleAtGrab = instance.localScale;
        _targetScaleAtGrab   = target.localScale;
        _activeStrategy      = ResolveStrategy(handle);
        _hierarchy?.OnHandleGrabbed(handle);

        // Highlight the grabbed handle (mesh material + outline) in the distinct active color;
        // restored in EndDragInternal. Uses the handle→parts map, so it lands on the renderer even
        // when it sits on a child of the handle GO (the scaler).
        _grabbedHandle = handle;
        _painter.Recolor(handle, _painter.GrabOutlineColor);
        // Гизмо — primary: strategy мутирует _instance во всех режимах.
        // Target подтягивается за гизмо в Update в зависимости от типа стратегии.
        _activeStrategy.BeginDrag(instance, handle.Axis, handPos, handRot);
        _bus?.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
    }

    public void Update(Vector3 handPos, Quaternion handRot)
    {
        if (!_dragActive) return;
        if (_target == null || _instance == null)
        {
            Debug.LogWarning($"[GizmoActivator] OnHandleAborted called, dragActive={_dragActive}");
            Abort();
            return;
        }
        _activeStrategy?.UpdateDrag(handPos, handRot);
        // Target follows гизмо. Тип синка зависит от стратегии.
        switch (_activeStrategy)
        {
            case AxisMoveStrategy:
                _target.position = _instance.position;
                break;
            case RingRotateStrategy:
                _target.rotation = _instance.rotation;
                break;
            case AxisScaleStrategy:
            case UniformScaleStrategy:
                // Гизмо сама масштабируется визуально → target получает тот же фактор изменения,
                // применённый к собственной исходной шкале (не к гизмовой bounds-fit шкале).
                var inst = _instance.localScale;
                var fX = SafeRatio(inst.x, _instanceScaleAtGrab.x);
                var fY = SafeRatio(inst.y, _instanceScaleAtGrab.y);
                var fZ = SafeRatio(inst.z, _instanceScaleAtGrab.z);
                _target.localScale = new Vector3(
                    _targetScaleAtGrab.x * fX,
                    _targetScaleAtGrab.y * fY,
                    _targetScaleAtGrab.z * fZ);
                break;
        }
    }

    public void End()
    {
        if (!_dragActive) return;
        _activeStrategy?.EndDrag();
        _hierarchy?.OnHandleReleased(_modeAtGrab);
        if (_target == null) { EndDragInternal(); return; }
        // Transform is already live from the drag; undo recording was removed, so keep the final pose.
        // Bounds-fit frozen: a scale drag mutated the instance scale, so reset it back to the fixed size.
        if (_instance != null && _config != null)
            _instance.localScale = Vector3.one * _resetSize;
        EndDragInternal();
    }

    public void Abort()
    {
        if (!_dragActive) return;
        if (_target != null)
        {
            _target.position   = _originalPos;
            _target.rotation   = _originalRot;
            _target.localScale = _originalScale;
        }
        _activeStrategy?.EndDrag();
        if (_hierarchy != null) _hierarchy.OnHandleReleased(_modeAtGrab);
        EndDragInternal();
    }

    private void EndDragInternal()
    {
        // Restore the grabbed handle (mesh material + outline) from the active color to its base.
        if (_grabbedHandle != null)
        {
            _painter.Restore(_grabbedHandle);
            _grabbedHandle = null;
        }

        var id = _targetNodeId;
        _activeStrategy = null;
        _dragActive     = false;
        _bus?.Publish(new GizmoDragEndedEvent { TargetNodeId = id });
    }

    private static float SafeRatio(float num, float den) => Mathf.Abs(den) < 1e-6f ? 1f : num / den;

    private IGizmoDragStrategy ResolveStrategy(GizmoHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:     return new AxisMoveStrategy();
            case HandleKind.ScaleAxis:    return new AxisScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
            case HandleKind.ScaleUniform: return new UniformScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
            case HandleKind.RotateRing:   return new RingRotateStrategy(_config.RotGain, _config.DeadzoneMeters);
            default:                      return new AxisMoveStrategy();
        }
    }
}
