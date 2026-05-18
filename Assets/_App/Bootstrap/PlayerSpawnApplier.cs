using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Scene = UnityEngine.SceneManagement.Scene;

// Teleports the XR Rig to the PlayerSpawnAnchor of any additively loaded scene.
// Uses XRBodyTransformer.QueueTransformation so the locomotion system applies the
// move correctly — direct transform.SetPositionAndRotation is overridden by XRBodyTransformer.
public class PlayerSpawnApplier : MonoBehaviour
{
    private XRBodyTransformer _bodyTransformer;

    private void Awake()     => _bodyTransformer = GetComponentInChildren<XRBodyTransformer>(true);
    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
            if (anchor == null) continue;
            _bodyTransformer?.QueueTransformation(
                new TeleportToAnchor(anchor.transform.position, anchor.transform.rotation),
                priority: int.MaxValue);
            return;
        }
    }

    private readonly struct TeleportToAnchor : IXRBodyTransformation
    {
        private readonly Vector3 _pos;
        private readonly Quaternion _rot;

        public TeleportToAnchor(Vector3 pos, Quaternion rot) { _pos = pos; _rot = rot; }

        public void Apply(XRMovableBody body) =>
            body.originTransform.SetPositionAndRotation(_pos, _rot);
    }

    // --- Previous attempts (kept for reference) ---
    // Direct SetPositionAndRotation was overridden by XRBodyTransformer each frame.
    // EventBus approach was unreliable due to injection/subscription timing.
}
