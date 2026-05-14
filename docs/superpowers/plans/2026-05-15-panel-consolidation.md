# Panel Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `BoneInspectorPanel`, `IkSetupWizard`, and `PropertyPanel` into `SpatialUi/UI/`; add minimal interface contracts so `SpatialUi.asmdef` depends only on `_Shared`.

**Architecture:** Four new interfaces (`ISelectionManager`, `ISceneGraph`, `IRigRuntime`) and three data models (`RigDefinition`, `BoneRecord`, `IkChainRecord`) go into `_Shared`. Concrete classes in SceneComposition and RigBuilder implement those interfaces. Panel files move to `SpatialUi/UI/` and inject interfaces instead of concrete types. Old files replaced with redirect comments (user deletes in Unity Editor).

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer 2.x, Unity Animation Rigging 1.4.x, TMP

---

## File Map

**Create (new):**
- `Assets/_App/_Shared/Interfaces/ISelectionManager.cs`
- `Assets/_App/_Shared/Interfaces/ISceneGraph.cs`
- `Assets/_App/_Shared/Interfaces/IRigRuntime.cs`
- `Assets/_App/_Shared/Models/RigDefinition.cs`
- `Assets/_App/_Shared/Models/BoneRecord.cs`
- `Assets/_App/_Shared/Models/IkChainRecord.cs`
- `Assets/_App/Subsystems/SpatialUi/UI/BoneInspectorPanel.cs`
- `Assets/_App/Subsystems/SpatialUi/UI/IkSetupWizard.cs`
- `Assets/_App/Subsystems/SpatialUi/UI/PropertyPanel.cs`

**Modify:**
- `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs` — add `: ISelectionManager`
- `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs` — add `: ISceneGraph`, add explicit `ISceneGraph.GetNode` returning `GameObject`
- `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` — add `: IRigRuntime`, change injected `SelectionManager` → `ISelectionManager`
- `Assets/_App/Subsystems/RigBuilder/BoneProxy.cs` — change injected `SelectionManager` → `ISelectionManager`
- `Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef` — remove `Subsystems.SceneComposition`, `Subsystems.SpatialUi`, `Unity.TextMeshPro`
- `Assets/_App/Bootstrap/VrEditingSceneScope.cs` — add Phase 6 registrations

**Replace with redirect comments (then delete in Unity Editor):**
- `Assets/_App/Subsystems/RigBuilder/Data/RigDefinition.cs`
- `Assets/_App/Subsystems/RigBuilder/Data/BoneRecord.cs`
- `Assets/_App/Subsystems/RigBuilder/Data/IkChainRecord.cs`
- `Assets/_App/Subsystems/RigBuilder/UI/BoneInspectorPanel.cs`
- `Assets/_App/Subsystems/RigBuilder/UI/IkSetupWizard.cs`
- `Assets/_App/Subsystems/SceneComposition/UI/PropertyPanel.cs`

---

## Task 1: Add interfaces to `_Shared/Interfaces/`

**Files:**
- Create: `Assets/_App/_Shared/Interfaces/ISelectionManager.cs`
- Create: `Assets/_App/_Shared/Interfaces/ISceneGraph.cs`
- Create: `Assets/_App/_Shared/Interfaces/IRigRuntime.cs`

- [ ] **Step 1: Create `ISelectionManager.cs`**

```csharp
public interface ISelectionManager
{
    string SelectedNodeId { get; }
    void Select(string nodeId);
}
```

- [ ] **Step 2: Create `ISceneGraph.cs`**

```csharp
using UnityEngine;

public interface ISceneGraph
{
    GameObject GetNode(string nodeId);
    void AddNode(GameObject go);
    void RemoveNode(string nodeId);
}
```

Note: `GetNode` returns `GameObject` (not `SceneNode`) so SpatialUi has no dependency on SceneComposition types. Callers do `go.transform` or `go.GetComponentInChildren<T>()`.

- [ ] **Step 3: Create `IRigRuntime.cs`**

```csharp
using UnityEngine;

public interface IRigRuntime
{
    RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr);
    void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr);
}
```

