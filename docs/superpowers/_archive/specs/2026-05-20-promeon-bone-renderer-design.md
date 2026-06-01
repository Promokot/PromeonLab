# PromeonInteractableRigBuilder Design

> **Status:** Implemented — `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

**Goal:** Runtime-visible, VR-interactable bone visualization for Meta Quest 3 (URP, single-pass instanced stereo). Generates diamond-shaped proxy bones from a SkinnedMeshRenderer, wires them to original bones via Animation Rigging `MultiParentConstraint`, and exposes each bone as a grabbable object (CapsuleCollider / convex MeshCollider + Outline silhouette).

**Architecture:** `PromeonInteractableRigBuilder : MonoBehaviour` on the Animator root. Driven by `RigRuntime.ApplyDefinition` — no per-frame logic. In constraint mode each bone pair produces two GOs: a **manipulator proxy** (collider only, under `_BoneProxies`) that drives the original bone via `MultiParentConstraint`, and a **visual** (diamond mesh + Outline, child of the original bone) that always reflects the bone's true hierarchy position. In visual-only mode a single combined GO is parented to the bone.

**Tech Stack:** Unity 6, URP 17, Unity Animation Rigging package, QuickOutline.

---

## Rename history

Originally `PromeonBoneRenderer`. Renamed to `PromeonInteractableRigBuilder` because scope expanded beyond rendering: proxy bones now carry colliders and Animation Rigging constraints that let them drive original bones.

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Main MonoBehaviour |
| `Assets/_App/Editor/PromeonInteractableRigBuilderEditor.cs` | "Rebuild" button in Inspector |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | EditMode unit tests (mesh + pair extraction) |

Assembly: `Subsystems.RigBuilder` — references `Unity.Animation.Rigging`, `QuickOutline`.

---

## Inspector Fields

```csharp
[SerializeField] private Material _boneMaterial;       // must be assigned; no auto-fallback
[SerializeField] private float    _boneWidth = 0.06f;  // world-space half-width (meters), capped by length
[SerializeField] private bool     _useConvexCollider = true;
[SerializeField] private bool     _buildConstraints  = true;
// _transforms is NOT serialized — set at runtime via SetTransforms() or auto-discovered
```

### Component menu

`[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]` — searchable by "Promeon Interactable" in Add Component dialog.

---

## Public API

| Method | Purpose |
|---|---|
| `SetTransforms(Transform[])` | Explicitly set bone transforms (called by RigRuntime) |
| `SetMaterial(Material)` | Set bone material at runtime (called by RigRuntime) |
| `SetConstraintRigParent(Transform)` | Set the Rig GO under which constraint GOs are created |
| `Rebuild()` | Destroy existing bones and recreate from current state |
| `SetVisualsEnabled(bool)` | Toggle MeshRenderer + Outline on all visual GOs (both modes) |

---

## Initialization Flow (via RigRuntime)

```
RigRuntime.ApplyDefinition(definition, smr)
  └─ GetOrAdd PromeonInteractableRigBuilder on animator.gameObject
  └─ SetMaterial(_boneMaterial)
  └─ SetConstraintRigParent(rigGo.transform)   ← the _Rig GO created by RigRuntime
  └─ SetTransforms(bones from smr)
  └─ Rebuild()
```

`Awake()` only calls `Rebuild()` if `_transforms` is already set — avoids a redundant first build when `AddComponent` fires before `SetTransforms`.

---

## Transform Resolution

`ResolveTransforms()` priority:
1. `_transforms` if set by `SetTransforms()`
2. Auto-discover: `GetComponentInChildren<SkinnedMeshRenderer>()?.bones` (or parent search)
3. Log warning and return null

This lets the component work standalone (added manually in the Inspector) without requiring `RigRuntime`.

---

## Bone Shape

Unit diamond mesh (octahedron variant), Y-axis from 0 to 1:

```
v0 = (0,    0,    0)    head — tapers to point
v1 = (+0.5, 0.15, 0)    shoulder ring (4 verts at Y=0.15)
v2 = (-0.5, 0.15, 0)
v3 = (0,    0.15, +0.5)
v4 = (0,    0.15, -0.5)
v5 = (0,    1,    0)    tail — tapers to point
```

8 triangles (4 head→shoulder, 4 shoulder→tail). Built once per component instance (`_boneMesh` instance field, not static).

### Width proportionality

```csharp
float effectiveWidth = Mathf.Min(_boneWidth, length * 0.2f);
```

At `length >= 5 × _boneWidth` → full `_boneWidth`. Shorter bones get proportionally narrower diamonds. Applied to `localScale.x` and `localScale.z` in both modes.

---

## Two Rendering Modes

### Visual mode (`_buildConstraints = false`)

One combined GO per bone pair, **parented to the original bone**. Contains MeshFilter + MeshRenderer + Collider + Outline. Follows animation automatically via the transform hierarchy.

```
CharacterRoot
└─ pelvis (bone)
   └─ Bone_pelvis   (MeshRenderer + CapsuleCollider + Outline — child of bone)
