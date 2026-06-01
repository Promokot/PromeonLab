# Builtin Assets Through the Entity Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Built-in assets are processed by type through the entity pipeline (recipe baked into the SO at edit time; image References generated into prefabs), so a bare prefab/texture becomes a fully selectable scene entity with no manual per-prefab capability baking.

**Architecture:** A shared synchronous "instance → recipe" core is reused by runtime `BuildAsync` (after glTF load) and a new editor bake (on the built-in prefab instance). `BuiltinLabAsset` carries the baked `AssetEntityRecipe`; `ILabAsset.Recipe` is hoisted so `AssetEntityBuilderRegistry.RestoreAsync` reads the recipe uniformly and applies `InteractionCapability` for built-in too (idempotent). Reference built-ins get a one-click editor generation pass that builds asset-backed quad mesh + material + prefab. Un-baked built-in → registry throws (existing `AssetSpawner`/`SceneGraph` catches log it).

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces runtime; editor code in `_App.Editor`), VContainer, glTFast, NUnit (EditMode), Unity MCP for refresh/console/tests.

## Project Rules (read before starting)

- **NO `git add` / `git commit`** — the user commits manually. Every task ends with a **Checkpoint**, not a commit.
- **Checkpoint procedure** (replaces the "commit" step):
  1. `refresh_unity` with `mode: force, scope: all, compile: request`.
  2. `read_console` with `types: ["error"], filter_text: "CS"`. Only `error CS####` matters. `MCP-FOR-UNITY: Client handler…`, `MissingReferenceException: m_Targets`, `SerializedObjectNotCreatableException` are harmless editor churn — ignore.
  3. For tasks with EditMode tests: `run_tests` with `mode: "EditMode", test_names: [<the new test class names>]` then `get_test_job` with `wait_timeout: 60`.
