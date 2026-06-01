# RigBuilder v2: Proxy Skeleton Fixes Design

> **Status:** Approved — ready for implementation
>
> **Supersedes:** Visual + hierarchy parts of `2026-05-20-rig-builder-v2-proxy-skeleton.md`. `BoneFollower` and `PromeonInteractableRigBuilder` are revised; `RigRuntime` stays as it is.

**Goal:** Fix three breakages observed after the initial proxy skeleton implementation:
1. Proxies are tiny because non-uniform localScale compounds through the nested proxy hierarchy
2. Diamond visuals are rotated wrong because `bone.rotation` rarely points toward the next bone
3. `BoneFollower` never moves the original bone — `LateUpdate` is silent in Edit Mode, and the `_proxy` reference is lost on domain reload

**Approach:** Switch from `GO.localScale + bone.rotation` to **per-bone baked meshes**. Each proxy carries its own `Mesh` asset with the diamond vertices already rotated to point toward the child bone and scaled to the right `length`/`width`. Proxy GO stays at `scale = (1, 1, 1)`, so no parent-scale compounding. Place the proxy rig as a **sibling of the bone armature**, mirroring the armature's `localTransform` so the local-coordinate copy in `BoneFollower` still works. Make `BoneFollower` survive both Edit Mode and domain reload.

**Tech Stack:** Unity 6, C#, URP 17, QuickOutline. No Animation Rigging.

---

## Why These Changes

### Issue 1: scale compounding kills deep proxies

The current code sets `proxyGo.transform.localScale = new Vector3(width, length, width)`. For a nested chain `proxy_pelvis → proxy_spine → proxy_hand_r`, world scales compound:

| Proxy | localScale | lossyScale (world) |
|---|---|---|
| `proxy_pelvis` | `(0.06, 0.5, 0.06)` | `(0.06, 0.5, 0.06)` |
| `proxy_spine` | `(0.06, 0.3, 0.06)` | `(0.0036, 0.15, 0.0036)` |
| `proxy_hand_r` | `(0.06, 0.2, 0.06)` | `(0.000216, 0.03, 0.000216)` |

By depth 3 the diamond is invisible. Setting `localScale = (1, 1, 1)` and baking width/length into the mesh removes the compounding entirely.

### Issue 2: `bone.rotation` is not the bone's "length" axis

The diamond mesh uses `Vector3.up` as the long axis. The proxy GO currently gets `bone.rotation`, which for most rigs is **not** the rotation that aligns `+Y` with the direction toward the child bone. The diamond ends up tilted relative to where the bone actually goes.

Baking the orientation into the per-bone mesh fixes this without changing the proxy GO's rotation (which still must equal `bone.rotation` so `BoneFollower` can drive the bone's natural rotation).

### Issue 3: `BoneFollower` lifecycle

- **Edit Mode**: `LateUpdate` does not fire on a plain `MonoBehaviour` in Edit Mode. Dragging a proxy in the Scene view does nothing. Adding `[ExecuteAlways]` makes Unity tick the component continuously in both modes.
- **Play Mode**: `_proxy` is a `private` field without `[SerializeField]`. Entering Play Mode triggers a domain reload that resets private fields to `default(T)`. `_proxy` becomes `null`, `Tick()` early-returns, and the bone never moves. Marking `_proxy` with `[SerializeField]` (or `[SerializeReference]`) makes Unity serialize the Transform reference across reloads.

### Issue 4 (raised by user): ProxyRig must not be inside the armature

A proxy rig parented inside the armature lives among bones in the Inspector and creates the impression of a parent-child cycle when a `BoneFollower` is attached. Putting `ProxyRig` as a **sibling of the armature** (one level up) keeps the proxy structure visually parallel to the bone rig. To preserve the local-coordinate copy invariant, `ProxyRig` mirrors `bone.parent`'s `localPosition`/`localRotation`/`localScale` exactly — same parent, same local transform, therefore same world transform. Coordinate spaces stay aligned.

---

## Runtime Hierarchy

```
Character                       (typically the GO holding PromeonInteractableRigBuilder)
├── Mesh                        (SkinnedMeshRenderer)
├── Armature                    (bone.parent of root bone)
│   └── pelvis                  (BoneFollower → proxy_pelvis)
│       └── spine               (BoneFollower → proxy_spine)
│           └── hand_r          (BoneFollower → proxy_hand_r)
└── ProxyRig                    (sibling of Armature, localTransform == Armature.localTransform)
    └── proxy_pelvis            (scale 1, baked mesh oriented toward proxy_spine)
        └── proxy_spine         (scale 1, baked mesh oriented toward proxy_hand_r)
            └── proxy_hand_r    (scale 1, baked leaf mesh)
```

