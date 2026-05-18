using UnityEngine;
using VContainer;

// Receives PlayerSpawnRequestedEvent and teleports the XR Rig to the anchor position.
// Lives on User XR Origin (XR Rig) in Bootstrap. Registered in RootLifetimeScope.
// CharacterController must be disabled before moving the transform — otherwise it overrides the position.
public class PlayerSpawnApplier : MonoBehaviour
{
    private EventBus _bus;
    private CharacterController _cc;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()      => _cc = GetComponent<CharacterController>();
    private void Start()     => _bus?.Subscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    private void OnDestroy() => _bus?.Unsubscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(PlayerSpawnRequestedEvent e)
    {
        if (_cc != null) _cc.enabled = false;
        transform.SetPositionAndRotation(e.Position, e.Rotation);
        if (_cc != null) _cc.enabled = true;
    }
}
