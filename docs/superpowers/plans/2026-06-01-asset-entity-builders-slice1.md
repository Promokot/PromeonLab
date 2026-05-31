# Asset Entity Builders — Slice 1 (Foundation + Object/Reference) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **PROJECT GIT RULE:** The user commits manually. Do **NOT** run `git commit`/`git add`. Treat each "Commit checkpoint" as a *pause point*: report the files changed and let the user commit.
>
> **Unity testing:** EditMode tests live in `Assets/_App/Tests/<Subsystem>/` (single `_App.Tests` asmdef). Run via `mcp__unityMCP__run_tests` (testMode `EditMode`, filter by class) then `mcp__unityMCP__get_test_job`, or the Test Runner window. After creating a NEW `.cs` file, run `mcp__unityMCP__refresh_unity (mode=force, scope=all, compile=request)` so Unity imports it; then `read_console` and confirm no `error CS####`.

**Goal:** Imported glTF objects and reference images become selectable and outlined in-scene by introducing a build-once/restore-many entity pipeline (`AssetEntityRecipe` + `IAssetEntityBuilder` + `InteractionCapability`), replacing the spawn-only `IAssetSpawner`.

**Architecture:** A per-`AssetType` builder makes all decisions once at import (`BuildAsync` → serializable `AssetEntityRecipe`) and deterministically materializes the entity at every spawn/scene-load (`RestoreAsync`). A shared static `InteractionCapability` attaches the scene-interaction components (`SceneNode` + collider + `Selectable` + `XRPromeonInteractable` + layer) — used now by runtime Restore, later by editor bake. Built-in prefabs are untouched (Restore = `Instantiate`).

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces for runtime code), VContainer DI, glTFast, JsonUtility, XRI `XRBaseInteractable`/`NearFarInteractor`.

---

## File Structure

**Create (VrInteraction — interaction primitives):**
- `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs` — enum `{ None, Box }`.
- `Assets/_App/Scripts/VrInteraction/IColliderStrategy.cs` — build-time collider measurement seam.
- `Assets/_App/Scripts/VrInteraction/BoundsBoxColliderStrategy.cs` — default: one local box from child renderer bounds.
- `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs` — static helper; applies capability from primitives.

**Create (AssetBrowser — entity build/restore):**
- `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs` — serializable recipe (references `InteractionLayer`, `ColliderKind`).
- `Assets/_App/Scripts/AssetBrowser/IAssetEntityBuilder.cs` — `BuildAsync`/`RestoreAsync` + `HandledType`.
- `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` — Slice 1: static (same path as Object); proxy-rig is Slice 2.
- `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`
- `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` — type-keyed dispatch; extracts recipe from the asset.

**Modify:**
- `Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs` — add `AssetEntityRecipe Recipe` (serialized).
- `Assets/_App/Scripts/AssetBrowser/ImportedAssetLibrary.cs` — `schemaVersion` on the library JSON.
- `Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs` — take aspect/bottomGap/twoSided as params (from recipe).
- `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs` — Build step after copy: fill the recipe.
- `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs` — `_builders.RestoreAsync(...)`.
- `Assets/_App/Scripts/SceneComposition/SceneGraph.cs` — reload path uses `_builders.RestoreAsync(...)`.
- `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — register strategy + builders + registry; drop old spawners.

**Delete (replaced by builders):**
- `IAssetSpawner.cs`, `AssetSpawnerRegistry.cs`, `ObjectSpawner.cs`, `RigSpawner.cs`, `ReferenceSpawner.cs`, `ModelSpawnCore.cs`.

**Keep:** `GltfModelLoader.cs`, `ReferenceQuadFactory.cs` (modified), `AssetSourceStore.cs`, `ImportRenderProfile.cs`.

---

## Task 1: Collider strategy seam (VrInteraction)

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/Data/ColliderKind.cs`
- Create: `Assets/_App/Scripts/VrInteraction/IColliderStrategy.cs`
- Create: `Assets/_App/Scripts/VrInteraction/BoundsBoxColliderStrategy.cs`
- Test: `Assets/_App/Tests/VrInteraction/BoundsBoxColliderStrategyTests.cs`

- [ ] **Step 1: Create `ColliderKind.cs`**

```csharp
// How the entity's selection collider is shaped. Box only for now; precise/mesh is a future swap.
public enum ColliderKind
{
    None,
    Box,
}
```

- [ ] **Step 2: Create `IColliderStrategy.cs`**

```csharp
using UnityEngine;

// Build-time seam: measures a freshly-built entity and returns a LOCAL-SPACE collider descriptor
// to bake into the recipe. NOT used at restore (restore applies the stored descriptor verbatim).
public interface IColliderStrategy
{
    void Measure(GameObject root, out ColliderKind kind, out Vector3 center, out Vector3 size);
}
```