- **EditMode baseline = 6 known pre-existing failures** (`PathProviderTests` ×4 Windows `\`, `RingRotateStrategyTests` ×2). A task is green if it adds zero *new* failures.
- PowerShell paths use `-LiteralPath` (repo path contains `[02]`).
- Conventions: `[SerializeField] private` (never public fields); one public type per file; no `#if UNITY_EDITOR` in runtime files (all editor code lives under `Assets/_App/Editor/`); forbidden type-name suffixes Manager/Handler/Utils/Helper/Controller/Processor/Service (Factory/Builder/Baker/Generator are fine).

---

## File Structure

**Runtime — modify:**
- `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs` — add `AssetEntityRecipe Recipe { get; }`.
- `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` — `_recipe` field + `Recipe` getter; `_image` (`Texture2D`) field + `Image` getter.
- `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs` — `Recipe => null`.
- `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs` — `static RecipeFromInstance(...)`; `BuildAsync` delegates to it.
- `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` — `static RecipeFromInstance(...)`; `BuildAsync` delegates; `RestoreAsync` reads axis/invert from `recipe.rig`.
- `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs` — built-in branch (`Instantiate(prefab)`).
- `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs` — `public static Mesh BuildCenteredQuad()`; `public static Material BuildMaterial(Texture2D, bool, ImportRenderProfile)`.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` — read `asset.Recipe`; throw for un-baked built-in.
- `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs` — read `e.Asset.Recipe` (not the cast).

**Editor — create (`Assets/_App/Editor/`):**
- `BuiltinRecipeBaker.cs` — reflection bake/write of `_recipe`/`_prefab` per entry.
- `ReferenceImagePrefabGenerator.cs` — image → quad mesh asset + material asset + prefab.
- `BuiltinAssetLibraryEditor.cs` — `[CustomEditor]` with "Bake All" + per-entry buttons.

**Tests — create/modify (`Assets/_App/Tests/AssetBrowser/`):**
- `BuiltinLabAssetRecipeTests.cs` (new) — recipe round-trips with the struct.
- `ObjectEntityBuilderTests.cs` (modify) — Object `RecipeFromInstance` measures box.
- `RigEntityBuilderRecipeTests.cs` (new) — Rig `RecipeFromInstance` extracts skeleton + folds axis/invert.
- `AssetEntityBuilderRegistryTests.cs` (modify) — throw for built-in without recipe.
- `ReferenceEntityFactoryQuadTests.cs` (new) — shared quad geometry.

---

## Task 1: Hoist `Recipe` onto `ILabAsset`; add recipe + image to `BuiltinLabAsset`

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs`
- Test: `Assets/_App/Tests/AssetBrowser/BuiltinLabAssetRecipeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/BuiltinLabAssetRecipeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class BuiltinLabAssetRecipeTests
{
    [Test]
    public void Recipe_RoundTrips_ThroughJsonUtility()
    {
        var json = "{\"_id\":\"b1\",\"_displayName\":\"Cube\",\"_type\":0," +
                   "\"_recipe\":{\"schemaVersion\":1,\"type\":0,\"selectable\":true," +
                   "\"colliderKind\":1,\"colliderSize\":{\"x\":2.0,\"y\":3.0,\"z\":4.0}}}";

        var back = JsonUtility.FromJson<BuiltinLabAsset>(json);

        Assert.IsNotNull(back.Recipe, "BuiltinLabAsset must expose a deserialized recipe");
        Assert.That(back.Recipe.colliderSize.z, Is.EqualTo(4f).Within(1e-4));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` mode `EditMode`, `test_names: ["BuiltinLabAssetRecipeTests"]`, then `get_test_job` (`wait_timeout: 60`).
Expected: FAIL — `BuiltinLabAsset` has no `Recipe` member (compile error or missing field).

- [ ] **Step 3: Add `Recipe` to the interface**

Replace `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs` with:

```csharp
using UnityEngine;

public interface ILabAsset
{
    string      Id          { get; }
    string      DisplayName { get; }
    AssetType   Type        { get; }
    AssetSource Source      { get; }   // which library this record lives in
    string      SourceRef   { get; }   // relative path under asset-libraries/sources; null for Builtin
    Sprite      Icon        { get; }
    AssetEntityRecipe Recipe { get; }  // baked Build→Restore contract; null until baked (Builtin/Imported)
}
```

- [ ] **Step 4: Add `_recipe` + `_image` to `BuiltinLabAsset`**

Replace `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` with:

```csharp
using System;
using UnityEngine;

[Serializable]
public struct BuiltinLabAsset : ILabAsset
{
    [SerializeField] private string     _id;
    [SerializeField] private string     _displayName;
    [SerializeField] private AssetType  _type;
    [SerializeField] private Sprite     _icon;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Texture2D  _image;                // Reference-only generation input; ignored otherwise
    [SerializeField] private TerminalBoneAxis _terminalBonesAxis;       // leaf-bone axis for Rig entries; ignored otherwise
    [SerializeField] private bool             _invertTerminalBonesAxis; // flip the chosen X/Y/Z axis
    [SerializeField] private AssetEntityRecipe _recipe;        // baked at edit time (see BuiltinRecipeBaker)

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Builtin;
    public string      SourceRef   => null;
    public Sprite      Icon        => _icon;
    public GameObject       Prefab        => _prefab;
    public Texture2D        Image         => _image;
    public TerminalBoneAxis TerminalBonesAxis       => _terminalBonesAxis;
    public bool             InvertTerminalBonesAxis => _invertTerminalBonesAxis;
    public AssetEntityRecipe Recipe       => _recipe;
}
```

- [ ] **Step 5: Add `Recipe => null` to `SavedLabAsset`**

In `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs`, add this getter alongside the others (after `Icon`):

```csharp
    public AssetEntityRecipe Recipe => null;   // Saved-library spawn flow is Slice 3 (not implemented)
```

(Note: `ImportedLabAsset` already declares `public AssetEntityRecipe Recipe => _recipe;` — it already satisfies the interface, no change needed.)

- [ ] **Step 6: Run test to verify it passes**

`run_tests` mode `EditMode`, `test_names: ["BuiltinLabAssetRecipeTests"]`, then `get_test_job`.
Expected: PASS.

- [ ] **Step 7: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none) → `run_tests` `["BuiltinLabAssetRecipeTests","ImportedLabAssetRecipeTests"]` (expect PASS). No commit.

