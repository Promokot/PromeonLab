# Rig Leaf-Bone Orientation Axis — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

> **GIT RULE (overrides skill defaults):** Project owner commits manually. **NEVER run `git add`/`git commit`.** Each task ends with a **Checkpoint** (compile + tests), not a commit.

> **Unity workflow:** After `.cs` edits → `refresh_unity (mode:force, scope:all, compile:request)` then `read_console (types:[error], filter_text:"CS")`. Only `error CS####` matters; `MCP-FOR-UNITY: Client handler…`/`MissingReferenceException: m_Targets`/`SerializedObjectNotCreatableException` are harmless. Tests: `run_tests (mode:"EditMode", test_names:[…])` + poll `get_test_job (wait_timeout:60)`. **EditMode baseline = 6 known pre-existing failures** (PathProviderTests×4 Windows `\`, RingRotateStrategyTests×2) — flag any NEW failure.

**Goal:** Let each rig choose, per-rig, which local axis its terminal (leaf) bones point along (`Auto`/`X`/`Y`/`Z`), instead of always orienting leaf bones along direction-from-parent.

**Architecture:** A new `TerminalBoneAxis` enum is stored per-rig — in `RigDefinition.TerminalAxis` for imported rigs (chosen in the import wizard, stamped onto the recipe by `ImportPipeline`) and on `BuiltinLabAsset` for builtin rigs (configured in the `BuiltinAssetLibrary` SO). `RigEntityFactory.BuildProxyRig` takes the axis as a parameter; its leaf-bone branch orients the diamond along the chosen positive local axis, or keeps the legacy from-parent direction when `Auto`.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces runtime), VContainer DI, NUnit (`_App.Tests`).

**Spec:** `docs/superpowers/specs/2026-06-01-rig-leaf-bone-axis-design.md`

---

## File Structure

- Create `Assets/_App/Scripts/RigBuilder/TerminalBoneAxis.cs` — the enum (one public type/file).
- Modify `Assets/_App/Scripts/RigBuilder/RigDefinition.cs` — add `TerminalAxis` field.
- Modify `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` — add serialized `_terminalAxis` + getter.
- Modify `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` — `BuildProxyRig`/`BuildProxyNode` axis param + leaf-branch switch.
- Modify `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs` — `RestoreAsync` resolves axis (recipe vs builtin) and passes it.
- Modify `Assets/_App/Scripts/RigBuilder/RigRuntime.cs` — `ApplyDefinition` passes `definition.TerminalAxis`.
- Modify `Assets/_App/Scripts/AssetBrowser/Events/ImportConfirmedEvent.cs` — add `TerminalAxis`.
- Modify `Assets/_App/Scripts/SpatialUi/Behaviors/ImportWizardSurface.cs` — serialize axis toggles, read selected → event.
- Modify `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs` — stamp `recipe.rig.TerminalAxis` from the event.
- Modify `Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs` — 3-arg calls + new orientation tests.
- Prefab wiring: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ImportWizard.prefab` — assign `Toggle X/Y/Z` to the new serialized fields.

---

## Task 1: `TerminalBoneAxis` enum + per-rig data carriers

Pure data. Adds the enum and the two fields that hold the per-rig axis. Nothing reads them yet, so nothing changes behaviorally.

**Files:**
- Create: `Assets/_App/Scripts/RigBuilder/TerminalBoneAxis.cs`
- Modify: `Assets/_App/Scripts/RigBuilder/RigDefinition.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs`

- [ ] **Step 1: Create the enum**

`Assets/_App/Scripts/RigBuilder/TerminalBoneAxis.cs`:

```csharp
// Which LOCAL axis a terminal (leaf) proxy bone points along. Auto = legacy behavior
// (orient along the direction from the parent bone) and the back-compat default for any
// rig without an explicit choice. X/Y/Z are the bone's positive local axes.
public enum TerminalBoneAxis
{
    Auto = 0,
    X    = 1,
    Y    = 2,
    Z    = 3,
}
```

- [ ] **Step 2: Add the field to `RigDefinition`**

In `Assets/_App/Scripts/RigBuilder/RigDefinition.cs`, add the field after `AssetId` (keep the existing `SchemaVersion` — this is an additive field with a sane `Auto=0` default, so no `StorageMigrator` migration is needed; old recipes deserialize to `Auto`):

```csharp
[Serializable]
public class RigDefinition
{
    public int SchemaVersion = 1;
    public string AssetId;
    public TerminalBoneAxis TerminalAxis;   // per-rig leaf-bone orientation; Auto = legacy/default
    public List<BoneRecord> Bones = new();
    public List<IkChainRecord> IkChains = new();
}
```

- [ ] **Step 3: Add the serialized field to `BuiltinLabAsset`**

In `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs`, add the backing field (after `_prefab`) and a getter (after `Prefab`). Meaningful only for Rig-type entries; harmless on others.

```csharp
    [SerializeField] private GameObject _prefab;
    [SerializeField] private TerminalBoneAxis _terminalAxis;   // leaf-bone axis for Rig entries; ignored otherwise
```

```csharp
    public GameObject  Prefab       => _prefab;
    public TerminalBoneAxis TerminalAxis => _terminalAxis;
```

- [ ] **Step 4: Checkpoint**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none. (No tests yet; nothing consumes the fields.)

---

## Task 2: `RigEntityFactory.BuildProxyRig` axis parameter + leaf-branch logic + caller updates

The construction core. `BuildProxyRig`/`BuildProxyNode` gain a `TerminalBoneAxis` parameter; the leaf branch orients the diamond along the chosen positive local axis (or legacy from-parent when `Auto`). All call sites are updated in this same task so the project compiles with final caller logic.

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`
- Modify: `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`
- Test: `Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs`

- [ ] **Step 1: Update the tests (TDD — new signature + orientation behavior)**

Replace `Assets/_App/Tests/RigBuilder/RigEntityFactoryBuildProxyTests.cs` entirely. Existing three tests now pass `TerminalBoneAxis.Auto` (preserving their assertions); two new tests pin the leaf orientation. (In `MakeSkeleton`, the leaf `Bone.001` sits at local +Y of its parent, so `Auto` makes its diamond tip point local +Y, while axis `X` makes it point local +X — distinguishable.)

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class RigEntityFactoryBuildProxyTests
{
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
        MakeFactory().BuildProxyRig(root, null, TerminalBoneAxis.Auto); // null → all smr.bones

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

        MakeFactory().BuildProxyRig(root, null, TerminalBoneAxis.Auto);

        Assert.IsNull(root.GetComponent<ProxyRigRuntime>(), "no skeleton → no proxy rig");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_NamedSubset_BuildsOnlyMatchedBones()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, new List<string> { "Bone" }, TerminalBoneAxis.Auto); // root bone only

        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(true);
        Assert.AreEqual(1, markers.Length, "only the named bone gets a proxy");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_LeafAxisX_OrientsDiamondAlongLocalX()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, null, TerminalBoneAxis.X);

        var tip = LeafDiamondTip(root, "proxy_Bone.001");
        Assert.AreEqual(1f, tip.x, 0.02f, "leaf diamond tip points local +X");
        Assert.AreEqual(0f, tip.y, 0.02f);

        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_LeafAuto_OrientsDiamondFromParent()
    {
        var (root, _) = MakeSkeleton(); // leaf sits at local +Y of its parent
        MakeFactory().BuildProxyRig(root, null, TerminalBoneAxis.Auto);

        var tip = LeafDiamondTip(root, "proxy_Bone.001");
        Assert.AreEqual(1f, tip.y, 0.02f, "Auto keeps the legacy from-parent (+Y here) direction");

        Object.DestroyImmediate(root);
    }

    // The diamond's far tip vertex sits at (localLongAxis * length) in the proxy's local space,
    // so its normalized direction IS the chosen local long-axis.
    private static Vector3 LeafDiamondTip(GameObject root, string proxyName)
    {
        var proxy = FindDeep(root.transform, proxyName);
        Assert.IsNotNull(proxy, $"{proxyName} exists");
        var mesh = proxy.GetComponent<MeshFilter>().sharedMesh;
        return mesh.vertices.OrderByDescending(v => v.magnitude).First().normalized;
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

`run_tests (mode:"EditMode", test_names:["RigEntityFactoryBuildProxyTests"])` → FAIL (`BuildProxyRig` has no 3-arg overload).

- [ ] **Step 3: Add the axis parameter to `BuildProxyRig` and thread it into `BuildProxyNode`**

In `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`, change the `BuildProxyRig` signature and its call into `BuildProxyNode`:

```csharp
    public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis)
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

            BuildProxyNode(bone, proxyRoot, set, proxyGOs, terminalAxis);
        }

        if (proxyRoot == null) return; // skeleton present but no buildable root bone

        var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot, proxyGOs);
    }
