# Rig Interaction Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make rig proxy bones selectable in VR (with their own inspector state), togglable from the rig's inspector, and visually highlighted when selected — without polluting the outliner.

**Architecture:** Each `proxy_X` gets a `SceneNode` with NodeId `bone:{rigNodeId}:{boneName}` so XR selection works through the existing pipeline. Bone SceneNodes are not registered in `SceneGraph._nodes`, so the outliner ignores them. A new `BoneInteractableFactory` adds `XRPromeonInteractable + Selectable` to proxies after Rebuild. `SceneInspectorView` gets a 4th state `Bone` and a `Show Bones` toggle in the rig single state.

**Tech Stack:** Unity 6, C#, VContainer, EventBus, QuickOutline.

> **No git commits** — the user manages version control manually. Skip all commit steps.

---

## File Map

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Add `_rigNodeId`, `_eventBus`, color fields, `SetRigNodeId`, `SetEventBus`, `SetBonesInteractive`, `ProxyGOs` getter, SceneNode-add in `BuildProxyNode`, SelectionChangedEvent subscription |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | 4 new tests |
| `Assets/_App/Subsystems/VrInteraction/IBoneInteractableFactory.cs` | **Create** — interface |
| `Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs` | **Create** — impl mirroring `SelectionInteractorFactory` |
| `Assets/_App/Bootstrap/VrEditingSceneScope.cs` | Register `BoneInteractableFactory` |
| `Assets/_App/Bootstrap/SandboxSceneScope.cs` | Register `BoneInteractableFactory` |
| `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` | Inject `IBoneInteractableFactory + EventBus`, wire rigNodeId/bus, call `MakeBoneInteractable` per proxy |
| `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` | Add Bone state + ShowBones toggle handling |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab` | **MANUAL** — add BoneState GameObject + ShowBonesToggle GameObject |

---

## Task 1: NodeId + SceneNode on proxies (TDD)

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`

- [ ] **Step 1: Add 3 failing tests**

Open `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`. Find the closing `}` of the most recent test (`BuildProxyHierarchy_MultipleChildren_BuildsCombinedMesh`). Insert these three tests immediately before the final class-closing `}`:

```csharp
    [Test]
    public void BuildProxyHierarchy_BoneNodeId_FollowsBoneFormat()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetRigNodeId("rig1");
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
        var sn = proxyPelvis.GetComponent<SceneNode>();
        Assert.IsNotNull(sn, "SceneNode missing on proxy_pelvis");
        Assert.AreEqual("bone:rig1:pelvis", sn.NodeId);
    }

    [Test]
    public void BuildProxyHierarchy_BoneNodeId_NoRigId_UsesDefaultNamespace()
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
        var sn = proxyPelvis.GetComponent<SceneNode>();
        Assert.IsNotNull(sn);
        Assert.AreEqual("bone:rig:pelvis", sn.NodeId, "Default namespace must be 'rig'");
    }

    [Test]
    public void BuildProxyHierarchy_AddsSceneNodeToEachProxy()
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
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        Assert.IsNotNull(proxyPelvis.GetComponent<SceneNode>(), "proxy_pelvis missing SceneNode");
        Assert.IsNotNull(proxySpine.GetComponent<SceneNode>(),  "proxy_spine missing SceneNode");
    }
```

- [ ] **Step 2: Run tests — confirm 3 fail**

Test Runner → EditMode → filter `BoneNodeId|AddsSceneNodeToEachProxy`.

Expected: 3 FAIL — methods `SetRigNodeId` not defined, or SceneNode not on proxies.

- [ ] **Step 3: Add `_rigNodeId` field and `SetRigNodeId` method**

In `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`, find the field block (around lines 7–15). Add a new private field after `_transforms`:

```csharp
    private Transform[] _transforms;
    private string      _rigNodeId;
```

Find the existing setter group near top of class (after `SetMaterial`). Add a new setter:

```csharp
    public void SetTransforms(Transform[] transforms) => _transforms   = transforms;
    public void SetMaterial(Material material)        => _boneMaterial = material;
    public void SetRigNodeId(string rigNodeId)        => _rigNodeId    = rigNodeId;
```

- [ ] **Step 4: Add SceneNode component to each proxy in `BuildProxyNode`**

Find `BuildProxyNode` in `PromeonInteractableRigBuilder.cs`. Locate the block where the proxy GO is created (the lines below `AddCollider(proxyGo, mesh);` and before `_proxyGOs.Add(proxyGo);`):

```csharp
        AddMeshAndOutline(proxyGo, mesh);
        AddCollider(proxyGo, mesh);
        _proxyGOs.Add(proxyGo);
```

Replace that exact block with:

```csharp
        AddMeshAndOutline(proxyGo, mesh);
        AddCollider(proxyGo, mesh);

        var nsRig = string.IsNullOrEmpty(_rigNodeId) ? "rig" : _rigNodeId;
        var sceneNode = proxyGo.AddComponent<SceneNode>();
        sceneNode.Init($"bone:{nsRig}:{bone.name}", default, bone.name);

        _proxyGOs.Add(proxyGo);
```

`default` here is `default(AssetRef)` — bones don't have an asset reference. `SceneNode.Init` only stores fields; it doesn't validate AssetRef.

- [ ] **Step 5: Wait for Unity recompile, then run the 3 new tests**

Switch to Unity, wait for recompile, check Console (zero errors).

Test Runner → EditMode → filter `BoneNodeId|AddsSceneNodeToEachProxy`.

Expected: 3 PASS.

- [ ] **Step 6: Run ALL EditMode tests — confirm no regressions**

Test Runner → EditMode → Run All.

Expected: previous test count + 3 new = all PASS. None of the existing proxy hierarchy tests reference SceneNode, so adding the component does not break them.

---

## Task 2: `SetBonesInteractive` + default OFF (TDD)

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`

- [ ] **Step 1: Add the failing test**

In `PromeonInteractableRigBuilderTests.cs`, append before the final class-closing `}` (after the tests added in Task 1):

```csharp
    [Test]
    public void SetBonesInteractive_TogglesRendererOutlineCollider()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis").gameObject;
        var mr  = proxyPelvis.GetComponent<MeshRenderer>();
        var ol  = proxyPelvis.GetComponent<Outline>();
        var col = proxyPelvis.GetComponent<MeshCollider>();

        // Default after Rebuild: bones disabled
        Assert.IsFalse(mr.enabled,  "MeshRenderer must default to disabled after Rebuild");
        Assert.IsFalse(ol.enabled,  "Outline must default to disabled after Rebuild");
        Assert.IsFalse(col.enabled, "MeshCollider must default to disabled after Rebuild");

        rig.SetBonesInteractive(true);
        Assert.IsTrue(mr.enabled);
        Assert.IsTrue(ol.enabled);
        Assert.IsTrue(col.enabled);

        rig.SetBonesInteractive(false);
        Assert.IsFalse(mr.enabled);
        Assert.IsFalse(ol.enabled);
        Assert.IsFalse(col.enabled);
    }
```

- [ ] **Step 2: Run test — confirm it fails**

Test Runner → EditMode → filter `SetBonesInteractive`.

Expected: FAIL — method `SetBonesInteractive` not defined (and proxies are enabled by default).

- [ ] **Step 3: Add `SetBonesInteractive` method**

In `PromeonInteractableRigBuilder.cs`, find the existing `SetVisualsEnabled(bool enabled)` method. Immediately after its closing `}`, add:

```csharp
    public void SetBonesInteractive(bool enabled)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var mr      = go.GetComponent<MeshRenderer>();
            if (mr      != null) mr.enabled      = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
            var col     = go.GetComponent<Collider>();
            if (col     != null) col.enabled     = enabled;
        }
    }
```

- [ ] **Step 4: Make `Rebuild` default proxies to disabled**

Find `Rebuild()` in `PromeonInteractableRigBuilder.cs`. Locate its body:

```csharp
    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
        BuildProxyHierarchy(transforms);
    }