---

## Task 2: Shared `RecipeFromInstance` core on Object/Rig builders

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Modify: `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs`
- Test: `Assets/_App/Tests/AssetBrowser/RigEntityBuilderRecipeTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `Assets/_App/Tests/AssetBrowser/ObjectEntityBuilderTests.cs` (keep the existing `HandledTypes_AreDistinct`):

```csharp
using UnityEngine;

public partial class ObjectEntityBuilderTests
{
    [Test]
    public void RecipeFromInstance_Object_MeasuresBoxAndSetsCapability()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // 1x1x1, centered
        try
        {
            var recipe = ObjectEntityBuilder.RecipeFromInstance(cube, new BoundsBoxColliderStrategy(), AssetType.Object);

            Assert.AreEqual(AssetType.Object, recipe.type);
            Assert.IsTrue(recipe.selectable);
            Assert.AreEqual(InteractionLayer.SceneObjects, recipe.interactionLayer);
            Assert.AreEqual(ColliderKind.Box, recipe.colliderKind);
            Assert.That(recipe.colliderSize.x, Is.EqualTo(1f).Within(0.05f));
            Assert.That(recipe.colliderSize.y, Is.EqualTo(1f).Within(0.05f));
        }
        finally { Object.DestroyImmediate(cube); }
    }
}
```

Make the existing class `partial` — change line 3 of that file from `public class ObjectEntityBuilderTests` to `public partial class ObjectEntityBuilderTests`.

Create `Assets/_App/Tests/AssetBrowser/RigEntityBuilderRecipeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class RigEntityBuilderRecipeTests
{
    [Test]
    public void RecipeFromInstance_Rig_ExtractsSkeleton_AndFoldsAxis()
    {
        var root = new GameObject("rig");
        var bone = new GameObject("pelvis");
        bone.transform.SetParent(root.transform);
        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform };
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(
                root, new BoundsBoxColliderStrategy(), TerminalBoneAxis.X, invert: true);

            Assert.AreEqual(AssetType.Rig, recipe.type);
            Assert.IsTrue(recipe.HasRig, "skeleton with one bone must populate recipe.rig");
            Assert.AreEqual(TerminalBoneAxis.X, recipe.rig.TerminalBonesAxis);
            Assert.IsTrue(recipe.rig.InvertTerminalBonesAxis);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RecipeFromInstance_Rig_NoSkeleton_LeavesRigNull()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // no SkinnedMeshRenderer
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(
                go, new BoundsBoxColliderStrategy(), TerminalBoneAxis.Auto, invert: false);
            Assert.IsFalse(recipe.HasRig, "no skeleton → recipe.rig stays null (graceful static object)");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

`run_tests` mode `EditMode`, `test_names: ["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests"]`, then `get_test_job`.
Expected: FAIL — `RecipeFromInstance` does not exist.

- [ ] **Step 3: Add `RecipeFromInstance` to `ObjectEntityBuilder` and delegate**

In `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs`, add the static method and rewrite the body of `BuildAsync` to call it. Final file:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Object = static mesh. Build loads the glTF once to measure its collider; Restore reloads the mesh
// (imported) or instantiates the prefab (builtin) and applies the baked recipe. The measurement core
// (RecipeFromInstance) is shared with the editor builtin bake so the recipe never diverges by call site.
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

    // Shared, synchronous, DI-light: inspect a live GameObject and produce the recipe. Reused by
    // runtime BuildAsync (after glTF load) and the editor builtin bake (on the prefab instance).
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, IColliderStrategy collider, AssetType chosenType)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = chosenType,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };
        collider.Measure(instance, out var kind, out var center, out var size);
        recipe.colliderKind   = kind;
        recipe.colliderCenter = center;
        recipe.colliderSize   = size;
        return recipe;
    }

    public virtual async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"ObjectEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try { return RecipeFromInstance(temp, _collider, chosenType); }
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

