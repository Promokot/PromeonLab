using UnityEngine;

public class BoneFollower : MonoBehaviour
{
    private Transform _proxy;

    public void SetProxy(Transform proxy) => _proxy = proxy;

    public void Tick()
    {
        if (_proxy == null) return;
        transform.localPosition = _proxy.localPosition;
        transform.localRotation = _proxy.localRotation;
    }

    void LateUpdate() => Tick();

    void OnDestroy() => _proxy = null;
}
