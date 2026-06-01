# Rig Slice B — Runtime Proxy Rig Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

> **GIT RULE (overrides skill defaults):** Project owner commits manually. **NEVER run `git add`/`git commit`.** Each task ends with a **Checkpoint** (compile + tests), not a commit.

> **Unity workflow:** After `.cs` edits → `refresh_unity (mode:force, scope:all, compile:request)` then `read_console (types:[error], filter_text:"CS")`. Only `error CS####` matters; `MCP-FOR-UNITY: Client handler…`/`MissingReferenceException: m_Targets`/`SerializedObjectNotCreatableException` are harmless. Tests: `run_tests (mode:"EditMode", test_names:[…])` + poll `get_test_job (wait_timeout:60)`. Baseline has **7 known pre-existing failures** (PathProviderTests x4 Windows `\`, PromeonProxyRigBuilderTests.BoneFollower_Tick float, RingRotateStrategyTests x2) — until B3 deletes `PromeonProxyRigBuilderTests`, expect those 7; flag any NEW failure.

**Goal:** Imported and builtin rigs build proxy-bone hierarchies at runtime (Approach A) with per-bone selection/outline, bone-mode toggle, and `BoneFollower` (gizmo drives bone) — reaching parity with today's `Crush Dummy` — by splitting `PromeonProxyRigBuilder` into `RigEntityFactory.BuildProxyRig` (construction) + `ProxyRigRuntime` (runtime coordination).

**Architecture:** `RigEntityFactory.BuildProxyRig(rigRoot, boneNames, cfg)` builds proxies into locals and hands them to a per-rig `ProxyRigRuntime` MonoBehaviour. Construction params come from a Root `ProxyRigConfig` SO (+ outline colors from existing `OutlineConfig`). The factory is invoked from `RigEntityBuilder.RestoreAsync` (import + builtin) and `RigRuntime.ApplyDefinition` (manual rigging). Built in three phases: B1 parallel core, B2 switch consumers, B3 dissolve `PromeonProxyRigBuilder` + clean `Crush Dummy`.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces runtime), VContainer DI, QuickOutline (`Outline`), NUnit (`_App.Tests`).

---

## File Structure

- Create `Assets/_App/Scripts/RigBuilder/ProxyRigConfig.cs` — SO: bone material, width, collider kind.
- Create `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs` — per-rig MonoBehaviour: holds proxy list, selection-outline, visuals/bone-mode toggles.
- Create `Assets/_App/Content/ScriptableObjects/ProxyRigConfig.asset` — default instance.
- Modify `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` — add `BuildProxyRig` (+ moved static mesh helpers).
- Modify `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` — `RestoreAsync` builds proxies after load.
- Modify `Assets/_App/Scripts/RigBuilder/RigRuntime.cs` — `ApplyDefinition` → factory; drop `_boneMaterial`/`SetMaterial`.
- Modify `Assets/_App/Scripts/SpatialUi/Panels/InspectorPanel.cs` + `OutlinerPanel.cs` — detection + `SetBonesInteractive` → `ProxyRigRuntime`.
- Modify `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — register `ProxyRigConfig`; pass to factory ctor.
- Modify `Assets/_App/Content/Prefabs/BuiltInAssets/Crush Dummy.prefab` — strip baked `ProxyRig` + `PromeonProxyRigBuilder`.
- Delete (B3) `PromeonProxyRigBuilder.cs`, `Assets/_App/Editor/PromeonProxyRigBuilderEditor.cs`, `Assets/_App/Tests/RigBuilder/PromeonProxyRigBuilderTests.cs`, `Assets/_App/Scripts/RigBuilder/BoneProxy.cs`.
- Tests: `Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs`, `Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs`.

---

# Phase B1 — Parallel core (nothing existing breaks)

### Task B1.1: `ProxyRigConfig` SO + registration + default asset

**Files:**
- Create: `Assets/_App/Scripts/RigBuilder/ProxyRigConfig.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`
- Create: `Assets/_App/Content/ScriptableObjects/ProxyRigConfig.asset` (orchestrator, see Step 4)

- [ ] **Step 1: Create the SO**

`Assets/_App/Scripts/RigBuilder/ProxyRigConfig.cs`:

```csharp
using UnityEngine;

// Build-time parameters for runtime proxy-rig construction (RigEntityFactory.BuildProxyRig).
// Outline COLORS are not here — they come from OutlineConfig (BoneColor/BoneSelectedColor).
[CreateAssetMenu(menuName = "PromeonLab/ProxyRigConfig", fileName = "ProxyRigConfig")]
public class ProxyRigConfig : ScriptableObject
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth = 0.06f;
    [SerializeField] private bool      _useConvexCollider = true;

    public Material BoneMaterial      => _boneMaterial;
    public float    BoneWidth         => _boneWidth;
    public bool     UseConvexCollider => _useConvexCollider;
}
```

- [ ] **Step 2: Add a serialized slot + registration in `RootLifetimeScope`**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, add a field next to the other configs (after `_importRenderProfile`):

```csharp
    [SerializeField] private ProxyRigConfig     _proxyRigConfig;
```

And register it next to `ReferenceEntityFactory`/`RigEntityFactory` (after the `RigEntityFactory` registration line), with a null-guard fallback so DI never fails:

```csharp
        var proxyRigConfig = _proxyRigConfig != null
            ? _proxyRigConfig
            : ScriptableObject.CreateInstance<ProxyRigConfig>();
        if (_proxyRigConfig == null)
            Debug.LogWarning("RootLifetimeScope: _proxyRigConfig not assigned — proxy bones spawn with no material (outline-only).");
        builder.RegisterInstance(proxyRigConfig);
```

- [ ] **Step 3: Compile**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none.

- [ ] **Step 4: Create the default asset (orchestrator action)**

Create `Assets/_App/Content/ScriptableObjects/ProxyRigConfig.asset` as a `ProxyRigConfig` instance. Populate it with the CURRENT values: `_boneWidth = 0.06`, `_useConvexCollider = true`, and `_boneMaterial` = the material currently assigned to the bone proxies. Find that material's GUID from the `RigRuntime` component's serialized `_boneMaterial` (search the VrEditing/Sandbox scene files) or from the `PromeonProxyRigBuilder` on `Assets/_App/Content/Prefabs/BuiltInAssets/Crush Dummy.prefab` (`_boneMaterial: {fileID, guid}`), and reference the same material asset. Then assign the `.asset` to `RootLifetimeScope._proxyRigConfig` in the scene/prefab that hosts the root scope.

- [ ] **Step 5: Checkpoint** — compile clean; `ProxyRigConfig.asset` created with the existing bone material + defaults and assigned to the root scope.

---

### Task B1.2: `ProxyRigRuntime` (runtime coordinator)

This is the runtime half of `PromeonProxyRigBuilder`: it holds the proxy list and drives selection-outline + visuals/bone-mode toggles. State lives here (per-rig), NOT in the singleton factory. No construction logic, no `RegenerateMissingProxyMeshes`, no `OnEnable` repopulation.

**Files:**
- Create: `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs`
- Test: `Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ProxyRigRuntimeTests
{
    private static GameObject MakeProxy(Transform parent)
    {
        var go = new GameObject("proxy");
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<BoxCollider>();
        return go;
    }

    [Test]
    public void SetBonesInteractive_True_EnablesProxyColliders_AndDisablesRootCollider()
    {
        var root      = new GameObject("rig");
        var rootCol   = root.AddComponent<BoxCollider>();
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var p1 = MakeProxy(proxyRoot.transform);
        var p2 = MakeProxy(proxyRoot.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1, p2 });

        runtime.SetBonesInteractive(true);

        Assert.IsTrue(p1.GetComponent<MeshRenderer>().enabled);
        Assert.IsTrue(p1.GetComponent<Collider>().enabled);
        Assert.IsFalse(rootCol.enabled, "root collider must be OFF in bone mode");

        runtime.SetBonesInteractive(false);
        Assert.IsFalse(p1.GetComponent<MeshRenderer>().enabled);
        Assert.IsTrue(rootCol.enabled, "root collider must be ON outside bone mode");

        Object.DestroyImmediate(root);
    }

    [Test]
    public void SetVisualsEnabled_TogglesRenderers()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var p1 = MakeProxy(proxyRoot.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1 });

        runtime.SetVisualsEnabled(false);
        Assert.IsFalse(p1.GetComponent<MeshRenderer>().enabled);
        runtime.SetVisualsEnabled(true);
        Assert.IsTrue(p1.GetComponent<MeshRenderer>().enabled);

        Object.DestroyImmediate(root);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`run_tests (mode:"EditMode", test_names:["ProxyRigRuntimeTests"])` → FAIL (`ProxyRigRuntime` undefined).

- [ ] **Step 3: Create `ProxyRigRuntime`**

`Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs` (runtime methods lifted from `PromeonProxyRigBuilder`; `Outline` calls guarded so the EditMode test — which adds no `Outline` — passes; outline colors from injected `OutlineConfig`):

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

// Per-rig runtime coordinator for the proxy-bone hierarchy. Built and bound by
// RigEntityFactory.BuildProxyRig. Drives selection outline + visuals/bone-mode toggles.
// Holds no construction logic (that is the factory's job).
public class ProxyRigRuntime : MonoBehaviour
{
    private readonly List<GameObject> _proxyGOs = new();
    private Transform     _proxyRoot;
    private Collider      _rootCollider;       // resolved lazily (added by the registry AFTER build)
    private bool          _rootColliderResolved;

    private EventBus      _eventBus;
    private OutlineConfig _outlineConfig;

    [Inject]
    public void Construct(EventBus bus, OutlineConfig outlineConfig)
    {
        _outlineConfig = outlineConfig;
        if (_eventBus == bus) return;
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = bus;
        if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnDestroy()
    {
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = null;
    }

    // Called once by the factory right after construction.
    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        SetBonesInteractive(false); // start in whole-rig select mode
    }

    private Collider RootCollider()
    {
        if (!_rootColliderResolved)
        {
            _rootCollider = GetComponent<Collider>();
            _rootColliderResolved = true;
        }
        return _rootCollider;
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

    public void SetBonesInteractive(bool enabled)
    {
        if (enabled && _proxyRoot != null && !_proxyRoot.gameObject.activeSelf)
            _proxyRoot.gameObject.SetActive(true);

        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            if (enabled && !go.activeSelf) go.SetActive(true);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = enabled;

            // QuickOutline.OnEnable appends outlineMask/outlineFill without dedupe — strip stacked
            // copies before re-enabling so stencil writes don't conflict (the "bone outline needs a
            // click" bug).
            if (enabled && mr != null)
            {
                var current = mr.sharedMaterials;
                var cleaned = current.Where(m => m == null ||
                    (!m.name.StartsWith("OutlineMask") && !m.name.StartsWith("OutlineFill"))).ToArray();
                if (cleaned.Length != current.Length)
                    mr.materials = cleaned;
            }

            var outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                if (enabled && _outlineConfig != null)
                    outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
                outline.enabled = enabled;
                if (enabled)
                {
                    outline.OutlineMode    = Outline.Mode.SilhouetteOnly;
                    outline.RenderPriority = 1; // above the selected-mesh outline (priority 0)
                }
            }

            var col = go.GetComponent<Collider>();
            if (col != null) col.enabled = enabled;

            if (enabled)
                go.GetComponent<XRPromeonInteractable>()?.SetInteractionLayer(InteractionLayer.BoneProxies);
        }

        var rootCol = RootCollider();
        if (rootCol != null) rootCol.enabled = !enabled;

        if (enabled) ApplyBoneOutlineColors(null);
    }

    private void OnSelectionChanged(SelectionChangedEvent evt) => ApplyBoneOutlineColors(evt.SelectedNodeId);

    private void ApplyBoneOutlineColors(string selectedId)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var sn      = go.GetComponent<SceneNode>();
            var outline = go.GetComponent<Outline>();
            if (sn == null || outline == null || _outlineConfig == null) continue;
            outline.OutlineColor = sn.NodeId == selectedId
                ? _outlineConfig.BoneSelectedColor
                : _outlineConfig.BoneColor;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

`refresh_unity` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode", test_names:["ProxyRigRuntimeTests"])`. Expected PASS (2/2).

- [ ] **Step 5: Checkpoint** — `ProxyRigRuntime` compiles, 2/2 green. (`PromeonProxyRigBuilder` still present, untouched.)

---

### Task B1.3: `RigEntityFactory.BuildProxyRig` (construction)

Construction lifted from `PromeonProxyRigBuilder`, but built into LOCAL collections (factory is a shared singleton) and handed to a freshly-attached `ProxyRigRuntime`. The static mesh builders move here verbatim.

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`
- Test: `Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RigEntityFactoryBuildProxyTests
{
    // Build a minimal skeleton: rigRoot → Armature → Bone → Bone.001, with a SkinnedMeshRenderer
    // whose .bones lists the two bones. (No mesh asset needed for .bones.)
    private static (GameObject root, SkinnedMeshRenderer smr) MakeSkeleton()
    {
        var root     = new GameObject("rig");
        var armature  = new GameObject("Armature"); armature.transform.SetParent(root.transform);
        var bone     = new GameObject("Bone");      bone.transform.SetParent(armature.transform);
        bone.transform.localPosition = Vector3.zero;
        var boneChild = new GameObject("Bone.001"); boneChild.transform.SetParent(bone.transform);
        boneChild.transform.localPosition = new Vector3(0f, 1f, 0f);

        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform, boneChild.transform };
        return (root, smr);
    }

    private static RigEntityFactory MakeFactory()
    {
        var cfg = ScriptableObject.CreateInstance<ProxyRigConfig>();
        return new RigEntityFactory(new GltfModelLoader(), cfg);
    }

    [Test]
    public void BuildProxyRig_AllBones_CreatesProxyHierarchyAndRuntime()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, null); // null → all smr.bones

        Assert.IsNotNull(root.GetComponent<ProxyRigRuntime>(), "ProxyRigRuntime attached to rig root");
        var proxyRoot = root.transform.Find("Armature/ProxyRig") ?? FindDeep(root.transform, "ProxyRig");
        Assert.IsNotNull(proxyRoot, "ProxyRig container created");
        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(true);
        Assert.AreEqual(2, markers.Length, "one proxy per bone");
        Assert.AreEqual(2, root.GetComponentsInChildren<BoneFollower>(true).Length, "a follower per bone");

        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_NoBones_IsNoOp()
    {
        var root = new GameObject("empty");
        root.AddComponent<SkinnedMeshRenderer>().bones = new Transform[0];

        MakeFactory().BuildProxyRig(root, null);

        Assert.IsNull(root.GetComponent<ProxyRigRuntime>(), "no skeleton → no proxy rig");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_NamedSubset_BuildsOnlyMatchedBones()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, new List<string> { "Bone" }); // only the root bone

        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(true);
        Assert.AreEqual(1, markers.Length, "only the named bone gets a proxy");
        Object.DestroyImmediate(root);
    }

    private static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`run_tests (mode:"EditMode", test_names:["RigEntityFactoryBuildProxyTests"])` → FAIL (`BuildProxyRig` undefined; ctor arity mismatch).

