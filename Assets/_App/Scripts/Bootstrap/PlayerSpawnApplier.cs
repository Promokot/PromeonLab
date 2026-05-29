using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Scene = UnityEngine.SceneManagement.Scene;

// Teleports the XR Rig to world origin on every scene load (single-mode transitions).
// Uses XRBodyTransformer.QueueTransformation so the locomotion system applies the move correctly —
// direct transform.SetPositionAndRotation is overridden by XRBodyTransformer.
public class PlayerSpawnApplier : MonoBehaviour
{
    private XRBodyTransformer _bodyTransformer;

    private void Awake()
    {
        _bodyTransformer = GetComponentInChildren<XRBodyTransformer>(true);
        if (_bodyTransformer == null)
            Debug.LogWarning("PlayerSpawnApplier: no XRBodyTransformer found on rig — teleport will be a no-op.");
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TeleportToOrigin();

    public void Respawn() => TeleportToOrigin();

    private void TeleportToOrigin()
    {
        if (_bodyTransformer == null) return;
        _bodyTransformer.QueueTransformation(
            new TeleportToAnchor(Vector3.zero, Quaternion.identity),
            priority: int.MaxValue);
    }

    private readonly struct TeleportToAnchor : IXRBodyTransformation
    {
        private readonly Vector3 _pos;
        private readonly Quaternion _rot;

        public TeleportToAnchor(Vector3 pos, Quaternion rot) { _pos = pos; _rot = rot; }

        public void Apply(XRMovableBody body) =>
            body.originTransform.SetPositionAndRotation(_pos, _rot);
    }
}
