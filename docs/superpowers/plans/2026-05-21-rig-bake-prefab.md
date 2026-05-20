# Rig Bake Into Prefab — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **No git commits from agents.** This repository's owner manages all git operations manually. Skip every "commit" step a default workflow would suggest. Do not run `git add`, `git commit`, or `git push`. Stop after the implementation+test step of each task and leave the working tree clean of any git activity.

**Goal:** Move all rig-interaction wiring (proxy bones + Selectable + XRPromeonInteractable + SceneNode) from runtime spawn-time into edit-time prefab bake, so library prefabs are self-contained and `AssetSpawner` only Instantiates, registers, and injects DI.

**Architecture:** Builder runs once in Prefab Mode and embeds the full proxy hierarchy (geometry + interaction components + marker) into the prefab. At runtime, `AssetSpawner` does `Instantiate → SceneGraph.AddNode → RewriteBoneNodeIds → IObjectResolver.InjectGameObject`. Factories (`SelectionInteractorFactory`, `BoneInteractableFactory`) and their interfaces are deleted. Bone nodes live in a parallel `SceneGraph._transientNodes` dictionary so the outliner doesn't see them.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer (DI via `[Inject]` + `IObjectResolver.InjectGameObject`), MessagePipe-style `EventBus`, Unity XR Interaction Toolkit.

**Spec:** `docs/superpowers/specs/2026-05-21-rig-bake-prefab-design.md`

---

## Task ordering rationale

Tasks are ordered so the codebase stays compilable after each task except where explicitly called out. Selectable.Init is dropped LAST because BoneInteractableFactory and SelectionInteractorFactory call it; those factories are deleted in Task 10. The plan finishes with manual prefab steps that the user (not an agent) performs in the Unity Editor.

---

## Task 1: Add `BoneSceneNodeMarker` component

**Files:**
- Create: `Assets/_App/Subsystems/RigBuilder/BoneSceneNodeMarker.cs`

Empty marker `MonoBehaviour`. Builder will attach it to every proxy GO at bake time so `AssetSpawner` can find proxies and rewrite their NodeIds.

- [ ] **Step 1: Create the marker class**

`Assets/_App/Subsystems/RigBuilder/BoneSceneNodeMarker.cs`:

```csharp
using UnityEngine;

/// Marker for a proxy bone's SceneNode. AssetSpawner uses this to locate
/// baked bone proxies in a spawned rig and rewrite their NodeId into the
/// runtime "bone:{rigNodeId}:{boneName}" form.
[DisallowMultipleComponent]
public class BoneSceneNodeMarker : MonoBehaviour
{
}
```

- [ ] **Step 2: Verify Unity compiles cleanly**

In Unity Editor, wait for compilation. Open Console (`Window → General → Console`). Expect no compile errors.

---

## Task 2: Convert `SceneNode` to serialized fields + add `SetNodeId`

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/SceneNode.cs`
- Test: `Assets/_App/Subsystems/SceneComposition/Tests/SceneNodeTests.cs` (new)

Current `NodeId`, `DisplayName`, `IsVisible`, `IsLocked` are C# auto-properties — backing fields are private compiler-generated and not serialized. Baked prefabs need `NodeId` to survive save/load, so we convert to `[SerializeField]` backing fields. `AssetRef` is a struct (not a UnityEngine.Object reference) and is also converted.

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Subsystems/SceneComposition/Tests/SceneNodeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class SceneNodeTests
{
    [Test]
    public void Init_StoresValues()
    {
        var go = new GameObject("n");
        var sn = go.AddComponent<SceneNode>();
        sn.Init("id-1", new AssetRef { Source = AssetSource.Builtin, AssetId = "a" }, "Display");

        Assert.AreEqual("id-1",   sn.NodeId);
        Assert.AreEqual("Display", sn.DisplayName);
        Assert.AreEqual("a",      sn.AssetRef.AssetId);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SetNodeId_ChangesNodeIdValue()
    {
        var go = new GameObject("n");
        var sn = go.AddComponent<SceneNode>();
        sn.Init("old", default, "x");

        sn.SetNodeId("new");

        Assert.AreEqual("new", sn.NodeId);

        Object.DestroyImmediate(go);
    }
}
```

Also create the test asmdef if one doesn't already exist for SceneComposition tests. Check `Assets/_App/Subsystems/SceneComposition/Tests/` — if there is already a `Subsystems.SceneComposition.Tests.asmdef`, do nothing here. If not, create it:

`Assets/_App/Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef`:

```json
{
    "name": "Subsystems.SceneComposition.Tests",
    "references": ["Subsystems.SceneComposition", "_Shared"],
    "includePlatforms": ["Editor"],
    "optionalUnityReferences": ["TestAssemblies"],
    "autoReferenced": false
}
```

- [ ] **Step 2: Run the test to verify it fails**

In Unity Editor: `Window → General → Test Runner → EditMode → Run All`.
Expected: `SceneNodeTests.SetNodeId_ChangesNodeIdValue` fails to compile (missing `SetNodeId` method).

- [ ] **Step 3: Rewrite `SceneNode.cs`**

Replace `Assets/_App/Subsystems/SceneComposition/SceneNode.cs` with:

```csharp
using UnityEngine;

public class SceneNode : MonoBehaviour
{
    [SerializeField] private string   _nodeId;
    [SerializeField] private AssetRef _assetRef;
    [SerializeField] private string   _displayName;
    [SerializeField] private bool     _isVisible = true;
    [SerializeField] private bool     _isLocked;

    public string   NodeId      => _nodeId;
    public AssetRef AssetRef    => _assetRef;
    public string   DisplayName => _displayName;
    public bool     IsVisible   => _isVisible;
    public bool     IsLocked    => _isLocked;

    public void Init(string nodeId, AssetRef assetRef, string displayName)
    {
        _nodeId      = nodeId;
        _assetRef    = assetRef;
        _displayName = displayName;
    }

    public void SetNodeId(string newId) => _nodeId = newId;

    public void SetDisplayName(string name)
    {
        _displayName = name;
        gameObject.name = name;
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => _isLocked = locked;
}
```

Note: `AssetRef` must already be a `[Serializable]` struct/class for Unity to serialize the field. If existing `AssetRef` is not `[Serializable]`, add the attribute to it (file `Assets/_App/_Shared/Data/AssetRef.cs`).

- [ ] **Step 4: Run the tests to verify they pass**

Test Runner → EditMode → Run All. Expected: both new `SceneNodeTests` pass.

- [ ] **Step 5: Verify no callers regressed**

In Unity Editor's Console, watch for compile errors after the change. None expected — existing callers use `Init(...)`, `NodeId`, `AssetRef`, `DisplayName`, `SetDisplayName(...)` which all still exist.

---

## Task 3: Add transient-nodes support to `SceneGraph`

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs`
- Test: `Assets/_App/Subsystems/SceneComposition/Tests/SceneGraphTests.cs` (existing — extend)

Adds a parallel dictionary `_transientNodes` for bone SceneNodes that should be findable by `GetNode(nodeId)` but not enumerated by the outliner. Stale entries (destroyed Unity objects) are pruned lazily.

- [ ] **Step 1: Read existing SceneGraph for context**

Open `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs`. The existing public API includes `AddNode(GameObject, AssetRef, string)`, `RemoveNode(string)`, `GetNode(string)`, and `Nodes` property. Keep all of that intact.

- [ ] **Step 2: Write failing tests**

Append to `Assets/_App/Subsystems/SceneComposition/Tests/SceneGraphTests.cs`:

```csharp
[Test]
public void AddTransientNode_DoesNotPublishSceneModified()
{
    var bus = new EventBus();
    var graph = MakeGraph(bus);
    var sn = MakeSceneNode("bone:rig:pelvis");

    int events = 0;
    bus.Subscribe<SceneModifiedEvent>(_ => events++);
    graph.AddTransientNode(sn);

    Assert.AreEqual(0, events, "AddTransientNode must not publish SceneModifiedEvent");
}

[Test]
public void GetNode_FindsTransientNode()
{
    var graph = MakeGraph(new EventBus());
    var sn = MakeSceneNode("bone:rig:pelvis");
    graph.AddTransientNode(sn);

    var found = graph.GetNode("bone:rig:pelvis");

    Assert.IsNotNull(found);
    Assert.AreSame(sn, found);
}

[Test]
public void GetNode_NotInEitherDictionary_ReturnsNull()
{
    var graph = MakeGraph(new EventBus());
    Assert.IsNull(graph.GetNode("missing"));
}

[Test]
public void Nodes_DoesNotIncludeTransientNodes()
{
    var graph = MakeGraph(new EventBus());
    graph.AddTransientNode(MakeSceneNode("bone:rig:pelvis"));

    Assert.AreEqual(0, graph.Nodes.Count,
        "outliner must not see bone proxies via Nodes enumeration");
}

