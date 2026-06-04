using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

// Places the player at world origin (0,0,0) facing +Z on every scene load and on Respawn.
// Uses XROrigin.MatchOriginUpCameraForward + MoveCameraToWorldLocation (camera-relative recenter)
// deferred one frame so the tracked HMD pose from the new scene is applied first.
public class XrRigRecenterer : MonoBehaviour
{
    private XROrigin _xrOrigin;
    private Coroutine _recenterCo;

    private void Awake()
    {
        _xrOrigin = GetComponentInChildren<XROrigin>(true);
        if (_xrOrigin == null)
            Debug.LogWarning("XrRigRecenterer: no XROrigin found on rig – recenter will be a no-op.");
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TeleportToOrigin();

    public void Respawn() => TeleportToOrigin();

    private void TeleportToOrigin()
    {
        if (_recenterCo != null)
            StopCoroutine(_recenterCo);
        _recenterCo = StartCoroutine(RecenterRoutine());
    }

    private IEnumerator RecenterRoutine()
    {
        // Wait one full frame so the new scene's tracked HMD pose is applied,
        // then wait until end-of-frame for extra safety.
        yield return null;
        yield return new WaitForEndOfFrame();

        if (_xrOrigin == null || _xrOrigin.Camera == null)
        {
            Debug.LogWarning("XrRigRecenterer: XROrigin or Camera is null – recenter skipped.");
            yield break;
        }

        // Preserve the camera's current world Y so the rig stays floor-grounded
        // (head is ~1.36 m above the floor; landing at y=0 would sink the rig).
        float camY = _xrOrigin.Camera.transform.position.y;

        // 1. Rotate the rig about the camera so the camera's flattened forward becomes world +Z.
        _xrOrigin.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);

        // 2. Move the camera's XZ to world origin, keeping its world Y.
        _xrOrigin.MoveCameraToWorldLocation(new Vector3(0f, camY, 0f));

        _recenterCo = null;
    }
}