- [ ] **Step 3: Rewrite `RigEntityFactory`**

Replace `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` entirely:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Rig: loads the static mesh AND builds the proxy-bone
// hierarchy. BuildProxyRig is the single construction core, invoked from RigEntityBuilder.RestoreAsync
// (import + builtin) and RigRuntime.ApplyDefinition (manual rigging). The factory is a shared
// singleton, so proxies are built into LOCALS and handed to a per-rig ProxyRigRuntime.
public class RigEntityFactory
{
    private readonly GltfModelLoader _loader;
    private readonly ProxyRigConfig  _config;

    public RigEntityFactory(GltfModelLoader loader, ProxyRigConfig config)
    {
        _loader = loader;
        _config = config;
    }

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);

    // Builds the proxy hierarchy onto rigRoot and attaches a bound ProxyRigRuntime.
    // boneNames: from recipe.rig (import) → mapped to live bones by name; null → all SkinnedMeshRenderer.bones
    // (builtin / manual rigging). No-op if there is no skeleton.
    public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames)
    {
        var transforms = ResolveTransforms(rigRoot, boneNames);
        if (transforms == null || transforms.Length == 0) return;

        var proxyGOs    = new List<GameObject>();
        Transform proxyRoot = null;

        var set = new HashSet<Transform>(transforms);
        set.Remove(null);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue; // not a root bone of the selected set
            if (bone.parent == null)       continue;

            if (proxyRoot == null)
            {
                var armature    = bone.parent;
                var grandParent = armature.parent;
                var rig = new GameObject("ProxyRig");
                rig.transform.SetParent(grandParent, worldPositionStays: false);
                rig.transform.localPosition = armature.localPosition;
                rig.transform.localRotation = armature.localRotation;
                rig.transform.localScale    = armature.localScale;
                proxyRoot = rig.transform;
            }

            BuildProxyNode(bone, proxyRoot, set, proxyGOs);
        }

        if (proxyRoot == null) return; // skeleton present but no buildable root bone

        var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot, proxyGOs);
    }

    private Transform[] ResolveTransforms(GameObject rigRoot, IReadOnlyList<string> boneNames)
    {
        var smr = rigRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

        if (boneNames == null || boneNames.Count == 0)
            return smr.bones;

        var wanted = new HashSet<string>(boneNames);
        return smr.bones.Where(b => b != null && wanted.Contains(b.name)).ToArray();
    }

    private void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set, List<GameObject> proxyGOs)
    {
        var children = new List<Transform>();
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (set.Contains(c)) children.Add(c);
        }

        Mesh mesh;
        if (children.Count > 0)
        {
            mesh = BuildCombinedDiamondMesh(bone, children, _config.BoneWidth);
        }
        else
        {
            var worldDir    = bone.position - bone.parent.position;
            float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
            float length    = parentLen * 0.5f;
            Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
            if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
            float width = EffectiveWidth(_config.BoneWidth, length);
            mesh = BuildOrientedDiamondMesh(localChildDir, length, width);
        }

        var proxyGo = new GameObject($"proxy_{bone.name}");
        proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
        proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
        proxyGo.transform.localScale = Vector3.one;

        proxyGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = proxyGo.AddComponent<MeshRenderer>();
        if (_config.BoneMaterial == null)
            Debug.LogWarning("RigEntityFactory: ProxyRigConfig.BoneMaterial not assigned — proxy renders outline-only.");
        mr.sharedMaterial = _config.BoneMaterial;

        var outline          = proxyGo.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineWidth = 3f;

        if (_config.UseConvexCollider)
        {
            var mc = proxyGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex     = true;
        }
        else
        {
            var col = proxyGo.AddComponent<CapsuleCollider>();
            col.direction = 1; col.height = 1f; col.radius = 0.5f;
        }

        var sceneNode = proxyGo.AddComponent<SceneNode>();
        sceneNode.Init(bone.name, default, bone.name);
        proxyGo.AddComponent<BoneSceneNodeMarker>();
        proxyGo.AddComponent<Selectable>();
        proxyGo.AddComponent<XRPromeonInteractable>().SetInteractionLayer(InteractionLayer.BoneProxies);

        proxyGOs.Add(proxyGo);

        foreach (var stale in bone.GetComponents<BoneFollower>())
            UnityEngine.Object.Destroy(stale);
        bone.gameObject.AddComponent<BoneFollower>().SetProxy(proxyGo.transform);

        foreach (var child in children)
            BuildProxyNode(child, proxyGo.transform, set, proxyGOs);
    }

    // ---- Static mesh builders (moved verbatim from PromeonProxyRigBuilder) ----

    private static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);

    private static Mesh BuildOrientedDiamondMesh(Vector3 localLongAxis, float length, float width)
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
            v = new Vector3(v.x * width, v.y * length, v.z * width);
            verts[i] = rot * v;
        }
        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = verts;
        mesh.triangles = new[] { 0,1,3, 0,3,2, 0,2,4, 0,4,1, 1,5,3, 3,5,2, 2,5,4, 4,5,1 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildCombinedDiamondMesh(Transform bone, List<Transform> children, float boneWidth)
    {
        var allVerts = new List<Vector3>();
        var allTris  = new List<int>();
        foreach (var child in children)
        {
            var worldDir = child.position - bone.position;
            float length        = Mathf.Max(worldDir.magnitude, 0.0001f);
            Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
            if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
            float width = EffectiveWidth(boneWidth, length);
            var rot = Quaternion.FromToRotation(Vector3.up, localChildDir);

            int baseIdx = allVerts.Count;
            var baseVerts = new[]
            {
                new Vector3( 0f,    0f,    0f),
                new Vector3( 0.5f,  0.15f, 0f),
                new Vector3(-0.5f,  0.15f, 0f),
                new Vector3( 0f,    0.15f, 0.5f),
                new Vector3( 0f,    0.15f,-0.5f),
                new Vector3( 0f,    1f,    0f),
            };
            foreach (var v in baseVerts)
            {
                var scaled = new Vector3(v.x * width, v.y * length, v.z * width);
                allVerts.Add(rot * scaled);
            }
            int[] tris = { 0,1,3, 0,3,2, 0,2,4, 0,4,1, 1,5,3, 3,5,2, 2,5,4, 4,5,1 };
            foreach (var t in tris) allTris.Add(t + baseIdx);
        }
        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = allVerts.ToArray();
        mesh.triangles = allTris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
}
```

- [ ] **Step 4: Update the factory registration arity**

`RigEntityFactory` now takes `(GltfModelLoader, ProxyRigConfig)`. Its `RootLifetimeScope` registration line (`builder.Register<RigEntityFactory>(Lifetime.Singleton);`) is unchanged — VContainer resolves both deps (`GltfModelLoader` + the `ProxyRigConfig` instance registered in B1.1). No edit needed; just confirm both are registered.

- [ ] **Step 5: Run to verify it passes**

`refresh_unity` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode", test_names:["RigEntityFactoryBuildProxyTests"])`. Expected PASS (3/3).

- [ ] **Step 6: Checkpoint** — `BuildProxyRig` builds proxies + binds runtime; 3/3 green. `PromeonProxyRigBuilder` still untouched & unused by new code.

---

# Phase B2 — Switch consumers to the new components

### Task B2.1: `RigEntityBuilder.RestoreAsync` builds proxies (import + builtin)

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`

- [ ] **Step 1: Update `RestoreAsync`**

Replace the `RestoreAsync` method in `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` so it builds proxies after loading (both branches), via the factory. `BuildAsync` is unchanged.

```csharp
    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        GameObject go;
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            go = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
        }
        else
        {
            if (string.IsNullOrEmpty(asset.SourceRef))
                throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");
            go = await _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
        }

        if (go == null) return null;

        // Build the proxy-bone hierarchy. Imported → bone names from the recipe; builtin → all live bones.
        // No-op when there is no skeleton (graceful static fallback). Whole-rig selectability is applied
        // by the registry; ProxyRigRuntime + per-bone selection are wired via InjectGameObject(go) there.
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames);

        return go;
    }