```

### Constraint mode (`_buildConstraints = true`, default)

Two GOs per bone pair:
- **Manipulator proxy** (`Proxy_*`): under `_BoneProxies`, collider only (no mesh), drives the original bone via `MultiParentConstraint`.
- **Visual** (`Visual_*`): child of the original bone, diamond mesh + Outline, no collider. Follows bone through hierarchy — automatically correct when any ancestor bone moves.

```
CharacterRoot
├── _BoneProxies
│   ├── Proxy_pelvis   (CapsuleCollider/MeshCollider — no mesh, no Outline)
│   └── Proxy_spine    (CapsuleCollider/MeshCollider)
└── SkinnedMesh
    ├── pelvis (bone)
    │   ├── Visual_pelvis  (diamond MeshRenderer + Outline — no collider)
    │   └── spine (bone)
    │       └── Visual_spine   (follows spine via hierarchy automatically)
    └── _Rig
        ├── PC_pelvis  (MultiParentConstraint → constrainedObject=pelvis,  source=Proxy_pelvis,  weight=1)
        └── PC_spine   (MultiParentConstraint → constrainedObject=spine,   source=Proxy_spine,   weight=1)
```

**Why two GOs:** in constraint mode the proxy is independent (required for VR grabbing), so it cannot follow ancestor bone movement via hierarchy. The visual must be a bone child to stay accurate when any parent bone is moved by the user or IK.

`SetConstraintRigParent(rigGo.transform)` must be called before `Rebuild()` when using constraint mode. `RigRuntime` does this automatically. When called from the Inspector "Rebuild" button without a rig parent set, the component silently falls back to visual mode (proxy parented to bone) — a soft `Debug.Log` is emitted instead of a warning.

---

## Internal Tracking Lists

| Field | Contents |
|---|---|
| `_boneGOs` | Proxy GOs (constraint mode) or combined GOs (visual mode) |
| `_visualGOs` | Visual-only GOs (constraint mode only; empty in visual mode) |
| `_constraintGOs` | Constraint GOs (`PC_*`) under the Rig GO |
| `_proxyRoot` | `_BoneProxies` container transform (constraint mode only) |

---

## `SetVisualsEnabled`

Iterates both `_boneGOs` and `_visualGOs` and toggles `MeshRenderer.enabled` and `Outline.enabled`. In constraint mode `_boneGOs` are collider-only (no MeshRenderer), so the GetComponent calls are no-ops — safe for both modes without branching.

---

## Cleanup

`DestroyBoneGOs()` (called by `Rebuild()` and `OnDestroy()`):
1. Destroys all `_constraintGOs`
2. Destroys `_proxyRoot.gameObject` (cascades to all `Proxy_*`) — or individual `_boneGOs` if no proxy root
3. Destroys all `_visualGOs` explicitly (they are children of original bones, not of `_proxyRoot`)

---

## Limitations

- Bone lengths are assumed constant (rotation-only animation). Position-animated bones show incorrect length.
- `_boneMaterial` must be assigned manually (no auto-fallback).
- In constraint mode, `_constraintRigParent` must be set before `Rebuild()` or the component silently falls back to visual mode.
- Proxy bone scale uses `effectiveWidth` as absolute world meters — may look odd for very short bones (X/Z > Y not possible due to the proportionality cap, but tiny bones will have tiny handles).

---

## Tests

`PromeonInteractableRigBuilderTests` (EditMode, `Subsystems.RigBuilder.Tests` assembly):

| Test | What it verifies |
|---|---|
| `BuildDiamondMesh_HasSixVertices` | Mesh has exactly 6 vertices |
| `BuildDiamondMesh_HasTwentyFourTriangleIndices` | 8 triangles = 24 indices |
| `BuildDiamondMesh_HeadVertexAtOrigin` | v0 = Vector3.zero |
| `BuildDiamondMesh_TailVertexAtUnitY` | v5 = Vector3.up |
| `ExtractPairs_ParentChildBothInSet_ReturnsPair` | Parent-child in set → 1 pair |
| `ExtractPairs_ChildNotInSet_NoPairReturned` | Child missing from set → 0 pairs |
| `ExtractPairs_LeafBone_NotReturnedAsPair` | Leaf bone → 0 pairs |
| `ExtractPairs_NullTransformInArray_SkippedSafely` | Nulls in array → no crash |