- [ ] **Step 3: Create `BoundsBoxColliderStrategy.cs`**

```csharp
using UnityEngine;

// Default strategy: one Box covering every child Renderer, expressed in root-local space so the
// stored center/size stay valid regardless of the root's spawn rotation.
public class BoundsBoxColliderStrategy : IColliderStrategy
{
    public void Measure(GameObject root, out ColliderKind kind, out Vector3 center, out Vector3 size)
    {
        kind = ColliderKind.Box;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers.Length == 0)
        {
            center = Vector3.zero;
            size   = Vector3.one * 0.1f;   // tiny fallback so the object is still hittable
            return;
        }

        bool has = false;
        var min  = Vector3.positiveInfinity;
        var max  = Vector3.negativeInfinity;
        var toLocal = root.transform.worldToLocalMatrix;

        foreach (var r in renderers)
        {
            var b = r.bounds; // world AABB
            // Encapsulate all 8 world corners transformed into root-local space.
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var local = toLocal.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
                has = true;
            }
        }

        if (!has) { center = Vector3.zero; size = Vector3.one * 0.1f; return; }
        center = (min + max) * 0.5f;
        size   = max - min;
    }
}
```

- [ ] **Step 4: Write the failing test `BoundsBoxColliderStrategyTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;

public class BoundsBoxColliderStrategyTests
{
    [Test]
    public void Measure_SingleUnitCubeAtOrigin_GivesUnitBox()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // 1x1x1, renderer bounds = unit
        try
        {
            new BoundsBoxColliderStrategy().Measure(cube, out var kind, out var center, out var size);
            Assert.AreEqual(ColliderKind.Box, kind);
            Assert.That(center.magnitude, Is.LessThan(0.01f));
            Assert.That(size.x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(size.y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(size.z, Is.EqualTo(1f).Within(0.01f));
        }
        finally { Object.DestroyImmediate(cube); }
    }

    [Test]
    public void Measure_NoRenderers_GivesTinyFallback()
    {
        var go = new GameObject("empty");
        try
        {
            new BoundsBoxColliderStrategy().Measure(go, out var kind, out _, out var size);
            Assert.AreEqual(ColliderKind.Box, kind);
            Assert.That(size.x, Is.GreaterThan(0f));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
```

- [ ] **Step 5: Refresh + run tests**

Run: `refresh_unity (force/all/compile)`, then `run_tests (EditMode, filter "BoundsBoxColliderStrategyTests")`.
Expected: 2/2 PASS, no `error CS`.

- [ ] **Step 6: Commit checkpoint** — files: the 3 new VrInteraction scripts + test. (User commits.)

---

## Task 2: `InteractionCapability` static helper (VrInteraction)

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/InteractionCapability.cs`
- Test: `Assets/_App/Tests/VrInteraction/InteractionCapabilityTests.cs`

- [ ] **Step 1: Create `InteractionCapability.cs`**

```csharp
using UnityEngine;

// The single definition of "make this GameObject a selectable scene entity".
// Pure application from primitive recipe values — makes no decisions. Used by runtime Restore now
// and editor bake later. Idempotent: skips if an XRPromeonInteractable is already present
// (a baked built-in prefab), so it can never disturb working assets.
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

        // 2) Collider on the root (so _includeChildColliders can stay false).
        if (colliderKind == ColliderKind.Box)
        {
            var box    = root.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size   = colliderSize;
            box.gameObject.SetInteractionLayer(layer);
        }

        if (!selectable) return;

        // 3) Outline driver + input-driven select/move/rotate. DI (Construct) wired later by
        //    IObjectResolver.InjectGameObject at the call site.
        root.AddComponent<Selectable>();
        root.AddComponent<XRPromeonInteractable>().SetInteractionLayer(layer);
    }
}
```

- [ ] **Step 2: Write the failing test `InteractionCapabilityTests.cs`**

> Note: `XRPromeonInteractable` derives from `XRBaseInteractable`; adding it in EditMode may log XRI warnings but the component exists. Keep assertions on presence/idempotency.

```csharp
using NUnit.Framework;
using UnityEngine;

public class InteractionCapabilityTests
{
    [Test]
    public void Apply_AddsSceneNodeColliderSelectableInteractable()
    {
        var go = new GameObject("entity");
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: true);

            Assert.IsNotNull(go.GetComponent<SceneNode>());
            var box = go.GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Assert.That(box.size, Is.EqualTo(Vector3.one));
            Assert.IsNotNull(go.GetComponent<Selectable>());
            Assert.IsNotNull(go.GetComponent<XRPromeonInteractable>());
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Apply_Idempotent_WhenInteractableAlreadyPresent()
    {
        var go = new GameObject("entity");
        go.AddComponent<XRPromeonInteractable>();
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: true);