[Test]
public void GetNode_DestroyedTransient_ReturnsNullAndPrunes()
{
    var graph = MakeGraph(new EventBus());
    var sn = MakeSceneNode("bone:rig:pelvis");
    graph.AddTransientNode(sn);

    Object.DestroyImmediate(sn.gameObject);
    var found = graph.GetNode("bone:rig:pelvis");

    Assert.IsNull(found, "destroyed transient must be reported as null");
    // Second call confirms it was removed from the internal dict
    Assert.IsNull(graph.GetNode("bone:rig:pelvis"));
}

// Helpers — add near the bottom of the test class if not already present.
private static SceneNode MakeSceneNode(string nodeId)
{
    var go = new GameObject(nodeId);
    var sn = go.AddComponent<SceneNode>();
    sn.Init(nodeId, default, nodeId);
    return sn;
}

private static SceneGraph MakeGraph(EventBus bus)
{
    // SceneGraph implements IStartable; call Start() before tests if it does setup work.
    var graph = new SceneGraph(bus /*, other ctor params as required */);
    return graph;
}
```

If `SceneGraph`'s constructor differs from `(EventBus)`, adapt `MakeGraph` to match — read `SceneGraph.cs` to confirm signature.

- [ ] **Step 3: Run tests to verify they fail**

Test Runner → EditMode → Run All. Expected: the new tests fail because `AddTransientNode` does not exist.

- [ ] **Step 4: Extend `SceneGraph.cs`**

Add at the top, alongside the existing `_nodes` field:

```csharp
private readonly Dictionary<string, SceneNode> _transientNodes = new();
```

Add the new public method (after the existing `RemoveNode` or anywhere logical):

```csharp
public void AddTransientNode(SceneNode sn)
{
    if (sn == null || string.IsNullOrEmpty(sn.NodeId)) return;
    _transientNodes[sn.NodeId] = sn;
    // Intentionally no SceneModifiedEvent — outliner does not rebuild for bones.
}
```

Replace the existing `GetNode` implementation:

```csharp
public SceneNode GetNode(string nodeId)
{
    if (string.IsNullOrEmpty(nodeId)) return null;
    if (_nodes.TryGetValue(nodeId, out var n) && n != null) return n;
    if (_transientNodes.TryGetValue(nodeId, out var t))
    {
        if (t == null)
        {
            _transientNodes.Remove(nodeId);
            return null;
        }
        return t;
    }
    return null;
}
```

Leave `Nodes` property unchanged (it still returns only `_nodes`).

- [ ] **Step 5: Run tests to verify they pass**

Test Runner → EditMode → Run All. Expected: all SceneGraphTests pass, including the new ones.

---

## Task 4: `XRPromeonInteractable` self-discovers colliders in Awake

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs`

Currently `Awake` only sets `_node = GetComponentInParent<SceneNode>()`. Colliders are registered externally by `BoneInteractableFactory` / `SelectionInteractorFactory`. Add automatic collider discovery so that pre-baked prefabs work without any external registration call.

- [ ] **Step 1: Modify Awake and add `_includeChildColliders` field**

In `XRPromeonInteractable.cs`, find the `_tapWindow` field declaration:

```csharp
[SerializeField] private float _tapWindow = 0.5f;
```

Add directly below:

```csharp
[Tooltip("If true, the interactable auto-registers colliders found in this GO and its children. " +
         "Default false: only colliders on the same GameObject are used (the right choice for " +
         "rig proxies and most prefabs where the collider sits on the root).")]
[SerializeField] private bool _includeChildColliders = false;
```

Then replace the existing `Awake` method:

```csharp
protected override void Awake()
{
    base.Awake();
    _node = GetComponentInParent<SceneNode>();

    if (colliders.Count == 0)
    {
        var found = _includeChildColliders
            ? GetComponentsInChildren<Collider>(includeInactive: true)
            : GetComponents<Collider>();
        foreach (var c in found)
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);
    }
}
```

The `if (colliders.Count == 0)` guard means `RegisterColliders(...)` called before `Awake` still wins. Default path: own GO colliders only.

- [ ] **Step 2: Verify Unity compiles cleanly**

Console: no compile errors. Existing factory callers continue to work because they call `RegisterColliders` (which still functions).

---