`RigDefinition` will be in `_Shared/Models/` after Task 2 — `_Shared.asmdef` has `autoReferenced: true` so this compiles once both files exist.

---

## Task 2: Move data models to `_Shared/Models/`

**Files:**
- Create: `Assets/_App/_Shared/Models/RigDefinition.cs`
- Create: `Assets/_App/_Shared/Models/BoneRecord.cs`
- Create: `Assets/_App/_Shared/Models/IkChainRecord.cs`
- Overwrite: `RigBuilder/Data/RigDefinition.cs` → redirect comment
- Overwrite: `RigBuilder/Data/BoneRecord.cs` → redirect comment
- Overwrite: `RigBuilder/Data/IkChainRecord.cs` → redirect comment

- [ ] **Step 1: Create `_Shared/Models/BoneRecord.cs`**

```csharp
using System;

[Serializable]
public class BoneRecord
{
    public string BoneName;
    public bool TranslationLocked = true;
}
```

- [ ] **Step 2: Create `_Shared/Models/IkChainRecord.cs`**

```csharp
using System;
using UnityEngine;

[Serializable]
public class IkChainRecord
{
    public string RootBone;
    public string EndBone;
    public string PoleBone;
    [Range(0f, 1f)]
    public float Weight = 1f;
}
```

- [ ] **Step 3: Create `_Shared/Models/RigDefinition.cs`**

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class RigDefinition
{
    public int SchemaVersion = 1;
    public string AssetId;
    public List<BoneRecord> Bones = new();
    public List<IkChainRecord> IkChains = new();
}
```

- [ ] **Step 4: Replace `RigBuilder/Data/RigDefinition.cs` with redirect comment**

```csharp
// Moved to Assets/_App/_Shared/Models/RigDefinition.cs
```

- [ ] **Step 5: Replace `RigBuilder/Data/BoneRecord.cs` with redirect comment**

```csharp
// Moved to Assets/_App/_Shared/Models/BoneRecord.cs
```

- [ ] **Step 6: Replace `RigBuilder/Data/IkChainRecord.cs` with redirect comment**

```csharp
// Moved to Assets/_App/_Shared/Models/IkChainRecord.cs
```

---

## Task 3: Implement `ISelectionManager` on `SelectionManager`

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs`

`SelectionManager` already has `SelectedNodeId { get; }` and `Select(string)`. Add `: ISelectionManager` to the class declaration. VContainer already registers it with `.AsImplementedInterfaces().AsSelf()` in `VrEditingSceneScope` — no scope changes needed for this interface.

- [ ] **Step 1: Update `SelectionManager.cs`**

Full file after change:

```csharp
using System;
using VContainer.Unity;

public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus _bus;
    private string _selectedNodeId;

    public string SelectedNodeId => _selectedNodeId;

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()  { }
    public void Dispose() { }

    public void Select(string nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = nodeId });
    }

    public void Deselect()
    {
        if (_selectedNodeId == null) return;
        _selectedNodeId = null;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = null });
    }
}
```

---

## Task 4: Implement `ISceneGraph` on `SceneGraph`

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs`

`SceneGraph` stores `SceneNode` objects internally. The interface requires `GetNode` to return `GameObject`. Use an explicit interface implementation alongside the existing `public SceneNode GetNode(string)` so other callers (AssetBrowser, etc.) keep working.

- [ ] **Step 1: Update `SceneGraph.cs`**

Full file after change:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public class SceneGraph : ISceneGraph, IStartable, IDisposable
{
    private readonly EventBus _bus;
    private readonly Dictionary<string, SceneNode> _nodes = new();

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public SceneGraph(EventBus bus) => _bus = bus;

    public void Start() =>
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

    public void Dispose() =>
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);

    public SceneNode AddNode(GameObject go)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        var node   = go.AddComponent<SceneNode>();
        node.Init(nodeId);
        _nodes[nodeId] = node;
        _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    public void RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        _nodes.Remove(nodeId);
        UnityEngine.Object.Destroy(node.gameObject);
        _bus.Publish(new SceneModifiedEvent());
    }

    public SceneNode GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var n) ? n : null;

    // Explicit ISceneGraph.GetNode returns GameObject for SpatialUi panels
    GameObject ISceneGraph.GetNode(string nodeId) => GetNode(nodeId)?.gameObject;

    // ISceneGraph.AddNode discards the return value
    void ISceneGraph.AddNode(GameObject go) => AddNode(go);

    private void OnSceneOpened(SceneOpenedEvent _) { }
}
```