```

Add the needed usings at the top of the file if missing: `using System.Collections.Generic;` and `using System.Linq;`.

- [ ] **Step 2: Compile**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none.

- [ ] **Step 3: Checkpoint** — compiles; rig restore now constructs proxies for builtin (all bones) and imported (recipe bones). (`PromeonProxyRigBuilder` no longer involved in the import path.)

---

### Task B2.2: `RigRuntime.ApplyDefinition` → factory; drop `_boneMaterial`

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`

- [ ] **Step 1: Rewrite `RigRuntime`**

Replace `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`. `BuildFromSkinnedMesh` delegates to the Slice-A extractor; `ApplyDefinition` delegates construction to the factory and injects DI. The `_boneMaterial` field + `SetMaterial` are removed (params now live in `ProxyRigConfig`).

```csharp
using System.Linq;
using UnityEngine;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    private IObjectResolver _resolver;
    private RigEntityFactory _factory;

    [Inject]
    public void Construct(IObjectResolver resolver, RigEntityFactory factory)
    {
        _resolver = resolver;
        _factory  = factory;
    }

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
        => RigDefinitionExtractor.FromSkinnedMesh(smr) ?? new RigDefinition { AssetId = smr != null ? smr.gameObject.name : "" };

    public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
    {
        if (smr == null) return;
        var rigRoot   = smr.transform.root.gameObject;
        var boneNames = definition?.Bones != null && definition.Bones.Count > 0
            ? definition.Bones.Select(b => b.BoneName).ToList()
            : null;

        _factory.BuildProxyRig(rigRoot, boneNames);

        // Proxies get programmatic Selectable/XRPromeonInteractable/SceneNode + ProxyRigRuntime;
        // wire their [Inject] deps now (recursive over children).
        _resolver?.InjectGameObject(rigRoot);
    }
}
```