```

- [ ] **Step 4: Update `BuildProxyNode` signature + leaf-branch orientation**

In the same file, change `BuildProxyNode`'s signature to accept the axis, replace the leaf (`else`) branch with the axis-aware version, and pass the axis through the recursive call. The non-leaf (`children.Count > 0`) branch is unchanged.

Signature:

```csharp
    private void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set, List<GameObject> proxyGOs, TerminalBoneAxis terminalAxis)
```

Leaf branch — replace the current `else { … }` block (the one computing `worldDir`/`localChildDir` and calling `BuildOrientedDiamondMesh`) with:

```csharp
        else
        {
            // Length always follows the bone's offset from its parent; only the DIRECTION is configurable.
            var worldDir    = bone.position - bone.parent.position;
            float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
            float length    = parentLen * 0.5f;

            Vector3 localLongAxis;
            if (terminalAxis == TerminalBoneAxis.Auto)
            {
                localLongAxis = bone.InverseTransformDirection(worldDir).normalized;
                if (localLongAxis.sqrMagnitude < 0.0001f) localLongAxis = Vector3.up;
            }
            else
            {
                localLongAxis = terminalAxis switch
                {
                    TerminalBoneAxis.X => Vector3.right,
                    TerminalBoneAxis.Y => Vector3.up,
                    TerminalBoneAxis.Z => Vector3.forward,
                    _                  => Vector3.up,
                };
            }

            float width = EffectiveWidth(_config.BoneWidth, length);
            mesh = BuildOrientedDiamondMesh(localLongAxis, length, width);
        }
