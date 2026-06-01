# Rig in Entity Pipeline — Slice A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **GIT RULE (overrides skill defaults):** This project's owner commits manually. **NEVER run `git add`/`git commit`.** Each task ends with a **Checkpoint** (compile + tests), not a commit. The user commits when satisfied.

> **Unity workflow notes:**
> - After creating/modifying `.cs`: `refresh_unity (mode: force, scope: all, compile: request)`, then `read_console (types:[error], filter_text:"CS")`. Only `error CS####` matters — `MCP-FOR-UNITY: Client handler ...` / `MissingReferenceException: m_Targets` / `SerializedObjectNotCreatableException` are harmless harness noise.
> - Tests: `run_tests (mode:"EditMode", test_names:[...])` then poll `get_test_job (wait_timeout: 120)`. If `test_names` filtering misbehaves, run all EditMode and confirm the named classes pass.

**Goal:** Make all three asset types share one symmetric shape (Builder + per-type Factory), funnel every spawn through the single `AssetEntityBuilderRegistry` door with a unified `InteractionCapability` finalizer, and lay the rig's recipe foundation (skeleton descriptor written at import; rig restores as a static, selectable mesh) — without touching the fragile `PromeonProxyRigBuilder`/`RigRuntime`/`Crush Dummy` machinery.

**Architecture:** Each `IAssetEntityBuilder` delegates type-specific construction to a thin `*EntityFactory` over the shared `GltfModelLoader` primitive. `Registry.RestoreAsync` is the one place that calls `InteractionCapability.Apply` (from the recipe), so builders only produce geometry. `RigEntityBuilder.BuildAsync` extracts a `RigDefinition` into the recipe; `RestoreAsync` loads the static mesh (proxy construction is Slice B).

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces for runtime), VContainer DI, glTFast, `JsonUtility`, NUnit (Unity Test Runner, `_App.Tests`).

---

