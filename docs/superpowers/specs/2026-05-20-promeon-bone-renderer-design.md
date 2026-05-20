# PromeonBoneRenderer Design

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Runtime-visible bone visualization for VR (Meta Quest 3, URP, single-pass instanced stereo) using classic diamond/octahedron shapes, built once per rig assignment and extensible with XRI grabbables.

**Architecture:** `PromeonBoneRenderer` inherits `BoneRenderer` to reuse its serialized `transforms[]` field and inspector properties (`boneColor`, `boneSize`, `drawBones`). Rendering is done via per-bone child GameObjects (MeshFilter + MeshRenderer) parented directly to joint transforms, so they follow animation automatically with zero per-frame script cost.

**Tech Stack:** Unity 6, URP 17, Unity Animation Rigging package, OpenXR.

---

## Why inheritance from `BoneRenderer`

`BoneRenderer` in the Animation Rigging package has all rendering code inside `#if UNITY_EDITOR` — it does nothing at runtime. Inheriting gives us:
- The serialized `Transform[] transforms` field (populated via the existing Bone Renderer workflow)
- Inspector properties: `boneColor`, `boneSize`, `boneShape`, `drawBones`
- Scene View rendering still works in Edit Mode (handled by `BoneRendererUtils` in the package)

We add runtime rendering on top.

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs` | Main MonoBehaviour |
| `Assets/_App/Subsystems/RigBuilder/Editor/PromeonBoneRendererEditor.cs` | "Rebuild" button in Inspector |

`PromeonBoneRenderer` lives in the `RigBuilder` subsystem alongside existing rig components. No new assembly definition needed — uses the existing `RigBuilder` asmdef (or `_App.asmdef` if RigBuilder has none).

---

## Bone Shape

A unit diamond mesh (octahedron variant), Y-axis from 0 to 1 (head at Y=0, tail at Y=1):

```
v0 = (0,    0,    0)     head — tapers to point
v1 = (+r,   0.15, 0)     shoulder ring (4 verts at Y=0.15)
v2 = (-r,   0.15, 0)
v3 = (0,    0.15, +r)
v4 = (0,    0.15, -r)
v5 = (0,    1,    0)     tail — tapers to point
```

Where `r = 0.06` (6% of bone length after scale is applied). Produces 8 triangles (4 from head to shoulder, 4 from shoulder to tail). Built once as `static Mesh s_BoneMesh` and shared across all instances.

---

## `PromeonBoneRenderer` Fields

```csharp
[SerializeField] private Material _boneMaterial;  // Unlit/Transparent, assigned in inspector
[SerializeField] private float _boneWidth = 0.06f; // shoulder radius relative to bone length
```

`boneColor`, `boneSize`, `drawBones` — inherited from `BoneRenderer`.

Internal:
```csharp
private readonly List<(Transform start, Transform end, GameObject go)> _boneObjects = new();
private static Mesh s_BoneMesh;
```

---

## `Rebuild()` — called once in `Awake` and via public API

1. Destroy all existing bone GOs in `_boneObjects`
2. If `!drawBones` or `transforms == null` — return
3. Build `s_BoneMesh` if not yet built
4. Extract bone pairs from `transforms` (runtime-safe copy of `ExtractBones` logic — no Editor API):
   - Build a `HashSet<Transform>` from `transforms`
   - For each transform: if any child is in the set → bone pair (transform, child); otherwise → tip
5. For each bone pair `(start, end)`:
   - Create GO `"Bone_{start.name}"` 
   - Add `MeshFilter` (assign `s_BoneMesh`) + `MeshRenderer` (assign `_boneMaterial`)
   - Set `MeshRenderer.material.color = boneColor`
   - Add `CapsuleCollider`: `direction = 1 (Y-axis)`, `height = length`, `radius = boneWidth * length * 0.5f` (for future grabbable support)
   - Parent to `start`
   - Set local TRS once:
     ```
     localPosition = Vector3.zero
     localDir      = InverseTransformPoint(end.position).normalized  (= end.localPosition.normalized)
     localRotation = Quaternion.FromToRotation(Vector3.up, localDir)
     localScale    = Vector3(boneWidth * length, length, boneWidth * length)
                     where length = Vector3.Distance(start.position, end.position)
     ```
6. Store `(start, end, go)` in `_boneObjects`

For tips (leaf joints): create a GO with the same diamond mesh, `localScale = Vector3.one * boneWidth * boneSize`, oriented along parent's Y-axis (no child direction needed).

---

## No LateUpdate

Bone GOs are parented to joint transforms. Unity's transform hierarchy propagates position and rotation automatically every frame — no script involvement. This works correctly when only rotations are animated (standard skeletal rig). If position curves are applied to intermediate bones, the bone shape may appear skewed; this is an acceptable limitation for the current scope.

---

## Grabbables (future extension)

Each bone GO is a stable, named child of its joint with a `CapsuleCollider`. Adding `XRPromeonInteractable` (or any XRI component) later requires no changes to `PromeonBoneRenderer`. 

One known conflict: if a bone GO is grabbed and its parent joint moves (animation), the grab system and the joint hierarchy will fight. Resolving this (e.g., detaching from parent on grab, re-attaching on release) is out of scope for this spec.

---

## Editor Support

`PromeonBoneRendererEditor` adds a single "Rebuild" button in the Inspector that calls `target.Rebuild()`. Useful after modifying the `transforms` array in Edit Mode.

`BoneRendererUtils` in the package continues to draw bones in the Scene View in Edit Mode — no changes needed.

---

## Limitations

- Bone lengths are assumed constant (rotation-only animation). Position-animated bones will show incorrect bone length.
- No per-bone color/highlight differentiation (e.g., selected bone) — uniform `boneColor` for all.
- Material must be assigned manually in the Inspector. No auto-fallback material creation.
