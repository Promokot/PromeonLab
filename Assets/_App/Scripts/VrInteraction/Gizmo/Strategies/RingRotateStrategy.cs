using UnityEngine;

public class RingRotateStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // degrees per metre
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform  _target;
    private Vector3    _axisWorld;
    private Quaternion _originalRot;

    public RingRotateStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target      = target;
        _axisWorld   = LocalAxis(target, axis);
        _originalRot = target.rotation;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        var angle = _gain * s;
        _target.rotation = Quaternion.AngleAxis(angle, _axisWorld) * _originalRot;
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