- [ ] **Step 4: Add `RecipeFromInstance` to `RigEntityBuilder`, delegate, and read axis from recipe in `RestoreAsync`**

Replace `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` with:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig: a static skinned mesh + a baked skeleton descriptor in the recipe. The shared RecipeFromInstance
// measures the collider AND extracts the skeleton (graceful: no skeleton → recipe.rig null → static
// object). RestoreAsync instantiates the geometry (builtin prefab / imported glTF), then builds the
// runtime proxy rig using axis/invert/bone-names taken from the recipe.
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

    // Shared with the editor builtin bake. axis/invert are folded into recipe.rig when a skeleton exists;
    // the import path passes Auto/false here and ImportPipeline stamps the wizard choice afterward.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, IColliderStrategy collider,
                                                       TerminalBoneAxis axis, bool invert)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };
        collider.Measure(instance, out var kind, out var center, out var size);
        recipe.colliderKind   = kind;
        recipe.colliderCenter = center;
        recipe.colliderSize   = size;

        var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
        if (recipe.rig != null)
        {
            recipe.rig.TerminalBonesAxis       = axis;
            recipe.rig.InvertTerminalBonesAxis = invert;
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
            var recipe = RecipeFromInstance(temp, _collider, TerminalBoneAxis.Auto, invert: false);
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

        // Axis/invert/bone-names come from the recipe for both sources (builtin is guaranteed to have a
        // recipe — the registry throws otherwise). No skeleton in the recipe → all-bones fallback.
        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert);

        return go;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

`run_tests` mode `EditMode`, `test_names: ["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests"]`, then `get_test_job`.
Expected: PASS (3 tests).

- [ ] **Step 6: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none) → `run_tests` `["ObjectEntityBuilderTests","RigEntityBuilderRecipeTests","AssetEntityRecipeRigTests"]` (expect PASS). No commit.

---

