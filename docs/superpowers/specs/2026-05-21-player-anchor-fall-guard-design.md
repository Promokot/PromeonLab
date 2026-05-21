# Player Anchor + Fall Guard — Design Spec

**Date:** 2026-05-21
**Scope:** Sub-project (single implementation plan)
**Goal:** Fix broken player anchor teleport on scene transitions, add fall protection that returns player to anchor when world Y < -20.

---

## Problem

Two duplicating paths currently try to spawn the player at `PlayerSpawnAnchor`:

1. **`PlayerSpawnApplier`** (on `User XR Origin (XR Rig)` prefab, persistent in `Bootstrap.unity`) — subscribes to `SceneManager.sceneLoaded`, directly calls `XRBodyTransformer.QueueTransformation`.
2. **`ModeOrchestrator.OnSceneLoadedForSpawn`** — also subscribes to `sceneLoaded`, publishes `PlayerSpawnRequestedEvent` on EventBus. **No subscriber consumes the event.**

User reports anchor never teleports the player, regardless of mode. There is no fall protection: a misplaced rig or future scene with terrain gaps can drop the player into the void with no recovery.

## Goal

- **One** publish path. **One** consume path. No dead code.
- Player teleported to scene's `PlayerSpawnAnchor` on every additive scene load (including initial Bootstrap → MainMenu).
- Player auto-respawned to last anchor when `transform.position.y < -20f`.
- Diagnostic warnings if the teleport silently fails, so root cause is visible if symptom persists.

## Architecture

**Single publisher chain:**

| Trigger | Publisher | Output |
|---|---|---|
| Bootstrap → MainMenu (initial app start) | `AppBootstrap.OnMainMenuLoaded` | `PlayerSpawnRequestedEvent` |
| Mode transition (MainMenu ↔ VrEditing/Sandbox/etc) | `ModeOrchestrator.OnSceneLoadedForSpawn` (already exists) | `PlayerSpawnRequestedEvent` |
| Player Y < -20 | `FallGuard.Update` → `PlayerSpawnApplier.Respawn()` | direct `QueueTransformation` (no event) |

**Single consumer:** `PlayerSpawnApplier` subscribes to `PlayerSpawnRequestedEvent` via `EventBus`, calls `XRBodyTransformer.QueueTransformation`, and caches `_lastSpawnPos/_lastSpawnRot` for fall-guard reuse.

**Dataflow:**

```
MainMenu load   → AppBootstrap.OnMainMenuLoaded → bus.Publish(PlayerSpawnRequestedEvent)
                                                         ↓
                                              PlayerSpawnApplier.OnSpawn → QueueTransformation + cache

Mode transition → ModeOrchestrator.OnSceneLoadedForSpawn → bus.Publish(...)
                                                         ↓
                                              PlayerSpawnApplier.OnSpawn → QueueTransformation + cache

Y < -20         → FallGuard.Update → PlayerSpawnApplier.Respawn() → QueueTransformation
```

## Components

### `PlayerSpawnApplier` (modified)

Lives on `User XR Origin (XR Rig)` prefab, persistent across all scenes.

**Responsibilities:**
- Subscribe to `PlayerSpawnRequestedEvent` via `EventBus` (replaces `SceneManager.sceneLoaded`).
- Cache last spawn pose for fall-guard reuse.
- Apply teleport via `XRBodyTransformer.QueueTransformation`.
- Expose public `Respawn()` for FallGuard.
- Warn if `_bodyTransformer == null`.

**Public surface:**

```csharp
public class PlayerSpawnApplier : MonoBehaviour
{
    [Inject] public void Construct(EventBus bus);
    public void Respawn();
}
```

**Internal state:**
- `XRBodyTransformer _bodyTransformer` (found in Awake)
- `EventBus _bus` (injected)
- `IDisposable _sub` (event subscription)
- `Vector3 _lastSpawnPos`, `Quaternion _lastSpawnRot`, `bool _hasSpawn`

### `FallGuard` (new)

