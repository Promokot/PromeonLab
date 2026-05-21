using UnityEngine;

[RequireComponent(typeof(PlayerSpawnApplier))]
public class FallGuard : MonoBehaviour
{
    private const float FALL_THRESHOLD_Y = -20f;

    private PlayerSpawnApplier _spawnApplier;

    private void Awake() => _spawnApplier = GetComponent<PlayerSpawnApplier>();

    private void Update()
    {
        if (transform.position.y < FALL_THRESHOLD_Y)
            _spawnApplier.Respawn();
    }
}