## Task 3: Expose shared quad + material statics on `ReferenceEntityFactory`

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs`
- Test: `Assets/_App/Tests/AssetBrowser/ReferenceEntityFactoryQuadTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/ReferenceEntityFactoryQuadTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class ReferenceEntityFactoryQuadTests
{
    [Test]
    public void BuildCenteredQuad_IsUnitCentered_FourVertsTwoTris()
    {
        var mesh = ReferenceEntityFactory.BuildCenteredQuad();
        try
        {
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            Assert.AreEqual(new Vector3(-0.5f, -0.5f, 0f), mesh.vertices[0]);
            Assert.AreEqual(new Vector3( 0.5f,  0.5f, 0f), mesh.vertices[2]);
        }
        finally { Object.DestroyImmediate(mesh); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` mode `EditMode`, `test_names: ["ReferenceEntityFactoryQuadTests"]`, then `get_test_job`.
Expected: FAIL — `BuildCenteredQuad` is `private` (compile error: inaccessible).

- [ ] **Step 3: Make quad + material statics public; keep instance behavior**

Replace `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs` with:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Reference: builds a centered textured quad from the
// recipe's baked aspect/two-sided. The quad IS the node (pivot at geometry center). BuildCenteredQuad
// and BuildMaterial are public statics so the editor builtin-image generator builds asset-backed
// equivalents from the SAME geometry/material logic.
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
        go.AddComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided, _renderProfile);
        return Task.FromResult(go);
    }

    public static Mesh BuildCenteredQuad()
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

    public static Material BuildMaterial(Texture2D tex, bool twoSided, ImportRenderProfile profile)
    {
        Shader shader = null;
        if (profile != null && profile.TryGet(AssetType.Reference, out var entry))
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

- [ ] **Step 4: Run test to verify it passes**

`run_tests` mode `EditMode`, `test_names: ["ReferenceEntityFactoryQuadTests"]`, then `get_test_job`.
Expected: PASS.

- [ ] **Step 5: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none — `ReferenceEntityBuilder.BuildAsync` still calls the instance `CreateAsync`, unaffected) → `run_tests` `["ReferenceEntityFactoryQuadTests","ReferenceEntityBuilderTests"]` (expect PASS). No commit.

---

## Task 4: Registry + AssetSpawner read `asset.Recipe`; registry throws for un-baked built-in

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs`
- Modify: `Assets/_App/Tests/AssetBrowser/AssetEntityBuilderRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

Append a test to `Assets/_App/Tests/AssetBrowser/AssetEntityBuilderRegistryTests.cs` (inside the class, keep the existing three tests and the `FakeBuilder`):

```csharp
    [Test]
    public void RestoreAsync_Throws_ForBuiltinWithoutRecipe()
    {
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[0]);
        var builtin = default(BuiltinLabAsset); // Source => Builtin, Recipe => null, Type => Object

        Assert.Throws<System.NotSupportedException>(() =>
            reg.RestoreAsync(builtin, Vector3.zero, Quaternion.identity, CancellationToken.None)
               .GetAwaiter().GetResult());
    }
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` mode `EditMode`, `test_names: ["AssetEntityBuilderRegistryTests"]`, then `get_test_job`.
Expected: FAIL — currently a `default(BuiltinLabAsset)` with null recipe does not throw (it would hit `Resolve` / return without capability), so no `NotSupportedException` of the expected origin, OR the empty-builder `Resolve` throws a *different* message. Confirm the new test is red.

- [ ] **Step 3: Update the registry**

Replace `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Type-keyed dispatch for both Build (import) and Restore (spawn / scene-load). Reads the baked recipe
// straight off the record (ILabAsset.Recipe) so callers never deal with recipes directly.
public class AssetEntityBuilderRegistry
{
    private readonly Dictionary<AssetType, IAssetEntityBuilder> _byType = new();

    public AssetEntityBuilderRegistry(IReadOnlyList<IAssetEntityBuilder> builders)
    {
        foreach (var b in builders) _byType[b.HandledType] = b;
    }

    public Task<AssetEntityRecipe> BuildAsync(AssetType type, string sourceAbsolutePath, CancellationToken ct)
        => Resolve(type).BuildAsync(sourceAbsolutePath, type, ct);

    public async Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var recipe = asset.Recipe; // null for un-baked builtin / Saved

        // Builtin must be baked: a bare prefab with no recipe would spawn uninteractive, so refuse it.
        // The existing catches in AssetSpawner / SceneGraph.OnSceneOpenedAsync log this without crashing.
        if (recipe == null && asset.Source == AssetSource.Builtin)
            throw new NotSupportedException(
                $"Builtin asset '{asset.Id}' has no baked recipe — bake it in the BuiltinAssetLibrary inspector.");

        var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

        // Single finalization point: builders produce only geometry; selectability/collider/identity
        // are applied here from the recipe. Idempotent (skips if XRPromeonInteractable already present).
        if (go != null && recipe != null)
            InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
                recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

        return go;
    }

    private IAssetEntityBuilder Resolve(AssetType type)
    {
        if (!_byType.TryGetValue(type, out var b))
            throw new NotSupportedException($"No entity builder registered for asset type {type}");
        return b;
    }
}
```

- [ ] **Step 4: Update `AssetSpawner` to read `e.Asset.Recipe`**

In `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs`, change the recipe lookup inside `SpawnCoreAsync` from:

```csharp
            var recipe = (e.Asset as ImportedLabAsset)?.Recipe;
```

to:

```csharp
            var recipe = e.Asset.Recipe;   // builtin (baked) + imported both carry spawnOffset now
```

- [ ] **Step 5: Run tests to verify they pass**

`run_tests` mode `EditMode`, `test_names: ["AssetEntityBuilderRegistryTests"]`, then `get_test_job`.
Expected: PASS (4 tests — the new throw test plus the 3 existing: dispatch, capability-when-present, skip-when-imported-null). The imported-null-recipe test still passes because only `Source == Builtin` throws.

- [ ] **Step 6: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none) → `run_tests` `["AssetEntityBuilderRegistryTests"]` (expect PASS). No commit.

---

## Task 5: Reference built-in restore branch

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`

- [ ] **Step 1: Add the built-in branch to `RestoreAsync`**

In `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`, replace the `RestoreAsync` method with:

```csharp
    public Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        // Builtin Reference is a generated prefab (see ReferenceImagePrefabGenerator): instantiate it
        // like Object/Rig; capability is applied by AssetEntityBuilderRegistry from the baked recipe.
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");
        if (recipe == null)
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no recipe");

        var abs = _store.AbsolutePath(asset.SourceRef);
        // Capability is applied by AssetEntityBuilderRegistry.RestoreAsync (single point).
        return _quads.CreateAsync(abs, position, rotation, recipe.referenceAspect, recipe.referenceTwoSided, ct);
    }
```

- [ ] **Step 2: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none). No new unit test (builtin prefab path is verified in the final VR check); `run_tests` `["ReferenceEntityBuilderTests"]` to confirm no regression (expect PASS). No commit.