---

## Task 5: Update `RigRuntime` and `BoneProxy` to use `ISelectionManager`

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/BoneProxy.cs`

Both currently inject the concrete `SelectionManager`. After this task they inject `ISelectionManager`. `RigRuntime` also implements `IRigRuntime`.

- [ ] **Step 1: Update `RigRuntime.cs`**

Full file after change:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private GameObject _boneProxyPrefab;

    private ISelectionManager     _selectionManager;
    private readonly List<BoneProxy> _proxies = new();

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
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
        ClearProxies();

        var animator = smr.GetComponentInParent<Animator>();
        if (animator == null) animator = smr.gameObject.AddComponent<Animator>();

        var rigGo = new GameObject("_Rig");
        rigGo.transform.SetParent(smr.transform, worldPositionStays: false);
        var rig = rigGo.AddComponent<Rig>();

        var rigBuilder = animator.gameObject.GetComponent<RigBuilder>();
        if (rigBuilder == null) rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
        rigBuilder.layers.Add(new RigLayer(rig));

        var boneRenderer = animator.gameObject.GetComponent<BoneRenderer>();
        if (boneRenderer == null) boneRenderer = animator.gameObject.AddComponent<BoneRenderer>();
        var transforms = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) transforms.Add(t);
        }
        boneRenderer.transforms = transforms.ToArray();

        foreach (var bone in definition.Bones)
        {
            var boneTr = FindBone(smr, bone.BoneName);
            if (boneTr == null || _boneProxyPrefab == null) continue;

            var proxyGo = Instantiate(_boneProxyPrefab, boneTr.position, boneTr.rotation);
            var proxy   = proxyGo.GetComponent<BoneProxy>();
            var nodeId  = $"bone_{bone.BoneName}";
            proxy.Construct(_selectionManager);
            proxy.Init(bone.BoneName, boneTr, nodeId);
            _proxies.Add(proxy);
        }

        foreach (var chain in definition.IkChains)
            AddTwoBoneIK(rigGo.transform, smr, chain);

        rigBuilder.Build();
    }

    private void AddTwoBoneIK(Transform rigTransform, SkinnedMeshRenderer smr, IkChainRecord chain)
    {
        var ikGo = new GameObject($"IK_{chain.RootBone}_{chain.EndBone}");
        ikGo.transform.SetParent(rigTransform, false);

        var constraint      = ikGo.AddComponent<TwoBoneIKConstraint>();
        constraint.data.root = FindBone(smr, chain.RootBone);
        constraint.data.mid  = FindMidBone(smr, chain.RootBone, chain.EndBone);
        constraint.data.tip  = FindBone(smr, chain.EndBone);
        constraint.weight    = chain.Weight;

        var target = new GameObject($"Target_{chain.EndBone}");
        target.transform.SetParent(rigTransform, false);
        if (constraint.data.tip != null)
            target.transform.SetPositionAndRotation(constraint.data.tip.position, constraint.data.tip.rotation);
        constraint.data.target = target.transform;
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }

    private Transform FindMidBone(SkinnedMeshRenderer smr, string root, string end)
    {
        bool inChain = false;
        foreach (var b in smr.bones)
        {
            if (b.name == root) { inChain = true; continue; }
            if (inChain && b.name != end) return b;
            if (b.name == end) break;
        }
        return null;
    }

    private void ClearProxies()
    {
        foreach (var p in _proxies)
            if (p != null) Destroy(p.gameObject);
        _proxies.Clear();
    }
}
```

- [ ] **Step 2: Update `BoneProxy.cs`**

Full file after change:

