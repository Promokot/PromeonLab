# Type-Keyed Selection Colliders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the selection collider chosen by asset type — Object → convex MeshCollider(s), Rig → box colliders along the skeleton to depth 3, Reference → unchanged Box — with the shape built at restore from live geometry.

**Architecture:** The recipe stores only `colliderKind` (+ `boneColliderDepth`). Bake just picks the kind. At restore, `InteractionCapability.Apply` builds Box / per-renderer ConvexMesh; the Rig's bone-box selector set is built by `RigEntityFactory` during its existing skeleton walk, owned/toggled by `ProxyRigRuntime`, and registered to the rig's root interactable after `Apply`. The old single-box `IColliderStrategy`/`BoundsBoxColliderStrategy` becomes dead and is removed.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces runtime; editor code in `_App.Editor`), VContainer, XRI (`XRPromeonInteractable`), NUnit (EditMode), Unity MCP for refresh/console/tests.

## Project Rules (read before starting)

- **NO `git add` / `git commit`** — the user commits manually. Every task ends with a **Checkpoint**, not a commit.
- **Checkpoint procedure:**
  1. `refresh_unity` (`mode: force, scope: all, compile: request`).
  2. `read_console` (`types: ["error"], filter_text: "CS"`). Only `error CS####` matters. `MCP-FOR-UNITY: …`, `MissingReferenceException: m_Targets`, `SerializedObjectNotCreatableException` are harmless — ignore.
  3. For tasks with EditMode tests: `run_tests` (`mode: "EditMode", test_names: [<the test class names>]`) then `get_test_job` (`wait_timeout: 60`).
- **EditMode baseline = 6 known pre-existing failures** (`PathProviderTests` ×4, `RingRotateStrategyTests` ×2). Green = zero *new* failures.
- PowerShell paths use `-LiteralPath` (repo path has `[02]`).
- Conventions: `[SerializeField] private`; one public type per file; no `#if UNITY_EDITOR` in runtime files; forbidden type-name suffixes Manager/Handler/Utils/Helper/Controller/Processor/Service (Factory/Builder/Planner/Config are fine).

---

## File Structure

**Modify (runtime):**
- `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs` — add `ConvexMesh`, `BoneBoxes`.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs` — add `int boneColliderDepth = 3`.
- `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs` — type-keyed collider build.
- `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs` — kind = ConvexMesh; drop `IColliderStrategy`.
- `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` — kind = BoneBoxes (or ConvexMesh fallback); drop `IColliderStrategy`.
- `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` — build selector boxes during the bone walk; thread `selectorDepth`.
- `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs` — `_selectorColliders` list + `RegisterSelectorColliders()`; toggle in `SetBonesInteractive`.
- `Assets/_App/Scripts/RigBuilder/RigRuntime.cs` — pass depth to `BuildProxyRig`; register selectors.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` — after `Apply`, trigger `RegisterSelectorColliders()` for `BoneBoxes`.
- `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — remove the `IColliderStrategy` registration.
- `Assets/_App/Editor/BuiltinRecipeBaker.cs` — drop the `BoundsBoxColliderStrategy` usage/arg.

**Create:**
- `Assets/_App/Scripts/RigBuilder/BoneSelectorBoxPlanner.cs` — pure traversal helper + `BoneBoxPlan`.

**Delete:**
- `Assets/_App/Scripts/VrInteraction/IColliderStrategy.cs`
- `Assets/_App/Scripts/VrInteraction/BoundsBoxColliderStrategy.cs`

**Tests (create/modify):**
- `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeColliderTests.cs` (new).
- `Assets/_App/Tests/RigBuilder/BoneSelectorBoxPlannerTests.cs` (new).
- `Assets/_App/Tests/VrInteraction/InteractionCapabilityConvexTests.cs` (new).
- `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs` (modify — ctor + kind).
- `Assets/_App/Tests/AssetBrowser/RigEntityBuilderRecipeTests.cs` (modify — signature + kind).

---

## Task 1: `ColliderKind` values + recipe depth field

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs`
- Test: `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeColliderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeColliderTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeColliderTests
{
    [Test]
    public void ColliderKind_HasConvexMeshAndBoneBoxes()
    {
        Assert.AreEqual(2, (int)ColliderKind.ConvexMesh);
        Assert.AreEqual(3, (int)ColliderKind.BoneBoxes);
    }

    [Test]
    public void Recipe_RoundTrips_KindAndBoneDepth()
    {
        var recipe = new AssetEntityRecipe { colliderKind = ColliderKind.BoneBoxes, boneColliderDepth = 3 };
        var back   = JsonUtility.FromJson<AssetEntityRecipe>(JsonUtility.ToJson(recipe));
        Assert.AreEqual(ColliderKind.BoneBoxes, back.colliderKind);
        Assert.AreEqual(3, back.boneColliderDepth);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` EditMode, `test_names: ["AssetEntityRecipeColliderTests"]`, `get_test_job`.