**Invariant:** for every proxy/bone pair, `proxy.transform.parent` and `bone.parent` have identical world transforms. This guarantees `proxy_X.localPosition` (in proxy-parent space) equals `bone_X.localPosition` (in bone-parent space), so the `BoneFollower` local copy is correct.

For root proxies, the parent of `proxy_pelvis` is `ProxyRig`, and the parent of `pelvis` is `Armature`. We enforce `ProxyRig.localTransform == Armature.localTransform` to satisfy the invariant.

For non-root proxies, the parent is the parent's proxy (e.g., `proxy_spine`'s parent is `proxy_pelvis`). Because every proxy GO has `position = bone.position` and `rotation = bone.rotation` at build time, parent-proxy world transform matches parent-bone world transform. Invariant holds recursively.

---

## `BoneFollower` Component

`Assets/_App/Subsystems/RigBuilder/BoneFollower.cs`:

```csharp
using UnityEngine;

[ExecuteAlways]
public class BoneFollower : MonoBehaviour
{
    [SerializeField] private Transform _proxy;

    public void SetProxy(Transform proxy) => _proxy = proxy;

    public void Tick()
    {
        if (_proxy == null) return;
        transform.localPosition = _proxy.localPosition;
        transform.localRotation = _proxy.localRotation;
    }

    void LateUpdate() => Tick();
    void OnDestroy() => _proxy = null;
}
```

Changes from previous version:
- `[ExecuteAlways]` so `LateUpdate` fires in Edit Mode
- `_proxy` is now `[SerializeField]` so the reference survives domain reload and entering Play Mode

`Tick()` stays public — used by EditMode tests to drive the component without entering Play Mode.

---

## `PromeonInteractableRigBuilder` Changes

### Fields

Add:
- `_proxyMeshes` (`List<Mesh>`) — every per-bone mesh created during `Rebuild`, destroyed in `DestroyBoneGOs`

Remove:
- `_boneMesh` (single shared diamond mesh) — replaced by per-bone meshes
- `BuildDiamondMesh()` static method becomes a static helper that takes parameters (see below)

Keep:
- `_boneMaterial`, `_boneWidth`, `_useConvexCollider`, `_transforms`, `_proxyGOs`, `_followers`, `_proxyRoot`

### `Rebuild()` — unchanged signature

```csharp
public void Rebuild()
{
    DestroyBoneGOs();
    var transforms = ResolveTransforms();
    if (transforms == null || transforms.Length == 0) return;
    BuildProxyHierarchy(transforms);
}
```

Note: `_boneMesh` initialization removed. Meshes are built per-bone inside `BuildProxyNode`.

### `BuildProxyHierarchy(Transform[] transforms)` — revised

```csharp
void BuildProxyHierarchy(Transform[] transforms)
{
    var set = new HashSet<Transform>(transforms);
    set.Remove(null);

    foreach (var bone in transforms)
    {
        if (bone == null) continue;
        if (set.Contains(bone.parent)) continue;   // not a root bone
        if (bone.parent == null) continue;         // root bone at scene root — skip, would break invariant

        if (_proxyRoot == null)
        {
            var armature    = bone.parent;
            var grandParent = armature.parent;     // may be null if armature is at scene root

            var rig = new GameObject("ProxyRig");
            rig.transform.SetParent(grandParent, worldPositionStays: false);
            rig.transform.localPosition = armature.localPosition;
            rig.transform.localRotation = armature.localRotation;
            rig.transform.localScale    = armature.localScale;
            _proxyRoot = rig.transform;
        }

        BuildProxyNode(bone, _proxyRoot, set);
    }
}
```

`ProxyRig` is parented to `armature.parent` and mirrors `armature.localTransform`. If `armature.parent` is `null` (armature is a scene root), `ProxyRig` also becomes a scene root with the same `localTransform`.

If a root bone has no parent at all (`bone.parent == null`), it is skipped — the invariant cannot be satisfied without a parent reference frame.

### `BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)` — revised