            Assert.AreEqual(1, go.GetComponents<XRPromeonInteractable>().Length);
            Assert.IsNull(go.GetComponent<Selectable>(), "should not add a second capability set");
            Assert.IsNull(go.GetComponent<BoxCollider>());
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Apply_NotSelectable_AddsColliderButNoInteractable()
    {
        var go = new GameObject("entity");
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: false);

            Assert.IsNotNull(go.GetComponent<BoxCollider>());
            Assert.IsNull(go.GetComponent<XRPromeonInteractable>());
        }
        finally { Object.DestroyImmediate(go); }
    }
}
```

- [ ] **Step 3: Refresh + run tests** — `run_tests (EditMode, "InteractionCapabilityTests")`. Expected 3/3 PASS. If `XRPromeonInteractable.Awake` throws without an `XRInteractionManager`, add `new GameObject("mgr").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();` in a `[SetUp]` and destroy in `[TearDown]`.

- [ ] **Step 4: Commit checkpoint** — `InteractionCapability.cs` + test.

---

## Task 3: `AssetEntityRecipe` (AssetBrowser)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs`
- Test: `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeTests.cs`

- [ ] **Step 1: Create `AssetEntityRecipe.cs`**

```csharp
using System;
using UnityEngine;

// The Build→Restore contract. Built once (at import or editor bake), applied verbatim at every
// spawn/scene-load so the entity's representation never drifts. JsonUtility-friendly (flat).
[Serializable]
public class AssetEntityRecipe
{
    public int              schemaVersion = 1;
    public AssetType        type;

    // Generic interaction capability.
    public bool             selectable = true;
    public InteractionLayer interactionLayer = InteractionLayer.SceneObjects;

    // Collider (local space).
    public ColliderKind     colliderKind = ColliderKind.Box;
    public Vector3          colliderCenter;
    public Vector3          colliderSize = Vector3.one;

    // Reference-specific.
    public float            referenceAspect = 1f;
    public float            referenceBottomGap = 0.5f;
    public bool             referenceTwoSided = true;
}
```

- [ ] **Step 2: Write the failing test `AssetEntityRecipeTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeTests
{
    [Test]
    public void JsonRoundTrip_PreservesFields()
    {
        var r = new AssetEntityRecipe
        {
            type = AssetType.Reference,
            interactionLayer = InteractionLayer.SceneObjects,
            colliderKind = ColliderKind.Box,
            colliderCenter = new Vector3(0f, 1f, 0f),
            colliderSize = new Vector3(1.5f, 1f, 0.05f),
            referenceAspect = 1.5f,
            referenceBottomGap = 0.5f,
            referenceTwoSided = true,
        };

        var json = JsonUtility.ToJson(r);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.AreEqual(AssetType.Reference, back.type);
        Assert.AreEqual(ColliderKind.Box, back.colliderKind);
        Assert.That(back.colliderCenter.y, Is.EqualTo(1f).Within(1e-4));
        Assert.That(back.referenceAspect, Is.EqualTo(1.5f).Within(1e-4));
        Assert.AreEqual(1, back.schemaVersion);
    }
}
```

- [ ] **Step 3: Refresh + run** — `run_tests (EditMode, "AssetEntityRecipeTests")`. Expected 1/1 PASS.

- [ ] **Step 4: Commit checkpoint** — `AssetEntityRecipe.cs` + test.

---

## Task 4: `IAssetEntityBuilder` interface (AssetBrowser)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/IAssetEntityBuilder.cs`

- [ ] **Step 1: Create `IAssetEntityBuilder.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IAssetEntityBuilder
{
    AssetType HandledType { get; }

    // Once: inspect the raw source, decide everything, return a serializable recipe.
    Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct);

    // Many: materialize deterministically. Builtin → Instantiate(prefab) (recipe ignored);
    // Imported → load source + apply the recipe. Never makes decisions.
    Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct);
}
```

- [ ] **Step 2: Refresh + confirm compile** — `refresh_unity (force/all/compile)`, `read_console` → no `error CS`.

- [ ] **Step 3: Commit checkpoint** — `IAssetEntityBuilder.cs`.

---

## Task 5: `ObjectEntityBuilder` + `RigEntityBuilder` (AssetBrowser)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Test: `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs`

- [ ] **Step 1: Create `ObjectEntityBuilder.cs`**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Object = static mesh. Build loads the glTF once to measure its collider; Restore reloads the mesh
// (imported) or instantiates the prefab (builtin) and applies the baked recipe.
public class ObjectEntityBuilder : IAssetEntityBuilder
{
    protected readonly AssetSourceStore _store;
    protected readonly GltfModelLoader  _loader;
    protected readonly IColliderStrategy _collider;