(Note: `RigRuntime` is scene-scoped and now depends on the root-scoped `RigEntityFactory` — allowed, child→parent. If `IRigRuntime` does not already match these signatures, it does — `BuildFromSkinnedMesh(SkinnedMeshRenderer)` and `ApplyDefinition(RigDefinition, SkinnedMeshRenderer)` are unchanged.)

- [ ] **Step 2: Confirm scene wiring still resolves**

`RigRuntime` is found via `FindAnyObjectByType<RigRuntime>` in `VrEditingSceneScope`/`SandboxSceneScope` and injected. Its new `RigEntityFactory` dep is root-registered, so injection resolves. No scope edit needed. (The scene's `RigRuntime` component previously had a `_boneMaterial` serialized value — now an orphaned serialized field, harmless; Unity drops it.)

- [ ] **Step 3: Compile**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none.

- [ ] **Step 4: Checkpoint** — `RigRuntime` builds via the factory; `BoneInspectorPanel`/`IkWizardPanel` authoring now goes through the new path. Compiles clean.

---

### Task B2.3: Panels detect/drive `ProxyRigRuntime`

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/InspectorPanel.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs`

- [ ] **Step 1: `InspectorPanel` — swap the type in all 5 sites**

In `Assets/_App/Scripts/SpatialUi/Panels/InspectorPanel.cs`, replace every `PromeonProxyRigBuilder` with `ProxyRigRuntime`. There are local variables typed `PromeonProxyRigBuilder rig` (lines ~117, ~263) and `GetComponentInChildren<PromeonProxyRigBuilder>(true)` calls (lines ~125, ~134, ~148, ~268, ~274, ~279), plus `rig.SetBonesInteractive(value)` (line ~284) — `ProxyRigRuntime` exposes the same `SetBonesInteractive(bool)` method, so the call is unchanged. Use a find/replace of the identifier `PromeonProxyRigBuilder` → `ProxyRigRuntime` within this file.

- [ ] **Step 2: `OutlinerPanel` — swap the detection type**

In `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs` line ~106:

```csharp
            var isRig = node.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true) != null;