```csharp
void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)
{
    Transform firstChild = null;
    for (int i = 0; i < bone.childCount; i++)
    {
        var c = bone.GetChild(i);
        if (set.Contains(c)) { firstChild = c; break; }
    }

    Vector3 localChildDir;
    float   length;
    if (firstChild != null)
    {
        var worldDir = firstChild.position - bone.position;
        length        = Mathf.Max(worldDir.magnitude, 0.0001f);
        localChildDir = bone.InverseTransformDirection(worldDir).normalized;
        if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
    }
    else
    {
        localChildDir = Vector3.up;
        length        = _boneWidth * 5f;
    }
    float width = EffectiveWidth(_boneWidth, length);

    var mesh = BuildOrientedDiamondMesh(localChildDir, length, width);
    _proxyMeshes.Add(mesh);

    var proxyGo = new GameObject($"proxy_{bone.name}");
    proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
    proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
    proxyGo.transform.localScale = Vector3.one;

    AddMeshAndOutline(proxyGo, mesh);
    AddCollider(proxyGo, mesh);
    _proxyGOs.Add(proxyGo);

    var follower = bone.gameObject.AddComponent<BoneFollower>();
    follower.SetProxy(proxyGo.transform);
    _followers.Add(follower);

    for (int i = 0; i < bone.childCount; i++)
    {
        var child = bone.GetChild(i);
        if (set.Contains(child))
            BuildProxyNode(child, proxyGo.transform, set);
    }
}
```

Differences from previous version:
- Compute `localChildDir` (the direction to the first child bone, expressed in bone-local coordinates) so it can be baked into the mesh
- Build a unique mesh per bone via `BuildOrientedDiamondMesh(localChildDir, length, width)`
- Proxy GO `localScale = Vector3.one` — width/length live in the mesh, not the transform
- `AddMeshAndOutline` and `AddCollider` take the per-bone `mesh` as an argument

### `BuildOrientedDiamondMesh(Vector3 localLongAxis, float length, float width)` — new static helper

```csharp
public static Mesh BuildOrientedDiamondMesh(Vector3 localLongAxis, float length, float width)
{
    var rot = Quaternion.FromToRotation(Vector3.up, localLongAxis.normalized);

    var baseVerts = new[]
    {
        new Vector3( 0f,    0f,    0f),
        new Vector3( 0.5f,  0.15f, 0f),
        new Vector3(-0.5f,  0.15f, 0f),
        new Vector3( 0f,    0.15f, 0.5f),
        new Vector3( 0f,    0.15f,-0.5f),
        new Vector3( 0f,    1f,    0f),
    };

    var verts = new Vector3[baseVerts.Length];
    for (int i = 0; i < baseVerts.Length; i++)
    {
        var v = baseVerts[i];
        v       = new Vector3(v.x * width, v.y * length, v.z * width);
        verts[i] = rot * v;
    }

    var mesh = new Mesh { name = "PromeonBoneDiamond" };
    mesh.vertices  = verts;
    mesh.triangles = new[]
    {
        0, 1, 3,  0, 3, 2,  0, 2, 4,  0, 4, 1,
        1, 5, 3,  3, 5, 2,  2, 5, 4,  4, 5, 1,
    };
    mesh.RecalculateNormals();
    return mesh;
}
```

`BuildDiamondMesh()` (no-args, returns the unit diamond at `(width=1, length=1, localLongAxis=Vector3.up)`) is preserved as a thin wrapper for the existing tests:

```csharp
public static Mesh BuildDiamondMesh() => BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);
```

This keeps `BuildDiamondMesh_*` tests valid without modification.

### `AddMeshAndOutline(GameObject go, Mesh mesh)` — minor signature change

```csharp
void AddMeshAndOutline(GameObject go, Mesh mesh)
{
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var mr = go.AddComponent<MeshRenderer>();
    if (_boneMaterial == null)
        Debug.LogWarning("[PromeonInteractableRigBuilder] _boneMaterial not assigned.", this);
    mr.sharedMaterial = _boneMaterial;

    var outline          = go.AddComponent<Outline>();
    outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
    outline.OutlineColor = Color.white;
    outline.OutlineWidth = 3f;
}
```

### `AddCollider(GameObject go, Mesh mesh)` — minor signature change

```csharp
void AddCollider(GameObject go, Mesh mesh)
{
    if (_useConvexCollider)
    {
        var mc        = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex     = true;
    }
    else
    {
        var col       = go.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.height    = 1f;
        col.radius    = 0.5f;
    }
}
```

The `CapsuleCollider` branch is now a poor fit (no per-bone sizing), but kept for compatibility. Effectively `_useConvexCollider` should remain `true`.

### `DestroyBoneGOs()` — revised

```csharp
void DestroyBoneGOs()
{
    if (_proxyRoot != null)
    {
        DestroyObj(_proxyRoot.gameObject);
        _proxyRoot = null;
    }
    _proxyGOs.Clear();

    foreach (var f in _followers)
        if (f != null) DestroyObj(f);
    _followers.Clear();

    foreach (var m in _proxyMeshes)
        if (m != null) DestroyObj(m);
    _proxyMeshes.Clear();
}
```

Drops the single `_boneMesh` cleanup; instead destroys every per-bone mesh tracked in `_proxyMeshes`.

### `SetVisualsEnabled(bool)` — unchanged