Expected: FAIL — `ConvexMesh`/`BoneBoxes`/`boneColliderDepth` don't exist (compile error).

- [ ] **Step 3: Extend the enum**

Replace `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs` with:

```csharp
// How the entity's selection collider is shaped. Int values are serialized in recipes — append only,
// never reorder. Box = single AABB; ConvexMesh = per-renderer convex hull; BoneBoxes = boxes along
// the skeleton (see BoneSelectorBoxPlanner).
public enum ColliderKind
{
    None       = 0,
    Box        = 1,
    ConvexMesh = 2,
    BoneBoxes  = 3,
}
```

- [ ] **Step 4: Add the depth field to the recipe**

In `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs`, add this field right after the `colliderSize` line (`public Vector3 colliderSize = Vector3.one;`):

```csharp

    // BoneBoxes only: how deep into the skeleton to place selector boxes (see BoneSelectorBoxPlanner).
    public int              boneColliderDepth = 3;
```

- [ ] **Step 5: Run test to verify it passes**

`run_tests` EditMode, `test_names: ["AssetEntityRecipeColliderTests"]`, `get_test_job`. Expected: PASS (2).

- [ ] **Step 6: Checkpoint** — refresh (force/all/compile) → console (no CS) → `run_tests ["AssetEntityRecipeColliderTests"]` PASS. No commit.

---

## Task 2: `BoneSelectorBoxPlanner` (pure traversal)

**Files:**
- Create: `Assets/_App/Scripts/RigBuilder/BoneSelectorBoxPlanner.cs`
- Test: `Assets/_App/Tests/RigBuilder/BoneSelectorBoxPlannerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/RigBuilder/BoneSelectorBoxPlannerTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class BoneSelectorBoxPlannerTests
{
    // root(0) → a(1) → b(2) → c(3) → d(4). maxDepth 3 → entries for root,a,b,c; c (depth 3)
    // encapsulates d; no entry for d.
    [Test]
    public void Plan_StopsAtDepth_AndDepth3SwallowsSubtree()
    {
        var t = new Transform[5];
        for (int i = 0; i < 5; i++) t[i] = new GameObject($"b{i}").transform;
        for (int i = 1; i < 5; i++) t[i].SetParent(t[i - 1]);
        t[4].position = new Vector3(0, 0, 10f); // distinct so encapsulation is observable
        try
        {
            var plan = BoneSelectorBoxPlanner.Plan(t[0], maxDepth: 3);

            var bones = plan.Select(p => p.Bone).ToList();
            Assert.Contains(t[0], bones);
            Assert.Contains(t[1], bones);
            Assert.Contains(t[2], bones);
            Assert.Contains(t[3], bones);
            Assert.IsFalse(bones.Contains(t[4]), "depth 4 bone gets no own entry");

            var depth3 = plan.First(p => p.Bone == t[3]);
            Assert.IsTrue(depth3.WorldOrigins.Contains(t[4].position),
                "depth-3 box must encapsulate the entire remaining subtree (the depth-4 origin)");
        }
        finally { for (int i = 0; i < 5; i++) Object.DestroyImmediate(t[i].gameObject); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` EditMode, `test_names: ["BoneSelectorBoxPlannerTests"]`, `get_test_job`.
Expected: FAIL — `BoneSelectorBoxPlanner` does not exist.

- [ ] **Step 3: Implement the planner**