```

- [ ] **Step 3: Compile**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none.

- [ ] **Step 4: Checkpoint** — panels now detect rigs by `ProxyRigRuntime` and drive its bone-mode toggle. After B2, nothing constructs `PromeonProxyRigBuilder`. Compiles clean; run full EditMode and confirm only the 7 known pre-existing failures.

---

# Phase B3 — Dissolve `PromeonProxyRigBuilder` + clean `Crush Dummy`

### Task B3.1: Delete `PromeonProxyRigBuilder`, its editor, its tests, and dead `BoneProxy`

**Files:**
- Delete: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs` (+`.meta`)
- Delete: `Assets/_App/Editor/PromeonProxyRigBuilderEditor.cs` (+`.meta`)
- Delete: `Assets/_App/Tests/RigBuilder/PromeonProxyRigBuilderTests.cs` (+`.meta`)
- Delete: `Assets/_App/Scripts/RigBuilder/BoneProxy.cs` (+`.meta`)

- [ ] **Step 1: Confirm no remaining references**

Grep the codebase for `PromeonProxyRigBuilder` and `BoneProxy` under `Assets/_App/Scripts` and `Assets/_App/Editor`. Expected: zero (B2 removed the last code references; `BoneProxy` was never referenced). If any remain, STOP and report — do not delete.

