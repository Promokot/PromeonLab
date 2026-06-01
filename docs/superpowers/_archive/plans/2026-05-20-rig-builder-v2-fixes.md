# RigBuilder v2 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the three breakages in RigBuilder v2 — invisible deep proxies (scale compounding), misaligned diamond visuals (wrong axis), and a non-functional `BoneFollower` (Edit Mode + domain reload).

**Architecture:** Bake per-bone diamond meshes (rotation + scale into vertices, GO at `scale = (1,1,1)`). Place `ProxyRig` as a sibling of the bone armature with the armature's `localTransform` mirrored, preserving the local-coordinate copy invariant. Mark `BoneFollower` `[ExecuteAlways]` and serialize its `_proxy` Transform.

**Tech Stack:** Unity 6, C#, URP 17, QuickOutline. No Animation Rigging.

> **No git commits** — the user manages version control manually. Skip all commit steps.

---

## File Map

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs` | **Modify** — `[ExecuteAlways]` attribute, `[SerializeField]` on `_proxy` |
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | **Modify** — add `BuildOrientedDiamondMesh`, rewrite `BuildProxyHierarchy`/`BuildProxyNode`/`DestroyBoneGOs`, swap to per-bone meshes, drop `_boneMesh` field |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | **Modify** — restructure 4 existing `BuildProxyHierarchy_*` tests, add 5 new tests |

---

## Task 1: `BoneFollower` Lifecycle Fixes

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/BoneFollower.cs`

Two changes — neither requires a new test. The existing three `BoneFollower_Tick_*` tests still verify behavior because they call `Tick()` manually (independent of `LateUpdate` firing).

- [ ] **Step 1: Replace the entire content of `BoneFollower.cs`**

Path: `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\RigBuilder\BoneFollower.cs`

Replace the whole file with:

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

Differences from previous version:
- `[ExecuteAlways]` attribute on the class — Unity will tick `LateUpdate` in Edit Mode
- `_proxy` is now `[SerializeField] private Transform _proxy` — Unity serializes the Transform reference so it survives domain reload (entering Play Mode, recompilation)

- [ ] **Step 2: Wait for Unity to recompile, check Console**

Switch to Unity Editor. Wait for the spinner to finish. Open the Console window.

Expected: zero compile errors.

If you see `CS0246: type or namespace 'ExecuteAlways' not found` — the attribute lives in `UnityEngine`. The `using UnityEngine;` line at the top must be present.

- [ ] **Step 3: Run BoneFollower tests — they must still pass**

Open `Window > General > Test Runner` → **EditMode** tab. Filter by `BoneFollower_`.

Expected: 3 PASS (`BoneFollower_Tick_CopiesLocalPositionFromProxy`, `BoneFollower_Tick_CopiesLocalRotationFromProxy`, `BoneFollower_Tick_NullProxy_DoesNotThrow`).

---

## Task 2: Add `BuildOrientedDiamondMesh` Helper (TDD)

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

Add a static helper that builds a diamond mesh with a configurable long-axis direction and per-bone `length`/`width` baked into the vertices.

- [ ] **Step 1: Add 3 failing tests**

Open `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`.

Find the existing `BuildDiamondMesh_TailVertexAtUnitY` test (around line 31–37). Immediately after its closing `}`, before the `private readonly List<GameObject> _created` field, insert these three tests:

```csharp
    [Test]
    public void BuildOrientedDiamondMesh_VerticalAxis_MatchesUnitMesh()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);
        Assert.AreEqual(6,  mesh.vertexCount);
        Assert.AreEqual(24, mesh.triangles.Length);
        Assert.AreEqual(new Vector3(0f, 1f, 0f), mesh.vertices[5]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildOrientedDiamondMesh_HorizontalAxis_RotatesVertices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.right, 1f, 1f);
        var tail = mesh.vertices[5];
        Assert.AreEqual(1f, tail.x, 0.0001f);
        Assert.AreEqual(0f, tail.y, 0.0001f);
        Assert.AreEqual(0f, tail.z, 0.0001f);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildOrientedDiamondMesh_NonUniformScale_AppliesBeforeRotation()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.up, 2f, 0.5f);
        Assert.AreEqual(2f,   mesh.bounds.size.y, 0.001f);
        Assert.AreEqual(0.5f, mesh.bounds.size.x, 0.001f);
        Assert.AreEqual(0.5f, mesh.bounds.size.z, 0.001f);
        Object.DestroyImmediate(mesh);
    }
```

- [ ] **Step 2: Run tests — confirm they fail**