```csharp
using UnityEngine;
using VContainer;

public class BoneProxy : MonoBehaviour
{
    public string    BoneName      { get; private set; }
    public Transform BoneTransform { get; private set; }

    private ISelectionManager _selectionManager;
    private string            _nodeId;

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public void Init(string boneName, Transform boneTransform, string nodeId)
    {
        BoneName       = boneName;
        BoneTransform  = boneTransform;
        _nodeId        = nodeId;
        gameObject.name = $"Proxy_{boneName}";
    }

    private void LateUpdate()
    {
        if (BoneTransform != null)
            transform.SetPositionAndRotation(BoneTransform.position, BoneTransform.rotation);
    }

    public void OnSelected() => _selectionManager?.Select(_nodeId);
}
```

---

## Task 6: Create new panel files in `SpatialUi/UI/`

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI/BoneInspectorPanel.cs`
- Create: `Assets/_App/Subsystems/SpatialUi/UI/IkSetupWizard.cs`
- Create: `Assets/_App/Subsystems/SpatialUi/UI/PropertyPanel.cs`

All three inject interfaces from `_Shared`. `SpatialUi.asmdef` already references `_Shared` — no asmdef changes needed here.

- [ ] **Step 1: Create `SpatialUi/UI/BoneInspectorPanel.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class BoneInspectorPanel : MonoBehaviour
{
    [SerializeField] private Button   _buildRigButton;
    [SerializeField] private Button   _openIkWizardButton;
    [SerializeField] private TMP_Text _boneCountText;

    private IRigRuntime       _rigRuntime;
    private ISelectionManager _selectionManager;
    private ISceneGraph       _sceneGraph;
    private IkSetupWizard     _ikWizard;

    [Inject]
    public void Construct(IRigRuntime rigRuntime, ISelectionManager selectionManager, ISceneGraph sceneGraph, IkSetupWizard ikWizard)
    {
        _rigRuntime       = rigRuntime;
        _selectionManager = selectionManager;
        _sceneGraph       = sceneGraph;
        _ikWizard         = ikWizard;
    }

    private void Awake()
    {
        _buildRigButton.onClick.AddListener(OnBuildRig);
        _openIkWizardButton.onClick.AddListener(OnOpenIkWizard);
    }

    private void OnBuildRig()
    {
        var nodeId = _selectionManager.SelectedNodeId;
        if (string.IsNullOrEmpty(nodeId)) return;

        var go = _sceneGraph.GetNode(nodeId);
        if (go == null) return;

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) { _boneCountText.text = "No SkinnedMeshRenderer"; return; }

        var def = _rigRuntime.BuildFromSkinnedMesh(smr);
        _rigRuntime.ApplyDefinition(def, smr);
        _boneCountText.text = $"{def.Bones.Count} bones";
    }

    private void OnOpenIkWizard() => _ikWizard?.OpenForSelection();
}
```

- [ ] **Step 2: Create `SpatialUi/UI/IkSetupWizard.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class IkSetupWizard : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown _rootBoneDropdown;
    [SerializeField] private TMP_Dropdown _endBoneDropdown;
    [SerializeField] private Button       _confirmButton;
    [SerializeField] private Button       _cancelButton;

    private IRigRuntime       _rigRuntime;
    private ISelectionManager _selectionManager;
    private ISceneGraph       _sceneGraph;

    private SkinnedMeshRenderer _currentSmr;
    private RigDefinition       _currentDef;

    [Inject]
    public void Construct(IRigRuntime rigRuntime, ISelectionManager selectionManager, ISceneGraph sceneGraph)
    {
        _rigRuntime       = rigRuntime;
        _selectionManager = selectionManager;
        _sceneGraph       = sceneGraph;
    }

    private void Awake()
    {
        _confirmButton.onClick.AddListener(OnConfirm);
        _cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void OpenForSelection()
    {
        var go = _sceneGraph.GetNode(_selectionManager.SelectedNodeId);
        if (go == null) return;

        _currentSmr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (_currentSmr == null) return;

        _currentDef = _rigRuntime.BuildFromSkinnedMesh(_currentSmr);
        PopulateDropdowns(_currentDef);
        gameObject.SetActive(true);
    }

    private void PopulateDropdowns(RigDefinition def)
    {
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var b in def.Bones)
            options.Add(new TMP_Dropdown.OptionData(b.BoneName));

        _rootBoneDropdown.ClearOptions();
        _endBoneDropdown.ClearOptions();
        _rootBoneDropdown.AddOptions(options);
        _endBoneDropdown.AddOptions(options);

        if (options.Count > 1)
            _endBoneDropdown.value = options.Count - 1;
    }

    private void OnConfirm()
    {
        if (_currentDef == null || _currentSmr == null) return;

        var chain = new IkChainRecord
        {
            RootBone = _rootBoneDropdown.options[_rootBoneDropdown.value].text,
            EndBone  = _endBoneDropdown.options[_endBoneDropdown.value].text,
            Weight   = 1f
        };
        _currentDef.IkChains.Add(chain);
        _rigRuntime.ApplyDefinition(_currentDef, _currentSmr);
        gameObject.SetActive(false);
    }
}
```

- [ ] **Step 3: Create `SpatialUi/UI/PropertyPanel.cs`**

```csharp
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TMPro;