---

## Task 6: Editor — Reference image → prefab generator

**Files:**
- Create: `Assets/_App/Editor/ReferenceImagePrefabGenerator.cs`

- [ ] **Step 1: Create the generator**

Create `Assets/_App/Editor/ReferenceImagePrefabGenerator.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only: turns a built-in Reference entry's Texture2D into asset-backed quad mesh + material +
// prefab, and returns the matching recipe. Mirrors ReferenceEntityBuilder.BuildAsync's recipe values
// and ReferenceEntityFactory's geometry/material so runtime and built-in references look identical.
public static class ReferenceImagePrefabGenerator
{
    private const string Dir      = "Assets/_App/Content/Generated/References";
    private const string MeshPath = Dir + "/ReferenceQuad.mesh";

    public static GameObject Generate(string id, Texture2D image, out AssetEntityRecipe recipe)
    {
        Directory.CreateDirectory(Dir);

        float aspect = image.height != 0 ? (float)image.width / image.height : 1f;

        // Shared centered-quad mesh asset (created once, reused by every reference prefab).
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = ReferenceEntityFactory.BuildCenteredQuad();
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        // Per-entry material asset (overwrite in place on re-generate).
        var profile = LoadRenderProfile();
        var matPath = $"{Dir}/{id}_Mat.mat";
        var mat = ReferenceEntityFactory.BuildMaterial(image, twoSided: true, profile);
        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existingMat != null)
        {
            existingMat.shader      = mat.shader;
            existingMat.mainTexture = image;
            if (mat.HasProperty("_Cull")) existingMat.SetFloat("_Cull", mat.GetFloat("_Cull"));
            existingMat.doubleSidedGI = mat.doubleSidedGI;
            Object.DestroyImmediate(mat);
            mat = existingMat;
            EditorUtility.SetDirty(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // Build the quad GameObject and save as a prefab.
        var go = new GameObject($"Ref_{id}");
        go.transform.localScale = new Vector3(aspect, 1f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh       = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        var prefabPath = $"{Dir}/{id}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        const float h = 1f, gap = 0.5f;
        recipe = new AssetEntityRecipe
        {
            type               = AssetType.Reference,
            selectable         = true,
            interactionLayer   = InteractionLayer.SceneObjects,
            colliderKind       = ColliderKind.Box,
            colliderCenter     = Vector3.zero,
            colliderSize       = new Vector3(1f, h, 0.02f),
            spawnOffset        = new Vector3(0f, gap + h * 0.5f, 0f),
            referenceAspect    = aspect,
            referenceBottomGap = gap,
            referenceTwoSided  = true,
        };

        return prefab;
    }

    private static ImportRenderProfile LoadRenderProfile()
    {
        var guids = AssetDatabase.FindAssets("t:ImportRenderProfile");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<ImportRenderProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
```

- [ ] **Step 2: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none). Editor code, no EditMode test (verified manually in Task 8). No commit.

---

## Task 7: Editor — recipe baker + `BuiltinAssetLibrary` inspector

**Files:**
- Create: `Assets/_App/Editor/BuiltinRecipeBaker.cs`
- Create: `Assets/_App/Editor/BuiltinAssetLibraryEditor.cs`