## Task 5: `PromeonProxyRigBuilder` bake refactor

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonProxyRigBuilder.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonProxyRigBuilderTests.cs`

The builder now bakes `Selectable`, `XRPromeonInteractable`, `BoneSceneNodeMarker` on every proxy. It exposes a `_rootCollider` SerializeField that gets toggled on/off in `SetBonesInteractive`. It replaces `SetEventBus` with `[Inject] Construct(EventBus)`. `Awake` no longer Rebuilds; `OnEnable` repopulates `_proxyGOs` from a previously baked `ProxyRig` if present.

Bone SceneNode is initialized at bake time with `NodeId = boneName` (just the name, no `bone:` prefix or rig id). Runtime rewrite happens in `AssetSpawner` (Task 7).

- [ ] **Step 1: Write the failing tests**

Append to `Assets/_App/Subsystems/RigBuilder/Tests/PromeonProxyRigBuilderTests.cs`:

```csharp
[Test]
public void BuildProxyHierarchy_AddsBoneSceneNodeMarkerToEachProxy()
{
    var characterGo = MakeGO("Character");
    var armatureGo  = MakeGO("Armature", characterGo.transform);
    var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
    var spineGo     = MakeGO("spine",    pelvisGo.transform);
    spineGo.transform.localPosition = Vector3.up * 0.5f;

    var rig = characterGo.AddComponent<PromeonProxyRigBuilder>();
    rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
    rig.Rebuild();

    var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
    var proxySpine  = proxyPelvis?.Find("proxy_spine");
    Assert.IsNotNull(proxyPelvis.GetComponent<BoneSceneNodeMarker>(),
        "proxy_pelvis missing BoneSceneNodeMarker");
    Assert.IsNotNull(proxySpine.GetComponent<BoneSceneNodeMarker>(),
        "proxy_spine missing BoneSceneNodeMarker");
}

[Test]
public void BuildProxyHierarchy_AddsSelectableAndXRInteractableToEachProxy()
{
    var characterGo = MakeGO("Character");
    var armatureGo  = MakeGO("Armature", characterGo.transform);
    var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
    var spineGo     = MakeGO("spine",    pelvisGo.transform);
    spineGo.transform.localPosition = Vector3.up * 0.5f;

    var rig = characterGo.AddComponent<PromeonProxyRigBuilder>();
    rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
    rig.Rebuild();

    var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis").gameObject;
    Assert.IsNotNull(proxyPelvis.GetComponent<Selectable>(),
        "proxy_pelvis missing Selectable");
    Assert.IsNotNull(proxyPelvis.GetComponent<XRPromeonInteractable>(),
        "proxy_pelvis missing XRPromeonInteractable");
}

[Test]
public void BuildProxyHierarchy_SceneNodeId_IsBoneNameOnly_AtBakeTime()
{
    var characterGo = MakeGO("Character");
    var armatureGo  = MakeGO("Armature", characterGo.transform);
    var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
    var spineGo     = MakeGO("spine",    pelvisGo.transform);
    spineGo.transform.localPosition = Vector3.up * 0.5f;

    var rig = characterGo.AddComponent<PromeonProxyRigBuilder>();
    rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
    rig.Rebuild();

    var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
    var sn = proxyPelvis.GetComponent<SceneNode>();
    Assert.AreEqual("pelvis", sn.NodeId,
        "Bake-time NodeId must be just the bone name; rig prefix is added at spawn.");
}

[Test]
public void SetBonesInteractive_TogglesRootCollider()
{
    var characterGo = MakeGO("Character");
    var armatureGo  = MakeGO("Armature", characterGo.transform);
    var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
    var spineGo     = MakeGO("spine",    pelvisGo.transform);
    spineGo.transform.localPosition = Vector3.up * 0.5f;

    var rootCollider = characterGo.AddComponent<BoxCollider>();
    var rig = characterGo.AddComponent<PromeonProxyRigBuilder>();
    rig.SetRootCollider(rootCollider);
    rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
    rig.Rebuild();

    // After Rebuild, default state: bones off → root collider on
    Assert.IsTrue(rootCollider.enabled, "Root collider must be enabled when bones are hidden");

    rig.SetBonesInteractive(true);
    Assert.IsFalse(rootCollider.enabled, "Root collider must be disabled when bones are shown");

    rig.SetBonesInteractive(false);
    Assert.IsTrue(rootCollider.enabled, "Root collider must re-enable when bones are hidden again");
}
```

Find the existing test `BuildProxyHierarchy_BoneNodeId_FollowsBoneFormat` and the test
`BuildProxyHierarchy_BoneNodeId_NoRigId_UsesDefaultNamespace`. Both currently assert
the OLD format `"bone:rig1:pelvis"` / `"bone:rig:pelvis"`. With the bake-time-only-name
contract, these need to be deleted or updated. Replace them with:

```csharp
[Test]
public void BuildProxyHierarchy_RigNodeIdSetter_DoesNotAffectBakeTimeNodeId()
{
    // Builder no longer composes the bone NodeId at bake time. SetRigNodeId is kept
    // for backwards-compatibility callers (RigRuntime) but does not affect baked output.
    var characterGo = MakeGO("Character");
    var armatureGo  = MakeGO("Armature", characterGo.transform);
    var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
    var spineGo     = MakeGO("spine",    pelvisGo.transform);
    spineGo.transform.localPosition = Vector3.up * 0.5f;

    var rig = characterGo.AddComponent<PromeonProxyRigBuilder>();
    rig.SetRigNodeId("rig1");
    rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
    rig.Rebuild();

    var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
    var sn = proxyPelvis.GetComponent<SceneNode>();
    Assert.AreEqual("pelvis", sn.NodeId,
        "Bake-time NodeId stays as bone name regardless of SetRigNodeId.");
}
```

(Replaces the two earlier tests that asserted the old composed format.)

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → EditMode → Run All. Expected: all four new tests fail (`BoneSceneNodeMarker missing`, `Selectable missing`, `XRPromeonInteractable missing`, `SetRootCollider missing`, `SetBonesInteractive_TogglesRootCollider` fails because rig has no SetRootCollider).

- [ ] **Step 3: Update `PromeonProxyRigBuilder.cs`**

Edits, in order. Start at the top of the file.

**a) Add VContainer using:**

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer;
```