Test Runner → EditMode → filter `BuildOrientedDiamondMesh_`.

Expected: 3 compile errors or test failures with "BuildOrientedDiamondMesh method not found" — the static method doesn't exist yet.

- [ ] **Step 3: Add `BuildOrientedDiamondMesh` and refactor `BuildDiamondMesh`**

Open `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`.

Find the existing `BuildDiamondMesh()` static method (around line 165–185). Replace it entirely (the whole method block, from `public static Mesh BuildDiamondMesh()` through its closing `}`) with these two methods:

```csharp
    public static Mesh BuildDiamondMesh() => BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);

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
            var v   = baseVerts[i];
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

`BuildDiamondMesh()` is now a thin wrapper that delegates to `BuildOrientedDiamondMesh(Vector3.up, 1f, 1f)`. This preserves the existing 4 `BuildDiamondMesh_*` tests without changes — they get the same vertices as before.

- [ ] **Step 4: Wait for compile, run all mesh tests — confirm pass**

Switch to Unity, wait for recompile, check Console (zero errors).

Test Runner → EditMode → filter `Mesh`.

Expected: 7 PASS (4 existing `BuildDiamondMesh_*` + 3 new `BuildOrientedDiamondMesh_*`).

---

## Task 3: Restructure Existing Proxy Hierarchy Tests + Add New Tests

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`