Create `Assets/_App/Scripts/RigBuilder/BoneSelectorBoxPlanner.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

// One planned selector box: the bone it attaches to, and the world-space origins its box must
// encapsulate (converted to a bone-local AABB by the caller).
public readonly struct BoneBoxPlan
{
    public readonly Transform     Bone;
    public readonly List<Vector3> WorldOrigins;
    public BoneBoxPlan(Transform bone, List<Vector3> worldOrigins) { Bone = bone; WorldOrigins = worldOrigins; }
}

// Pure, build-time: walks the skeleton from `root` (depth 0). Every bone at depth 0..maxDepth gets a
// plan entry. depth < maxDepth → encapsulate the bone + its DIRECT children. depth == maxDepth →
// encapsulate the bone + ALL descendants. depth > maxDepth → no entry. `bones`, when non-null,
// restricts traversal to that set (the proxied bones); null = every transform child counts as a bone.
public static class BoneSelectorBoxPlanner
{
    public static List<BoneBoxPlan> Plan(Transform root, int maxDepth, HashSet<Transform> bones = null)
    {
        var result = new List<BoneBoxPlan>();
        if (root != null) Walk(root, 0, maxDepth, bones, result);
        return result;
    }

    private static void Walk(Transform bone, int depth, int maxDepth, HashSet<Transform> bones, List<BoneBoxPlan> result)
    {
        if (depth > maxDepth) return;

        var origins = new List<Vector3> { bone.position };
        if (depth < maxDepth)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (!IsBone(c, bones)) continue;
                origins.Add(c.position);
            }
            result.Add(new BoneBoxPlan(bone, origins));
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (!IsBone(c, bones)) continue;
                Walk(c, depth + 1, maxDepth, bones, result);
            }
        }
        else // depth == maxDepth: swallow the whole remaining subtree
        {
            CollectDescendants(bone, bones, origins);
            result.Add(new BoneBoxPlan(bone, origins));
        }
    }

    private static void CollectDescendants(Transform bone, HashSet<Transform> bones, List<Vector3> origins)
    {
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (!IsBone(c, bones)) continue;
            origins.Add(c.position);
            CollectDescendants(c, bones, origins);
        }
    }

    private static bool IsBone(Transform t, HashSet<Transform> bones) => bones == null || bones.Contains(t);
}
```

- [ ] **Step 4: Run test to verify it passes**

`run_tests` EditMode, `test_names: ["BoneSelectorBoxPlannerTests"]`, `get_test_job`. Expected: PASS.

- [ ] **Step 5: Checkpoint** — refresh → console (no CS) → `run_tests ["BoneSelectorBoxPlannerTests"]` PASS. No commit.

---