### Task 1: `AssetEntityRecipe` carries the rig descriptor

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs`
- Test: `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeRigTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/AssetEntityRecipeRigTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeRigTests
{
    [Test]
    public void Recipe_WithRig_RoundTripsThroughJsonUtility()
    {
        var recipe = new AssetEntityRecipe { type = AssetType.Rig };
        recipe.rig = new RigDefinition { AssetId = "a" };
        recipe.rig.Bones.Add(new BoneRecord { BoneName = "hips" });
        recipe.rig.Bones.Add(new BoneRecord { BoneName = "spine" });

        var json = JsonUtility.ToJson(recipe);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.IsTrue(back.HasRig);
        Assert.AreEqual(2, back.rig.Bones.Count);
        Assert.AreEqual("hips",  back.rig.Bones[0].BoneName);
        Assert.AreEqual("spine", back.rig.Bones[1].BoneName);
    }

    [Test]
    public void Recipe_WithoutRig_HasRigIsFalseAfterRoundTrip()
    {
        // JsonUtility cannot store null for a nested [Serializable] field — it may deserialize as an
        // empty object. HasRig is the canonical "has a skeleton" signal (Bones.Count > 0), so it
        // must read false whether `rig` came back null or empty.
        var recipe = new AssetEntityRecipe { type = AssetType.Object };
        var json = JsonUtility.ToJson(recipe);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.IsFalse(back.HasRig);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run `run_tests (mode:"EditMode", test_names:["AssetEntityRecipeRigTests"])`.
Expected: compile error / FAIL — `AssetEntityRecipe` has no `rig` field and no `HasRig`.

- [ ] **Step 3: Add the `rig` field + `HasRig` helper**

In `Assets/_App/Scripts/AssetBrowser/AssetEntityRecipe.cs`, add a `rig` field after the reference block and a `HasRig` helper (methods/props are not serialized, so this stays JsonUtility-safe):

```csharp
    // Reference-specific.
    public float            referenceAspect = 1f;
    public float            referenceBottomGap = 0.5f;
    public bool             referenceTwoSided = true;

    // Rig-specific (null / empty for non-rig). Reuses the existing RigDefinition (Bones + IkChains).
    public RigDefinition    rig;

    // Canonical "this recipe describes a skeleton" check. JsonUtility cannot persist a null nested
    // object, so `rig` may come back as an empty object — guard on the bone count, never on null.
    public bool HasRig => rig != null && rig.Bones != null && rig.Bones.Count > 0;
```

- [ ] **Step 4: Run the test to verify it passes**

Run `refresh_unity (force/all/compile)` → `read_console (error, "CS")` (expect none) → `run_tests (mode:"EditMode", test_names:["AssetEntityRecipeRigTests"])` → poll `get_test_job`.
Expected: PASS (2/2).

- [ ] **Step 5: Checkpoint** — compile clean, `AssetEntityRecipeRigTests` 2/2 green. (No commit — user commits manually.)

---

### Task 2: `RigDefinitionExtractor` (skeleton → descriptor)

**Files:**
- Create: `Assets/_App/Scripts/RigBuilder/RigDefinitionExtractor.cs`
- Test: `Assets/_App/Tests/RigBuilder/RigDefinitionExtractorTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/RigBuilder/RigDefinitionExtractorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class RigDefinitionExtractorTests
{
    [Test]
    public void FromSkinnedMesh_NullRenderer_ReturnsNull()
    {
        Assert.IsNull(RigDefinitionExtractor.FromSkinnedMesh(null));
    }

    [Test]
    public void FromSkinnedMesh_NoBones_ReturnsNull()
    {
        var go  = new GameObject("smr");
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new Transform[0];

        Assert.IsNull(RigDefinitionExtractor.FromSkinnedMesh(smr));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FromSkinnedMesh_CollectsBoneNamesInOrder()
    {
        var root  = new GameObject("smr");
        var smr   = root.AddComponent<SkinnedMeshRenderer>();
        var hips  = new GameObject("hips").transform;
        var spine = new GameObject("spine").transform;
        smr.bones = new[] { hips, spine };

        var def = RigDefinitionExtractor.FromSkinnedMesh(smr);

        Assert.IsNotNull(def);
        Assert.AreEqual(2, def.Bones.Count);
        Assert.AreEqual("hips",  def.Bones[0].BoneName);
        Assert.AreEqual("spine", def.Bones[1].BoneName);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(hips.gameObject);
        Object.DestroyImmediate(spine.gameObject);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run `run_tests (mode:"EditMode", test_names:["RigDefinitionExtractorTests"])`.
Expected: compile error — `RigDefinitionExtractor` does not exist.

- [ ] **Step 3: Create the extractor**

Create `Assets/_App/Scripts/RigBuilder/RigDefinitionExtractor.cs`:

```csharp
using UnityEngine;

// Pure build-time decision: a SkinnedMeshRenderer's skeleton → a serializable RigDefinition (bone
// names only for Slice A). Returns null when there is no usable skeleton, so the rig gracefully
// degrades to a static object. Builds no GameObjects.
public static class RigDefinitionExtractor
{
    public static RigDefinition FromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            if (bone != null)
                def.Bones.Add(new BoneRecord { BoneName = bone.name });

        return def.Bones.Count > 0 ? def : null;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

`refresh_unity` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode", test_names:["RigDefinitionExtractorTests"])`.
Expected: PASS (3/3).

- [ ] **Step 5: Checkpoint** — compile clean, `RigDefinitionExtractorTests` 3/3 green.

---

### Task 3: Rename `ReferenceQuadFactory` → `ReferenceEntityFactory`

`ReferenceQuadFactory` is a plain (non-MonoBehaviour) C# class, so no scene/prefab references its `.meta` GUID — a free rename. Real code references: `ReferenceEntityBuilder`, `RootLifetimeScope`. (Docs are handled later by cleanup task #16.)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs`
- Delete: `Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs` (+ `.meta`)
- Modify: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Create the renamed file**

Create `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs` (same body, class renamed, stale "empty pivot" comment corrected to the centered-quad reality):

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Reference: builds a centered textured quad from the
// recipe's baked aspect/two-sided. The quad IS the node (pivot at geometry center, so the gizmo
// rotates around the middle); the vertical lift comes from the recipe's spawnOffset, applied once at
// spawn, so it survives reload without drifting.
public class ReferenceEntityFactory
{
    private readonly ImportRenderProfile _renderProfile;

    public ReferenceEntityFactory(ImportRenderProfile renderProfile)
    {
        _renderProfile = renderProfile;
    }

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation,
                                        float aspect, bool twoSided, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceEntityFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        var go = new GameObject("ReferenceImage");
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = new Vector3(aspect, 1f, 1f);

        go.AddComponent<MeshFilter>().sharedMesh       = BuildCenteredQuad();
        go.AddComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided);
        return Task.FromResult(go);
    }

    private static Mesh BuildCenteredQuad()
    {
        var mesh = new Mesh { name = "ReferenceQuad" };
        mesh.vertices  = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv        = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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

- [ ] **Step 2: Delete the old file**

Run (PowerShell):

```powershell
Remove-Item -LiteralPath "Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs","Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs.meta" -Force
```

- [ ] **Step 3: Update `ReferenceEntityBuilder`**

In `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`, rename the field type and ctor param:

```csharp
    private readonly AssetSourceStore       _store;
    private readonly ReferenceEntityFactory _quads;

    public ReferenceEntityBuilder(AssetSourceStore store, ReferenceEntityFactory quads)
    {
        _store = store;
        _quads = quads;
    }
```

- [ ] **Step 4: Update the DI registration**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, change line 48:

```csharp
        builder.Register<ReferenceEntityFactory>(Lifetime.Singleton);
```

- [ ] **Step 5: Compile + regression**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")` (expect none) → `run_tests (mode:"EditMode")` (full suite).
Expected: compiles; pre-existing AssetBrowser tests still pass (no new failures vs. baseline).

- [ ] **Step 6: Checkpoint** — `ReferenceQuadFactory` gone, references updated, compile clean.

---

### Task 4: Hoist `InteractionCapability.Apply` into the registry

Move the single "make it selectable" finalizer out of every builder and into `Registry.RestoreAsync`. Drop the now-dead legacy `recipe ?? BuildAsync(...)` fallback (the `asset-libraries` rename already orphaned all pre-recipe imports → every current imported asset has a recipe). Builtin assets have `recipe == null` → no `Apply` (the prefab is pre-baked; `Apply` is idempotent anyway).

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`
- Test: `Assets/_App/Tests/AssetBrowser/AssetEntityBuilderRegistryTests.cs` (extend)

- [ ] **Step 1: Write the failing tests**

In `Assets/_App/Tests/AssetBrowser/AssetEntityBuilderRegistryTests.cs`, extend `FakeBuilder` to return a GameObject and add two tests. Replace the `FakeBuilder` class and append the new tests:

```csharp
    private class FakeBuilder : IAssetEntityBuilder
    {
        public AssetType HandledType { get; set; }
        public AssetEntityRecipe LastRecipe;
        public GameObject ReturnGo;
        public Task<AssetEntityRecipe> BuildAsync(string p, AssetType t, CancellationToken ct)
            => Task.FromResult(new AssetEntityRecipe { type = t });
        public Task<GameObject> RestoreAsync(ILabAsset a, AssetEntityRecipe r, Vector3 pos, Quaternion rot, CancellationToken ct)
        { LastRecipe = r; return Task.FromResult(ReturnGo); }
    }

    [Test]
    public void RestoreAsync_AppliesCapabilityFromRecipe_WhenRecipePresent()
    {
        var go  = new GameObject("imported");
        var b   = new FakeBuilder { HandledType = AssetType.Object, ReturnGo = go };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { b });

        var recipe = new AssetEntityRecipe
        {
            type = AssetType.Object, selectable = true,
            colliderKind = ColliderKind.Box, colliderSize = new Vector3(2f, 3f, 4f),
        };
        var asset = new ImportedLabAsset("id", "n", AssetType.Object, "asset-libraries/sources/id.glb", recipe);

        var result = reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None)
                        .GetAwaiter().GetResult();

        var box = result.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "Registry should apply InteractionCapability when a recipe is present");
        Assert.AreEqual(new Vector3(2f, 3f, 4f), box.size);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RestoreAsync_SkipsCapability_WhenRecipeNull()
    {
        var go  = new GameObject("builtin-like");
        var b   = new FakeBuilder { HandledType = AssetType.Object, ReturnGo = go };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { b });

        var asset = new ImportedLabAsset("id", "n", AssetType.Object, "ref", null); // no recipe

        var result = reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None)
                        .GetAwaiter().GetResult();

        Assert.IsNull(result.GetComponent<BoxCollider>(), "No recipe → no capability applied by the registry");
        Object.DestroyImmediate(go);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run `run_tests (mode:"EditMode", test_names:["AssetEntityBuilderRegistryTests"])`.
