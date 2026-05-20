# RigBuilder v2: Proxy Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the broken `MultiParentConstraint`-based rig system with a mirrored proxy skeleton hierarchy driven by a simple `BoneFollower` component — no Animation Rigging required.

**Architecture:** `PromeonInteractableRigBuilder.Rebuild()` builds a `_ProxyBones` container that mirrors the original bone hierarchy one-to-one. Each proxy GO carries the diamond mesh + collider. Each original bone receives a `BoneFollower` component that copies `localPosition`/`localRotation` from its proxy in `LateUpdate`. `RigRuntime` is simplified to remove all Animation Rigging setup.

**Tech Stack:** Unity 6, C#, URP 17, QuickOutline.

> **No git commits** — user manages version control manually. Skip all commit steps.

---

## File Map

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs` | **Create** — new MonoBehaviour, ~20 lines |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | **Modify** — add 7 new tests (3 BoneFollower + 4 proxy hierarchy) |
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | **Rewrite** — swap constraint-based proxy logic for hierarchy builder |
| `Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef` | **Modify** — remove `Unity.Animation.Rigging` reference |
| `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` | **Simplify** — remove Rig/RigBuilder/BoneProxy/IK setup |

---

## Task 1: Create `BoneFollower` component (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`

- [ ] **Step 1: Add 3 failing tests to the test file**

Open `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`.

Append these three tests inside the class, before the final closing brace. They use the existing `MakeGO` helper and `_created` list for cleanup:

```csharp
    [Test]
    public void BoneFollower_Tick_CopiesLocalPositionFromProxy()
    {
        var boneGo  = MakeGO("bone");
        var proxyGo = MakeGO("proxy");
        proxyGo.transform.localPosition = new Vector3(1f, 2f, 3f);

        var follower = boneGo.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        follower.Tick();

        Assert.AreEqual(new Vector3(1f, 2f, 3f), boneGo.transform.localPosition);
    }

    [Test]
    public void BoneFollower_Tick_CopiesLocalRotationFromProxy()
    {
        var boneGo  = MakeGO("bone");
        var proxyGo = MakeGO("proxy");
        var expected = Quaternion.Euler(45f, 90f, 0f);
        proxyGo.transform.localRotation = expected;

        var follower = boneGo.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        follower.Tick();

        Assert.AreEqual(expected, boneGo.transform.localRotation);
    }

    [Test]
    public void BoneFollower_Tick_NullProxy_DoesNotThrow()
    {
        var boneGo   = MakeGO("bone");
        var follower = boneGo.AddComponent<BoneFollower>();
        // No proxy set — Tick must not throw
        Assert.DoesNotThrow(() => follower.Tick());
    }
```

- [ ] **Step 2: Run tests — confirm they fail**

Open `Window > General > Test Runner` → **EditMode** tab. Run `BoneFollower_*`.

