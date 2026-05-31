# Interaction Layer Priority — Design (v2: XRI-native context masks)

**Date:** 2026-05-31
**Status:** Approved (brainstorm v2). Supersedes the v1 resolver design below.

## Problem

The XR ray can't reach an interactive thing that sits behind a collider: a gizmo handle behind
the floor, a bone behind the body mesh. We want occluders to not block interaction, and we want the
right thing selected when interactive things overlap.

## Why v1 (RayInteractionResolver) was wrong — recorded so we don't repeat it

v1 added a `RayInteractionResolver` doing its own `Physics.RaycastAll` + layer-priority, called from
`XRPromeonInteractable.IsPrimaryFor` / `GizmoHandle.IsPrimaryFor` via `ni.GetComponentInChildren<XRRayInteractor>()`.

Hard evidence from the live rig (`User XR Origin (XR Rig)`, both hands):
- The interactor is **`NearFarInteractor`** — there is **no `XRRayInteractor`** component. So
  `GetComponentInChildren<XRRayInteractor>()` returns null → the resolver branch never executed.
  Selection always went through the existing Near path (`interactablesHovered[0] == this`), i.e. XRI's
  own hover. **The resolver was dead code on this rig.**
- Selection is gated by what the interactor's casters HOVER. The casters:
  - `SphereInteractionCaster.physicsLayerMask = 1` (Default only)
  - `CurveInteractionCaster.raycastMask = -2147483615` = `0x80000021` (Default + UI + layer 31)
  Neither includes the new interaction layers (13/14/15).
- So when v1 moved objects onto `SceneObjects`(13), the casters could no longer see them → no hover →
  clicking did nothing (outliner still worked — it bypasses XRI via `SelectionManager`).

**Lesson:** on this project the *interactor's physics cast mask* is the real gate. Don't build a parallel
raycast; configure the casters.

## Approach (v2): context-driven cast masks

Disambiguation is done by **what layer the casters are allowed to see in the current context**, not by a
priority comparison. In each context the interactor's mask contains a single interactive layer, so there
is no overlap contest and no priority logic is needed. Occluders live on a layer outside the interactor
mask, so the ray passes through them.

### Layers (reuse existing + one new)

| Unity layer | Role | In interactor mask? |
|---|---|---|
| `GizmoHandles` (14) | gizmo handles | only in Gizmo context |
| `BoneProxies` (15) | bone proxies | only in Bone context |
| `SceneObjects` (13) | spawned/selectable assets | only in Object context |
| `Environment` (**new**) | floor / scenery / occluders | **never** — ray passes through; reserved for a future floor-placement raycast |
| `UiPanels` (7), `UI` (5) | UI | handled by XRI's UI channel, not the physics mask |

### Object → layer assignment (variant C: the interactable owns it)

Each interactable tags its own colliders in `Awake`, from a serialized `InteractionLayer _layer`:
- `XRPromeonInteractable` (spawned assets, bone proxies) — tags every registered collider's GameObject
  (handles multi-part prefabs whose colliders sit on children, e.g. the toilet).
- `GizmoHandle` — tags itself `GizmoHandles`.
- The bone builder sets `_layer = BoneProxies` on proxy interactables it creates.
- Default `_layer = SceneObjects`.
No tagging in spawn paths (`AssetSpawner`/`SceneGraph`) or `GizmoActivator` — removes the smearing.
The floor/scenery objects are authored onto `Environment` (manual, once).

### Context state machine — `InteractionMaskBinder`

A persistent component (lives with the XR rig; subscribes to the single root `EventBus`). Holds
references to both hands' `SphereInteractionCaster` + `CurveInteractionCaster`. On each relevant event it
recomputes the active mask and writes it to all four casters.

State inputs:
- `BonesVisibilityChangedEvent.Visible` → `_bonesMode`
- `GizmoToolsPanelOpenedEvent` / `GizmoToolsPanelClosedEvent` → `_panelOpen`
- `SelectionChangedEvent.SelectedNodeId != null` → `_hasSelection`

Active context → mask:
```
if (_panelOpen && _hasSelection)  mask = GizmoHandles      // gizmo modal
else if (_bonesMode)              mask = BoneProxies
else                              mask = SceneObjects
```

Behaviour this yields:
- **Gizmo modal:** while a gizmo is up the ray sees only handles — the target behind can't be mis-hit,
  and there's no gizmo-vs-object contest.
- **Empty click dismisses gizmo:** clicking empty space deselects → `SelectionChanged(null)` →
  `_hasSelection=false` → mask falls back to the base context (`Bone` or `Object`), so you can
  immediately re-select an object or a bone depending on the previous state.
- **Bone vs object are explicit, separate contexts** (no simultaneous mix → fewer mis-clicks): in Bone
  context the body (`SceneObjects`) is outside the mask, so the ray passes through the body and hits the
  bone behind it — this is what solves "bone behind body".
- **Through-floor:** the floor is on `Environment`, never in the mask, so the ray reaches whatever is
  behind it in the current context.

`IsPrimaryFor` stays as the original `interactablesHovered[0] == this` (the mask guarantees only relevant
things are hovered) — the v1 resolver rewrite is reverted.

### What we keep / drop from v1 work

Keep: `InteractionLayer` enum, `InteractionLayers` (used by the binder to build masks by layer name),
`SetInteractionLayer` / `SetInteractionLayerOnColliders`, `InteractionLayerTag`.
Drop: `RayInteractionResolver` (+ its scene-scope registrations), the `IsPrimaryFor` resolver rewrites
(revert to original), and `InteractionLayers.PickWinnerIndex` + its tests (no priority comparison needed).

## Components summary

1. `Environment` Unity layer (new); floor/scenery authored onto it.
2. Self-tagging: `XRPromeonInteractable.Awake` (+ serialized `_layer`), `GizmoHandle.Awake`, bone builder sets proxy `_layer`.
3. `InteractionMaskBinder` (persistent, EventBus-driven) — sets both casters' masks on both hands per context.
4. Caster masks are dynamic (binder-owned); startup default = Object (`SceneObjects`).

## Out of scope
- UI (separate XRI UI channel).
- Future floor-placement spawn (will raycast `Environment` only).
- Colinear multi-interactive priority beyond context masking (not needed once contexts are single-layer).

## Verification (VR)
1. Object context: point at a spawned object → highlights → tap selects.
2. Open gizmo → only handles interactable; target behind not mis-hit; grab/move/rotate works.
3. Click empty while gizmo up → gizmo dismissed, selection cleared, can re-select object/bone.
4. Bone context (Show Bones): bone behind body is selectable (body excluded from mask).
5. Gizmo/handle behind the floor → grabbable (floor on Environment, out of mask).
6. In-progress drag is not aborted by a mask change (locked state held independently).
7. Empty-space tap still triggers deselect under the narrow mask.

---

## (Archived) v1 design — RayInteractionResolver

The original priority-resolver design is superseded; see the "Why v1 was wrong" section. Kept only as a
pointer: it assumed an `XRRayInteractor` and a parallel `Physics.RaycastAll`, neither of which fit the
`NearFarInteractor` rig.
