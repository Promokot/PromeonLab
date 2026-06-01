# Type-Keyed Selection Colliders — Design

**Date:** 2026-06-01
**Status:** Approved (pending user review of this doc)

## Problem

Selection colliders are currently one-size-fits-all: every entity bakes a single
axis-aligned `BoxCollider` from renderer bounds (`BoundsBoxColliderStrategy` → recipe
`colliderKind/center/size` → `InteractionCapability.Apply`). A big AABB is a poor selection
target for irregular meshes and for skeletal rigs (one giant box around a humanoid).

Goal: the selection collider is chosen by asset type —
- **Object** (builtin + imported) → a convex `MeshCollider` (hugs the shape).
- **Rig** → box colliders placed along the skeleton down to nesting depth 3.
- **Reference** → unchanged single `Box`.

## Decisions

| Decision | Choice |
|---|---|
| Where the collider shape is computed | **At restore, from live geometry** (mesh / skeleton). Recipe stores only `colliderKind` (+ bone depth). |
| Object multi-mesh | **One convex `MeshCollider` per `MeshFilter`** under the root. |
| Rig bone-box coverage | **A box on every bone at depth 0–3.** Depth 0–2: box = local AABB of the bone + its **direct** children. Depth 3: box = local AABB of the bone + its **entire** remaining subtree. Depth >3: no own box. |
| Bone-box attachment | Child GO parented to the bone transform (follows pose), layer `SceneObjects`, min thickness from `ProxyRigConfig`. |
| Old recipes | `colliderKind=Box` (=1) keeps applying a box until re-baked / re-imported (manual, as today). No data migration. |

## Architecture

### Recipe / kind

- `ColliderKind` (VrInteraction) gains `ConvexMesh` and `BoneBoxes` (keep `None`, `Box`).
  Enum serializes as int via JsonUtility, so existing `Box` recipes stay valid.
- `AssetEntityRecipe`: keep `colliderKind`, `colliderCenter`, `colliderSize` (center/size used
  **only** by `Box`); add `int boneColliderDepth = 3` (used only by `BoneBoxes`).

### Bake (decides kind only — no measurement)

- `ObjectEntityBuilder.RecipeFromInstance` → `colliderKind = ConvexMesh`.
- `RigEntityBuilder.RecipeFromInstance` → `colliderKind = BoneBoxes`, `boneColliderDepth = 3`.
- `ReferenceEntityBuilder.BuildAsync` → `colliderKind = Box` with its hand-set unit box (unchanged).

### Cleanup (dead after this change)

Nothing measures a bounds-box anymore: Object/Rig set the kind directly; Reference sets its
box directly. Therefore remove:
- `IColliderStrategy` + `BoundsBoxColliderStrategy`.
- The `IColliderStrategy _collider` ctor param on `ObjectEntityBuilder` / `RigEntityBuilder`,
  and the `_collider` argument of `RecipeFromInstance` (signature: `(GameObject, AssetType)` for
  Object; `(GameObject, TerminalBoneAxis, bool)` for Rig).
- The `builder.Register<BoundsBoxColliderStrategy>().As<IColliderStrategy>()` line in
  `RootLifetimeScope`.
- The `new BoundsBoxColliderStrategy()` usage + arg in `BuiltinRecipeBaker.BakeIndex`.

### Restore — `InteractionCapability.Apply`, type-keyed

`Apply` still adds identity (`SceneNode`), `Selectable`, `XRPromeonInteractable` on the root,
then builds colliders per `colliderKind`:
- **`Box`** → one root `BoxCollider` from `colliderCenter/size` (current behavior, unchanged).
- **`ConvexMesh`** → for each `MeshFilter` under the root, add `MeshCollider { convex = true,
  sharedMesh = mf.sharedMesh }`; register each to the root interactable
  (`RegisterColliders`); put each on the recipe's `interactionLayer`. Zero `MeshFilter`s →
  warn, no collider.
- **`BoneBoxes`** → the boxes are built on the rig side (below); `Apply` itself adds no collider
  for this kind. After `Apply` creates the interactable, the registry triggers the rig to
  register its selector boxes (see Routing). Empty skeleton → fall back to `ConvexMesh`.

`Apply` stays idempotent (skips entirely if an `XRPromeonInteractable` is already present), so
pre-baked builtin prefabs are untouched.

### Rig bone-boxes (built in `RigEntityFactory.BuildProxyRig`)

The factory already walks `SkinnedMeshRenderer.bones` to build proxy diamonds; the selector
boxes piggyback on that walk:

1. Determine the skeleton root = the topmost bone in the set (a bone whose parent is not in the
   bone set). Depth 0 = root.