**b) Add `_rootCollider` SerializeField below `_boneOutlineColorSelected`:**

```csharp
[SerializeField] private Color    _boneOutlineColorSelected = new Color(1f, 0.5f, 0f);
[SerializeField] private Collider _rootCollider;
```

**c) Add `SetRootCollider` public method (place near `SetMaterial`):**

```csharp
public void SetRootCollider(Collider rootCollider) => _rootCollider = rootCollider;
```

**d) Replace `Awake` and `SetEventBus` with `[Inject] Construct` + `OnEnable` repopulation:**

Remove the existing `Awake` and `SetEventBus`. Replace with:

```csharp
void Awake()
{
    // No automatic Rebuild — proxies are baked into the prefab.
    // OnEnable handles re-population of _proxyGOs from baked children.
}

void OnEnable()
{
    if (_proxyGOs.Count > 0) return;
    var proxyRoot = transform.Find("ProxyRig");
    if (proxyRoot == null) return;
    _proxyRoot = proxyRoot;
    foreach (var marker in proxyRoot.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true))
        _proxyGOs.Add(marker.gameObject);
}

[Inject]
public void Construct(EventBus bus)
{
    if (_eventBus == bus) return;
    if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    _eventBus = bus;
    if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
}
```

Keep the existing `OnDestroy` (it already unsubscribes from `_eventBus`).

**e) Update `SetBonesInteractive` to toggle `_rootCollider`:**

Replace the existing method body:

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
    if (_rootCollider != null) _rootCollider.enabled = !enabled;
}
```

**f) Update `BuildProxyNode` to bake all components:**

Find the existing `BuildProxyNode` method and locate the section right after `AddCollider(proxyGo, mesh);`. Currently it sets up `SceneNode` then `BoneFollower`. Replace the section between `AddCollider(proxyGo, mesh);` and `_proxyGOs.Add(proxyGo);` with:

```csharp
AddMeshAndOutline(proxyGo, mesh);
AddCollider(proxyGo, mesh);

// SceneNode + bone marker — runtime AssetSpawner rewrites NodeId into "bone:{rigId}:{boneName}".
var sceneNode = proxyGo.AddComponent<SceneNode>();
sceneNode.Init(bone.name, default, bone.name);
proxyGo.AddComponent<BoneSceneNodeMarker>();

// Interaction components (Selectable + XRPromeonInteractable). DI deps are wired by
// IObjectResolver.InjectGameObject at spawn time. Colliders auto-discover in
// XRPromeonInteractable.Awake.
proxyGo.AddComponent<Selectable>();
proxyGo.AddComponent<XRPromeonInteractable>();

_proxyGOs.Add(proxyGo);
```

(Note: `_rigNodeId` and `nsRig` are no longer used inside `BuildProxyNode`. The `_rigNodeId` field and `SetRigNodeId` method stay on the class — RigRuntime still calls `SetRigNodeId` for backwards compatibility, but the builder ignores the value at bake time.)

- [ ] **Step 4: Run tests to verify they pass**

Test Runner → EditMode → Run All. Expected: all new tests pass plus the existing PromeonProxyRigBuilderTests pass.

- [ ] **Step 5: Verify Unity compiles cleanly**

Console: no compile errors. Existing callers of `SetEventBus` (RigRuntime) will fail to compile — that's expected and gets fixed in Task 6.

---

## Task 6: `RigRuntime` drops factory dependency

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs`

