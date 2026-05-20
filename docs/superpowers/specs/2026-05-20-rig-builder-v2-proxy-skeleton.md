# RigBuilder v2: Proxy Skeleton Design

> **Status:** Approved — ready for implementation

**Goal:** Replace the broken `MultiParentConstraint`-based system with a mirrored proxy skeleton hierarchy driven by a simple `BoneFollower` component, making bone manipulation in VR reliable without Animation Rigging dependencies.

**Architecture:** `PromeonInteractableRigBuilder.Rebuild()` builds a `_ProxyBones` container that mirrors the original bone hierarchy. Each proxy GO carries the diamond mesh + collider. Each original bone receives a `BoneFollower` component that copies `localPosition`/`localRotation` from its proxy every `LateUpdate`. No Animation Rigging constraints involved.

**Tech Stack:** Unity 6, C#, URP 17, QuickOutline.

---

## Why This Approach

The previous `MultiParentConstraint` approach required a `Rig`/`RigBuilder` setup that was difficult to configure correctly at edit time. The proxy-as-driver pattern is simpler: proxies are ordinary GameObjects, bones follow via `LateUpdate`, no constraint baking needed. Local-coordinate copy means no execution-order dependency between followers.

---

## File Structure

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Full rewrite of proxy-building logic |
| `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs` | New component (~15 lines) |
| `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` | Remove constraint setup, BoneProxy prefab, IK wiring |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | Remove obsolete proxy-mode tests; keep EffectiveWidth + mesh + pair tests |

---

## Runtime Hierarchy

```
CharacterRoot
├── _ProxyBones                       (container, same parent as root bone)
│   └── proxy_pelvis                  (diamond mesh + collider)
│       └── proxy_spine               (diamond mesh + collider)
│           ├── proxy_hand_r          (diamond mesh + collider)
│           └── proxy_hand_l          (diamond mesh + collider)
└── [SkinnedMesh]
    └── pelvis  (BoneFollower → proxy_pelvis)
        └── spine  (BoneFollower → proxy_spine)
            ├── hand_r  (BoneFollower → proxy_hand_r)
            └── hand_l  (BoneFollower → proxy_hand_l)
```

`_ProxyBones` is parented to the same transform as the root bone's parent, at local position (0,0,0) / identity rotation. This ensures proxy local-space == bone local-space, making the local-coordinate copy in `BoneFollower` correct.

---

## `BoneFollower` Component

New file: `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs`

```csharp
public class BoneFollower : MonoBehaviour
{
    private Transform _proxy;

    public void SetProxy(Transform proxy) => _proxy = proxy;

    void LateUpdate()
    {
        if (_proxy == null) return;
        transform.localPosition = _proxy.localPosition;
        transform.localRotation = _proxy.localRotation;
    }

    void OnDestroy() => _proxy = null;
}
```

**Why `localPosition`/`localRotation`:** The proxy hierarchy mirrors the bone hierarchy, so local coordinates are identical. Copying local avoids execution-order dependency — it doesn't matter if spine's follower runs before or after pelvis's follower.

**Scale is not copied:** proxy scale encodes diamond dimensions (`width, length, width`), not bone scale.

**Cleanup:** `PromeonInteractableRigBuilder.DestroyBoneGOs()` calls `DestroyObj(follower)` on each tracked follower, removing the component from the original bone without touching the bone itself.

---

## `PromeonInteractableRigBuilder` Changes

### Fields removed
- `_buildConstraints` (bool)
- `_constraintRigParent` (Transform)
- `_constraintGOs` (List)
- `_boneGOs`, `_visualGOs` → replaced by `_proxyGOs`
- `using UnityEngine.Animations.Rigging`

### Fields added
- `_proxyGOs` — all proxy GOs (for `SetVisualsEnabled` and cascade destroy via `_proxyRoot`)
- `_followers` — all `BoneFollower` instances on original bones (for explicit cleanup)

### Public API removed
- `SetConstraintRigParent(Transform)`

### Public API unchanged
- `SetTransforms(Transform[])`, `SetMaterial(Material)`, `Rebuild()`, `SetVisualsEnabled(bool)`

### `Rebuild()` — simplified
```csharp
public void Rebuild()
{
    DestroyBoneGOs();
    var transforms = ResolveTransforms();
    if (transforms == null || transforms.Length == 0) return;
    if (_boneMesh == null) _boneMesh = BuildDiamondMesh();
    BuildProxyHierarchy(transforms);
}
```

### `BuildProxyHierarchy(Transform[] transforms)` — new
1. Build a `HashSet<Transform>` of all valid bones
2. Find root bones: bones whose `parent` is NOT in the set
3. Create `_ProxyBones` GO, parent to root bone's parent at local (0,0,0) / identity
4. For each root bone, call `BuildProxyNode(bone, _proxyRoot, set)` recursively