    public ObjectEntityBuilder(AssetSourceStore store, GltfModelLoader loader, IColliderStrategy collider)
    {
        _store    = store;
        _loader   = loader;
        _collider = collider;
    }

    public virtual AssetType HandledType => AssetType.Object;

    public virtual async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = chosenType,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var temp = await _loader.LoadAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"ObjectEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            _collider.Measure(temp, out var kind, out var center, out var size);
            recipe.colliderKind   = kind;
            recipe.colliderCenter = center;
            recipe.colliderSize   = size;
        }
        finally { UnityEngine.Object.Destroy(temp); }

        return recipe;
    }

    public virtual async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");

        var go = await _loader.LoadAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
        if (go == null) return null;

        var r = recipe ?? await BuildAsync(_store.AbsolutePath(asset.SourceRef), asset.Type, ct); // legacy fallback
        InteractionCapability.Apply(go, r.interactionLayer, r.colliderKind, r.colliderCenter, r.colliderSize, r.selectable);
        return go;
    }
}
```

- [ ] **Step 2: Create `RigEntityBuilder.cs`** (Slice 1 = static; Slice 2 adds the proxy rig)

```csharp
// Slice 1: a Rig import behaves exactly like a static Object (selectable static skinned mesh).
// Slice 2 will replace this with runtime proxy-rig building + a bone descriptor in the recipe.
public class RigEntityBuilder : ObjectEntityBuilder
{
    public RigEntityBuilder(AssetSourceStore store, GltfModelLoader loader, IColliderStrategy collider)
        : base(store, loader, collider) { }

    public override AssetType HandledType => AssetType.Rig;
}
```

- [ ] **Step 3: Write the failing test `ObjectEntityBuilderTests.cs`** (pure-logic only; glTF load is PlayMode/manual)

```csharp
using NUnit.Framework;

public class ObjectEntityBuilderTests
{
    [Test]
    public void HandledTypes_AreDistinct()
    {
        var obj = new ObjectEntityBuilder(null, null, null);
        var rig = new RigEntityBuilder(null, null, null);
        Assert.AreEqual(AssetType.Object, obj.HandledType);
        Assert.AreEqual(AssetType.Rig,    rig.HandledType);
    }
}
```

> Build/Restore glTF paths are verified in Task 10's manual VR pass (they need real `.glb` + glTFast instantiation, impractical in EditMode).

- [ ] **Step 4: Refresh + run** — `run_tests (EditMode, "ObjectEntityBuilderTests")`. Expected 1/1 PASS, no `error CS`.

- [ ] **Step 5: Commit checkpoint** — `ObjectEntityBuilder.cs`, `RigEntityBuilder.cs` + test.

---

## Task 6: `ReferenceEntityBuilder` + `ReferenceQuadFactory` change (AssetBrowser)

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`
- Test: `Assets/_App/Tests/AssetBrowser/ReferenceEntityBuilderTests.cs`

- [ ] **Step 1: Modify `ReferenceQuadFactory.cs`** — take aspect/bottomGap/twoSided from the recipe instead of recomputing aspect locally. Replace the method body's aspect/offset/material wiring; full file:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceQuadFactory
{
    private readonly ImportRenderProfile _renderProfile;

    public ReferenceQuadFactory(ImportRenderProfile renderProfile)
    {
        _renderProfile = renderProfile;
    }

    // Builds the empty pivot + child quad using the RECIPE's baked aspect/gap/two-sided. The pivot
    // sits at the spawn point on the floor; the image's bottom edge clears the floor by bottomGap.
    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation,
                                        float aspect, float bottomGap, bool twoSided, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceQuadFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        var root = new GameObject("ReferenceImage");
        root.transform.SetPositionAndRotation(position, rotation);

        var quad  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Image";
        quad.transform.SetParent(root.transform, worldPositionStays: false);

        const float h = 1f;
        quad.transform.localScale    = new Vector3(aspect, h, 1f);
        quad.transform.localPosition = new Vector3(0f, bottomGap + h * 0.5f, 0f);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        quad.GetComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided);
        return Task.FromResult(root);
    }

    private Material BuildMaterial(Texture2D tex, bool twoSided)
    {
        Shader shader = null;
        if (_renderProfile != null && _renderProfile.TryGet(AssetType.Reference, out var entry))
        {
            shader   = entry.Shader;
            twoSided = entry.TwoSided || twoSided;
        }
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        var mat = new Material(shader) { mainTexture = tex };
        if (twoSided && mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            mat.doubleSidedGI = true;
        }
        return mat;
    }
}
```

- [ ] **Step 2: Create `ReferenceEntityBuilder.cs`**

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Reference = a textured quad standing on the floor. Build reads the image dimensions once to fix
// aspect + collider box; Restore rebuilds the quad from the recipe and attaches capability.
public class ReferenceEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore     _store;
    private readonly ReferenceQuadFactory _quads;

    public ReferenceEntityBuilder(AssetSourceStore store, ReferenceQuadFactory quads)
    {
        _store = store;
        _quads = quads;
    }

    public AssetType HandledType => AssetType.Reference;

    public Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(sourceAbsolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        float aspect = 1f;
        if (tex.LoadImage(bytes) && tex.height != 0)
            aspect = (float)tex.width / tex.height;
        UnityEngine.Object.Destroy(tex);

        const float h = 1f, gap = 0.5f;
        var recipe = new AssetEntityRecipe
        {
            type               = AssetType.Reference,
            selectable         = true,
            interactionLayer   = InteractionLayer.SceneObjects,
            colliderKind       = ColliderKind.Box,
            colliderCenter     = new Vector3(0f, gap + h * 0.5f, 0f),
            colliderSize       = new Vector3(aspect, h, 0.02f),
            referenceAspect    = aspect,
            referenceBottomGap = gap,
            referenceTwoSided  = true,
        };
        return Task.FromResult(recipe);
    }

    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");

        var abs = _store.AbsolutePath(asset.SourceRef);
        var r   = recipe ?? await BuildAsync(abs, AssetType.Reference, ct); // legacy fallback

        var go = await _quads.CreateAsync(abs, position, rotation,
            r.referenceAspect, r.referenceBottomGap, r.referenceTwoSided, ct);
        if (go == null) return null;

        InteractionCapability.Apply(go, r.interactionLayer, r.colliderKind, r.colliderCenter, r.colliderSize, r.selectable);
        return go;
    }
}
```