```

Recursive call at the end of `BuildProxyNode` — add the axis argument:

```csharp
        foreach (var child in children)
            BuildProxyNode(child, proxyGo.transform, set, proxyGOs, terminalAxis);
```

- [ ] **Step 5: Update caller `RigEntityBuilder.RestoreAsync`**

In `Assets/_App/Scripts/AssetBrowser/RigEntityBuilder.cs`, resolve the axis per source (builtin → `BuiltinLabAsset.TerminalAxis`; imported → `recipe.rig.TerminalAxis`) and pass it to `BuildProxyRig`. Replace the `RestoreAsync` body:

```csharp
    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        GameObject       go;
        TerminalBoneAxis axis;
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            go   = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
            axis = b.TerminalAxis;
        }
        else
        {
            if (string.IsNullOrEmpty(asset.SourceRef))
                throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");
            go   = await _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
            axis = recipe != null && recipe.HasRig ? recipe.rig.TerminalAxis : TerminalBoneAxis.Auto;
        }

        if (go == null) return null;

        // Slice B: build the proxy-bone hierarchy after load (both branches). Imported → bone names from
        // the recipe; builtin → null (all live bones). Leaf-bone orientation axis is per-rig (recipe for
        // imports, BuiltinLabAsset for builtin). No-op when there is no skeleton.
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis);

        return go;
    }
```

- [ ] **Step 6: Update caller `RigRuntime.ApplyDefinition`**

In `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`, pass the definition's axis (manual in-VR rigging leaves it `Auto`). Change only the `BuildProxyRig` call:

```csharp
        _factory.BuildProxyRig(rigRoot, boneNames, definition != null ? definition.TerminalAxis : TerminalBoneAxis.Auto);
