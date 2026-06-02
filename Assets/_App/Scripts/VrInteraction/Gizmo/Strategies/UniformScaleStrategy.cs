using UnityEngine;

public class UniformScaleStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // factor = exp(gain * metres), applied to all axes
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform _target;
    private Vector3   _originalScale;

    public UniformScaleStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target        = target;
        _originalScale = target.localScale;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        _target.localScale = _originalScale * Mathf.Exp(_gain * s);
    }

    public void EndDrag() => _target = null;
}