RigRuntime no longer needs `IBoneInteractableFactory` (builder bakes interactables) nor `EventBus` propagation via `SetEventBus` (replaced by `[Inject]` on the builder).

- [ ] **Step 1: Rewrite `RigRuntime.cs`**

Replace the entire file with:

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    private IObjectResolver _resolver;

    [Inject]
    public void Construct(IObjectResolver resolver) => _resolver = resolver;

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            def.Bones.Add(new BoneRecord { BoneName = bone.name });
        return def;
    }

    public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
    {
        var boneRenderer = smr.GetComponentInParent<PromeonProxyRigBuilder>();
        if (boneRenderer == null)
            boneRenderer = smr.gameObject.AddComponent<PromeonProxyRigBuilder>();

        if (_boneMaterial != null) boneRenderer.SetMaterial(_boneMaterial);

        var bones = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) bones.Add(t);
        }
        boneRenderer.SetTransforms(bones.ToArray());
        boneRenderer.Rebuild();

        // Rebuild may have created brand-new proxy GameObjects (Selectable + XRPromeonInteractable +
        // BoneSceneNodeMarker + SceneNode are added programmatically by the builder). Their [Inject]
        // methods have not fired yet — wire DI deps now so they are functional immediately.
        if (_resolver != null) _resolver.InjectGameObject(boneRenderer.gameObject);
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
```

- [ ] **Step 2: Verify Unity compiles cleanly**

Console: no compile errors at this point in `RigRuntime`. But `BoneInteractableFactory` may now have a dangling reference issue from removed `using` or interface conformance — that's fine, it gets deleted in Task 10. Until then it should still compile.

---

## Task 7: `AssetSpawner` rewrite

**Files:**
- Modify: `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs`

Spawn flow collapses to: Instantiate → AddNode → RewriteBoneNodeIds → InjectGameObject. No `IInteractableFactory`, no `IRigRuntime`.

- [ ] **Step 1: Confirm `SceneGraph.AddNode` returns the created `SceneNode`**

Open `SceneGraph.cs`. The existing method signature should be either `void AddNode(...)` or `SceneNode AddNode(...)`. The plan assumes a `SceneNode` return. If it currently returns `void`, change it to return the created `SceneNode` (the internal variable already holds it; just `return node;`). This is a non-breaking change — existing callers that ignore the return value continue to work.

Update the file:

```csharp
public SceneNode AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null)
{
    var node = AddNodeInternal(go, GenerateNodeId(assetRef), assetRef, displayName, parentId, isLoad: false);
    _bus.Publish(new SceneModifiedEvent());
    return node;
}
```

(Adjust to match the existing signature and internals; the key change is `return node`.)

- [ ] **Step 2: Replace `AssetSpawner.cs`**

```csharp
using System;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus        _bus;
    private readonly SceneGraph      _graph;
    private readonly IObjectResolver _resolver;

    public AssetSpawner(EventBus bus, SceneGraph graph, IObjectResolver resolver)
    {
        _bus      = bus;
        _graph    = graph;
        _resolver = resolver;
    }

    public void Start()   => _bus.Subscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);
    public void Dispose() => _bus.Unsubscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
        _ = SpawnCoreAsync(e);

    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var go = await e.Asset.SpawnAsync(e.Position, e.Rotation, CancellationToken.None);
            var assetRef = new AssetRef
            {
                Source  = e.Asset is BuiltinLabAsset  ? AssetSource.Builtin
                        : e.Asset is ImportedLabAsset ? AssetSource.Imported
                        : AssetSource.Saved,
                AssetId = e.Asset.Id,
            };
            var rigNode = _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            if (rigNode != null) RewriteBoneNodeIds(go, rigNode.NodeId);
            _resolver.InjectGameObject(go);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }

    private void RewriteBoneNodeIds(GameObject root, string rigNodeId)
    {
        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true);
        foreach (var marker in markers)
        {
            var sn = marker.GetComponent<SceneNode>();
            if (sn == null) continue;
            var boneName = sn.NodeId;          // baked as just the bone name
            sn.SetNodeId($"bone:{rigNodeId}:{boneName}");
            _graph.AddTransientNode(sn);
        }
    }
}
```

- [ ] **Step 3: Verify Unity compiles cleanly**

Console: no compile errors. `IInteractableFactory.MakeInteractable` is no longer called by `AssetSpawner` but the type still exists (deleted in Task 10).

---

## Task 8: `SceneInspectorView` drops `IBoneInteractableFactory`

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs`