### `BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)` — new
1. Compute proxy orientation: `bone.rotation` (world rotation — preserves bone's natural axis; diamond Y = bone local Y)
2. Create proxy GO named `proxy_{bone.name}`
3. Parent to `proxyParent` with `worldPositionStays: false`, then `SetPositionAndRotation(bone.position, bone.rotation)`
4. Compute length: iterate `bone.GetChild(i)` in order, take first child that is in `set` — use distance to that child; if no children in set (leaf bone), use `_boneWidth * 5f`
5. Compute width: `EffectiveWidth(_boneWidth, length)`
6. Set `localScale = new Vector3(width, length, width)`
7. Call `AddMeshAndOutline(proxyGo)` and `AddCollider(proxyGo)`
8. Add `BoneFollower` to `bone.gameObject`, call `follower.SetProxy(proxyGo.transform)`
9. Track: `_proxyGOs.Add(proxyGo)`, `_followers.Add(follower)`
10. Recurse: for each child of `bone` that is in `set`, call `BuildProxyNode(child, proxyGo.transform, set)`

### `SetVisualsEnabled(bool)` — updated
Iterates `_proxyGOs` only (proxies now carry the mesh and outline). Toggles `MeshRenderer.enabled` and `Outline.enabled`.

### `DestroyBoneGOs()` — updated
1. Destroy `_proxyRoot.gameObject` (cascades to all proxy GOs)
2. `_proxyGOs.Clear()`
3. For each follower in `_followers`: `DestroyObj(follower)` — removes component from bone
4. `_followers.Clear()`
5. Destroy `_boneMesh`, set null

### Static methods unchanged
`EffectiveWidth`, `BuildDiamondMesh`, `ExtractPairs` — no changes.

---

## `RigRuntime` Changes

### Fields removed
- `[SerializeField] private GameObject _boneProxyPrefab`
- `private readonly List<BoneProxy> _proxies`

### Methods removed
- `ClearProxies()`
- `AddTwoBoneIK()` (deferred — separate concern, depends on Animation Rigging)
- `FindMidBone()`

### `ApplyDefinition` — simplified
```csharp
public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
{
    var boneRenderer = smr.GetComponentInParent<PromeonInteractableRigBuilder>();
    if (boneRenderer == null)
        boneRenderer = smr.gameObject.AddComponent<PromeonInteractableRigBuilder>();

    if (_boneMaterial != null) boneRenderer.SetMaterial(_boneMaterial);

    var bones = new List<Transform>();
    foreach (var bone in definition.Bones)
    {
        var t = FindBone(smr, bone.BoneName);
        if (t != null) bones.Add(t);
    }
    boneRenderer.SetTransforms(bones.ToArray());
    boneRenderer.Rebuild();
}
```

`BuildFromSkinnedMesh()` and `FindBone()` unchanged.

---

## Cleanup Contract

`DestroyBoneGOs()` is called by `Rebuild()` and `OnDestroy()`. It must:
- Destroy all proxy GOs (via `_proxyRoot` cascade)
- Remove all `BoneFollower` components from original bones
- Destroy the `_boneMesh` asset
- Reset all tracking lists and references

No original bone GOs are ever destroyed — only the components (`BoneFollower`) added to them.

---

## Known Limitations

- Proxy rotation is set to `bone.rotation` (bone's natural axis), not to the child-pointing direction. Diamond visual may look slightly misaligned on bones whose local Y doesn't point toward their child. Acceptable for v1; can be fixed with a child visual GO approach in a later iteration.
- `XRPromeonInteractable` is NOT added to proxy bones in this phase. Grabbing bones in VR is sub-project 2.
- IK chains (`AddTwoBoneIK`) are deferred — they require Animation Rigging which this phase removes from `RigRuntime`. IK will need a separate solution.
- `BoneProxy` prefab and `BoneProxy.cs` become unused after this change — safe to delete or leave as dead code for now.

---

## Tests

Keep from existing suite:
- `EffectiveWidth_*` (3 tests) — static method, unchanged
- `BuildDiamondMesh_*` (4 tests) — static method, unchanged
- `ExtractPairs_*` (4 tests) — static method, unchanged

Remove:
- Any tests that reference proxy/visual split or constraint mode (these test the old architecture)

New tests for `BuildProxyHierarchy`:
- `BuildProxyHierarchy_TwoBones_CreatesTwoProxies` — pair (parent→child) in set → two proxy GOs: `proxy_{parent}` under `_ProxyBones`, `proxy_{child}` under `proxy_{parent}`
- `BuildProxyHierarchy_NestedHierarchy_MirrorsParenting` — three-level chain → proxy hierarchy nesting matches bone hierarchy
- `BuildProxyHierarchy_AddsBoneFollowerToOriginalBone` — each original bone in set has `BoneFollower` after rebuild
- `BuildProxyHierarchy_LeafBone_UsesDefaultLength` — leaf bone proxy has `scale.y = _boneWidth * 5f`