```

Add a single call at the end:

```csharp
    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
        BuildProxyHierarchy(transforms);
        SetBonesInteractive(false);
    }
```

- [ ] **Step 5: Wait for recompile, run the new test**

Test Runner → EditMode → filter `SetBonesInteractive`.

Expected: PASS.

- [ ] **Step 6: Run ALL EditMode tests — confirm no regressions**

Some existing tests (e.g., `BuildProxyHierarchy_TwoBones_CreatesTwoProxies`) only check that proxies EXIST in the hierarchy. None assert the renderer is enabled. Default-disabled should not break them.

If `BuildProxyHierarchy_LeafBone_SizedSmallerThanParent` fails (it inspects `MeshFilter.sharedMesh.bounds`), check that the mesh assignment still happens — disabling the renderer does not null the mesh.

Expected: ALL PASS.

---

## Task 3: Color highlight + EventBus subscription

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

No unit tests for this task — EventBus subscription is integration-level. Manual VR test covers it in Task 7.

- [ ] **Step 1: Add color fields and `SetEventBus` method**

In `PromeonInteractableRigBuilder.cs`, find the field block. After the existing `[SerializeField] private bool _useConvexCollider` line, add two color fields:

```csharp
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth                = 0.06f;
    [SerializeField] private bool     _useConvexCollider        = true;
    [SerializeField] private Color    _boneOutlineColorDefault  = Color.white;
    [SerializeField] private Color    _boneOutlineColorSelected = new Color(1f, 0.5f, 0f);
```

Then find the private fields block (around `_transforms`, `_rigNodeId`). Add an EventBus field:

```csharp
    private Transform[] _transforms;
    private string      _rigNodeId;
    private EventBus    _eventBus;
```

Add a setter near `SetRigNodeId`:

```csharp
    public void SetRigNodeId(string rigNodeId) => _rigNodeId = rigNodeId;

    public void SetEventBus(EventBus bus)
    {
        if (_eventBus == bus) return;
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = bus;
        if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    }
```

- [ ] **Step 2: Add the SelectionChangedEvent handler**

Below the `SetBonesInteractive` method (added in Task 2), add:

```csharp
    private void OnSelectionChanged(SelectionChangedEvent evt)
    {
        ApplyBoneOutlineColors(evt.SelectedNodeIds);
    }

    private void ApplyBoneOutlineColors(string[] selectedIds)
    {
        var selected = selectedIds != null
            ? new System.Collections.Generic.HashSet<string>(selectedIds)
            : null;
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var sn      = go.GetComponent<SceneNode>();
            var outline = go.GetComponent<Outline>();
            if (sn == null || outline == null) continue;
            outline.OutlineColor = selected != null && selected.Contains(sn.NodeId)
                ? _boneOutlineColorSelected
                : _boneOutlineColorDefault;
        }
    }
```

- [ ] **Step 3: Unsubscribe in `OnDestroy`**

Find the existing `OnDestroy` (line `void OnDestroy() => DestroyBoneGOs();`). Replace with:

```csharp
    void OnDestroy()
    {
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = null;
        DestroyBoneGOs();
    }
```

- [ ] **Step 4: Apply default outline color when building proxies**

The current `AddMeshAndOutline` sets `outline.OutlineColor = Color.white`. Change it to use the configurable default field. Find:

```csharp
        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 3f;
```

Replace with:

```csharp
        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = _boneOutlineColorDefault;
        outline.OutlineWidth = 3f;