Expected: the two new tests FAIL — the registry does not apply capability yet (no `BoxCollider`).

- [ ] **Step 3: Make the registry apply capability**

Replace `RestoreAsync` in `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` (it becomes `async`):

```csharp
    public async Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var recipe = (asset as ImportedLabAsset)?.Recipe; // null for builtin (prefab already baked)
        var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

        // Single finalization point: builders produce only geometry; selectability/collider/identity
        // are applied here from the recipe. Builtin (recipe == null) is pre-baked, so skip.
        if (go != null && recipe != null)
            InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
                recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

        return go;
    }
```

- [ ] **Step 4: Remove `Apply` + legacy fallback from `ObjectEntityBuilder.RestoreAsync`**

Replace `RestoreAsync` in `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`:

```csharp
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

        // Capability is applied by AssetEntityBuilderRegistry.RestoreAsync (single point).
        return _loader.LoadAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
```

(`recipe` is now unused here — that is fine; the interface requires the parameter.)

- [ ] **Step 5: Remove `Apply` + legacy fallback from `ReferenceEntityBuilder.RestoreAsync`**

Replace `RestoreAsync` in `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`:

```csharp
    public Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");
        if (recipe == null)
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no recipe");

        var abs = _store.AbsolutePath(asset.SourceRef);
        // Capability is applied by AssetEntityBuilderRegistry.RestoreAsync (single point).
        return _quads.CreateAsync(abs, position, rotation, recipe.referenceAspect, recipe.referenceTwoSided, ct);
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode", test_names:["AssetEntityBuilderRegistryTests"])`.
Expected: all registry tests PASS (the original dispatch test + the two new ones).

