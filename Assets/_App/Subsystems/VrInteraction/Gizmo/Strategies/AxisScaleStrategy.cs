using UnityEngine;

public class AxisScaleStrategy : IGizmoDragStrategy
{
    private const float MinFactor = 0.01f;

    private Transform _target;
    private Vector3   _axisWorld;
    private int       _axisIndex;
    private Vector3   _originalScale;
    private Vector3   _targetPosAtGrab;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _axisIndex       = (int)axis;
        _axisWorld       = LocalAxis(target, axis);
        _originalScale   = target.localScale;
        _targetPosAtGrab = target.position;
        _distAtGrab      = Mathf.Max(MinFactor, Vector3.Dot(handPos - _targetPosAtGrab, _axisWorld));
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = Vector3.Dot(handPos - _targetPosAtGrab, _axisWorld);
        var factor  = Mathf.Max(MinFactor, distNow / _distAtGrab);
        var scl     = _originalScale;
        scl[_axisIndex] = _originalScale[_axisIndex] * factor;
        _target.localScale = scl;
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