```

- [ ] **Step 5: Wait for recompile, run ALL EditMode tests**

Test Runner → EditMode → Run All.

Expected: ALL PASS. Existing `SetBonesInteractive_*` and `BoneNodeId_*` tests do not depend on color or EventBus.

If `BuildProxyHierarchy_*` tests now fail because of `EventBus` type resolution — confirm that the test assembly references the assembly containing `EventBus` and `SelectionChangedEvent` (it should, since other tests already use SelectionChangedEvent-related setup elsewhere).

---

## Task 4: `ProxyGOs` getter + `BoneInteractableFactory`

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/IBoneInteractableFactory.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs`
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/SandboxSceneScope.cs`

- [ ] **Step 1: Expose `ProxyGOs` getter on `PromeonInteractableRigBuilder`**

In `PromeonInteractableRigBuilder.cs`, find `private readonly List<GameObject> _proxyGOs = new();` Add a public getter just below the field block (around the public setters):

```csharp
    public System.Collections.Generic.IReadOnlyList<GameObject> ProxyGOs => _proxyGOs;
```

- [ ] **Step 2: Create `IBoneInteractableFactory.cs`**

Create new file `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\VrInteraction\IBoneInteractableFactory.cs` with:

```csharp
using UnityEngine;

public interface IBoneInteractableFactory
{
    void MakeBoneInteractable(GameObject proxyGo);
}
```

- [ ] **Step 3: Create `BoneInteractableFactory.cs`**

Create new file `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\VrInteraction\BoneInteractableFactory.cs` with:

```csharp
using UnityEngine;
using VContainer;

public class BoneInteractableFactory : IBoneInteractableFactory
{
    private readonly ISelectionManager _selectionManager;
    private readonly IObjectResolver   _resolver;
    private GizmoController            _gizmoCached;

    public BoneInteractableFactory(ISelectionManager selectionManager, IObjectResolver resolver)
    {
        _selectionManager = selectionManager;
        _resolver         = resolver;
    }

    public void MakeBoneInteractable(GameObject proxyGo)
    {
        if (proxyGo == null) return;

        var sn = proxyGo.GetComponent<SceneNode>();
        var existing = proxyGo.GetComponentsInChildren<Collider>(includeInactive: true);

        var sel = proxyGo.GetComponent<Selectable>() ?? proxyGo.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        _gizmoCached ??= _resolver.Resolve<GizmoController>();

        var xri = proxyGo.GetComponent<XRPromeonInteractable>() ?? proxyGo.AddComponent<XRPromeonInteractable>();
        xri.RegisterColliders(existing);
        xri.Construct(_selectionManager, _gizmoCached);
    }
}
```

- [ ] **Step 4: Register the factory in `VrEditingSceneScope`**

Open `Assets/_App/Bootstrap/VrEditingSceneScope.cs`. Find the existing line that registers `SelectionManager` (it looks like `builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();`). Immediately after that line, add:

```csharp
        builder.Register<BoneInteractableFactory>(Lifetime.Scoped).AsImplementedInterfaces();
```

- [ ] **Step 5: Register the factory in `SandboxSceneScope`**

Open `Assets/_App/Bootstrap/SandboxSceneScope.cs`. Find the same `SelectionManager` registration line. Immediately after it, add the same line:

```csharp
        builder.Register<BoneInteractableFactory>(Lifetime.Scoped).AsImplementedInterfaces();
```

- [ ] **Step 6: Wait for recompile**

Switch to Unity. Wait for recompile. Check Console — zero errors.

If you see `error CS0246: 'XRPromeonInteractable' could not be found` in `BoneInteractableFactory.cs` — confirm the new file's location is `Assets/_App/Subsystems/VrInteraction/` (same folder as `SelectionInteractorFactory.cs`). If still failing, the VrInteraction subsystem may have its own .asmdef — the new files should land inside that asmdef automatically because they're in the same folder.

- [ ] **Step 7: Run ALL EditMode tests**

Test Runner → EditMode → Run All. Expected: PASS. The new factory is not wired into any test path; it's a no-op as far as existing tests are concerned.

---

## Task 5: `RigRuntime` wiring

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs`

- [ ] **Step 1: Replace the entire content of `RigRuntime.cs`**

Path: `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\RigBuilder\RigRuntime.cs`

