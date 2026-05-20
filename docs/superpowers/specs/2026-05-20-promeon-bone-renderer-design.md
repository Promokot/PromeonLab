# PromeonInteractableRigBuilder Design

> **Status:** Implemented — `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

**Goal:** Runtime-visible, VR-interactable bone visualization for Meta Quest 3 (URP, single-pass instanced stereo). Generates diamond-shaped proxy bones from a SkinnedMeshRenderer, wires them to original bones via Animation Rigging `MultiParentConstraint`, and exposes each bone as a grabbable object (CapsuleCollider / convex MeshCollider + Outline silhouette).

**Architecture:** `PromeonInteractableRigBuilder : MonoBehaviour` on the Animator root. Driven by `RigRuntime.ApplyDefinition` — no per-frame logic. All rendering and interaction is via child GameObjects parented to a `_BoneProxies` container (constraint mode) or directly to joint transforms (visual-only mode).

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
[SerializeField] private float    _boneWidth = 0.06f;  // world-space half-width (meters)
[SerializeField] private bool     _useConvexCollider = true;
[SerializeField] private bool     _buildConstraints  = true;
// _transforms is NOT serialized — set at runtime via SetTransforms() or auto-discovered
```

---

## Public API

| Method | Purpose |
|---|---|
| `SetTransforms(Transform[])` | Explicitly set bone transforms (called by RigRuntime) |
| `SetMaterial(Material)` | Set bone material at runtime (called by RigRuntime) |
| `SetConstraintRigParent(Transform)` | Set the Rig GO under which constraint GOs are created |
| `Rebuild()` | Destroy existing bones and recreate from current state |
| `SetVisualsEnabled(bool)` | Toggle MeshRenderer + Outline on all bone GOs |

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

---

## Bone GO Setup (per bone pair)

Each proxy bone GO contains:
- `MeshFilter` — shared diamond mesh
- `MeshRenderer` — `_boneMaterial`
- Collider (one of):
  - `MeshCollider` with `convex = true`, `sharedMesh = diamond` (**default**, `_useConvexCollider = true`)
  - `CapsuleCollider` Y-axis, height=1, radius=0.5 (in local space — world size determined by scale)
- `Outline` — `Mode.SilhouetteOnly`, white, width=3

**Scale:** `localScale = (_boneWidth, length, _boneWidth)` where `length` = world-space bone length (in constraint mode) or local-space magnitude (in visual mode). Result: X/Z are absolute world meters; Y spans the bone.

---

## Two Rendering Modes

### Visual mode (`_buildConstraints = false`)

Proxy GO is **parented to the original bone**. Follows animation automatically via the transform hierarchy. Zero per-frame script cost. Used for pure visualization.

```
CharacterRoot
└─ Pelvis (bone)
   └─ Bone_Pelvis (proxy GO — child of bone)
```

### Constraint mode (`_buildConstraints = true`, default)

Proxy GOs live under `_BoneProxies` (child of this component's GO). Each original bone gets a `MultiParentConstraint` created **under the Rig GO** with the proxy as source (weight=1). Moving the proxy drives the original bone.

```
CharacterRoot
├─ _BoneProxies
│  └─ Bone_Pelvis (proxy GO — independent)
└─ SkinnedMesh
   └─ _Rig (Rig GO, set via SetConstraintRigParent)
      ├─ PC_Pelvis (MultiParentConstraint → constrainedObject=Pelvis, source=Bone_Pelvis)
      └─ ...
```

`SetConstraintRigParent(rigGo.transform)` must be called before `Rebuild()` when using constraint mode. `RigRuntime` does this automatically.

---

## Cleanup

`DestroyBoneGOs()` (called by `Rebuild()` and `OnDestroy()`):
1. Destroys all constraint GOs (removes `MultiParentConstraint` from rig)
2. Destroys `_proxyRoot` GO (which destroys all proxy bone GOs as children)
3. OR destroys individual bone GOs if in visual mode

---

## Limitations

- Bone lengths are assumed constant (rotation-only animation). Position-animated bones show incorrect length.
- `_boneMaterial` must be assigned manually (no auto-fallback).
- In constraint mode, `_constraintRigParent` must be set before `Rebuild()` or constraints are silently skipped with a warning.
- Proxy bone scale uses `_boneWidth` as absolute world meters — may look odd for very short bones (X/Z > Y).

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
