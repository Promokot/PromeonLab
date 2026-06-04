using UnityEngine;

[ExecuteAlways]
public class BoneFollower : MonoBehaviour
{
    [SerializeField] private Transform _proxy;

    // The bone's rest localScale, captured once. The proxy rests at (1,1,1) and the gizmo writes its
    // scale onto proxy.localScale, so we treat that as a MULTIPLIER on the rest scale rather than a
    // direct copy – this preserves any non-identity rest scale and won't break the rig. Child bones
    // stretch with a scaled parent automatically through the localScale hierarchy.
    private Vector3 _baseScale = Vector3.one;
    private bool    _baseCaptured;

    public void SetProxy(Transform proxy) => _proxy = proxy;

    private void Awake() => CaptureBase();

    private void CaptureBase()
    {
        if (_baseCaptured) return;
        _baseScale    = transform.localScale;
        _baseCaptured = true;
    }

    public void Tick()
    {
        if (_proxy == null) return;
        if (!_baseCaptured) CaptureBase();
        transform.localPosition = _proxy.localPosition;
        transform.localRotation = _proxy.localRotation;
        transform.localScale    = Vector3.Scale(_baseScale, _proxy.localScale);
    }

    void LateUpdate() => Tick();
    void OnDestroy() => _proxy = null;
}