public class PropertyPanel : MonoBehaviour, IStartable, IDisposable
{
    [SerializeField] private TMP_Text _positionText;
    [SerializeField] private TMP_Text _rotationText;
    [SerializeField] private TMP_Text _scaleText;

    private EventBus    _bus;
    private ISceneGraph _sceneGraph;

    [Inject]
    public void Construct(EventBus bus, ISceneGraph sceneGraph)
    {
        _bus        = bus;
        _sceneGraph = sceneGraph;
    }

    public void Start() => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (e.SelectedNodeId == null) { ClearDisplay(); return; }
        var go = _sceneGraph.GetNode(e.SelectedNodeId);
        if (go == null) return;
        var t = go.transform;
        _positionText.text = $"Pos: {t.position:F2}";
        _rotationText.text = $"Rot: {t.eulerAngles:F1}";
        _scaleText.text    = $"Scl: {t.localScale:F2}";
    }

    private void ClearDisplay()
    {
        _positionText.text = "Pos: —";
        _rotationText.text = "Rot: —";
        _scaleText.text    = "Scl: —";
    }
}
```

---

## Task 7: Replace old panel and data files with redirect comments

**Files:**
- Overwrite: `RigBuilder/UI/BoneInspectorPanel.cs`
- Overwrite: `RigBuilder/UI/IkSetupWizard.cs`
- Overwrite: `SceneComposition/UI/PropertyPanel.cs`

Emptying the class definitions removes duplicate-type errors. The `.meta` files stay harmless — delete the `.cs` files (and their `.meta` siblings) later in Unity Editor via right-click → Delete.

- [ ] **Step 1: Clear `RigBuilder/UI/BoneInspectorPanel.cs`**

```csharp
// Moved to Assets/_App/Subsystems/SpatialUi/UI/BoneInspectorPanel.cs
```

- [ ] **Step 2: Clear `RigBuilder/UI/IkSetupWizard.cs`**

```csharp
// Moved to Assets/_App/Subsystems/SpatialUi/UI/IkSetupWizard.cs
```

- [ ] **Step 3: Clear `SceneComposition/UI/PropertyPanel.cs`**

```csharp
// Moved to Assets/_App/Subsystems/SpatialUi/UI/PropertyPanel.cs
```

> **Unity Editor clean-up (do after verification in Task 9):** In Unity Project window, right-click each of these six files and select Delete:
> - `RigBuilder/Data/RigDefinition.cs` + `.meta`
> - `RigBuilder/Data/BoneRecord.cs` + `.meta`
> - `RigBuilder/Data/IkChainRecord.cs` + `.meta`
> - `RigBuilder/UI/BoneInspectorPanel.cs` + `.meta`
> - `RigBuilder/UI/IkSetupWizard.cs` + `.meta`
> - `SceneComposition/UI/PropertyPanel.cs` + `.meta`

---

## Task 8: Update `RigBuilder.asmdef`

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef`