Bone transform is now resolved via `_graph.GetNode(boneNodeId)?.transform` instead of the factory's registry.

- [ ] **Step 1: Update `Construct` signature**

In `SceneInspectorView.cs`, replace:

```csharp
private IBoneInteractableFactory _boneFactory;

[Inject]
public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, IBoneInteractableFactory boneFactory)
{
    _bus         = bus;
    _graph       = graph;
    _selection   = selection;
    _boneFactory = boneFactory;
}
```

with:

```csharp
[Inject]
public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
{
    _bus       = bus;
    _graph     = graph;
    _selection = selection;
}
```

(Delete the `_boneFactory` field declaration.)

- [ ] **Step 2: Replace bone transform resolution in `BindBone`**

Find inside `BindBone`:

```csharp
_boneTransform = _boneFactory != null ? _boneFactory.GetBoneTransform(boneNodeId) : null;
```

Replace with:

```csharp
_boneTransform = _graph.GetNode(boneNodeId)?.transform;
```

- [ ] **Step 3: Verify Unity compiles cleanly**

Console: no compile errors. `IBoneInteractableFactory` interface still exists (deleted in Task 10).

---

## Task 9: Drop factory registrations from DI scopes

**Files:**
- Modify: `Assets/_App/Bootstrap/SandboxSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`

Both scenes register `SelectionInteractorFactory` (as `IInteractableFactory`) and `BoneInteractableFactory` (as `IBoneInteractableFactory`). Drop both registrations from both files.

- [ ] **Step 1: Grep both files for the registrations**

Open `Assets/_App/Bootstrap/SandboxSceneScope.cs` and `Assets/_App/Bootstrap/VrEditingSceneScope.cs`. Locate lines that register:

- `SelectionInteractorFactory` (probably `builder.Register<SelectionInteractorFactory>(...).AsImplementedInterfaces()`)
- `BoneInteractableFactory` (`builder.Register<BoneInteractableFactory>(Lifetime.Scoped).AsImplementedInterfaces()`)

- [ ] **Step 2: Delete both registration lines in each file**

Example — find and delete lines like:

```csharp
builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
builder.Register<BoneInteractableFactory>(Lifetime.Scoped).AsImplementedInterfaces();
```

Do this in both `SandboxSceneScope.cs` and `VrEditingSceneScope.cs`.

- [ ] **Step 3: Verify Unity compiles cleanly**

Console: no compile errors. Other registrations still work.

---

## Task 10: Delete factories and their interfaces

**Files:**
- Delete: `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs` (and `.meta`)
- Delete: `Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs` (and `.meta`)
- Delete: `Assets/_App/_Shared/Interfaces/IInteractableFactory.cs` (and `.meta`)
- Delete: `Assets/_App/_Shared/Interfaces/IBoneInteractableFactory.cs` (and `.meta`)

- [ ] **Step 1: Search for remaining references**

In Unity, use Project search or run grep externally:

```
Grep pattern: "IInteractableFactory|IBoneInteractableFactory|SelectionInteractorFactory|BoneInteractableFactory"
Scope: Assets/_App/**/*.cs
```

Expected matches at this point: only the four files about to be deleted. If any other file references these types, that's a regression — go back and fix that file before deleting.

- [ ] **Step 2: Delete the four `.cs` files and their `.meta` files**

From a shell at the repo root:

```bash
rm "Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs" \
   "Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs.meta" \
   "Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs" \
   "Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs.meta" \
   "Assets/_App/_Shared/Interfaces/IInteractableFactory.cs" \
   "Assets/_App/_Shared/Interfaces/IInteractableFactory.cs.meta" \
   "Assets/_App/_Shared/Interfaces/IBoneInteractableFactory.cs" \
   "Assets/_App/_Shared/Interfaces/IBoneInteractableFactory.cs.meta"
```

- [ ] **Step 3: Verify Unity compiles cleanly**

In Unity Editor, wait for compilation. Console: no compile errors. `Selectable.Init(SceneNode)` is now uncalled from anywhere — Task 11 removes it.

---

## Task 11: `Selectable` drops `Init`, auto-discovers SceneNode in Awake

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/Selectable.cs`

- [ ] **Step 1: Rewrite `Selectable.cs`**

```csharp
using UnityEngine;

public class Selectable : MonoBehaviour
{
    private SceneNode _node;
    private Outline   _outline;

    public string    NodeId => _node?.NodeId;
    public SceneNode Node   => _node;