Replace the whole file with:

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    private IBoneInteractableFactory _boneInteractableFactory;
    private EventBus                 _eventBus;

    [Inject]
    public void Construct(IBoneInteractableFactory boneInteractableFactory, EventBus bus)
    {
        _boneInteractableFactory = boneInteractableFactory;
        _eventBus                = bus;
    }

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

        var rigNode   = boneRenderer.GetComponentInParent<SceneNode>();
        var rigNodeId = rigNode != null ? rigNode.NodeId : null;
        boneRenderer.SetRigNodeId(rigNodeId);
        boneRenderer.SetEventBus(_eventBus);

        var bones = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) bones.Add(t);
        }
        boneRenderer.SetTransforms(bones.ToArray());
        boneRenderer.Rebuild();

        if (_boneInteractableFactory != null)
        {
            foreach (var proxyGo in boneRenderer.ProxyGOs)
                _boneInteractableFactory.MakeBoneInteractable(proxyGo);
        }
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
```

Differences from the previous version:
- `Construct` now takes `IBoneInteractableFactory` and `EventBus`
- `ApplyDefinition` finds the rig's `SceneNode` (walking up from the rig renderer), passes `rigNodeId` to the builder
- Passes `EventBus` to the builder so it can subscribe to `SelectionChangedEvent`
- After `Rebuild`, walks `ProxyGOs` and calls `MakeBoneInteractable` on each so VR selection wiring is in place

- [ ] **Step 2: Wait for recompile, check Console**

Switch to Unity. Wait for recompile. Check Console — zero errors.

- [ ] **Step 3: Run ALL EditMode tests**

Test Runner → EditMode → Run All. Expected: PASS.

---

## Task 6: `SceneInspectorView` — Bone state + ShowBones toggle

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs`

This task wires up the C# side. The actual prefab GameObjects for the Bone state and toggle are added in Task 7 (manual).

- [ ] **Step 1: Add serialized fields for the new UI elements**

Open `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs`. Find the `[SerializeField]` block at the top. After `_deleteButton`, add:

```csharp
    [SerializeField] private Button         _deleteButton;
    [SerializeField] private GameObject     _boneState;
    [SerializeField] private TMP_Text       _boneNameLabel;
    [SerializeField] private TMP_Text       _boneParentRigLabel;
    [SerializeField] private Toggle         _showBonesToggle;
```

At the top of the file, ensure `using UnityEngine.UI;` is present (it already is for `Button`).

- [ ] **Step 2: Extend the `InspectorState` enum**

Find:

```csharp
    private enum InspectorState { Empty, Single, Multi }
```

Replace with:

```csharp
    private enum InspectorState { Empty, Single, Multi, Bone }
```

- [ ] **Step 3: Rewrite `Refresh()` for 4 states + Show Bones logic**

Find the existing `Refresh()` method. Replace its entire body with:

```csharp
    private void Refresh()
    {
        if (_selection == null || _graph == null) return;

        var count    = _selection.SelectedIds?.Count ?? 0;
        var activeId = _selection.ActiveId;
        var state    = count == 0                                ? InspectorState.Empty
                     : count > 1                                 ? InspectorState.Multi
                     : (activeId != null && activeId.StartsWith("bone:")) ? InspectorState.Bone
                     :                                             InspectorState.Single;

        if (_emptyState != null) _emptyState.SetActive(state == InspectorState.Empty);
        if (_content    != null) _content   .SetActive(state == InspectorState.Single);
        if (_multiState != null) _multiState.SetActive(state == InspectorState.Multi);
        if (_boneState  != null) _boneState .SetActive(state == InspectorState.Bone);

        if (state == InspectorState.Multi && _multiCountLabel != null)
            _multiCountLabel.text = $"Multiple Objects Selected ({count})";

        if (state == InspectorState.Bone)
        {
            RefreshBoneState(activeId);
            _bound = null;
            return;
        }

        if (state != InspectorState.Single)
        {
            _bound = null;
            if (_showBonesToggle != null) _showBonesToggle.gameObject.SetActive(false);
            return;
        }

        _bound = _graph.GetNode(_selection.ActiveId);
        if (_bound == null) return;

        if (_nameField != null) _nameField.SetTextWithoutNotify(_bound.DisplayName);
        if (_typeLabel != null) _typeLabel.text = $"Type: {_bound.AssetRef}";

        var pos   = _bound.transform.position;
        var rot   = _bound.transform.rotation.eulerAngles;
        var scale = _bound.transform.localScale;

        if (_posX != null) _posX.text = pos.x.ToString("F2");
        if (_posY != null) _posY.text = pos.y.ToString("F2");
        if (_posZ != null) _posZ.text = pos.z.ToString("F2");

        if (_rotX != null) _rotX.text = rot.x.ToString("F1");
        if (_rotY != null) _rotY.text = rot.y.ToString("F1");
        if (_rotZ != null) _rotZ.text = rot.z.ToString("F1");

        if (_scaleX != null) _scaleX.text = scale.x.ToString("F2");
        if (_scaleY != null) _scaleY.text = scale.y.ToString("F2");
        if (_scaleZ != null) _scaleZ.text = scale.z.ToString("F2");

        // Show Bones toggle is visible only when the selected node carries a rig builder.
        if (_showBonesToggle != null)
        {
            var rig = _bound.GetComponentInChildren<PromeonInteractableRigBuilder>(true);
            _showBonesToggle.gameObject.SetActive(rig != null);
            _showBonesToggle.SetIsOnWithoutNotify(rig != null && AreBonesInteractive(rig));
        }
    }

    private void RefreshBoneState(string boneNodeId)
    {
        // NodeId format: "bone:{rigNodeId}:{boneName}"
        var parts = boneNodeId.Split(':');
        var boneName  = parts.Length >= 3 ? parts[2] : boneNodeId;
        var rigNodeId = parts.Length >= 2 ? parts[1] : "";

        if (_boneNameLabel != null)
            _boneNameLabel.text = $"Bone: {boneName}";

        if (_boneParentRigLabel != null)
        {
            var rigNode = _graph.GetNode(rigNodeId);
            _boneParentRigLabel.text = rigNode != null ? $"Rig: {rigNode.DisplayName}" : $"Rig: {rigNodeId}";
        }

        if (_showBonesToggle != null) _showBonesToggle.gameObject.SetActive(false);
    }

    private static bool AreBonesInteractive(PromeonInteractableRigBuilder rig)
    {
        foreach (var go in rig.ProxyGOs)
        {
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            return mr != null && mr.enabled;
        }
        return false;
    }
```

- [ ] **Step 4: Wire the Show Bones toggle in `OnEnable`/`OnDisable`**

Find the `OnEnable` method. Inside, after the existing button/field listeners, add toggle subscription. After the line `if (_deleteButton != null) _deleteButton.onClick.AddListener(OnDeleteClicked);`, add:

```csharp
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.AddListener(OnShowBonesToggleChanged);
```

In `OnDisable`, mirror the unsubscribe after `_deleteButton.onClick.RemoveListener(OnDeleteClicked);`:

```csharp
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.RemoveListener(OnShowBonesToggleChanged);
```

Then add the new handler at the end of the class (after `OnDeleteClicked`):

```csharp
    private void OnShowBonesToggleChanged(bool value)
    {
        if (_bound == null) return;
        var rig = _bound.GetComponentInChildren<PromeonInteractableRigBuilder>(true);
        if (rig == null) return;
        rig.SetBonesInteractive(value);
    }
```

- [ ] **Step 5: Wait for recompile, check Console**

Switch to Unity. Wait for recompile. Zero errors expected.

If `error CS0246: 'PromeonInteractableRigBuilder' could not be found` — confirm the SpatialUi asmdef references the RigBuilder asmdef. If it doesn't, that's a project-level fix outside this plan's scope; in practice the `BoneInspectorPanel.cs` already uses `IRigRuntime` so cross-references exist. If the compile still fails, you may need to expose the rig-detection through an interface instead — flag for follow-up.