Expected: 3 failures with "BoneFollower" type not found (class doesn't exist yet).

- [ ] **Step 3: Create `BoneFollower.cs`**

Create `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs` with this exact content:

```csharp
using UnityEngine;

public class BoneFollower : MonoBehaviour
{
    private Transform _proxy;

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

`Tick()` is public so tests can call it directly without needing play-mode `LateUpdate` execution.

- [ ] **Step 4: Run tests — confirm they pass**

Test Runner → EditMode → run `BoneFollower_*`.

Expected: 3 PASS. All 14 tests total (11 existing + 3 new) should pass.

---

## Task 2: Add proxy hierarchy tests (TDD — failing first)

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`

These tests verify the NEW `PromeonInteractableRigBuilder.Rebuild()` behavior. They will fail on the current code (which creates `Bone_*` GOs, not `proxy_*` GOs in a mirrored hierarchy).

- [ ] **Step 1: Add 4 failing tests**

Append these four tests inside the class, before the final closing brace:

```csharp
    [Test]
    public void BuildProxyHierarchy_TwoBones_CreatesTwoProxies()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRoot   = characterGo.transform.Find("_ProxyBones");
        Assert.IsNotNull(proxyRoot, "_ProxyBones container not found under characterGo");

        var proxyPelvis = proxyRoot.Find("proxy_pelvis");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under _ProxyBones");

        var proxySpine  = proxyPelvis.Find("proxy_spine");
        Assert.IsNotNull(proxySpine, "proxy_spine not found under proxy_pelvis");
    }

    [Test]
    public void BuildProxyHierarchy_NestedHierarchy_MirrorsParenting()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        var chestGo     = MakeGO("chest",  spineGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;
        chestGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform, chestGo.transform });
        rig.Rebuild();

        var proxyRoot   = characterGo.transform.Find("_ProxyBones");
        var proxyPelvis = proxyRoot?.Find("proxy_pelvis");
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        var proxyChest  = proxySpine?.Find("proxy_chest");

        Assert.IsNotNull(proxyRoot,   "_ProxyBones not found");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found — not child of _ProxyBones");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found — not child of proxy_pelvis");
        Assert.IsNotNull(proxyChest,  "proxy_chest not found — not child of proxy_spine");
    }

    [Test]
    public void BuildProxyHierarchy_AddsBoneFollowerToEachBone()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        Assert.IsNotNull(pelvisGo.GetComponent<BoneFollower>(), "pelvis missing BoneFollower");
        Assert.IsNotNull(spineGo.GetComponent<BoneFollower>(),  "spine missing BoneFollower");
    }

    [Test]
    public void BuildProxyHierarchy_LeafBone_UsesDefaultLength()
    {
        // Default length for a leaf bone = _boneWidth * 5 = 0.06 * 5 = 0.3
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        // Put spine 1m above pelvis so pelvis proxy length=1.0 ≠ leaf default 0.3
        spineGo.transform.localPosition = Vector3.up * 1.0f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxySpine = characterGo.transform
            .Find("_ProxyBones")
            ?.Find("proxy_pelvis")
            ?.Find("proxy_spine");

        Assert.IsNotNull(proxySpine, "proxy_spine not found");
        // spine is a leaf in the set — no children → default length = 0.06 * 5 = 0.3
        Assert.AreEqual(0.3f, proxySpine.localScale.y, 0.001f,
            "Leaf bone proxy scale.y should equal boneWidth * 5 = 0.3");
    }
```

- [ ] **Step 2: Run tests — confirm they fail**

Test Runner → EditMode → run `BuildProxyHierarchy_*`.

Expected: 4 failures. The current code creates `Bone_pelvis` (not `proxy_pelvis`) and uses a flat `_BoneProxies` container (not a mirrored hierarchy). `_ProxyBones` or `proxy_*` names won't be found.

---

## Task 3: Rewrite `PromeonInteractableRigBuilder`

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef`

- [ ] **Step 1: Replace the entire content of `PromeonInteractableRigBuilder.cs`**

Replace the entire file with:

```csharp
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]
public class PromeonInteractableRigBuilder : MonoBehaviour
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth          = 0.06f;
    [SerializeField] private bool     _useConvexCollider  = true;
    private Transform[] _transforms;

    private readonly List<GameObject>   _proxyGOs  = new();
    private readonly List<BoneFollower> _followers = new();
    private Transform _proxyRoot;
    private Mesh      _boneMesh;

    void Awake()     { if (_transforms != null && _transforms.Length > 0) Rebuild(); }
    void OnDestroy() => DestroyBoneGOs();

    public void SetTransforms(Transform[] transforms) => _transforms   = transforms;
    public void SetMaterial(Material material)        => _boneMaterial = material;

    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
        if (_boneMesh == null) _boneMesh = BuildDiamondMesh();
        BuildProxyHierarchy(transforms);
    }

    public void SetVisualsEnabled(bool enabled)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var mr      = go.GetComponent<MeshRenderer>();
            if (mr      != null) mr.enabled      = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
        }
    }

    void BuildProxyHierarchy(Transform[] transforms)
    {
        var set = new HashSet<Transform>(transforms);
        set.Remove(null);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue;    // not a root bone

            if (_proxyRoot == null)
            {
                var container = new GameObject("_ProxyBones");
                container.transform.SetParent(bone.parent, worldPositionStays: false);
                _proxyRoot = container.transform;
            }

            BuildProxyNode(bone, _proxyRoot, set);
        }
    }

    void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)
    {
        Transform firstChild = null;
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (set.Contains(c)) { firstChild = c; break; }
        }

        float length = firstChild != null
            ? Mathf.Max((firstChild.position - bone.position).magnitude, 0.0001f)
            : _boneWidth * 5f;
        float width = EffectiveWidth(_boneWidth, length);

        var proxyGo = new GameObject($"proxy_{bone.name}");
        proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
        proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
        proxyGo.transform.localScale = new Vector3(width, length, width);

        AddMeshAndOutline(proxyGo);
        AddCollider(proxyGo);
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

    void AddCollider(GameObject go)
    {
        if (_useConvexCollider)
        {
            var mc           = go.AddComponent<MeshCollider>();
            mc.sharedMesh    = _boneMesh;
            mc.convex        = true;
        }
        else
        {
            var col       = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height    = 1f;
            col.radius    = 0.5f;
        }
    }

    void AddMeshAndOutline(GameObject go)
    {
        go.AddComponent<MeshFilter>().sharedMesh = _boneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonInteractableRigBuilder] _boneMaterial not assigned.", this);
        mr.sharedMaterial    = _boneMaterial;
        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 3f;
    }

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

        if (_boneMesh != null) { DestroyObj(_boneMesh); _boneMesh = null; }
    }

    static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                        DestroyImmediate(obj);
    }

    Transform[] ResolveTransforms()
    {
        if (_transforms != null && _transforms.Length > 0)
            return _transforms;

        var smr = GetComponentInChildren<SkinnedMeshRenderer>()
               ?? GetComponentInParent<SkinnedMeshRenderer>();
        if (smr != null && smr.bones.Length > 0)
            return smr.bones;

        Debug.LogWarning("[PromeonInteractableRigBuilder] No transforms set and no SkinnedMeshRenderer found.", this);
        return null;
    }

    public static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);

    public static Mesh BuildDiamondMesh()
    {
        var mesh = new Mesh { name = "PromeonBoneDiamond" };

        mesh.vertices = new[]
        {
            new Vector3( 0f,    0f,    0f),
            new Vector3( 0.5f,  0.15f, 0f),
            new Vector3(-0.5f,  0.15f, 0f),
            new Vector3( 0f,    0.15f, 0.5f),
            new Vector3( 0f,    0.15f,-0.5f),
            new Vector3( 0f,    1f,    0f),
        };

        mesh.triangles = new[]
        {
            0, 1, 3,  0, 3, 2,  0, 2, 4,  0, 4, 1,
            1, 5, 3,  3, 5, 2,  2, 5, 4,  4, 5, 1,
        };

        mesh.RecalculateNormals();
        return mesh;
    }

    public static (Transform start, Transform end)[] ExtractPairs(Transform[] transforms)
    {
        var set    = new HashSet<Transform>(transforms);
        set.Remove(null);
        var result = new List<(Transform, Transform)>();

        foreach (var t in transforms)
        {
            if (t == null) continue;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (set.Contains(child))
                    result.Add((t, child));
            }
        }
        return result.ToArray();
    }
}
```

- [ ] **Step 2: Remove `Unity.Animation.Rigging` from the assembly definition**

Open `Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef` and replace its entire content with:

```json
{ "name": "Subsystems.RigBuilder", "references": ["_Shared","VContainer","QuickOutline"], "autoReferenced": false }
```

- [ ] **Step 3: Verify Unity compiles without errors**

Switch to Unity Editor. Wait for domain reload. Check the Console — **zero compile errors** required before continuing.

If you see `error CS0246: The type or namespace name 'MultiParentConstraint'` — you missed a `using` or `AddParentConstraint` reference. Check `PromeonInteractableRigBuilder.cs` is fully replaced (no leftover `using UnityEngine.Animations.Rigging`).

- [ ] **Step 4: Run all tests — confirm 18 pass**

Test Runner → EditMode → Run All.

Expected: **18 PASS** (11 original + 3 BoneFollower + 4 BuildProxyHierarchy), 0 fail.

---

## Task 4: Simplify `RigRuntime`

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs`

