using UnityEngine;

public class UniformScaleStrategy : IGizmoDragStrategy
{
    private const float MinFactor = 0.01f;

    private Transform _target;
    private Vector3   _originalScale;
    private Vector3   _targetPosAtGrab;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _originalScale   = target.localScale;
        _targetPosAtGrab = target.position;
        _distAtGrab      = Mathf.Max(MinFactor, (handPos - _targetPosAtGrab).magnitude);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = (handPos - _targetPosAtGrab).magnitude;
        var factor  = Mathf.Max(MinFactor, distNow / _distAtGrab);
        _target.localScale = _originalScale * factor;
    }

    public void EndDrag() => _target = null;
}