- [ ] **Step 6: Run ALL EditMode tests**

Test Runner → EditMode → Run All. Expected: PASS. No tests touch `SceneInspectorView` directly.

---

## Task 7: Manual prefab updates + smoke test

This task cannot be done from code — requires Unity Editor.

**Files (Editor UI):**
- `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab`

- [ ] **Step 1: Open the prefab in Prefab Mode**

In the Project window, navigate to `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab`. Double-click to enter Prefab Mode.

- [ ] **Step 2: Add the BoneState GameObject**

In the prefab hierarchy, find the existing `_emptyState`, `_content`, and `_multiState` GameObjects (children of the inspector root that the `SceneInspectorView` references).

Duplicate `_emptyState` (or create a new GameObject under the same parent). Name it `BoneState`. Replace its content with two child UI Text (TMP) elements:
- `BoneNameLabel` — TextMeshProUGUI showing "Bone: <name>"
- `BoneParentRigLabel` — TextMeshProUGUI showing "Rig: <rigName>"

Make `BoneState` inactive (the script will activate it when needed).

- [ ] **Step 3: Add the ShowBones toggle**

Inside `_content` (the Single-state container), add a child UI Toggle. Name it `ShowBonesToggle`. Position it visibly within the inspector layout (above or below the existing transform fields — wherever fits).

Default `IsOn` = false. The script will set its state when refreshing.

- [ ] **Step 4: Wire references in `SceneInspectorView` inspector**

Select the GameObject that has the `SceneInspectorView` component. In the Inspector, find the new fields:
- `_boneState` → drag the new `BoneState` GameObject
- `_boneNameLabel` → drag the `BoneNameLabel` (TMP_Text)
- `_boneParentRigLabel` → drag the `BoneParentRigLabel` (TMP_Text)
- `_showBonesToggle` → drag the new `ShowBonesToggle` (Toggle)

Save the prefab (`Ctrl+S`).

- [ ] **Step 5: Smoke test in Play mode**

1. Press Play in the Editor.
2. Open a scene with a skinned-mesh rig and a `PromeonInteractableRigBuilder` (or use `BoneInspectorPanel`'s "Build Rig" flow to attach one).
3. Select the rig in the outliner. The inspector should:
   - Show the Single state (transform fields)
   - Show the `Show Bones` toggle (because the selected node has a rig builder)
   - Toggle is OFF, no diamonds visible
4. Toggle `Show Bones` ON. Diamonds should appear at each bone, with the default white Outline.
5. Tap one of the diamonds in VR (or click in the Scene view if using the editor input bridge). The inspector should:
   - Switch to the Bone state
   - Show "Bone: <boneName>" and "Rig: <rigName>"
   - Show Bones toggle is hidden in this state
   - The selected diamond's Outline changes to the selected color (orange by default)
6. Tap a different bone. Selected outline moves to the new bone, previous one goes back to default.
7. Re-select the rig (e.g., tap the body mesh or use the outliner). Inspector returns to Single + Show Bones toggle visible + ON.
8. Toggle OFF. Diamonds disappear, no longer grabbable.

- [ ] **Step 6: Verify outliner is unchanged**

Throughout the test, the outliner panel should never list bone proxies — only top-level scene nodes (the rig itself, other assets). Bones are absent from the outliner by design.

---

## Done

After Task 7 Step 6 passes:
- Bones get a `SceneNode` with deterministic NodeId `bone:{rigNodeId}:{boneName}` and a default-disabled visual state
- `BoneInteractableFactory` wires `XRPromeonInteractable + Selectable` to each proxy after `RigRuntime` rebuilds
- `RigRuntime` connects the rig's `SceneNode` ID + `EventBus` to the builder so selection highlighting and bone-ID namespacing work
- `SceneInspectorView` shows a 4th state for bone selection and a Show Bones toggle when a rig is selected
- Tapping a bone in VR selects it, opens the bone state, and recolors its Outline
- Outliner never lists bones — they're invisible to the scene graph