- [ ] **Step 1: Replace the entire content of `RigRuntime.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            def.Bones.Add(new BoneRecord { BoneName = bone.name });
        return def;
    }

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

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
```

- [ ] **Step 2: Verify Unity compiles without errors**

Switch to Unity Editor. Wait for domain reload. Console must show zero compile errors.

If you see `error CS0246` for `TwoBoneIKConstraint`, `Rig`, `RigLayer`, or `BoneProxy` — the file was not fully replaced. Re-check Step 1.

Note: `BoneProxy.cs` still exists and still compiles (it's unused but not deleted). This is intentional.

- [ ] **Step 3: Manual smoke test in Unity Editor**

1. Open a scene that has a GameObject with `SkinnedMeshRenderer` in its hierarchy.
2. Add `PromeonInteractableRigBuilder` to the root GO (or it may already be present).
3. Make sure `_boneMaterial` is assigned in the Inspector.
4. Press the **"Rebuild"** button in the Inspector.
5. Check the **Hierarchy** panel. Verify:
   - A `_ProxyBones` container appears under the character root
   - Inside it, proxy GOs named `proxy_{boneName}` are nested to match bone hierarchy
   - Each `proxy_*` GO has a `MeshRenderer` (diamond shape) and a `MeshCollider` or `CapsuleCollider`
   - Each original bone GO in the hierarchy has a `BoneFollower` component (visible in Inspector when selected)
6. Check the **Console** — no errors or exceptions.

- [ ] **Step 4: Verify BoneFollower drives bones at runtime**

1. Enter Play mode.
2. In the Hierarchy, select a `proxy_*` GO.
3. In the Inspector's Transform section, manually change its Position or Rotation.
4. The corresponding original bone should move — visually confirmed by the skinned mesh deforming.

---

## Done

After Task 4 Step 4 passes, the proxy skeleton system is working:

- `_ProxyBones` hierarchy mirrors original bones one-to-one
- Diamond mesh + collider on each proxy (VR grab points for future sub-project 2)
- `BoneFollower.LateUpdate` drives original bones via local-coordinate copy
- No Animation Rigging dependencies remaining in `RigRuntime` or `PromeonInteractableRigBuilder`
- 18 EditMode tests pass
