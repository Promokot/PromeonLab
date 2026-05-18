using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

// Teleports the XR Rig to the PlayerSpawnAnchor of any additively loaded scene.
// Subscribes directly to SceneManager.sceneLoaded — no EventBus dependency.
// CharacterController must be disabled before moving the transform.
public class PlayerSpawnApplier : MonoBehaviour
{
    private CharacterController _cc;

    private void Awake()     => _cc = GetComponent<CharacterController>();
    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
            if (anchor == null) continue;
            if (_cc != null) _cc.enabled = false;
            transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
            if (_cc != null) _cc.enabled = true;
            return;
        }
    }

    // --- EventBus-based approach (kept for reference, disabled) ---
    // private EventBus _bus;
    // [Inject] public void Construct(EventBus bus) => _bus = bus;
    // private void Start()     => _bus?.Subscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    // private void OnDestroy() => _bus?.Unsubscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    // private void OnSpawnRequested(PlayerSpawnRequestedEvent e)
    // {
    //     if (_cc != null) _cc.enabled = false;
    //     transform.SetPositionAndRotation(e.Position, e.Rotation);
    //     if (_cc != null) _cc.enabled = true;
    // }
}