- [ ] **Step 3: Write the failing test `ReferenceEntityBuilderTests.cs`**

```csharp
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

public class ReferenceEntityBuilderTests
{
    [Test]
    public void BuildAsync_FromImage_SetsAspectAndColliderFromDimensions()
    {
        // 4x2 white texture → aspect 2.0
        var tex = new Texture2D(4, 2);
        var png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        var path = Path.Combine(Application.temporaryCachePath, "ref_test.png");
        File.WriteAllBytes(path, png);

        var builder = new ReferenceEntityBuilder(null, null);
        var recipe  = builder.BuildAsync(path, AssetType.Reference, CancellationToken.None).Result;

        Assert.AreEqual(AssetType.Reference, recipe.type);
        Assert.That(recipe.referenceAspect, Is.EqualTo(2f).Within(0.01f));
        Assert.That(recipe.colliderCenter.y, Is.EqualTo(1f).Within(0.01f)); // gap .5 + half-height .5
        Assert.That(recipe.colliderSize.x, Is.EqualTo(2f).Within(0.01f));
        Assert.IsTrue(recipe.selectable);
    }
}
```

- [ ] **Step 4: Refresh + run** — `run_tests (EditMode, "ReferenceEntityBuilderTests")`. Expected 1/1 PASS.

- [ ] **Step 5: Commit checkpoint** — `ReferenceQuadFactory.cs`, `ReferenceEntityBuilder.cs` + test.

---

## Task 7: `AssetEntityBuilderRegistry` (replaces `AssetSpawnerRegistry`)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs`
- Test: `Assets/_App/Tests/AssetBrowser/AssetEntityBuilderRegistryTests.cs`

- [ ] **Step 1: Create `AssetEntityBuilderRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Type-keyed dispatch for both Build (import) and Restore (spawn / scene-load). Extracts the baked
// recipe from the asset record so callers never deal with recipes directly.
public class AssetEntityBuilderRegistry
{
    private readonly Dictionary<AssetType, IAssetEntityBuilder> _byType = new();

    public AssetEntityBuilderRegistry(IReadOnlyList<IAssetEntityBuilder> builders)
    {
        foreach (var b in builders) _byType[b.HandledType] = b;
    }

    public Task<AssetEntityRecipe> BuildAsync(AssetType type, string sourceAbsolutePath, CancellationToken ct)
        => Resolve(type).BuildAsync(sourceAbsolutePath, type, ct);

    public Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var recipe = (asset as ImportedLabAsset)?.Recipe; // null for builtin/legacy
        return Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);
    }

    private IAssetEntityBuilder Resolve(AssetType type)
    {
        if (!_byType.TryGetValue(type, out var b))
            throw new NotSupportedException($"No entity builder registered for asset type {type}");
        return b;
    }
}
```