    private void Awake()
    {
        _node = GetComponent<SceneNode>();
    }

    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.InSet:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.55f, 0f);
                _outline.OutlineWidth = 4f;
                break;
            case SelectionVisual.Active:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth = 6f;
                break;
        }
    }

    private void EnsureOutline()
    {
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
    }
}
```

(`Init(SceneNode)` is removed. `Awake` discovers SceneNode on the same GO.)

- [ ] **Step 2: Verify Unity compiles cleanly**

Console: no compile errors. No remaining callers of `Selectable.Init`.

- [ ] **Step 3: Run all tests**

Test Runner → EditMode → Run All. Expected: every test passes.

---

## Task 12: Manual prefab work + smoke test (user)

**Files:** (no code changes — Unity Editor only)

This task is performed by the repo owner in the Unity Editor. It bakes the new components into each library rig prefab. Without this step the runtime spawn produces an empty interactable hierarchy.

- [ ] **Step 1: Backup current state**

If you have uncommitted work elsewhere, commit it first. Subsequent prefab edits are user-driven; the user should commit them after smoke testing.

- [ ] **Step 2: For each rig prefab in `Assets/_App/DemoAssets/` (Crush Dummy + any other rig):**

  1. Open the prefab in **Prefab Mode** (double-click the prefab asset).
  2. On the **root** GameObject, ensure these components exist (add via `Add Component` if missing):
     - `Box Collider` — size it to enclose the body (rough bounds).
     - `Selectable`
     - `XRPromeonInteractable` (leave `_includeChildColliders` at the default `false`)
     - `SceneNode` (leave fields blank; `AssetSpawner.AddNode` populates at spawn)
  3. Find the `PromeonProxyRigBuilder` component on the root (or wherever it currently lives). In the inspector:
     - Drag the new root `Box Collider` into the **`Root Collider`** slot.
     - Click the **Rebuild** button at the bottom of the inspector.
  4. Verify in the prefab hierarchy that `ProxyRig` now exists with `proxy_*` children. Each `proxy_*` should have: `MeshFilter`, `MeshRenderer`, `Outline`, `MeshCollider`, `SceneNode`, `BoneSceneNodeMarker`, `Selectable`, `XRPromeonInteractable`.
  5. Save the prefab (`Ctrl+S`). Exit Prefab Mode.

- [ ] **Step 3: For each non-rig library prefab (bushes, props in `Assets/_App/DemoAssets/`):**

  1. Open the prefab in Prefab Mode.
  2. On the root GameObject, ensure these components exist:
     - One `Collider` (BoxCollider is fine) on the root GO. If the prefab has its existing collider on a child, either:
       - Move the collider to the root, OR
       - On the root's `XRPromeonInteractable`, set `_includeChildColliders = true`.
     - `Selectable`
     - `XRPromeonInteractable`
     - `SceneNode`
  3. Save the prefab.

- [ ] **Step 4: Smoke test in Play mode**

  1. Open scene `VrEditing` (or `Sandbox`) and enter Play.
  2. Spawn Crush Dummy from the asset browser. Confirm:
     - The rig is selectable in the 3D scene (click the body → outliner highlights it).
     - The inspector shows the rig in `Content` state with the Show Bones toggle visible (OFF).
     - Toggling Show Bones ON: proxy bones appear, root BoxCollider deactivates.
     - Clicking a bone proxy: inspector switches to `BoneState` with name/transforms/parent rig info.
     - Toggling Show Bones OFF while a bone is selected: selection jumps back to the rig, bones disappear, root BoxCollider re-activates.
  3. Spawn a non-rig prefab (bush). Confirm:
     - Selectable in 3D scene.
     - Inspector shows it in `Content` state with no Show Bones toggle (no rig builder present).

- [ ] **Step 5: Verify no console errors**

During the smoke test, `Console` should be free of `MissingReferenceException`, `VContainerException`, or `NullReferenceException` from the rig path. Warnings from `AssetSpawner` about missing `IRigRuntime` are gone (that dependency was dropped). Warnings about "Rig capability but no SMR" are also gone (capability is no longer read on spawn).

If any error appears, capture the full stack trace and report it back — fix it before considering this task complete.

---

## Out of Scope

- Import wizard automation (Sub-project B) — will be planned separately, but the building blocks (`RigRuntime.ApplyDefinition`, `PromeonProxyRigBuilder.Rebuild`) remain available for it.
- Bone-level animation track UI — future work.
- Bone hierarchy mutations at runtime on already-spawned rigs — future work.
- Update of `OutlinerItem` icon variants (`_iconObject` / `_iconRig`) — already implemented in a previous iteration.