- [ ] **Step 1: Create the baker (reflection write of `_recipe`/`_prefab`)**

Create `Assets/_App/Editor/BuiltinRecipeBaker.cs`:

```csharp
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only: bakes the AssetEntityRecipe (and, for References, generates the prefab) into each
// BuiltinLabAsset entry of a BuiltinAssetLibrary. Built-in source is a prefab (already a GameObject),
// so the bake instantiates it and reuses the same synchronous measurement core as runtime import.
// Writes the struct's private serialized fields via reflection so the runtime types stay editor-clean.
public static class BuiltinRecipeBaker
{
    private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void BakeAll(BuiltinAssetLibrary lib)
    {
        var list = Entries(lib);
        if (list == null) return;
        for (int i = 0; i < list.Count; i++) BakeIndex(lib, list, i);
        Persist(lib);
    }

    public static void BakeOne(BuiltinAssetLibrary lib, int index)
    {
        var list = Entries(lib);
        if (list == null || index < 0 || index >= list.Count) return;
        BakeIndex(lib, list, index);
        Persist(lib);
    }

    public static IList Entries(BuiltinAssetLibrary lib)
        => typeof(BuiltinAssetLibrary).GetField("_entries", Priv)?.GetValue(lib) as IList;

    private static void BakeIndex(BuiltinAssetLibrary lib, IList list, int i)
    {
        var entry = (BuiltinLabAsset)list[i];
        var collider = new BoundsBoxColliderStrategy();

        AssetEntityRecipe recipe;
        GameObject generatedPrefab = null;

        switch (entry.Type)
        {
            case AssetType.Object:
                if (entry.Prefab == null) { Debug.LogWarning($"Bake: '{entry.Id}' Object has no prefab — skipped."); return; }
                {
                    var temp = Object.Instantiate(entry.Prefab);
                    try { recipe = ObjectEntityBuilder.RecipeFromInstance(temp, collider, AssetType.Object); }
                    finally { Object.DestroyImmediate(temp); }
                }
                break;

            case AssetType.Rig:
                if (entry.Prefab == null) { Debug.LogWarning($"Bake: '{entry.Id}' Rig has no prefab — skipped."); return; }
                {
                    var temp = Object.Instantiate(entry.Prefab);
                    try { recipe = RigEntityBuilder.RecipeFromInstance(temp, collider, entry.TerminalBonesAxis, entry.InvertTerminalBonesAxis); }
                    finally { Object.DestroyImmediate(temp); }
                }
                break;

            case AssetType.Reference:
                if (entry.Image == null) { Debug.LogWarning($"Bake: '{entry.Id}' Reference has no image — skipped."); return; }
                generatedPrefab = ReferenceImagePrefabGenerator.Generate(entry.Id, entry.Image, out recipe);
                break;

            default:
                return;
        }

        object boxed = entry; // box the struct so reflected SetValue sticks
        typeof(BuiltinLabAsset).GetField("_recipe", Priv).SetValue(boxed, recipe);
        if (generatedPrefab != null)
            typeof(BuiltinLabAsset).GetField("_prefab", Priv).SetValue(boxed, generatedPrefab);
        list[i] = (BuiltinLabAsset)boxed;
    }

    private static void Persist(BuiltinAssetLibrary lib)
    {
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }
}
```

- [ ] **Step 2: Create the custom inspector**

Create `Assets/_App/Editor/BuiltinAssetLibraryEditor.cs`:

```csharp
using System.Collections;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuiltinAssetLibrary))]
public class BuiltinAssetLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var lib = (BuiltinAssetLibrary)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recipe Bake", EditorStyles.boldLabel);

        if (GUILayout.Button("Bake All"))
            BuiltinRecipeBaker.BakeAll(lib);

        var entries = BuiltinRecipeBaker.Entries(lib);
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = (BuiltinLabAsset)entries[i];
            var verb  = e.Type == AssetType.Reference ? "Generate" : "Bake";
            var label = $"{verb}: {(string.IsNullOrEmpty(e.DisplayName) ? e.Id : e.DisplayName)} ({e.Type})";
            if (GUILayout.Button(label))
                BuiltinRecipeBaker.BakeOne(lib, i);
        }
    }
}
```

