using Unity.XR.CoreUtils;
using UnityEngine;

[RequireComponent(typeof(XrRigRecenterer))]
public class FallGuard : MonoBehaviour
{
    private const float FALL_THRESHOLD_Y = -20f;

    private XrRigRecenterer _spawnApplier;
    private XROrigin           _xrOrigin;

    private void Awake()
    {
        _spawnApplier = GetComponent<XrRigRecenterer>();
        _xrOrigin     = GetComponentInChildren<XROrigin>(true);
    }

    private void Update()
    {
        // Room-scale: the rig ROOT stays at the play-area origin; only the camera (HMD) moves with the
        // player. Falling off the map drops the CAMERA's Y, not the root's — so guard on the camera
        // (fall back to the root only if no XROrigin/camera is resolved).
        var probe = _xrOrigin != null && _xrOrigin.Camera != null
            ? _xrOrigin.Camera.transform
            : transform;

        if (probe.position.y < FALL_THRESHOLD_Y)
            _spawnApplier.Respawn();
    }
}