- [ ] **Step 2: Delete the four files (+ their `.meta`)**

Orchestrator deletes via absolute `-LiteralPath` (repo path contains brackets). Files:
`Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs`, `Assets/_App/Editor/PromeonProxyRigBuilderEditor.cs`, `Assets/_App/Tests/RigBuilder/PromeonProxyRigBuilderTests.cs`, `Assets/_App/Scripts/RigBuilder/BoneProxy.cs` — each with its `.cs.meta`.

- [ ] **Step 3: Compile**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none. The `Crush Dummy` prefab still has a baked `PromeonProxyRigBuilder` MonoBehaviour reference that is now a missing script — that is expected and resolved in B3.2; it does NOT cause a CS error.

- [ ] **Step 4: Checkpoint** — four files gone; compiles. Full EditMode now shows **5** pre-existing failures (the 2 `PromeonProxyRigBuilderTests` are gone; PathProvider x4 — wait, recount: PathProvider x4 + RingRotate x2 = 6 remain, the 1 BoneFollower float test was inside PromeonProxyRigBuilderTests and is now gone). Expected remaining: PathProviderTests x4 + RingRotateStrategyTests x2 = **6**. Flag anything else.

---

### Task B3.2: Strip baked proxies from `Crush Dummy` prefab

The builtin rig must now build proxies at runtime (Approach A), so its prefab should be a plain skinned mesh with no baked `ProxyRig` child and no `PromeonProxyRigBuilder` (now a missing script).

