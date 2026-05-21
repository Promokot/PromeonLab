using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using VContainer;

// Teleports the XR Rig to the PlayerSpawnAnchor of newly loaded scenes.
// Driven by PlayerSpawnRequestedEvent on EventBus; publishers are AppBootstrap (initial MainMenu)
// and ModeOrchestrator (subsequent scene transitions). Uses XRBodyTransformer.QueueTransformation
// so the locomotion system applies the move correctly — direct transform.SetPositionAndRotation
// is overridden by XRBodyTransformer.
public class PlayerSpawnApplier : MonoBehaviour
{
    private XRBodyTransformer _bodyTransformer;
    private EventBus          _bus;

    private Vector3    _lastSpawnPos;
    private Quaternion _lastSpawnRot;
    private bool       _hasSpawn;

    [Inject]
    public void Construct(EventBus bus)
    {
        if (_bus != null) _bus.Unsubscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
        _bus = bus;
        _bus.Subscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    }

    private void Awake()
    {
        _bodyTransformer = GetComponentInChildren<XRBodyTransformer>(true);
        if (_bodyTransformer == null)
            Debug.LogWarning("PlayerSpawnApplier: no XRBodyTransformer found on rig — teleport will be a no-op.");
    }

    private void OnDestroy()
    {
        _bus?.Unsubscribe<PlayerSpawnRequestedEvent>(OnSpawnRequested);
    }

    private void OnSpawnRequested(PlayerSpawnRequestedEvent e)
    {
        _lastSpawnPos = e.Position;
        _lastSpawnRot = e.Rotation;
        _hasSpawn     = true;
        ApplyTeleport(e.Position, e.Rotation);
    }

    public void Respawn()
    {
        if (!_hasSpawn)
        {
            Debug.LogWarning("PlayerSpawnApplier.Respawn: no cached anchor yet — call ignored.");
            return;
        }
        ApplyTeleport(_lastSpawnPos, _lastSpawnRot);
    }

    private void ApplyTeleport(Vector3 pos, Quaternion rot)
    {
        if (_bodyTransformer == null) return;
        _bodyTransformer.QueueTransformation(
            new TeleportToAnchor(pos, rot),
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