- [ ] **Step 7: Checkpoint** — registry is the single finalizer; Object/Reference builders produce geometry only; compile clean; registry tests green.

---

### Task 5: Extract `ObjectEntityFactory`, rewire `ObjectEntityBuilder`

Introduce the trio's first factory and route `ObjectEntityBuilder` through it (the load behavior is byte-for-byte the same as the previous direct `GltfModelLoader` call). `RigEntityBuilder` still inherits `ObjectEntityBuilder` at this point, so its ctor is updated to pass the factory.

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ObjectEntityFactory.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Create the factory**

Create `Assets/_App/Scripts/AssetBrowser/ObjectEntityFactory.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Object: loads the imported glTF mesh. Thin wrapper over the
// shared low-level GltfModelLoader. Mirrors ReferenceEntityFactory / RigEntityFactory.
public class ObjectEntityFactory
{
    private readonly GltfModelLoader _loader;

    public ObjectEntityFactory(GltfModelLoader loader) => _loader = loader;

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);
}
```

- [ ] **Step 2: Rewire `ObjectEntityBuilder` to depend on the factory**

Replace the fields/ctor and the two methods' `_loader` uses in `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`:

```csharp
public class ObjectEntityBuilder : IAssetEntityBuilder
{
    protected readonly AssetSourceStore    _store;
    protected readonly ObjectEntityFactory _factory;
    protected readonly IColliderStrategy   _collider;

    public ObjectEntityBuilder(AssetSourceStore store, ObjectEntityFactory factory, IColliderStrategy collider)
    {
        _store    = store;
        _factory  = factory;
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

        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
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

- [ ] **Step 3: Update `RigEntityBuilder`'s base ctor call**

`RigEntityBuilder` still inherits `ObjectEntityBuilder` here — pass the factory through. Replace `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`:

```csharp
// Slice 1/A: a Rig import behaves like a static Object (selectable static skinned mesh). Slice B will
// replace this with runtime proxy-rig building + a bone descriptor in the recipe.
public class RigEntityBuilder : ObjectEntityBuilder
{
    public RigEntityBuilder(AssetSourceStore store, ObjectEntityFactory factory, IColliderStrategy collider)
        : base(store, factory, collider) { }

