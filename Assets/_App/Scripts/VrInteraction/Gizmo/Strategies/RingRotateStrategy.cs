using UnityEngine;

public class RingRotateStrategy : IGizmoDragStrategy
{
    private Transform  _target;
    private Vector3    _normalWorld;
    private Vector3    _targetPosAtGrab;
    private Vector3    _grabDirOnPlane;
    private Quaternion _originalRot;
    private bool       _validGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _normalWorld     = LocalAxis(target, axis);
        _originalRot     = target.rotation;
        _targetPosAtGrab = target.position;

        var fromPivot   = handPos - _targetPosAtGrab;
        var grabOnPlane = Vector3.ProjectOnPlane(fromPivot, _normalWorld);
        _validGrab      = grabOnPlane.sqrMagnitude > 1e-8f;
        _grabDirOnPlane = _validGrab ? grabOnPlane.normalized : Vector3.zero;
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null || !_validGrab) return;
        var fromPivot  = handPos - _targetPosAtGrab;
        var nowOnPlane = Vector3.ProjectOnPlane(fromPivot, _normalWorld);
        if (nowOnPlane.sqrMagnitude < 1e-8f) return;
        var nowDir = nowOnPlane.normalized;
        var angle  = Vector3.SignedAngle(_grabDirOnPlane, nowDir, _normalWorld);
        _target.rotation = Quaternion.AngleAxis(angle, _normalWorld) * _originalRot;
    }

    public void EndDrag() => _target = null;

    private static Vector3 LocalAxis(Transform target, AxisKind axis)
    {
        switch (axis)
        {
            case AxisKind.X: return target.right;
            case AxisKind.Y: return target.up;
            default:         return target.forward;
        }
    }
}