- [ ] **Step 2: Write the failing test `AssetEntityBuilderRegistryTests.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class AssetEntityBuilderRegistryTests
{
    private class FakeBuilder : IAssetEntityBuilder
    {
        public AssetType HandledType { get; init; }
        public AssetEntityRecipe LastRecipe;
        public Task<AssetEntityRecipe> BuildAsync(string p, AssetType t, CancellationToken ct)
            => Task.FromResult(new AssetEntityRecipe { type = t });
        public Task<GameObject> RestoreAsync(ILabAsset a, AssetEntityRecipe r, Vector3 pos, Quaternion rot, CancellationToken ct)
        { LastRecipe = r; return Task.FromResult<GameObject>(null); }
    }

    [Test]
    public void RestoreAsync_DispatchesByType_AndPassesRecord()
    {
        var refBuilder = new FakeBuilder { HandledType = AssetType.Reference };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { refBuilder });

        var recipe = new AssetEntityRecipe { type = AssetType.Reference, referenceAspect = 3f };
        var asset  = new ImportedLabAsset("id1", "name", AssetType.Reference, "asset-library/sources/id1.png", recipe);

        reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None);

        Assert.IsNotNull(refBuilder.LastRecipe);
        Assert.That(refBuilder.LastRecipe.referenceAspect, Is.EqualTo(3f));
    }
}
```

> This test references the new `ImportedLabAsset` ctor with a recipe (Task 8). If executing strictly in order, write Task 8's ctor first or stub the recipe via a property setter.

- [ ] **Step 3: Refresh + run** — after Task 8 lands, `run_tests (EditMode, "AssetEntityBuilderRegistryTests")`. Expected 1/1 PASS.

- [ ] **Step 4: Commit checkpoint** — `AssetEntityBuilderRegistry.cs` + test.

---

## Task 8: Recipe on `ImportedLabAsset` + library `schemaVersion`

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportedAssetLibrary.cs`
- Test: `Assets/_App/Tests/AssetBrowser/ImportedLabAssetRecipeTests.cs`

- [ ] **Step 1: Modify `ImportedLabAsset.cs`** — add the recipe (full file):

```csharp
using System;
using UnityEngine;

[Serializable]
public class ImportedLabAsset : ILabAsset
{
    [SerializeField] private string            _id;
    [SerializeField] private string            _displayName;
    [SerializeField] private AssetType         _type;
    [SerializeField] private string            _sourceRef;
    [SerializeField] private AssetEntityRecipe _recipe;

    public string            Id          => _id;
    public string            DisplayName => _displayName;
    public AssetType         Type        => _type;
    public AssetSource       Source      => AssetSource.Imported;
    public string            SourceRef   => _sourceRef;
    public Sprite            Icon        => null;
    public AssetEntityRecipe Recipe      => _recipe;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string sourceRef, AssetEntityRecipe recipe = null)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _sourceRef   = sourceRef;
        _recipe      = recipe;
    }

    public void SetRecipe(AssetEntityRecipe recipe) => _recipe = recipe;
}
```

- [ ] **Step 2: Modify `ImportedAssetLibrary.cs`** — add `schemaVersion` to the saved JSON. Change the `LibraryJson` class and `SaveAsync`:

```csharp
    public async Task SaveAsync(CancellationToken ct)
    {
        var path = _paths.ImportedLibraryPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var data = new LibraryJson { schemaVersion = 2, entries = _entries };
        var json = JsonUtility.ToJson(data, prettyPrint: true);
        await File.WriteAllTextAsync(path, json, ct);
    }
```
```csharp
    [Serializable]
    private class LibraryJson
    {
        public int schemaVersion = 2; // v2 adds per-entry AssetEntityRecipe
        public List<ImportedLabAsset> entries = new();
    }
```

> Migration note: v1 files (no `schemaVersion`, no `_recipe`) load fine — `_recipe` is `null`, and `RestoreAsync` rebuilds it once via the legacy fallback (Tasks 5/6). No `StorageMigrator` change required for this additive field.

- [ ] **Step 3: Write the failing test `ImportedLabAssetRecipeTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;

public class ImportedLabAssetRecipeTests
{
    [Test]
    public void Recipe_SerializesWithRecord()
    {
        var recipe = new AssetEntityRecipe { type = AssetType.Object, colliderSize = new Vector3(2,3,4) };
        var asset  = new ImportedLabAsset("id", "n", AssetType.Object, "asset-library/sources/id.glb", recipe);

        var json = JsonUtility.ToJson(asset);
        var back = JsonUtility.FromJson<ImportedLabAsset>(json);

        Assert.IsNotNull(back.Recipe);
        Assert.That(back.Recipe.colliderSize.z, Is.EqualTo(4f).Within(1e-4));
    }
}
```

- [ ] **Step 4: Refresh + run** — `run_tests (EditMode, "ImportedLabAssetRecipeTests")` + re-run `AssetEntityBuilderRegistryTests`. Expected PASS.

- [ ] **Step 5: Commit checkpoint** — `ImportedLabAsset.cs`, `ImportedAssetLibrary.cs` + test.

---

## Task 9: `ImportPipeline` — Build step fills the recipe

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs:51-72` (the `RunImportAsync` body)