    public override AssetType HandledType => AssetType.Rig;
}
```

- [ ] **Step 4: Register the factory**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, add next to the other AssetBrowser registrations (after `GltfModelLoader`):

```csharp
        builder.Register<ObjectEntityFactory>(Lifetime.Singleton);
```

- [ ] **Step 5: Compile + regression**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode")` (full suite).
Expected: compiles; all previously-green tests still green (behavior unchanged — factory just forwards to the loader).

- [ ] **Step 6: Checkpoint** — `ObjectEntityFactory` in place and wired; compile clean; no test regressions.

---

### Task 6: `RigEntityFactory` + standalone `RigEntityBuilder` writing `recipe.rig`

Complete the trio: give Rig its own factory (Slice A: static load, same as Object), detach `RigEntityBuilder` from `ObjectEntityBuilder`, and have `BuildAsync` extract the skeleton into `recipe.rig`. `RestoreAsync` still loads the static mesh; whole-rig selectability comes from the registry's `InteractionCapability` step (Task 4).

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Create the rig factory**

Create `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Rig. Slice A: loads the static imported mesh (no proxies),
// mirroring ObjectEntityFactory. Slice B adds BuildProxyRig (runtime proxy-bone construction).
public class RigEntityFactory
{
    private readonly GltfModelLoader _loader;

    public RigEntityFactory(GltfModelLoader loader) => _loader = loader;

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);
}
```

- [ ] **Step 2: Make `RigEntityBuilder` standalone + write the skeleton**

Replace `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig (Slice A): a static skinned mesh, selectable as a whole, PLUS a baked skeleton descriptor in the
// recipe for the future proxy-rig slice. BuildAsync measures the collider AND extracts the skeleton
// (graceful: no skeleton → recipe.rig stays null → behaves as a static object). RestoreAsync loads the
// static mesh; whole-rig selectability is applied by the registry. Proxy construction is Slice B.
public class RigEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore  _store;
    private readonly RigEntityFactory  _factory;
    private readonly IColliderStrategy _collider;

    public RigEntityBuilder(AssetSourceStore store, RigEntityFactory factory, IColliderStrategy collider)
    {
        _store    = store;
        _factory  = factory;
        _collider = collider;
    }

    public AssetType HandledType => AssetType.Rig;

    public async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"RigEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            _collider.Measure(temp, out var kind, out var center, out var size);
            recipe.colliderKind   = kind;
            recipe.colliderCenter = center;
            recipe.colliderSize   = size;

            var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
            if (recipe.rig == null)
                Debug.LogWarning($"RigEntityBuilder: '{sourceAbsolutePath}' has no skeleton — importing as a static object.");
        }
        finally { UnityEngine.Object.Destroy(temp); }

        return recipe;
    }

    public Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");

        // Slice A: static mesh; whole-rig selectability is applied by the registry from the recipe.
        // Slice B: branch on recipe.HasRig here to build the proxy hierarchy via the factory.
        return _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
```