## Task 3: `InteractionCapability` type-keyed collider build

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs`
- Test: `Assets/_App/Tests/VrInteraction/InteractionCapabilityConvexTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/VrInteraction/InteractionCapabilityConvexTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class InteractionCapabilityConvexTests
{
    [Test]
    public void Apply_ConvexMesh_AddsConvexMeshColliderPerRenderer_AndRegisters()
    {
        var root = new GameObject("obj");
        var a = GameObject.CreatePrimitive(PrimitiveType.Cube);   // has MeshFilter
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); // has MeshFilter
        // strip the primitives' own colliders so only our convex ones are counted
        Object.DestroyImmediate(a.GetComponent<Collider>());
        Object.DestroyImmediate(b.GetComponent<Collider>());
        a.transform.SetParent(root.transform);
        b.transform.SetParent(root.transform);
        try
        {
            InteractionCapability.Apply(root, InteractionLayer.SceneObjects,
                ColliderKind.ConvexMesh, Vector3.zero, Vector3.one, selectable: true);

            var meshCols = root.GetComponentsInChildren<MeshCollider>(true);
            Assert.AreEqual(2, meshCols.Length, "one convex MeshCollider per mesh renderer");
            Assert.IsTrue(meshCols.All(m => m.convex));

            var it = root.GetComponent<XRPromeonInteractable>();
            Assert.IsNotNull(it);
            foreach (var m in meshCols)
                Assert.IsTrue(it.IsRegistered(m), "each convex collider must be registered to the interactable");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` EditMode, `test_names: ["InteractionCapabilityConvexTests"]`, `get_test_job`.
Expected: FAIL — `ConvexMesh` builds nothing today, and `XRPromeonInteractable.IsRegistered` doesn't exist.

- [ ] **Step 3: Add an `IsRegistered` query to the interactable**

In `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs`, add this method right after `RegisterColliders` (around line 41):

```csharp
    public bool IsRegistered(Collider c) => c != null && colliders.Contains(c);
```

- [ ] **Step 4: Make `InteractionCapability.Apply` type-keyed**

Replace `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

// The single definition of "make this GameObject a selectable scene entity".
// Pure application from the recipe's collider kind. Idempotent: skips if an XRPromeonInteractable is
// already present (a baked built-in prefab), so it can never disturb working assets.
//   Box        → one BoxCollider on the root (center/size from the recipe).
//   ConvexMesh → one convex MeshCollider per mesh-bearing renderer, registered to the interactable.
//   BoneBoxes  → built on the rig side (RigEntityFactory) and registered via
//                ProxyRigRuntime.RegisterSelectorColliders(); nothing is built here.
public static class InteractionCapability
{
    public static void Apply(
        GameObject root,
        InteractionLayer layer,
        ColliderKind colliderKind,
        Vector3 colliderCenter,
        Vector3 colliderSize,
        bool selectable)
    {
        if (root == null) return;
        if (root.GetComponent<XRPromeonInteractable>() != null) return; // already a complete entity

        // 1) Identity FIRST so Selectable/XRPromeonInteractable Awake-time lookups resolve.
        //    NodeId is stamped later by SceneGraph.AddNode (re-uses this SceneNode).
        if (root.GetComponent<SceneNode>() == null) root.AddComponent<SceneNode>();

        // 2) Build the collider shape for this kind. Box sits on the root (auto-discovered by the
        //    interactable's Awake); ConvexMesh colliders sit on child renderers and are registered
        //    explicitly below. BoneBoxes builds nothing here.
        List<Collider> childColliders = null;
        if (colliderKind == ColliderKind.Box)
        {
            var box    = root.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size   = colliderSize;
            box.gameObject.SetInteractionLayer(layer);
        }
        else if (colliderKind == ColliderKind.ConvexMesh)
        {
            childColliders = BuildConvexColliders(root);
            if (childColliders.Count == 0)
                Debug.LogWarning($"InteractionCapability: '{root.name}' has no mesh renderers — no convex collider built.");
        }

        if (!selectable) return;

        // 3) Outline driver + input-driven select/move/rotate. DI (Construct) wired later by
        //    IObjectResolver.InjectGameObject at the call site.
        root.AddComponent<Selectable>();
        var interactable = root.AddComponent<XRPromeonInteractable>();
        interactable.SetInteractionLayer(layer);

        // 4) Child colliders (ConvexMesh) aren't found by the interactable's root-only Awake scan —
        //    register them and re-tag the layer.
        if (childColliders != null && childColliders.Count > 0)
        {
            interactable.RegisterColliders(childColliders);
            interactable.SetInteractionLayer(layer);
        }
    }

    private static List<Collider> BuildConvexColliders(GameObject root)
    {
        var result = new List<Collider>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            AddConvex(mf.gameObject, mf.sharedMesh, result);
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            AddConvex(smr.gameObject, smr.sharedMesh, result);
        return result;
    }

    private static void AddConvex(GameObject go, Mesh mesh, List<Collider> result)
    {
        if (mesh == null || go.GetComponent<MeshCollider>() != null) return;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex     = true;
        result.Add(mc);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

`run_tests` EditMode, `test_names: ["InteractionCapabilityConvexTests"]`, `get_test_job`. Expected: PASS.

- [ ] **Step 6: Checkpoint** — refresh → console (no CS) → `run_tests ["InteractionCapabilityConvexTests","AssetEntityBuilderRegistryTests"]` (the registry Box tests must stay green) PASS. No commit.

---

## Task 4: Builders pick the kind; remove the box strategy

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`
- Modify: `Assets/_App/Editor/BuiltinRecipeBaker.cs`
- Delete: `Assets/_App/Scripts/VrInteraction/IColliderStrategy.cs`, `Assets/_App/Scripts/VrInteraction/BoundsBoxColliderStrategy.cs`
- Modify tests: `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs`, `Assets/_App/Tests/AssetBrowser/RigEntityBuilderRecipeTests.cs`

- [ ] **Step 1: Rewrite the builder tests (failing)**

Replace `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs` with:

```csharp
using NUnit.Framework;
using UnityEngine;

public partial class ObjectEntityBuilderTests
{
    [Test]
    public void HandledTypes_AreDistinct()
    {
        var obj = new ObjectEntityBuilder(null, null);
        var rig = new RigEntityBuilder(null, null);
        Assert.AreEqual(AssetType.Object, obj.HandledType);
        Assert.AreEqual(AssetType.Rig,    rig.HandledType);
    }

    [Test]
    public void RecipeFromInstance_Object_SetsConvexMesh()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var recipe = ObjectEntityBuilder.RecipeFromInstance(cube, AssetType.Object);
            Assert.AreEqual(AssetType.Object, recipe.type);
            Assert.IsTrue(recipe.selectable);
            Assert.AreEqual(InteractionLayer.SceneObjects, recipe.interactionLayer);
            Assert.AreEqual(ColliderKind.ConvexMesh, recipe.colliderKind);
        }
        finally { Object.DestroyImmediate(cube); }
    }
}
```

Replace `Assets/_App/Tests/AssetBrowser/RigEntityBuilderRecipeTests.cs` with:

```csharp
using NUnit.Framework;
using UnityEngine;

public class RigEntityBuilderRecipeTests
{
    [Test]
    public void RecipeFromInstance_Rig_ExtractsSkeleton_FoldsAxis_SetsBoneBoxes()
    {
        var root = new GameObject("rig");
        var bone = new GameObject("pelvis");
        bone.transform.SetParent(root.transform);
        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform };
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(root, TerminalBoneAxis.X, invert: true);

            Assert.AreEqual(AssetType.Rig, recipe.type);
            Assert.IsTrue(recipe.HasRig, "skeleton with one bone must populate recipe.rig");
            Assert.AreEqual("pelvis", recipe.rig.Bones[0].BoneName);
            Assert.AreEqual(TerminalBoneAxis.X, recipe.rig.TerminalBonesAxis);
            Assert.IsTrue(recipe.rig.InvertTerminalBonesAxis);
            Assert.AreEqual(ColliderKind.BoneBoxes, recipe.colliderKind);
            Assert.AreEqual(3, recipe.boneColliderDepth);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RecipeFromInstance_Rig_NoSkeleton_FallsBackToConvexMesh()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // no SkinnedMeshRenderer
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(go, TerminalBoneAxis.Auto, invert: false);
            Assert.IsFalse(recipe.HasRig, "no skeleton → recipe.rig stays null");
            Assert.AreEqual(ColliderKind.ConvexMesh, recipe.colliderKind, "skeleton-less rig is a static mesh");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

`run_tests` EditMode, `test_names: ["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests"]`, `get_test_job`.
Expected: FAIL — ctors still take 3 args and `RecipeFromInstance` still takes a collider strategy.

- [ ] **Step 3: Rewrite `ObjectEntityBuilder`**

Replace `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs` with:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Object = static mesh. Build loads the glTF once (imported) to confirm it loads; the collider is a
// per-renderer convex MeshCollider built at restore from the live mesh, so the recipe only records the
// kind. RecipeFromInstance is shared with the editor builtin bake.
public class ObjectEntityBuilder : IAssetEntityBuilder
{
    protected readonly AssetSourceStore    _store;
    protected readonly ObjectEntityFactory _factory;

    public ObjectEntityBuilder(AssetSourceStore store, ObjectEntityFactory factory)
    {
        _store   = store;
        _factory = factory;
    }

    public virtual AssetType HandledType => AssetType.Object;

    // Shared, synchronous, DI-light: decide the recipe from a live GameObject. Object → ConvexMesh.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, AssetType chosenType)
        => new AssetEntityRecipe
        {
            type             = chosenType,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
            colliderKind     = ColliderKind.ConvexMesh,
        };

    public virtual async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"ObjectEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try { return RecipeFromInstance(temp, chosenType); }
        finally { UnityEngine.Object.Destroy(temp); }
    }

    public virtual Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");

        return _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
```

- [ ] **Step 4: Rewrite `RigEntityBuilder`**

Replace `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` with:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig: a static skinned mesh + a baked skeleton descriptor. Selection collider = box colliders along
// the skeleton to boneColliderDepth, built at restore by RigEntityFactory (BoneBoxes). A skeleton-less
// import is a static mesh → ConvexMesh fallback so it is still selectable.
public class RigEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore _store;
    private readonly RigEntityFactory _factory;

    public RigEntityBuilder(AssetSourceStore store, RigEntityFactory factory)
    {
        _store   = store;
        _factory = factory;
    }

    public AssetType HandledType => AssetType.Rig;

    // Shared with the editor builtin bake. axis/invert fold into recipe.rig when a skeleton exists;
    // the import path passes Auto/false here and ImportPipeline stamps the wizard choice afterward.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, TerminalBoneAxis axis, bool invert)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
        if (recipe.rig != null)
        {
            recipe.rig.TerminalBonesAxis       = axis;
            recipe.rig.InvertTerminalBonesAxis = invert;
            recipe.colliderKind     = ColliderKind.BoneBoxes;
            recipe.boneColliderDepth = 3;
        }
        else
        {
            recipe.colliderKind = ColliderKind.ConvexMesh; // skeleton-less → static mesh
        }
        return recipe;
    }

    public async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"RigEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            var recipe = RecipeFromInstance(temp, TerminalBoneAxis.Auto, invert: false);
            if (recipe.rig == null)
                Debug.LogWarning($"RigEntityBuilder: '{sourceAbsolutePath}' has no skeleton — importing as a static object.");
            return recipe;
        }
        finally { UnityEngine.Object.Destroy(temp); }
    }

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

        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var depth     = recipe != null ? recipe.boneColliderDepth : 3;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert, depth);

        return go;
    }
}
```

(Note: `BuildProxyRig`'s new `depth` parameter is added in Task 5; this call compiles only after Task 5. Tasks are applied in order, but the Checkpoint compile for Task 4 will fail on this single call until Task 5 lands. To keep Task 4 self-compiling, temporarily call `_factory.BuildProxyRig(go, boneNames, axis, invert)` here and switch to the 5-arg form in Task 5 Step 4. The `depth` local is then unused in Task 4 — prefix discard `_ = depth;` or omit `depth` until Task 5.) **Apply the 4-arg call now; add `depth` + the 5-arg call in Task 5.**

So for Task 4, the last lines are:

```csharp
        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert);

        return go;
```

- [ ] **Step 5: Update `BuiltinRecipeBaker` call sites**

In `Assets/_App/Editor/BuiltinRecipeBaker.cs`, inside `BakeIndex`, remove the collider strategy and update the two `RecipeFromInstance` calls. Replace the `Object` and `Rig` cases with:

```csharp
            case AssetType.Object:
                if (!TryGetPrefabPath(entry, "Object", out var objPath)) return;
                recipe = MeasurePrefab(objPath, go => ObjectEntityBuilder.RecipeFromInstance(go, AssetType.Object));
                break;

            case AssetType.Rig:
                if (!TryGetPrefabPath(entry, "Rig", out var rigPath)) return;
                recipe = MeasurePrefab(rigPath, go => RigEntityBuilder.RecipeFromInstance(
                    go, entry.TerminalBonesAxis, entry.InvertTerminalBonesAxis));
                break;
```

Also delete the now-unused local in `BakeIndex`:

```csharp
        var collider = new BoundsBoxColliderStrategy();
```

(Remove that line entirely.)

- [ ] **Step 6: Remove the strategy registration**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, delete this line (around line 60):

```csharp
        builder.Register<BoundsBoxColliderStrategy>(Lifetime.Singleton).As<IColliderStrategy>();
```

- [ ] **Step 7: Delete the dead strategy files**

```powershell
Remove-Item -LiteralPath "S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Scripts\VrInteraction\IColliderStrategy.cs"
Remove-Item -LiteralPath "S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Scripts\VrInteraction\IColliderStrategy.cs.meta"
Remove-Item -LiteralPath "S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Scripts\VrInteraction\BoundsBoxColliderStrategy.cs"
Remove-Item -LiteralPath "S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Scripts\VrInteraction\BoundsBoxColliderStrategy.cs.meta"
```

- [ ] **Step 8: Run tests to verify they pass**

`run_tests` EditMode, `test_names: ["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests"]`, `get_test_job`.
Expected: PASS (4). (Compile must be clean — confirm no other caller still references `IColliderStrategy`/`BoundsBoxColliderStrategy` via the console.)

- [ ] **Step 9: Checkpoint** — refresh → console (no CS; in particular no "type or namespace `IColliderStrategy`/`BoundsBoxColliderStrategy`" errors) → `run_tests ["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests","AssetEntityBuilderRegistryTests"]` PASS. No commit.

---

## Task 5: Rig selector boxes — factory build, runtime toggle, registry wiring

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`
- Modify: `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs`
- Modify: `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` (switch to the 5-arg `BuildProxyRig`)
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs`

- [ ] **Step 1: `ProxyRigRuntime` — selector list + register + toggle**

In `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs`:

(a) Replace the root-collider fields:

```csharp
    private Transform     _proxyRoot;
    private Collider      _rootCollider;       // resolved lazily (added by the registry AFTER build)
    private bool          _rootColliderResolved;
```

with:

```csharp
    private Transform           _proxyRoot;
    private readonly List<Collider> _selectorColliders = new(); // whole-rig select boxes (SceneObjects)
```

(b) Delete the `RootCollider()` method entirely:

```csharp
    private Collider RootCollider()
    {
        if (!_rootColliderResolved)
        {
            _rootCollider = GetComponent<Collider>();
            _rootColliderResolved = true;
        }
        return _rootCollider;
    }
```

(c) Replace `Bind` to accept the selector colliders:

```csharp
    // Called once by the factory right after construction.
    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs, List<Collider> selectorColliders)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        _selectorColliders.Clear();
        if (selectorColliders != null) _selectorColliders.AddRange(selectorColliders);
        SetBonesInteractive(false); // start in whole-rig select mode
    }

    // Registers the whole-rig selector boxes with the root interactable so a hit on any of them selects
    // the rig. Called after InteractionCapability.Apply has created the interactable (by the registry /
    // RigRuntime). No-op if there is no interactable yet.
    public void RegisterSelectorColliders()
    {
        var it = GetComponent<XRPromeonInteractable>();
        if (it == null) return;
        it.RegisterColliders(_selectorColliders);
        it.SetInteractionLayer(InteractionLayer.SceneObjects);
    }
```

(d) In `SetBonesInteractive`, replace the root-collider toggle:

```csharp
        var rootCol = RootCollider();
        if (rootCol != null) rootCol.enabled = !enabled;
```

with the selector-list toggle:

```csharp
        foreach (var sc in _selectorColliders)
            if (sc != null) sc.enabled = !enabled;
```

- [ ] **Step 2: `RigEntityFactory` — build selector boxes during the walk**

In `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`:

(a) Change the `BuildProxyRig` signature and the `Bind` call. Replace the method header line:

```csharp
    public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis, bool invertAxis)
```

with:

```csharp
    public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis, bool invertAxis, int selectorDepth = 3)
```

(b) Replace the binding tail of `BuildProxyRig`:

```csharp
        if (proxyRoot == null) return; // skeleton present but no buildable root bone

        var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot, proxyGOs);
    }
```

with:

```csharp
        if (proxyRoot == null) return; // skeleton present but no buildable root bone

        var selectorColliders = BuildSelectorColliders(transforms, set, selectorDepth);

        var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot, proxyGOs, selectorColliders);
    }

    // Whole-rig selection colliders: boxes placed along the skeleton to `depth` (BoneSelectorBoxPlanner),
    // each on a child GO parented to its bone (so it follows the pose), on the SceneObjects layer. Sized
    // to a bone-local AABB of the planned origins, padded to a minimum thickness so straight chains are
    // still hittable. Returned for ProxyRigRuntime to own/toggle/register.
    private List<Collider> BuildSelectorColliders(Transform[] transforms, HashSet<Transform> set, int depth)
    {
        var colliders = new List<Collider>();
        float minThk = Mathf.Max(_config.BoneWidth, 0.01f);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue; // only root bones of the set start a walk
            if (bone.parent == null)       continue;

            foreach (var plan in BoneSelectorBoxPlanner.Plan(bone, depth, set))
            {
                var boxGo = new GameObject($"selector_{plan.Bone.name}");
                boxGo.transform.SetParent(plan.Bone, worldPositionStays: false);
                boxGo.transform.localPosition = Vector3.zero;
                boxGo.transform.localRotation = Quaternion.identity;
                boxGo.transform.localScale    = Vector3.one;

                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var world in plan.WorldOrigins)
                {
                    var local = plan.Bone.InverseTransformPoint(world);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

                var box    = boxGo.AddComponent<BoxCollider>();
                box.center = (min + max) * 0.5f;
                box.size   = Vector3.Max(max - min, Vector3.one * minThk);
                boxGo.SetInteractionLayer(InteractionLayer.SceneObjects);
                colliders.Add(box);
            }
        }
        return colliders;
    }
```

- [ ] **Step 3: `RigRuntime` — pass depth + register selectors**

In `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`, find the `ApplyDefinition` call to `BuildProxyRig`. It currently reads (approximately):

```csharp
        _factory.BuildProxyRig(rigRoot, boneNames, definition != null ? definition.TerminalBonesAxis : TerminalBoneAxis.Auto, definition != null && definition.InvertTerminalBonesAxis);
```

Replace it with the depth-aware call followed by a registration:

```csharp
        _factory.BuildProxyRig(rigRoot, boneNames,
            definition != null ? definition.TerminalBonesAxis : TerminalBoneAxis.Auto,
            definition != null && definition.InvertTerminalBonesAxis,
            3);
        rigRoot.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders();
```

(If the existing line differs, keep its axis/invert expressions verbatim and only add the `3` arg + the `RegisterSelectorColliders()` line after it.)

- [ ] **Step 4: `RigEntityBuilder` — switch to the 5-arg call**

In `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` `RestoreAsync`, restore the `depth` local and pass it:

```csharp
        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var depth     = recipe != null ? recipe.boneColliderDepth : 3;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert, depth);

        return go;
```

- [ ] **Step 5: `AssetEntityBuilderRegistry` — register selectors after Apply**

In `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` `RestoreAsync`, after the `InteractionCapability.Apply(...)` block and before `return go;`, add:

```csharp
        // BoneBoxes selectors are built on the rig side; register them with the interactable Apply just
        // created so a hit on any selector box selects the whole rig.
        if (go != null && recipe != null && recipe.colliderKind == ColliderKind.BoneBoxes)
            go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders();

        return go;
```

- [ ] **Step 6: Checkpoint** — refresh → console (no CS) → `run_tests` for the suites touched indirectly: `["ProxyRigRuntimeTests","RigEntityFactoryBuildProxyTests","BoneSelectorBoxPlannerTests","AssetEntityBuilderRegistryTests"]` (whichever exist) → `get_test_job`. Expected: PASS (only the 6 baseline failures may appear in a full run, none here). No commit.

> Note: `ProxyRigRuntimeTests` / `RigEntityFactoryBuildProxyTests` may construct `Bind`/`BuildProxyRig` with the old arity. If they fail to compile, update those calls to the new signatures (`Bind(proxyRoot, proxyGOs, selectorColliders)`, `BuildProxyRig(..., selectorDepth)`) — pass an empty `new List<Collider>()` / depth `3` respectively. Do this as part of this task so the suite compiles.

---

## Task 6: Final verification + hand-off

- [ ] **Step 1: Full EditMode run** — `run_tests` EditMode (all) → `get_test_job` (`wait_timeout: 90`). Expected: only the 6 known pre-existing failures; the new suites (`AssetEntityRecipeColliderTests`, `BoneSelectorBoxPlannerTests`, `InteractionCapabilityConvexTests`) and the modified ones are green.
- [ ] **Step 2: Console clean** — `read_console` (`types: ["error"], filter_text: "CS"`) → none.
- [ ] **Step 3: Hand off to the user** (manual): re-bake builtin assets (`Bake All`) and re-import any glTF assets so their recipes pick up the new kinds; then in VR verify: Object selection ray hits hug the mesh (convex); Rig whole-select works by pointing at the body (bone boxes), and switching to bone mode still selects individual proxies; Reference unchanged. Commit.

---

## Self-Review

**Spec coverage:**
- `ColliderKind` += ConvexMesh/BoneBoxes; recipe `boneColliderDepth` → Task 1. ✓
- Bake picks kind (Object→ConvexMesh, Rig→BoneBoxes/ConvexMesh fallback, Reference unchanged) → Task 4. ✓
- Remove `IColliderStrategy`/`BoundsBoxColliderStrategy` + ctor params + Root registration + baker arg → Task 4. ✓
- Restore type-keyed (Box unchanged, ConvexMesh per-renderer + register, BoneBoxes no-op in Apply) → Task 3. ✓
- Bone-box traversal (depth 0–2 self+direct children, depth 3 swallows subtree) → Task 2 (planner) + Task 5 (boxes). ✓
- Selector boxes built in factory, owned/toggled by ProxyRigRuntime, registered to the root interactable after Apply (via registry) → Task 5. ✓
- ConvexMesh fallback for skeleton-less rig → Task 4 (`RecipeFromInstance`). ✓
- Min thickness from `ProxyRigConfig.BoneWidth` → Task 5 (`BuildSelectorColliders`). ✓
- Tests 1–6 from the spec → Tasks 1–4 cover enum/round-trip, ConvexMesh apply, kind selection, traversal. ✓

**Placeholder scan:** none — every code step has complete code; the one ordering caveat (Task 4 calls 4-arg `BuildProxyRig`, Task 5 switches to 5-arg) is called out explicitly with the exact code for each task so each Checkpoint compiles.

**Type consistency:** `RecipeFromInstance(GameObject, AssetType)` (Object) and `(GameObject, TerminalBoneAxis, bool)` (Rig) — used identically in Tasks 3/4/5 and the editor baker. `BuildProxyRig(..., int selectorDepth = 3)` — defined Task 5, called from `RigEntityBuilder` (Task 5 Step 4) and `RigRuntime` (Task 5 Step 3). `ProxyRigRuntime.Bind(Transform, List<GameObject>, List<Collider>)` + `RegisterSelectorColliders()` — defined Task 5 Step 1, called from the factory (Step 2) and registry (Step 5). `ColliderKind.ConvexMesh/BoneBoxes`, `AssetEntityRecipe.boneColliderDepth`, `XRPromeonInteractable.IsRegistered/RegisterColliders/SetInteractionLayer` — consistent across tasks. ✓
</content>
