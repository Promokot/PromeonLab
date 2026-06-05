using UnityEngine;

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
    private Vector3             _instanceScaleAtGrab;
    private Vector3             _targetScaleAtGrab;

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

        _grabbedHandle = handle;
        _painter.Recolor(handle, _painter.GrabOutlineColor);
        _activeStrategy.BeginDrag(instance, handle.Axis, handPos, handRot);
        _bus?.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
    }

    public void Update(Vector3 handPos, Quaternion handRot)
    {
        if (!_dragActive) return;
        if (_target == null || _instance == null)
        {
            Debug.LogWarning($"[GizmoDriver] OnHandleAborted called, dragActive={_dragActive}");
            Abort();
            return;
        }
        _activeStrategy?.UpdateDrag(handPos, handRot);
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