**Files:**
- Modify: `Assets/_App/Content/Prefabs/BuiltInAssets/Crush Dummy.prefab`

- [ ] **Step 1: Inspect the prefab in the Unity Editor**

Open `Assets/_App/Content/Prefabs/BuiltInAssets/Crush Dummy.prefab`. Identify: (a) the `ProxyRig` child GameObject (baked proxy hierarchy), (b) the missing-script component left by the deleted `PromeonProxyRigBuilder`, (c) any baked `BoneFollower` components on the bone transforms.

- [ ] **Step 2: Remove baked proxy artifacts**

Delete the `ProxyRig` child GameObject and all its descendants. Remove the missing-script component from the GameObject that hosted `PromeonProxyRigBuilder`. Remove any `BoneFollower` components baked onto the armature bones. Leave the SkinnedMeshRenderer + armature/skeleton intact. Save the prefab.

(Use `manage_gameobject`/`manage_prefabs` MCP or the Editor UI. Verify via `read_console` for missing-script warnings cleared.)

- [ ] **Step 3: Checkpoint** — `Crush Dummy` is a clean skinned-mesh prefab; no missing scripts, no baked `ProxyRig`. Compiles; full EditMode = 6 pre-existing failures.

---

### Task B3.3: Final verification

- [ ] **Step 1: Full EditMode run** — `run_tests (mode:"EditMode")`; confirm only the 6 known pre-existing failures, plus the new `ProxyRigRuntimeTests` (2) and `RigEntityFactoryBuildProxyTests` (3) green.