2. For each bone at depth `d`, `0 ≤ d ≤ depth(=3)`:
   - `d < 3`: encapsulate (in the bone's local space) the bone's own origin + each **direct**
     child bone's origin.
   - `d == 3`: encapsulate the bone's origin + **all** descendant bone origins (entire subtree).
   - Create a child GO under the bone, add a `BoxCollider` with that local center/size, padded
     to a minimum thickness (`ProxyRigConfig`), layer `SceneObjects`.
   - `d > 3`: skip.
3. Hand the box list to `ProxyRigRuntime` (`Bind` or a dedicated setter).

The traversal decision (which bones get a box and which origins each encapsulates) is extracted
into a **pure function** for unit testing — input: skeleton root transform + depth; output:
a list of `(bone, IReadOnlyList<Vector3> worldOriginsToEncapsulate)`. The factory turns each
entry into a padded local-space `BoxCollider`.

### Routing selector boxes → whole-rig selectable

A hit on a selector box must select the whole rig. `XRPromeonInteractable` only treats a hit
as its own when the collider is in its `colliders` list (`IsPrimaryFor` →
`colliders.Contains(hit.collider)`), and it does **not** auto-include child colliders
(`_includeChildColliders = false`) — which is correct, because the proxy diamonds (on
`BoneProxies`) must stay separate.

Order: `registry.RestoreAsync` → `builder.RestoreAsync` (factory builds proxies **and** selector
boxes, stored on `ProxyRigRuntime`) → `InteractionCapability.Apply` (creates the root
`XRPromeonInteractable`). After `Apply`, for `colliderKind == BoneBoxes` the **registry** calls
`go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders()`, which does
`interactable.RegisterColliders(_selectorColliders)`. This keeps `InteractionCapability`
(VrInteraction) free of any `ProxyRigRuntime` (RigBuilder) reference; the cross-call is
registry→ProxyRigRuntime→XRPromeonInteractable (allowed directions).

### Whole-rig vs bone-mode toggle

`ProxyRigRuntime` replaces its single `RootCollider()` field/logic with a
`List<Collider> _selectorColliders`:
- whole-rig mode (`SetBonesInteractive(false)`) → selector boxes **enabled**, proxies disabled.
- bone mode (`SetBonesInteractive(true)`) → selector boxes **disabled**, proxies enabled.

This replaces the existing `rootCol.enabled = !enabled` line; everything else in
`SetBonesInteractive` is unchanged.

## Data Flow

```
BAKE (decision only):
  Object → recipe.colliderKind = ConvexMesh
  Rig    → recipe.colliderKind = BoneBoxes, boneColliderDepth = 3
  Ref    → recipe.colliderKind = Box (+ unit box)

RESTORE:
  go = builder.RestoreAsync(...)        # Rig: factory builds proxies + selector boxes → ProxyRigRuntime
  InteractionCapability.Apply(go, recipe):
     Box        → root BoxCollider
     ConvexMesh → per-MeshFilter convex MeshCollider, registered to interactable
     BoneBoxes  → (no collider here)
  if recipe.colliderKind == BoneBoxes:
     go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders()   # registers boxes to the interactable
```

## Error Handling

- `BoneBoxes` with an empty/missing skeleton → fall back to `ConvexMesh` (per skinned renderer)
  so the rig stays selectable.
- `ConvexMesh` with zero `MeshFilter`s → warn, no collider (object not selectable — surfaced).
- Straight (colinear) bone chains → box would be zero-thickness in two axes → padded to a
  minimum thickness from `ProxyRigConfig`.
- Old `Box` recipes keep working; convex/bone-box behavior arrives on the next manual
  re-bake / re-import.

## Testing (EditMode)

1. `ColliderKind` has `ConvexMesh`/`BoneBoxes`; `AssetEntityRecipe` round-trips `colliderKind`
   and `boneColliderDepth` through `JsonUtility`.
2. `ObjectEntityBuilder.RecipeFromInstance` → `colliderKind == ConvexMesh` (and no longer needs
   a collider strategy arg).
3. `RigEntityBuilder.RecipeFromInstance` → `colliderKind == BoneBoxes`, `boneColliderDepth == 3`.
4. **Bone-box traversal** (pure function): a synthetic transform skeleton nested to depth 4+ →
   entries exist for bones at depth 0–3; the depth-3 entry's encapsulated set includes the deep
   (>3) descendant origins; no entry for depth >3 bones.
5. `InteractionCapability.Apply` with `ConvexMesh`: a root with two `MeshFilter`s → two
   `MeshCollider`s, both `convex`, both registered to the interactable's `colliders`.
6. `InteractionCapability.Apply` with `Box`: unchanged (existing tests stay green).

Baseline stays at the 6 known pre-existing EditMode failures (PathProvider ×4, RingRotate ×2).

## Out of Scope

- Thumbnail generation (parked, separate spec).
- Re-baking/re-importing existing assets (manual, user-side).
- Per-bone collider tuning UI.

## Affected Files (indicative; finalized in the plan)

- `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs` — `+ConvexMesh, +BoneBoxes`.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs` — `+boneColliderDepth`.
- `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs` — type-keyed collider build
  (Box/ConvexMesh; BoneBoxes no-op).
- `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs` / `RigEntityBuilder.cs` — set kind,
  drop collider strategy param from `RecipeFromInstance` + ctor.
- `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` — build selector boxes during the bone
  walk; pure traversal helper.
- `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs` — `_selectorColliders` list +
  `RegisterSelectorColliders()`; toggle them in `SetBonesInteractive`.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` — after `Apply`, trigger
  `RegisterSelectorColliders()` for `BoneBoxes`.
- `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — remove the `IColliderStrategy`
  registration.
- `Assets/_App/Editor/BuiltinRecipeBaker.cs` — drop the `BoundsBoxColliderStrategy` usage/arg.
- **Delete:** `Assets/_App/Scripts/VrInteraction/IColliderStrategy.cs`,
  `Assets/_App/Scripts/VrInteraction/BoundsBoxColliderStrategy.cs` (+ their tests if any).
- `Assets/_App/Tests/AssetBrowser/` + `Assets/_App/Tests/VrInteraction/` — tests above.
</content>