```

- [ ] **Step 7: Run to verify it passes**

`refresh_unity` → `read_console (error,"CS")` (none) → `run_tests (mode:"EditMode", test_names:["RigEntityFactoryBuildProxyTests"])`. Expected PASS (5/5).

- [ ] **Step 8: Checkpoint** — factory honors the axis; all callers compile and pass it; 5/5 green. Imported rigs still get `Auto` until the wizard path (Task 3) stamps a choice; builtin reads its SO field (default `Auto`).

---

## Task 3: Import wizard → recipe stamping

Carry the wizard's axis choice from the UI to the recipe. The shared `IAssetEntityBuilder.BuildAsync` signature stays unchanged — `ImportPipeline` stamps the choice onto `recipe.rig` after the build.

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/Events/ImportConfirmedEvent.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Behaviors/ImportWizardSurface.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs`

- [ ] **Step 1: Add the axis to `ImportConfirmedEvent`**

Replace `Assets/_App/Scripts/AssetBrowser/Events/ImportConfirmedEvent.cs`:

```csharp
// wizard → pipeline: the user confirmed (Confirmed=false means cancelled).
public struct ImportConfirmedEvent
{
    public bool             Confirmed;
    public string           FilePath;
    public string           DisplayName;
    public AssetType        ChosenType;
    public TerminalBoneAxis TerminalAxis;   // leaf-bone orientation chosen in the wizard (Rig only)
}
```

- [ ] **Step 2: Read the axis toggles in `ImportWizardSurface` and publish it**

In `Assets/_App/Scripts/SpatialUi/Behaviors/ImportWizardSurface.cs`:

Add serialized fields next to the existing type toggles (after `_referenceToggle`):

```csharp
    [Header("Leaf-Bone Axis (Rig)")]
    [SerializeField] private Toggle _axisXToggle;
    [SerializeField] private Toggle _axisYToggle;
    [SerializeField] private Toggle _axisZToggle;
```

Add a resolver method (next to `SelectedType()`):

```csharp
    private TerminalBoneAxis SelectedTerminalAxis()
    {
        if (_axisYToggle != null && _axisYToggle.isOn) return TerminalBoneAxis.Y;
        if (_axisZToggle != null && _axisZToggle.isOn) return TerminalBoneAxis.Z;
        return TerminalBoneAxis.X; // X is the default selection
    }
```

In `OnImport()`, add the field to the published event:

```csharp
        _bus?.Publish(new ImportConfirmedEvent
        {
            Confirmed    = true,
            FilePath     = _filePath,
            DisplayName  = string.IsNullOrWhiteSpace(_nameInput?.text) ? System.IO.Path.GetFileNameWithoutExtension(_filePath) : _nameInput.text,
            ChosenType   = SelectedType(),
            TerminalAxis = SelectedTerminalAxis(),
        });
```

(The axis group stays always visible — it sits under the rig-selection section in the prefab. For Object/Reference imports the published value is simply ignored downstream, since there is no `recipe.rig` to stamp.)

- [ ] **Step 3: Stamp the axis onto the recipe in `ImportPipeline`**

In `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs`, inside `RunImportAsync`, stamp the choice between building the recipe and setting it on the record:

```csharp
            // Build once: bake the entity recipe now so spawn/scene-load can restore deterministically.
            var recipe = await _builders.BuildAsync(record.Type, _store.AbsolutePath(record.SourceRef), CancellationToken.None);

            // Per-rig leaf-bone orientation comes from the wizard. Only rigs have recipe.rig.
            if (recipe.rig != null)
                recipe.rig.TerminalAxis = e.TerminalAxis;

            record.SetRecipe(recipe);
```

- [ ] **Step 4: Checkpoint**

`refresh_unity (force/all/compile)` → `read_console (error,"CS")`. Expected: none. The import path now threads the chosen axis: wizard → event → `recipe.rig.TerminalAxis` → (on spawn) `BuildProxyRig`.

---

## Task 4: Prefab wiring + final verification

Wire the prefab's existing axis toggles to the new serialized fields, then verify the whole feature.