- [ ] **Step 2: VR — imported rig parity** — import a rigged `.glb`; spawn → mesh appears, selectable as whole; enter bone mode via the Inspector ShowBones toggle → proxy bones appear, per-bone selectable with outline; grab a bone proxy with the gizmo → the skeleton bone follows; exit bone mode → whole-rig select restored.

- [ ] **Step 3: VR — builtin Crush Dummy parity** — spawn `Crush Dummy`; same checks as Step 2 (proxies built at runtime now, not baked).

- [ ] **Step 4: VR — manual rigging** — select an in-scene skinned mesh, use `BoneInspectorPanel`/`IkWizard` to build the rig → proxies appear and behave as above.

- [ ] **Step 5: VR — persistence** — reload scene + restart app; rigs reappear, bone mode still works.

- [ ] **Step 6: Checkpoint** — Slice B behaviorally verified. Hand to user for commit. (Slice C = `*BakeTool`; doc cleanup = task #16.)

---

## Self-Review

**1. Spec coverage:**
- `RigEntityFactory.BuildProxyRig` (construction, locals→runtime) — B1.3 ✓
- `ProxyRigRuntime` (selection-outline, SetVisualsEnabled, SetBonesInteractive, lazy root collider) — B1.2 ✓
- `ProxyRigConfig` (material/width/collider) + default asset + Root registration; colors from OutlineConfig — B1.1 ✓
- Approach A: proxies runtime-built for builtin too — B2.1 (builtin branch builds proxies) + B3.2 (strip baked) ✓
- `RigRuntime` kept + rewired to factory; `BuildFromSkinnedMesh` via extractor; `SetMaterial`/`_boneMaterial` dropped — B2.2 ✓
- Panels detect/drive `ProxyRigRuntime` — B2.3 ✓
- Delete `PromeonProxyRigBuilder`(+editor+tests) + dead `BoneProxy` — B3.1 ✓
- Bone source rule (recipe names vs all bones) — B1.3 `ResolveTransforms` + B2.1 ✓
- Error handling: no-skeleton no-op (B1.3 ResolveTransforms/early return), null material warning (B1.3), outline-stack cleanup (B1.2 SetBonesInteractive) ✓
- DI via InjectGameObject — B2.1 (registry call site, unchanged) + B2.2 (ApplyDefinition) ✓
- Testing: BuildProxyRig synthetic skeleton + ProxyRigRuntime toggles — B1.2/B1.3 ✓; outline-color-by-selection covered indirectly (ApplyBoneOutlineColors), VR for real glTF/Crush Dummy — B3.3 ✓
- Slice A debt (AssetId) — not load-bearing for B (mapping by BoneName); left as-is, noted in spec. ✓

**2. Placeholder scan:** No TBD/vague steps; full code in every code step; deletions list exact files. ✓

**3. Type consistency:** `BuildProxyRig(GameObject, IReadOnlyList<string>)`, `ProxyRigRuntime.Bind(Transform, List<GameObject>)` / `SetBonesInteractive(bool)` / `SetVisualsEnabled(bool)`, `RigEntityFactory(GltfModelLoader, ProxyRigConfig)`, `ProxyRigConfig.BoneMaterial/BoneWidth/UseConvexCollider`, `RigDefinitionExtractor.FromSkinnedMesh`, `OutlineConfig.MaskMaterial/FillMaterial/BoneColor/BoneSelectedColor` — consistent across tasks. `RigEntityBuilder` ctor unchanged from Slice A (`AssetSourceStore, RigEntityFactory, IColliderStrategy`); only `RestoreAsync` body changes (B2.1). ✓

> Note: B1.3 changes `RigEntityFactory`'s ctor arity (adds `ProxyRigConfig`). The `BuildProxyRigTests` construct it directly with `new RigEntityFactory(new GltfModelLoader(), cfg)`; the Slice-A registration resolves the new dep from the B1.1 registration. `RigEntityBuilder` resolves `RigEntityFactory` via DI (unaffected).