- [ ] **Step 3: Register the rig factory**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, add next to the other factories:

```csharp
        builder.Register<RigEntityFactory>(Lifetime.Singleton);
```

(`RigEntityBuilder` is already registered `.As<IAssetEntityBuilder>()`; VContainer now resolves its new `RigEntityFactory` dependency.)

- [ ] **Step 4: Compile + full regression**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode")` (full suite).
Expected: compiles; `AssetEntityRecipeRigTests`, `RigDefinitionExtractorTests`, `AssetEntityBuilderRegistryTests` all green; no regressions elsewhere.

- [ ] **Step 5: Checkpoint** — trio complete (`ObjectEntityFactory`/`ReferenceEntityFactory`/`RigEntityFactory`); `RigEntityBuilder` standalone, writes `recipe.rig`; compile clean; tests green.

---

### Task 7: Manual VR verification + final sweep

Automated tests can't exercise glTFast loading; verify the runtime paths by hand and run the full suite once more.

- [ ] **Step 1: Full EditMode run** — `run_tests (mode:"EditMode")`; confirm no failures beyond the known pre-existing baseline.

- [ ] **Step 2: VR — import + spawn each type** (in VrEditing): import a `.glb` object → spawns, selectable, outline works; import an image → reference quad spawns, selectable; import a rigged `.glb` → spawns as a static mesh, selectable as a whole, **no errors**, and (check console) the skeleton warning is ABSENT for a real rig / PRESENT for a boneless mesh.

- [ ] **Step 3: VR — reload persistence** — re-open the scene; all three reappear at saved transforms, still selectable (the registry restore path).

- [ ] **Step 4: Inspect the card** — open `imported-lib.json` in `persistentDataPath/asset-libraries/`; confirm the rig record has a populated `rig.Bones` array, object/reference records have empty/absent `rig`.

- [ ] **Step 5: Checkpoint** — Slice A behaviorally verified. Hand back to the user for commit. (Slice B = proxy construction + `ProxyRigRuntime`; Slice C = bake tools; doc/rudiment cleanup = task #16.)

---

## Self-Review

**1. Spec coverage (Slice A bullets):**
- Trio factories — Task 3 (Reference rename), Task 5 (Object extract), Task 6 (Rig create). ✓
- Hoist `InteractionCapability` into `Registry.RestoreAsync` — Task 4. ✓
- `AssetEntityRecipe += RigDefinition rig` — Task 1. ✓
- `RigEntityBuilder.BuildAsync` writes `recipe.rig` (or null) + `RestoreAsync` static — Task 6. ✓
- Don't touch `PromeonProxyRigBuilder`/`RigRuntime`/`Crush Dummy`/panels — no task modifies them. ✓
- Error handling (no-bones fallback) — Task 2 (extractor returns null) + Task 6 (warning + null `rig`). ✓
- Tests: recipe round-trip (T1), skeleton extraction + no-bones (T2), registry single-Apply dispatch (T4). ✓ (Bone-name→live-transform mapping and proxy build are Slice B, not A — correctly deferred.)

**2. Placeholder scan:** No TBD/"handle errors"/"similar to"/uncoded steps. Every code step shows full content. ✓

**3. Type consistency:** `*EntityFactory.CreateAsync(absolutePath, position, rotation[, aspect, twoSided], ct)`; `RigDefinitionExtractor.FromSkinnedMesh(smr)`; `AssetEntityRecipe.rig` + `.HasRig`; ctor signatures `(AssetSourceStore, <Type>EntityFactory, IColliderStrategy)` consistent across Tasks 5–6; `Registry.RestoreAsync` async in Task 4 and both callers already `await`. ✓
