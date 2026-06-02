using UnityEngine;

public class AxisScaleStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // factor = exp(gain * metres)
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform _target;
    private int       _axisIndex;
    private Vector3   _originalScale;

    public AxisScaleStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target        = target;
        _axisIndex     = (int)axis;
        _originalScale = target.localScale;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        var factor = Mathf.Exp(_gain * s);
        var scl = _originalScale;
        scl[_axisIndex] = _originalScale[_axisIndex] * factor;
        _target.localScale = scl;
    }

    public void EndDrag() => _target = null;
}