Remove `Subsystems.SceneComposition` (RigRuntime now injects ISelectionManager from _Shared), `Subsystems.SpatialUi` (no UI panels in RigBuilder), and `Unity.TextMeshPro` (no TMP usage remains in RigBuilder).

- [ ] **Step 1: Overwrite `Subsystems.RigBuilder.asmdef`**

```json
{
  "name": "Subsystems.RigBuilder",
  "references": [
    "_Shared",
    "VContainer",
    "Unity.Animation.Rigging"
  ],
  "autoReferenced": false
}
```

---

## Task 9: Update `VrEditingSceneScope` with Phase 6 registrations

**Files:**
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`

Add `RigRuntime` registered with `.AsImplementedInterfaces().AsSelf()` so VContainer provides `IRigRuntime`. Add `IkSetupWizard`, `BoneInspectorPanel`, `PropertyPanel` via `RegisterComponentInHierarchy` — these MonoBehaviours must be present in the VrEditing scene hierarchy at scope startup (placed on Canvas or as child objects). Also fix `CommandStack` to include `.AsImplementedInterfaces().AsSelf()` for future `ICommandStack` usage.

- [ ] **Step 1: Update `VrEditingSceneScope.cs`**

Full file after change:

```csharp
using VContainer;
using VContainer.Unity;
using UnityEngine;

public class VrEditingSceneScope : LifetimeScope
{
    [SerializeField] private PanelRegistry _panelRegistry;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        builder.RegisterInstance(_panelRegistry);
        builder.RegisterInstance(Camera.main);
        builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
        builder.Register<AssetImporter>(Lifetime.Scoped);
        builder.RegisterComponentInHierarchy<UndoKeyHandler>();

        // Phase 6 — RigBuilder
        builder.RegisterComponentInHierarchy<RigRuntime>().AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<IkSetupWizard>();
        builder.RegisterComponentInHierarchy<BoneInspectorPanel>();
        builder.RegisterComponentInHierarchy<PropertyPanel>().AsImplementedInterfaces().AsSelf();
    }
}
```

`PropertyPanel` implements `IStartable` and `IDisposable`, so it needs `.AsImplementedInterfaces().AsSelf()` for VContainer to call `Start()` and `Dispose()` at the right scope lifecycle moments.

---

## Task 10: Verification in Unity Editor

- [ ] **Step 1: Open Unity Editor**

Switch focus to Unity. Wait for the auto-recompile to finish (progress bar bottom-right).

- [ ] **Step 2: Check Console for errors**

Open `Window > General > Console`. Errors to watch for:

| Error | Fix |
|---|---|
| `The type 'RigDefinition' exists in both...` | Old `.cs` file still has class — redo Task 2 Steps 4–6 |
| `The type 'BoneInspectorPanel' exists in both...` | Redo Task 7 Steps 1–3 |
| `ISceneGraph does not contain definition for GetNode` | Forgot to add explicit interface impl in Task 4 |
| `Cannot implicitly convert SceneNode to GameObject` | Check `ISceneGraph.GetNode` explicit impl returns `?.gameObject` |
| `VContainerException: no registration for IRigRuntime` | `RigRuntime` GameObject not in VrEditing scene hierarchy — add it |

- [ ] **Step 3: Place missing MonoBehaviours in VrEditing scene if needed**

`RegisterComponentInHierarchy` requires `RigRuntime`, `IkSetupWizard`, `BoneInspectorPanel`, `PropertyPanel` to exist as GameObjects in the scene at the moment the scope builds. If any are missing:
- Create an empty GameObject in the VrEditing hierarchy
- Add the MonoBehaviour component
- For panels: attach to the Canvas hierarchy used in VrEditing

- [ ] **Step 4: Run Unity Test Runner**

`Window > General > Test Runner > Run All`. Tests in `Subsystems.StorageCore.Tests` and `Subsystems.SceneComposition.Tests` should still pass — they don't depend on the moved types.

- [ ] **Step 5: Delete old redirect files in Unity Editor**

In the Project window, right-click each file listed in Task 7's clean-up note → Delete. This removes both the `.cs` and its `.meta` file cleanly.
