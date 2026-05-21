using UnityEngine;

public class AxisMoveStrategy : IGizmoDragStrategy
{
    private Transform _target;
    private Vector3   _axisWorld;
    private Vector3   _originalTargetPos;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target            = target;
        _axisWorld         = LocalAxis(target, axis);
        _originalTargetPos = target.position;
        _distAtGrab        = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
        var delta   = distNow - _distAtGrab;
        _target.position = _originalTargetPos + _axisWorld * delta;
    }

    public void EndDrag()
    {
        _target = null;
    }

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