Iterates `_proxyGOs`, toggles `MeshRenderer.enabled` and `Outline.enabled`.

### `ResolveTransforms()` and `ExtractPairs()` — unchanged

---

## Tests

### Keep from existing suite

- `EffectiveWidth_*` (3 tests) — static method, unchanged
- `BuildDiamondMesh_*` (4 tests) — pass because `BuildDiamondMesh()` wraps `BuildOrientedDiamondMesh(Vector3.up, 1f, 1f)` and base vertices are unchanged
- `ExtractPairs_*` (4 tests) — static method, unchanged
- `BoneFollower_Tick_*` (3 tests) — still valid; `[ExecuteAlways]` does not affect manual `Tick()` calls
- `BuildProxyHierarchy_AddsBoneFollowerToEachBone` — still valid

### Update

- `BuildProxyHierarchy_TwoBones_CreatesTwoProxies` — replace `_ProxyBones` lookup with `ProxyRig`, and adjust the parent path: `characterGo.transform.Find("ProxyRig")` will only succeed if `bone.parent.parent` of the first root bone is `characterGo`. The existing test creates `Character → pelvis → spine` and calls `SetTransforms` with `[pelvis, spine]` — so `pelvis.parent.parent = null`, and `ProxyRig` becomes a scene root. The test must be restructured to create one extra level: `Character → Armature → pelvis → spine`, with `bones = [pelvis, spine]`. Then `ProxyRig` is found under `Character`.

- `BuildProxyHierarchy_NestedHierarchy_MirrorsParenting` — same restructure: `Character → Armature → pelvis → spine → chest`, expect `Character/ProxyRig/proxy_pelvis/proxy_spine/proxy_chest`.

- `BuildProxyHierarchy_LeafBone_UsesDefaultLength` — leaf bone proxy now has `localScale = (1, 1, 1)` (not `(width, length, width)`). Replace the `localScale.y` assertion with a check on the bounding box of `MeshFilter.sharedMesh`: leaf mesh's bounds height equals `_boneWidth * 5f = 0.3f`.

### New tests

- `BuildOrientedDiamondMesh_VerticalAxis_MatchesUnitMesh` — `BuildOrientedDiamondMesh(Vector3.up, 1f, 1f)` returns vertices identical to the legacy unit diamond
- `BuildOrientedDiamondMesh_HorizontalAxis_RotatesVertices` — pass `localLongAxis = Vector3.right`, check the tail vertex (originally at `(0, 1, 0)`) ends up at approximately `(1, 0, 0)`
- `BuildOrientedDiamondMesh_NonUniformScale_AppliesBeforeRotation` — pass `length = 2`, `width = 0.5`, vertical axis, check bounds height ≈ 2 and bounds width ≈ 0.5
- `BuildProxyHierarchy_ProxyRig_MirrorsArmatureLocalTransform` — give `Armature` a non-identity `localPosition` and `localRotation`, run `Rebuild`, verify `ProxyRig.localPosition` and `ProxyRig.localRotation` match
- `BuildProxyHierarchy_ProxyGO_ScaleIsOne` — every `proxy_*` GO has `localScale == Vector3.one`

Total target: existing 18 (minus 1 retired leaf-scale assertion replaced with bounds check) + 5 new ≈ 22 tests.

---

## `RigRuntime` — Unchanged

`RigRuntime.ApplyDefinition` already calls `boneRenderer.Rebuild()`. No further changes needed.

---

## Cleanup Contract

`DestroyBoneGOs()` must:
- Destroy `ProxyRig` (cascades to all proxy GOs)
- Remove every `BoneFollower` component from original bones
- Destroy every per-bone `Mesh` asset (no longer a single shared mesh)
- Clear `_proxyGOs`, `_followers`, `_proxyMeshes` lists; null out `_proxyRoot`

Original bone GameObjects are never destroyed — only the `BoneFollower` components added to them.

---

## Known Limitations

- **Multi-root skeletons:** still only one `ProxyRig` is created (placed under the first root bone's grandparent). Disconnected roots whose grandparents differ would land in the wrong frame. Accepted limitation for v1.
- **Root bone at scene root:** if a root bone has no parent at all, it is skipped (the invariant requires a parent reference frame). Practically this never happens for imported FBX rigs.
- **Non-uniform armature scale:** `ProxyRig.localScale = armature.localScale` keeps the world transforms aligned. If the armature has non-uniform scale, world copying still works because we use `localPosition`/`localRotation` only — non-uniform scale does not break local-coordinate copy semantics.
- **`XRPromeonInteractable` is NOT added** to proxies in this phase. VR grabbing is still sub-project 2.
- **IK chains** are still deferred.
- **`BoneProxy.cs`** remains as dead code, safe to delete later.