- [ ] **Step 3: Checkpoint** — `refresh_unity` (force/all/compile) → `read_console` (error/CS, expect none). Editor-only. No commit.

---

## Task 8: Final verification + hand-off

**Files:** none (verification only).

- [ ] **Step 1: Full EditMode run**

`run_tests` mode `EditMode` (all), then `get_test_job` (`wait_timeout: 60`).
Expected: only the **6 known pre-existing failures** (`PathProviderTests` ×4, `RingRotateStrategyTests` ×2). The new test classes (`BuiltinLabAssetRecipeTests`, `RigEntityBuilderRecipeTests`, `ReferenceEntityFactoryQuadTests`) and the modified ones (`ObjectEntityBuilderTests`, `AssetEntityBuilderRegistryTests`) are green. No *new* failures.

- [ ] **Step 2: Console clean**

`read_console` `types: ["error"], filter_text: "CS"` → expect none.

- [ ] **Step 3: Hand off to the user for in-editor + VR verification** (these are the user's manual steps, not the agent's):
  - In the `DefaultBuiltinAssetLibrary` inspector: assign a bare Object prefab / Rig prefab / Reference `Texture2D` to entries, press **Bake All**.
  - Confirm: Object/Rig entries get a populated `_recipe`; Reference entries get a generated `_prefab` under `Assets/_App/Content/Generated/References/` and a `_recipe`.
  - Enter VR: spawn each built-in → it is selectable/outlined; Rig builds proxy bones; Reference stands above the floor; positions survive a scene reload.
  - Confirm an un-baked built-in is simply not spawned (a warning in the log, no crash).

---

## Self-Review

**Spec coverage:**
- Recipe carrier (`BuiltinLabAsset._recipe` + `Recipe` on `ILabAsset`) → Task 1. ✓
- `Texture2D _image` slot → Task 1. ✓
- Shared instance→recipe core (Object/Rig), reused by `BuildAsync` + editor bake → Task 2. ✓
- Reference generation (shared quad mesh asset + material + prefab, `localScale=aspect`, recipe values) → Tasks 3 (statics) + 6 (generator). ✓
- Runtime unification: registry reads `asset.Recipe`, applies capability for built-in (idempotent), throws for un-baked built-in; `AssetSpawner` reads `asset.Recipe` → Task 4. ✓
- Rig reads axis/invert/bones from `recipe.rig` → Task 2 (`RestoreAsync`). ✓
- Reference built-in restore branch → Task 5. ✓
- Editor bake UX (custom inspector, Bake All + per-entry, skips null prefab/image, no abort) → Tasks 6–7. ✓
- Error handling (graceful no-skeleton; built-in throw caught by existing `AssetSpawner`/`SceneGraph`) → Tasks 2, 4. ✓
- Tests 1–6 from the spec → Tasks 1–4 cover serialization, Object measure, Rig extract+fold, registry throw, shared quad. ✓ (Editor generation = manual per spec.)

**Placeholder scan:** none — every code step has complete code; every test step has exact `run_tests`/`get_test_job` invocations and expected results.

**Type consistency:** `RecipeFromInstance` signatures: Object `(GameObject, IColliderStrategy, AssetType)`, Rig `(GameObject, IColliderStrategy, TerminalBoneAxis, bool)` — used identically in Tasks 2, 6, 7. `ReferenceEntityFactory.BuildCenteredQuad()` / `BuildMaterial(Texture2D, bool, ImportRenderProfile)` — defined Task 3, used Tasks 3, 6. `ILabAsset.Recipe` — defined Task 1, used Tasks 4 (registry, spawner). `BuiltinLabAsset.Image`/`Prefab`/`TerminalBonesAxis`/`InvertTerminalBonesAxis` — defined Task 1, used Task 7. `BuiltinRecipeBaker.Entries/BakeAll/BakeOne` — defined Task 7 step 1, used Task 7 step 2. Consistent. ✓
</content>
