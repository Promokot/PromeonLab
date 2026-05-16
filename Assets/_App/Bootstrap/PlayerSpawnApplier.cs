using VContainer;

// Receives PlayerSpawnRequestedEvent and teleports the XR Rig to the anchor position.
// Lives on User XR Origin (XR Rig) in Bootstrap. Registered in RootLifetimeScope.
public class PlayerSpawnApplier : UnityEngine.MonoBehaviour
{
    private EventBus _bus;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Start()     => _bus?.Subscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    private void OnDestroy() => _bus?.Unsubscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(PlayerSpawnRequestedEvent e) =>
        transform.SetPositionAndRotation(e.Position, e.Rotation);
}