The existing 4 `BuildProxyHierarchy_*` tests expect the old `_ProxyBones` container directly under `characterGo`. The new architecture puts `ProxyRig` under `armature.parent` (one level up from the root bone's parent), so the tests need an extra `Armature` GO in the setup. Also: the leaf-bone test now asserts mesh bounds instead of `localScale.y`.

All updates AND additions go in this task. After this task, 6 tests will FAIL (the implementation hasn't caught up yet) and the rest pass.

- [ ] **Step 1: Replace the 4 existing `BuildProxyHierarchy_*` tests**

Open `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`.

Find the block of 4 tests starting with `BuildProxyHierarchy_TwoBones_CreatesTwoProxies` (around line 163) and ending with the closing brace of `BuildProxyHierarchy_LeafBone_UsesDefaultLength` (around line 249).

Replace that entire block (all 4 test methods, from `[Test]` above `BuildProxyHierarchy_TwoBones_CreatesTwoProxies` through the closing `}` of `BuildProxyHierarchy_LeafBone_UsesDefaultLength`) with:

```csharp
    [Test]
    public void BuildProxyHierarchy_TwoBones_CreatesTwoProxies()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRig    = characterGo.transform.Find("ProxyRig");
        Assert.IsNotNull(proxyRig, "ProxyRig container not found under characterGo");

        var proxyPelvis = proxyRig.Find("proxy_pelvis");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under ProxyRig");

        var proxySpine  = proxyPelvis.Find("proxy_spine");
        Assert.IsNotNull(proxySpine, "proxy_spine not found under proxy_pelvis");
    }

    [Test]
    public void BuildProxyHierarchy_NestedHierarchy_MirrorsParenting()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        var chestGo     = MakeGO("chest",    spineGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;
        chestGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform, chestGo.transform });
        rig.Rebuild();

        var proxyRig    = characterGo.transform.Find("ProxyRig");
        var proxyPelvis = proxyRig?.Find("proxy_pelvis");
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        var proxyChest  = proxySpine?.Find("proxy_chest");

        Assert.IsNotNull(proxyRig,    "ProxyRig not found");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under ProxyRig");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found under proxy_pelvis");
        Assert.IsNotNull(proxyChest,  "proxy_chest not found under proxy_spine");
    }

    [Test]
    public void BuildProxyHierarchy_AddsBoneFollowerToEachBone()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
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
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 1.0f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxySpine = characterGo.transform
            .Find("ProxyRig")
            ?.Find("proxy_pelvis")
            ?.Find("proxy_spine");

        Assert.IsNotNull(proxySpine, "proxy_spine not found");
        var meshFilter = proxySpine.GetComponent<MeshFilter>();
        Assert.IsNotNull(meshFilter, "proxy_spine has no MeshFilter");
        Assert.IsNotNull(meshFilter.sharedMesh, "proxy_spine MeshFilter has no Mesh");
        // Leaf bone: localLongAxis = Vector3.up, length = boneWidth * 5 = 0.3
        // Diamond bounds height should be 0.3 in the proxy's local space
        Assert.AreEqual(0.3f, meshFilter.sharedMesh.bounds.size.y, 0.001f,
            "Leaf bone proxy mesh bounds.y should equal boneWidth * 5 = 0.3");
    }
```

Key differences from the old tests:
- Each test now creates an `Armature` GO between `Character` and `pelvis`
- `_ProxyBones` lookup → `ProxyRig` lookup
- `ProxyRig` is found under `characterGo` (the grandparent of `pelvis`), not under the immediate parent
- Leaf test asserts on `MeshFilter.sharedMesh.bounds.size.y` instead of `proxy.localScale.y`

- [ ] **Step 2: Add 2 new tests for the proxy-rig invariant**

Immediately after the closing `}` of the replaced `BuildProxyHierarchy_LeafBone_UsesDefaultLength`, before the final class closing `}`, insert:

```csharp
    [Test]
    public void BuildProxyHierarchy_ProxyRig_MirrorsArmatureLocalTransform()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        armatureGo.transform.localPosition = new Vector3(1f, 2f, 3f);
        armatureGo.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
        armatureGo.transform.localScale    = new Vector3(1.5f, 1.5f, 1.5f);

        var pelvisGo = MakeGO("pelvis", armatureGo.transform);
        var spineGo  = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRig = characterGo.transform.Find("ProxyRig");
        Assert.IsNotNull(proxyRig, "ProxyRig not found");
        Assert.AreEqual(armatureGo.transform.localPosition, proxyRig.localPosition);
        Assert.AreEqual(armatureGo.transform.localRotation, proxyRig.localRotation);
        Assert.AreEqual(armatureGo.transform.localScale,    proxyRig.localScale);
    }

    [Test]
    public void BuildProxyHierarchy_ProxyGO_ScaleIsOne()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
        var proxySpine  = characterGo.transform.Find("ProxyRig/proxy_pelvis/proxy_spine");

        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found");
        Assert.AreEqual(Vector3.one, proxyPelvis.localScale, "proxy_pelvis.localScale must be (1,1,1)");
        Assert.AreEqual(Vector3.one, proxySpine.localScale,  "proxy_spine.localScale must be (1,1,1)");
    }
```

- [ ] **Step 3: Wait for compile, run all `BuildProxyHierarchy_*` tests — confirm they fail**

Switch to Unity, wait for recompile, check Console (zero errors).

Test Runner → EditMode → filter `BuildProxyHierarchy_`.

Expected: 6 FAIL (4 restructured + 2 new). They fail because the current implementation creates `_ProxyBones` (not `ProxyRig`) under the wrong parent, sets `localScale` to `(width, length, width)`, and doesn't mirror the armature's transform.

Other tests should still PASS: 4 `BuildDiamondMesh_*` + 3 `BuildOrientedDiamondMesh_*` + 3 `EffectiveWidth_*` + 4 `ExtractPairs_*` + 3 `BoneFollower_Tick_*` = 17 PASS.

---

## Task 4: Rewrite `PromeonInteractableRigBuilder` for New Architecture

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

This task makes Task 3's failing tests pass. The whole file is rewritten because the field set changes (no more `_boneMesh`, new `_proxyMeshes`) and several methods get new signatures (`AddMeshAndOutline(go, mesh)`, `AddCollider(go, mesh)`).

- [ ] **Step 1: Replace the entire content of `PromeonInteractableRigBuilder.cs`**

Path: `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\RigBuilder\PromeonInteractableRigBuilder.cs`

Replace the whole file with this exact content:

```csharp
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]
public class PromeonInteractableRigBuilder : MonoBehaviour
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth         = 0.06f;
    [SerializeField] private bool     _useConvexCollider = true;
    private Transform[] _transforms;

    private readonly List<GameObject>   _proxyGOs    = new();
    private readonly List<BoneFollower> _followers   = new();
    private readonly List<Mesh>         _proxyMeshes = new();
    private Transform _proxyRoot;

    void Awake()     { if (_transforms != null && _transforms.Length > 0) Rebuild(); }
    void OnDestroy() => DestroyBoneGOs();

    public void SetTransforms(Transform[] transforms) => _transforms   = transforms;
    public void SetMaterial(Material material)        => _boneMaterial = material;

    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
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
            if (set.Contains(bone.parent)) continue;   // not a root bone
            if (bone.parent == null)       continue;   // root bone at scene root — skip

            if (_proxyRoot == null)
            {
                var armature    = bone.parent;
                var grandParent = armature.parent;

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

    void AddMeshAndOutline(GameObject go, Mesh mesh)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
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

        foreach (var m in _proxyMeshes)
            if (m != null) DestroyObj(m);
        _proxyMeshes.Clear();
    }

    private static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                       DestroyImmediate(obj);
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

    public static Mesh BuildDiamondMesh() => BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);

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
            var v    = baseVerts[i];
            v        = new Vector3(v.x * width, v.y * length, v.z * width);
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

Differences from the previous version:
- `_boneMesh` field removed; `_proxyMeshes` list added (one mesh per proxy)
- `Rebuild()` no longer initializes a shared mesh
- `BuildProxyHierarchy` creates `ProxyRig` (not `_ProxyBones`) parented to `armature.parent` (grandparent of root bone), mirroring `armature.localTransform`
- Root bone with `bone.parent == null` is explicitly skipped (invariant unsatisfiable)
- `BuildProxyNode` computes `localChildDir` and uses `BuildOrientedDiamondMesh(localChildDir, length, width)` per bone
- Proxy GO `localScale = Vector3.one` — width/length baked into mesh, not scale
- `AddMeshAndOutline(go, mesh)` and `AddCollider(go, mesh)` take per-bone mesh
- `DestroyBoneGOs` destroys every mesh in `_proxyMeshes`, not a single shared mesh

- [ ] **Step 2: Wait for Unity recompile, check Console**

Switch to Unity. Wait for spinner. Check Console.

Expected: zero compile errors.

- [ ] **Step 3: Run all EditMode tests — confirm 23 PASS**

Test Runner → EditMode → Run All.

Expected: **23 PASS**, 0 FAIL.

Breakdown:
- 4 `BuildDiamondMesh_*`
- 3 `BuildOrientedDiamondMesh_*`
- 3 `EffectiveWidth_*`
- 4 `ExtractPairs_*`
- 3 `BoneFollower_Tick_*`
- 6 `BuildProxyHierarchy_*` (4 restructured + 2 new)

If `BuildProxyHierarchy_*` tests still fail, check that the test fixture has the `Character → Armature → pelvis → spine` layer order in the GO hierarchy (not the old `Character → pelvis → spine`).

- [ ] **Step 4: Manual Edit Mode smoke test — proxies look right**

In Unity Editor, open a scene with a humanoid skinned mesh:

1. Add `PromeonInteractableRigBuilder` to the character root if not present.
2. Assign `_boneMaterial` in the Inspector.
3. Click the "Rebuild" button (or trigger it via the Inspector editor).

In the Hierarchy:
- A `ProxyRig` GO appears as a SIBLING of the Armature (under the character root), not inside the Armature.
- Inside `ProxyRig`: nested `proxy_<bone>` GOs mirroring the bone hierarchy.
- Each `proxy_<bone>` has `localScale = (1, 1, 1)` (Inspector).
- Each `proxy_<bone>` has a MeshFilter with a unique Mesh asset — diamonds look correctly sized at every depth (no shrinking).

In the Scene view:
- Diamonds visually point from each bone toward the next.
- Deep bones (hands, fingers) are the same physical size as shallow bones — no compounding.

- [ ] **Step 5: Manual Edit Mode smoke test — BoneFollower drives bones**

1. Still in Edit Mode (no Play needed).
2. In the Hierarchy, select a `proxy_*` GO (e.g., `proxy_spine`).
3. In the Inspector, change its Transform Position by, say, `(0.5, 0, 0)` on X.
4. The corresponding original bone (`spine`) should move along with the proxy in the Scene view, and the skinned mesh should deform.

If the bone doesn't move, verify:
- `BoneFollower` has `[ExecuteAlways]` (Task 1)
- The `_proxy` field on the bone's `BoneFollower` is set to the correct proxy Transform (visible in Inspector since it's now `[SerializeField]`)

- [ ] **Step 6: Manual Play Mode smoke test — survives domain reload**

1. Enter Play Mode.
2. Verify all `proxy_*` GOs still exist in `ProxyRig`.
3. Select a `proxy_*` in the Hierarchy, drag it in the Scene view.
4. The bone should follow, mesh should deform.

If movement breaks in Play Mode but worked in Edit Mode: `_proxy` is being reset on domain reload. Verify `_proxy` has `[SerializeField]` in `BoneFollower.cs`.

---

## Done

After Task 4 Step 6 passes:
- Proxies have correct proportions at every hierarchy depth (no scale compounding)
- Diamond visuals point from bone to next bone (rotation baked into mesh)
- `BoneFollower` drives bones in both Edit Mode and Play Mode
- `ProxyRig` lives as a sibling of the Armature, not inside it
- 23 EditMode tests pass
