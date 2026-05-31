# Interaction Layer Priority — Design

**Date:** 2026-05-31
**Status:** Approved (brainstorm). Next: implementation plan.

## Problem

The XR ray picks the **nearest collider** (`XRRayInteractor.TryGetCurrent3DRaycastHit`, used by
`XRPromeonInteractable.IsPrimaryFor:171-173` and `GizmoHandle.IsPrimaryFor:134-138`). So a gizmo handle
behind the floor, or a bone behind the body mesh, cannot be reached — an occluding collider wins. We
want a **layer-priority** rule: among everything the ray passes through, the highest-priority
interaction layer wins, with distance breaking ties only within the same layer.

Related logged bug: `docs/developer-notes/2026-05-31-gizmo-occluded-not-selectable.md`.

## Priority order

```
Gizmo  >  Bone  >  Selectable        (UI handled separately — see Out of scope)
```

Environment (floor, scenery) is **not** an interaction layer — it is "everything else." The resolver
raycasts only against the three interaction layers, so environment never occludes interactive things
and never needs a layer assigned. A ray that hits none of the three layers = empty (deselect), same as
today.

## Components

### 1. `InteractionLayer` (enum) + unified assignment
- `enum InteractionLayer { Gizmo, Bone, Selectable }` — single source of truth; declaration order = priority (Gizmo highest).
- Extension `GameObject.SetInteractionLayer(InteractionLayer)` — maps the enum to the Unity layer
  index via `LayerMask.NameToLayer("Gizmo"/"Bone"/"Selectable")` and assigns it to the target
  GameObject (the one carrying the collider). One funnel used by every script site.
- Component `InteractionLayerTag` (`[SerializeField] private InteractionLayer _layer;`) — for
  **prefab-authored** objects: applies the layer to its GameObject on `Awake` (and in `OnValidate` for
  editor visibility). Drop it on a prefab, pick the layer.

Three Unity layers must be created once: `Gizmo`, `Bone`, `Selectable`.

### 2. `RayInteractionResolver` (scene-scope DI)
Given an interactor ray, returns the prioritized hit:
```
ResolvePrimary(Ray ray, float maxDistance) -> (Collider winner or none)
```
- `Physics.RaycastAll(ray.origin, ray.direction, maxDistance, interactionMask, QueryTriggerInteraction.Ignore)`
  where `interactionMask = Gizmo | Bone | Selectable`.
- Group hits by layer priority; pick the highest-priority layer present; within it pick the nearest
  (smallest `hit.distance`). Return that collider (or none if no hits).
- `interactionMask` is built once from the three layer names.

### 3. Consumers
`XRPromeonInteractable.IsPrimaryFor` and `GizmoHandle.IsPrimaryFor` replace their nearest-hit check
with: build the ray from the interactor's `XRRayInteractor` (origin/direction/maxRaycastDistance), call
`resolver.ResolvePrimary(...)`, and return `true` iff the winning collider is one of the interactable's
own colliders. The resolver is injected (scene scope); `GizmoHandle` receives it from `GizmoActivator`
(which is DI-injected) via the existing `Bind` path, or via DI.

## Layer assignment sites

| Object | Where layer is set | Layer |
|---|---|---|
| Gizmo handles | `GizmoActivator.Spawn` (where outlines are installed) | `Gizmo` |
| Bone proxies | `PromeonProxyRigBuilder` where the proxy collider is created/enabled | `Bone` |
| Spawned assets | `SceneGraph` / `AssetSpawner` spawn path (where `InjectGameObject` is called) | `Selectable` |
| Editor-authored interactables | `InteractionLayerTag` on the prefab (assigned by user or via MCP) | per tag |

Runtime sites use `go.SetInteractionLayer(...)` on the GameObject(s) carrying the collider(s).

## Simplifications (folded in)

- **Remove the gizmo target-collider disable.** `GizmoActivator.Spawn` currently disables the selected
  object's `Collider` (`:130-131`) and re-enables on `Despawn` (`:148`). With `Gizmo > Selectable` the
  gizmo wins over its own target regardless, so this is redundant — remove it (and the
  `_originalTargetCollider` bookkeeping).

## Out of scope

- **UI.** UI is its own raycast channel (`NearFarInteractor.TryGetCurrentUIRaycastResult`,
  `WorldClickCatcher.IsOverUI`). It already takes precedence for world clicks; not changed here.
- **Ray visual.** The `XRRayInteractor` line still terminates at its own nearest hit (e.g. the floor)
  even when selection resolves to something behind it. Accepted as-is; extending the visual to the
  prioritized target is a possible later polish.
- **Explicit `Environment` layer.** Not created — environment = absence of an interaction layer.

## Testing / verification (in VR)

1. Gizmo behind the floor / behind the target object → grabbable (Gizmo layer wins).
2. Bone behind the body mesh → selectable (Bone > Selectable).
3. Two selectable objects in line → nearest one selected (distance tie-break within Selectable).
4. Pointing at empty floor (nothing interactive behind) → deselect (no hit in mask).
5. Selecting an object then manipulating its gizmo still works after removing the target-collider disable.

## Open questions

- **Resolver call frequency.** `IsPrimaryFor` may be queried multiple times per frame per interactor.
  `RaycastAll` is cheap for the few interactors in play; a per-frame/per-interactor cache is a possible
  optimization but is deferred (YAGNI) until profiling shows a need.
- **maxDistance source.** Use the interactor's `XRRayInteractor` max raycast distance; confirm the
  exact field at implementation time.