- [ ] **Step 1: Inject the registry + add the Build step.** Change the ctor and `RunImportAsync`:

```csharp
    private readonly EventBus                  _bus;
    private readonly ImportedAssetLibrary      _library;
    private readonly IReadOnlyList<IAssetImportHandler> _handlers;
    private readonly AssetEntityBuilderRegistry _builders;
    private readonly AssetSourceStore           _store;

    public ImportPipeline(EventBus bus, ImportedAssetLibrary library, IReadOnlyList<IAssetImportHandler> handlers,
                          AssetEntityBuilderRegistry builders, AssetSourceStore store)
    {
        _bus      = bus;
        _library  = library;
        _handlers = handlers;
        _builders = builders;
        _store    = store;
    }
```
```csharp
    private async Task RunImportAsync(ImportConfirmedEvent e)
    {
        try
        {
            var handler = HandlerFor(e.FilePath);
            if (handler == null) return;
            var record = await handler.ImportAsync(e.FilePath, e.ChosenType, e.DisplayName, CancellationToken.None);

            // Build once: bake the entity recipe now so spawn/scene-load can restore deterministically.
            var recipe = await _builders.BuildAsync(record.Type, _store.AbsolutePath(record.SourceRef), CancellationToken.None);
            record.SetRecipe(recipe);

            _library.Add(record);
            await _library.SaveAsync(CancellationToken.None);
            _bus.Publish(new AssetImportedEvent { AssetId = record.Id });
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImportPipeline: import failed for '{e.FilePath}'. {ex}");
        }
    }
```

> `handler.ImportAsync` returns `ImportedLabAsset` (concrete) so `record.SetRecipe(...)` is available. If the return type is declared as the interface, cast: `if (record is ImportedLabAsset imp) imp.SetRecipe(recipe);`.

- [ ] **Step 2: Refresh + confirm compile** — `read_console` → no `error CS`. (No new unit test; covered by Task 10 manual import.)

- [ ] **Step 3: Commit checkpoint** — `ImportPipeline.cs`.

---

## Task 10: Wire spawn + reload, swap DI registration, delete old spawners, verify

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs:33`
- Modify: `Assets/_App/Scripts/SceneComposition/SceneGraph.cs:148` (reload path)
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs:45-52`
- Delete: `IAssetSpawner.cs`, `AssetSpawnerRegistry.cs`, `ObjectSpawner.cs`, `RigSpawner.cs`, `ReferenceSpawner.cs`, `ModelSpawnCore.cs`

- [ ] **Step 1: `AssetSpawner.cs`** — swap the dependency + call. Change the field/ctor type `AssetSpawnerRegistry _spawners` → `AssetEntityBuilderRegistry _builders`, and line 33:

```csharp
            var go = await _builders.RestoreAsync(e.Asset, e.Position, e.Rotation, CancellationToken.None);
```
(Update the ctor parameter type and the `private readonly` field name accordingly; everything else in `SpawnCoreAsync` — `AddNode`, `InjectGameObject` — stays.)

- [ ] **Step 2: `SceneGraph.cs`** — the reload path. The field is currently `AssetSpawnerRegistry _spawners` (used at line 148). Rename its type to `AssetEntityBuilderRegistry` (ctor + field) and change the call:

```csharp
                go = await _spawners.RestoreAsync(asset, nd.Position, nd.Rotation, CancellationToken.None);
```
(Keep `AddNodeInternal(..., isLoad:true)` + `InjectGameObject(go)` exactly as they are — `InteractionCapability` already ran inside `RestoreAsync`, and `AddNodeInternal` re-uses the `SceneNode` it added.)

> Verify the SceneGraph ctor parameter name. If tests construct `SceneGraph` with a positional `AssetSpawnerRegistry` arg (e.g. `SceneGraphTests`/`SceneContextTests`), update those `new SceneGraph(...)` calls to pass `AssetEntityBuilderRegistry` (or `null`).

- [ ] **Step 3: `RootLifetimeScope.cs`** — replace the spawner registrations (lines ~45-52) with builders + strategy:

```csharp
        // Render presets for runtime-imported assets (shader + two-sided per AssetType).
        var renderProfile = _importRenderProfile != null
            ? _importRenderProfile
            : ScriptableObject.CreateInstance<ImportRenderProfile>();
        if (_importRenderProfile == null)
            Debug.LogWarning("RootLifetimeScope: _importRenderProfile not assigned — imported images fall back to built-in URP/Unlit (two-sided).");
        builder.RegisterInstance(renderProfile);

        // Runtime loaders + collider strategy + per-type entity builders (Build/Restore).
        builder.Register<AssetSourceStore>(Lifetime.Singleton);
        builder.Register<GltfModelLoader>(Lifetime.Singleton);
        builder.Register<ReferenceQuadFactory>(Lifetime.Singleton);
        builder.Register<BoundsBoxColliderStrategy>(Lifetime.Singleton).As<IColliderStrategy>();
        builder.Register<ObjectEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<RigEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<ReferenceEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<AssetEntityBuilderRegistry>(Lifetime.Singleton);
```