New file: `Assets/_App/Bootstrap/FallGuard.cs`. Sits on same GameObject as `PlayerSpawnApplier` (User XR Origin rig).

**Responsibilities:**
- Per-frame check: if `transform.position.y < -20f`, call `_spawnApplier.Respawn()`.

**Constants:**
- `FALL_THRESHOLD_Y = -20f` (private const, world-space Y).

No cooldown. Anchor is by contract above -20, so the condition naturally clears after a successful respawn.

### `AppBootstrap` (modified)

Already loads MainMenu additively at app start. Add anchor lookup and event publish after MainMenu loaded.

**New dependency:** `EventBus` (injected).

**Behavior change:** in `OnMainMenuLoaded`, after `SetActiveScene`, walk root GameObjects, find first `PlayerSpawnAnchor`, publish `PlayerSpawnRequestedEvent`.

### `RootLifetimeScope` (minor change)

Add Inject callback for `AppBootstrap` (parallel to existing `PlayerSpawnApplier` registration), so `_bus` gets injected.

### What does not change

- `ModeOrchestrator.OnSceneLoadedForSpawn` — already publishes the right event, keep as-is. No longer dead code after this spec lands.
- `PlayerSpawnAnchor` MonoBehaviour — marker only, unchanged.
- `PlayerSpawnRequestedEvent` struct — unchanged.
- All scene `.unity` files — unchanged (anchors already placed).

## Manual prefab work (user)

- Add `FallGuard` component to `User XR Origin (XR Rig)` prefab variant. `[RequireComponent(typeof(PlayerSpawnApplier))]` ensures the dependency, but the component itself needs to be added by hand.

## Edge cases

| Case | Behavior |
|---|---|
| Scene has no `PlayerSpawnAnchor` | No event published; applier no-op. Legit for Bootstrap.unity. No warning. |
| `_bodyTransformer == null` on rig | Warn logged once in Awake; `ApplyTeleport` silently exits. Visible in console. |
| `Respawn()` called before first spawn event | Warn logged; no-op. Should not happen if Bootstrap publishes on load. |
| Multiple anchors in one scene | First found wins (existing behavior). |
| Anchor placed below -20 | Recursive teleport (every frame fall-guard fires). Treated as scene-author error. Documented, not guarded. |
| Anchor `Transform` destroyed after publish | Safe — pose copied into struct, no live reference. |
| FallGuard fires during scene transition | Respawn to previous anchor; next sceneLoaded refreshes cache. One stray teleport, acceptable. |

## Testing

Manual smoke tests only (MonoBehaviour + scene-bound DI; PlayMode tests overkill for this scope):

1. **Boot test:** launch app → player stands at MainMenu's `PlayerSpawnAnchor`, not at world origin.
2. **Transition test:** enter Sandbox → player at Sandbox's anchor.
3. **Fall test:** drag rig to Y < -20 in Editor → player snapped back to last anchor.
4. **Diagnostic check:** scan console for `PlayerSpawnApplier: no XRBodyTransformer` warning. If present, it identifies the root cause of the reported "anchor never teleports" symptom (independent of this refactor).

## Rollout order

1. Modify `PlayerSpawnApplier` (bus subscription, cache, `Respawn()`).
2. Modify `AppBootstrap` (publish event after MainMenu load).
3. Modify `RootLifetimeScope` (inject `AppBootstrap`).
4. Create `FallGuard.cs`.
5. **Manual:** user adds `FallGuard` to `User XR Origin (XR Rig)` prefab variant.
6. Smoke test.

## Open risks

- **If symptom (c) persists after refactor** — root cause is `_bodyTransformer == null` or `XRBodyTransformer` queue behavior. New warn-log in `PlayerSpawnApplier.Awake` will surface it immediately. Follow-up fix is a one-liner (e.g., `GetComponent` vs `GetComponentInChildren`, or alternative transformer source).
- **One-frame stale cache** — if player falls during scene transition before new spawn event arrives, respawn uses prior anchor. Acceptable.