**Files:**
- Prefab: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ImportWizard.prefab` (the `ImportWizardSurface` component + `Toggle X/Y/Z` under "Axis Toggle Group").

- [ ] **Step 1: Assign the toggle references**

On the `ImportWizardSurface` component in `ImportWizard.prefab`, assign the three new fields:
- `_axisXToggle` → the `Toggle X` GameObject's `Toggle` component
- `_axisYToggle` → `Toggle Y`
- `_axisZToggle` → `Toggle Z`

Use the interactive prefab stage via MCP (per the prefab-edit pattern: one `batch_execute` = `open_prefab_stage` + `manage_components`/`manage_gameobject` set_property by path + `save_prefab_stage`), or assign in the Inspector. Verify they resolve (not `None`) by re-reading the component.

Also confirm the "Axis Toggle Group" `ToggleGroup` has one toggle on by default (so `SelectedTerminalAxis()` returns a real axis; `X` is the intended default).

- [ ] **Step 2: Full EditMode run**

`run_tests (mode:"EditMode")`. Expected: only the **6** known pre-existing failures (PathProviderTests×4 + RingRotateStrategyTests×2); `RigEntityFactoryBuildProxyTests` 5/5 green. Flag anything else.

- [ ] **Step 3: VR verification (human-gated)**

- Import a rigged `.glb` as **Rig**, pick axis **X** → spawn, enter bone mode → terminal bones (finger tips / head) point along local +X; re-import with **Y**/**Z** → orientation changes accordingly.
- Import with the default selection → leaf bones use the default axis.
- Spawn builtin **Crush Dummy** (its `BuiltinAssetLibrary` entry left at `Auto`) → leaf bones keep the current from-parent look; set the entry's axis in the SO → look changes on next spawn.
- In-VR manual rig (BoneInspector/IkWizard) → builds with `Auto` (legacy look), no errors.
- Reload scene + restart app → the chosen axis persists for imported rigs (it's in the recipe).

- [ ] **Step 4: Checkpoint** — feature behaviorally verified. Hand to user for commit.

---

## Self-Review

**1. Spec coverage:**
- `TerminalBoneAxis { Auto, X, Y, Z }` — Task 1 ✓
- `RigDefinition.TerminalAxis` (imported, additive, no migration) — Task 1 ✓
- `BuiltinLabAsset._terminalAxis` + getter (SO-configured) — Task 1 ✓
- `BuildProxyRig`/`BuildProxyNode` axis param + leaf-branch positive-local-axis vs Auto-from-parent; length unchanged; non-leaf untouched — Task 2 ✓
- Callers resolve axis: `RestoreAsync` (recipe vs builtin), `RigRuntime.ApplyDefinition` (definition→Auto) — Task 2 ✓
- `ImportConfirmedEvent.TerminalAxis`; wizard reads toggles, always-visible group, default X — Task 3 ✓
- `ImportPipeline` stamps `recipe.rig.TerminalAxis` (BuildAsync signature unchanged) — Task 3 ✓
- Prefab toggle wiring — Task 4 ✓
- Testing: 3-arg update + leaf X / Auto orientation — Task 2 ✓; full EditMode + VR — Task 4 ✓
- No-skeleton no-op (axis irrelevant), old recipes → Auto, non-Rig builtin carries ignored field — covered by `Auto=0` default + `recipe.rig != null` guard ✓

**2. Placeholder scan:** No TBD/vague steps; full code in every code step; exact file paths. ✓

**3. Type consistency:** `TerminalBoneAxis` (Auto/X/Y/Z); `RigDefinition.TerminalAxis`; `BuiltinLabAsset.TerminalAxis`; `ImportConfirmedEvent.TerminalAxis`; `BuildProxyRig(GameObject, IReadOnlyList<string>, TerminalBoneAxis)`; `BuildProxyNode(…, TerminalBoneAxis)`; `SelectedTerminalAxis()`. Consistent across tasks. `RigEntityFactory` ctor `(GltfModelLoader, ProxyRigConfig)` unchanged. ✓