> Confirm no other file references `AssetSpawnerRegistry`, `IAssetSpawner`, `ObjectSpawner`, `RigSpawner`, `ReferenceSpawner`, or `ModelSpawnCore` before deleting (use Grep). `ImportPipeline` now depends on `AssetEntityBuilderRegistry` (Task 9).

- [ ] **Step 4: Delete the six obsolete files** (and their `.meta`): `IAssetSpawner.cs`, `AssetSpawnerRegistry.cs`, `ObjectSpawner.cs`, `RigSpawner.cs`, `ReferenceSpawner.cs`, `ModelSpawnCore.cs`.

- [ ] **Step 5: Refresh + full compile** — `refresh_unity (force/all/compile)`, `read_console` → zero `error CS`. Fix any missed references (likely `SceneGraphTests`/`SceneContextTests` ctor args).

- [ ] **Step 6: Run the full EditMode suite** — `run_tests (EditMode)`. Expected: all new tests PASS; pre-existing unrelated failures only (4 `PathProviderTests` Windows separators, `BoneFollower` precision, 2 `RingRotateStrategy`).

- [ ] **Step 7: Manual VR verification (the build/restore glTF + image paths)** — report results:
  - Import a `.glb` as **Object** → place → **tap-trigger selects it (outline shows), gizmo works, hold-grip moves**.
  - Exit to MainMenu and reopen the scene → object **reloads selectable/outlined** (recipe restored).
  - Import a `.png` as **Reference** → textured quad standing 0.5 m off the floor → **selectable + outlined**, reloads selectable.
  - Built-in asset → still selectable/outlined (unchanged).
  - Check `Application.persistentDataPath/asset-library/imported.json` contains a `recipe` block per entry with `schemaVersion: 2`.

- [ ] **Step 8: Commit checkpoint** — `AssetSpawner.cs`, `SceneGraph.cs`, `RootLifetimeScope.cs`, deletions, any test ctor fixes.

---

## Self-Review

**Spec coverage:**
- §2 build-once/restore-many → Tasks 5/6 (`BuildAsync` at import via Task 9; `RestoreAsync` at spawn/reload via Task 10). ✓
- §3 `IAssetEntityBuilder` → Task 4; `AssetEntityRecipe` → Task 3; `InteractionCapability` → Task 2; `IColliderStrategy`/`BoundsBoxColliderStrategy` → Task 1. ✓
- §4 import Build step → Task 9; spawn+reload single Restore point → Task 10 (registry used by both `AssetSpawner` and `SceneGraph`). ✓
- §5 ordering (SceneNode before Selectable/Interactable; AddNode re-uses) → InteractionCapability adds SceneNode first (Task 2); AddNode unchanged. ✓
- §6 idempotency / builtin untouched → Task 2 guard + Object/Reference Restore branch on `Source`. ✓
- §7 error handling: unreadable source → `BuildAsync` throws, ImportPipeline catches (Task 9). Rig-without-bones graceful fallback → **deferred to Slice 2** (Slice 1 Rig = static Object, so no bone logic to fail). ✓ (documented)
- §8 persistence: recipe inline on record + `schemaVersion` → Task 8. ✓
- §9 testing → Tasks 1-8 EditMode; glTF/image runtime → Task 10 manual. ✓

**Placeholder scan:** No TBD/"add error handling"/bare "write tests". glTF/image runtime paths are explicitly routed to manual VR verification (Task 10) with concrete steps — not a placeholder. ✓

**Type consistency:** `AssetEntityRecipe` fields (`colliderCenter/Size`, `referenceAspect/BottomGap/TwoSided`, `interactionLayer`, `colliderKind`, `selectable`) used identically across Tasks 3/5/6/7/8. `InteractionCapability.Apply(GameObject, InteractionLayer, ColliderKind, Vector3, Vector3, bool)` matches its callers in Tasks 5/6. `AssetEntityBuilderRegistry.RestoreAsync(ILabAsset, Vector3, Quaternion, CancellationToken)` matches Task 10 callers. `ImportedLabAsset` 5-arg ctor + `SetRecipe`/`Recipe` consistent across Tasks 7/8/9. ✓

**Note vs spec:** `InteractionCapability` takes primitives (not the recipe type / not the strategy) so VrInteraction stays free of AssetBrowser types; the strategy is a Build-time-only dependency of the builders. `ColliderKind` lives in VrInteraction. This refines the spec's "indicative" §3 signatures without changing behavior.
