# VR 3D Gizmo System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a 3D gizmo system for VR object manipulation (axis-constrained Move/Rotate/Scale via grip-drag), preceded by single-select cleanup of `SelectionManager` and an input-swap in `XRPromeonInteractable` (tap-trigger=select, hold-trigger=rotate, hold-grip=move).

**Architecture:** Three phases. Phase 0 trims `SelectionManager` to single-select (deletes `Toggle`, `SelectedIds`, `SelectedNodeIds`, `SelectionVisual.InSet`). Phase 1 relabels and swaps capture in `XRPromeonInteractable` state machine. Phase 2 introduces `GizmoActivator` (scene-managed orchestrator), `GizmoHierarchy` (rename of existing controller), `GizmoHandle` (thin XR interactable), four `IGizmoDragStrategy` implementations (pure axis math, unit-tested), and `GizmoToolsPanel` UI. Phase 3 is user-side prefab work + smoke test.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer DI, MessagePipe-style `EventBus` (already in project), XR Interaction Toolkit (`XRBaseInteractable`, `NearFarInteractor`), NUnit (`Window > General > Test Runner` → EditMode).

**Project rules acknowledgement (durable user instructions):**
- No git commands during dev — user commits manually. **This plan contains zero git steps.**
- Manual prefab work is done by the user. Plan calls them out as explicit user-action tasks.
- Hard cleanup (Phase 0) deletes code (explicit user authorisation in brainstorming session). Anywhere else, prefer comment-out + `// TODO:` over delete.

**Verification convention:** After each task, **the engineer must verify Unity compiles** (`Window > General > Console`, no red errors) and **EditMode tests pass** (`Window > General > Test Runner > EditMode > Run All`). Where a task adds tests, that's the verification step. Where a task only touches runtime code without tests (e.g., DI wiring), the verification is "Unity compiles, no console errors after entering Play mode briefly".

---

## File Structure (full picture)

```
Assets/_App/Subsystems/VrInteraction/Gizmo/                  ← NEW folder
├── GizmoMode.cs                    enum Move/Rotate/Scale
├── HandleKind.cs                   enum MoveAxis/RotateRing/ScaleAxis/ScaleUniform
├── AxisKind.cs                     enum X/Y/Z
├── GizmoConfig.cs                  ScriptableObject (prefab ref + bounds params)
├── GizmoHierarchy.cs               rename from SceneComposition/VrGizmoHierarchyController.cs
├── GizmoHandle.cs                  rewrite of VrInteraction/GizmoHandle.cs
├── GizmoActivator.cs               new scene-managed orchestrator
├── BoundsFitter.cs                 static helper for combined-bounds + clamp
├── Strategies/
│   ├── IGizmoDragStrategy.cs       interface
│   ├── AxisMoveStrategy.cs
│   ├── AxisScaleStrategy.cs
│   ├── UniformScaleStrategy.cs
│   └── RingRotateStrategy.cs
└── UI/
    ├── GizmoToolsPanel.cs          sub-panel with 3 mode buttons
    └── GizmoToolsPanelOpener.cs    UserPanel toggle button

Assets/_App/Subsystems/VrInteraction/Tests/                  ← NEW tests
├── AxisMoveStrategyTests.cs
├── AxisScaleStrategyTests.cs
├── UniformScaleStrategyTests.cs
├── RingRotateStrategyTests.cs
├── BoundsFitterTests.cs
└── GizmoActivatorStateTests.cs

Assets/_App/_Shared/Events/                                  ← ADD 5 events
└── AppEvents.cs                    extend with gizmo events (file already exists)

Assets/_App/_Shared/Data/SelectionVisual.cs                  ← MODIFY (drop InSet)
Assets/_App/_Shared/Interfaces/ISelectionManager.cs          ← MODIFY (single-select API)
Assets/_App/Subsystems/SceneComposition/SelectionManager.cs  ← MODIFY (drop multi)
Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs ← REWRITE
Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs ← MODIFY (input swap)
Assets/_App/Subsystems/VrInteraction/Selectable.cs           ← MODIFY (drop InSet)
Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs  ← MODIFY (simplify)
Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs ← MODIFY
Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs ← MODIFY
Assets/_App/Subsystems/RigBuilder/PromeonProxyRigBuilder.cs  ← MODIFY (drop SelectedNodeIds usage)
Assets/_App/Bootstrap/UndoKeyHandler.cs                      ← MODIFY (drag-aware)
Assets/_App/Bootstrap/VrEditingSceneScope.cs                 ← MODIFY (DI)
Assets/_App/Bootstrap/SandboxSceneScope.cs                   ← MODIFY (DI)

DELETE: Assets/_App/Subsystems/SceneComposition/VrGizmoHierarchyController.cs
        Assets/_App/Subsystems/SceneComposition/VrGizmoHierarchyController.cs.meta
        (Both moved to Gizmo/GizmoHierarchy.cs)
```

---

# Phase 0 — SelectionManager hard cleanup to single-select

Phase 0 is one atomic compile unit: API + impl + tests + all call-sites change together. Splitting across tasks would leave Unity in a broken compile state. So Phase 0 is **one large task** with sub-step checkpoints.

## Task 0.1: Single-select cleanup

**Files:**
- Modify: `Assets/_App/_Shared/Interfaces/ISelectionManager.cs`
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs` (line 5)
- Modify: `Assets/_App/_Shared/Data/SelectionVisual.cs`
- Modify: `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs`
- Modify: `Assets/_App/Subsystems/VrInteraction/Selectable.cs`
- Modify: `Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs`
- Modify: `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs:184-191`
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs:92-104`
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs:30-40`
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonProxyRigBuilder.cs:219`
- Rewrite: `Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs`

- [ ] **Step 1: Replace ISelectionManager**

Rewrite `Assets/_App/_Shared/Interfaces/ISelectionManager.cs` fully:

```csharp
public interface ISelectionManager
{
    string SelectedNodeId { get; }     // null = nothing selected
    void Select(string nodeId);        // null = clear
}
```

- [ ] **Step 2: Trim SelectionChangedEvent**

In `Assets/_App/_Shared/Events/AppEvents.cs` line 5, change:

```csharp
public struct SelectionChangedEvent  { public string SelectedNodeId; public string[] SelectedNodeIds; }
```

to:

```csharp
public struct SelectionChangedEvent  { public string SelectedNodeId; }
```

- [ ] **Step 3: Trim SelectionVisual enum**

Rewrite `Assets/_App/_Shared/Data/SelectionVisual.cs` fully:

```csharp
public enum SelectionVisual
{
    None,
    Selected,
}
```

- [ ] **Step 4: Rewrite SelectionManager impl**

Rewrite `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs` fully:

```csharp
using System;
using VContainer.Unity;

public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus _bus;
    private string _selectedNodeId;

    public string SelectedNodeId => _selectedNodeId;

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()   { }
    public void Dispose() { }

    public void Select(string nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = _selectedNodeId });
    }
}
```

- [ ] **Step 5: Update Selectable**

In `Assets/_App/Subsystems/VrInteraction/Selectable.cs`, replace the `SetVisualState` method body (lines 16-35) with:

```csharp
    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.Selected:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth = 6f;
                break;
        }
    }
```

- [ ] **Step 6: Simplify SelectionVisualSync**

Rewrite `Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs` fully:

```csharp
using System;
using VContainer.Unity;

public class SelectionVisualSync : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;

    public SelectionVisualSync(EventBus bus, SceneGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void Start()   => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        foreach (var pair in _graph.Nodes)
        {
            var sel = pair.Value.GetComponent<Selectable>();
            if (sel == null) continue;
            sel.SetVisualState(pair.Key == e.SelectedNodeId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
}
```

- [ ] **Step 7: Fix XRPromeonInteractable.IsObjectSelected**

In `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` replace `IsObjectSelected` (lines 184-191):

```csharp
    private bool IsObjectSelected()
    {
        if (_node == null || _selectionManager == null) return false;
        return _selectionManager.SelectedNodeId == _node.NodeId;
    }
```

- [ ] **Step 8: Fix SceneOutlinerView.ApplyHighlight**

In `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs` replace `ApplyHighlight` (lines 92-104):

```csharp
    private void ApplyHighlight()
    {
        if (_rowsRoot == null || _selection == null) return;
        var selectedId = _selection.SelectedNodeId;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
        {
            row.SetVisualState(row.NodeId == selectedId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
```

Also remove `using System.Collections.Generic;` if it becomes unused (HashSet is gone).

- [ ] **Step 9: Fix OutlinerItem.SetVisualState**

In `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs` replace `SetVisualState` (lines 30-40):

```csharp
    public void SetVisualState(SelectionVisual state)
    {
        if (_highlight == null) return;
        _highlight.enabled = state != SelectionVisual.None;
        _highlight.color = state == SelectionVisual.Selected
            ? new Color(1f, 0.95f, 0.15f, 0.35f)
            : Color.clear;
    }
```

- [ ] **Step 10: Fix PromeonProxyRigBuilder selection subscription**

In `Assets/_App/Subsystems/RigBuilder/PromeonProxyRigBuilder.cs` find the `OnSelectionChanged` handler around line 219. The current code passes `evt.SelectedNodeIds` (which was a `string[]`) to `ApplyBoneOutlineColors`. Change to pass single id:

Replace the offending line (originally `ApplyBoneOutlineColors(evt.SelectedNodeIds);`) with:

```csharp
        ApplyBoneOutlineColors(evt.SelectedNodeId);
```

Then locate the `ApplyBoneOutlineColors` method (signature was `ApplyBoneOutlineColors(string[] selectedIds)`). Change signature and impl to:

```csharp
    private void ApplyBoneOutlineColors(string selectedId)
    {
        // … existing logic, but treat as single id instead of array.
        // If the old code did `selectedIds?.Contains(id)`, replace with `id == selectedId`.
        // If the old code looped over selectedIds, it now handles at most one id.
    }
```

**Read the actual current body first** before rewriting — the change is mechanical (collection → single string) but preserve logic.

- [ ] **Step 11: Rewrite SelectionManagerTests**

Replace `Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs` fully:

```csharp
using NUnit.Framework;

public class SelectionManagerTests
{
    private EventBus _bus;
    private SelectionManager _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _sut = new SelectionManager(_bus);
    }

    [Test]
    public void Select_SetsSelectedNodeId()
    {
        _sut.Select("a");
        Assert.AreEqual("a", _sut.SelectedNodeId);
    }

    [Test]
    public void Select_ReplacesPrevious()
    {
        _sut.Select("a");
        _sut.Select("b");
        Assert.AreEqual("b", _sut.SelectedNodeId);
    }

    [Test]
    public void Select_Null_Clears()
    {
        _sut.Select("a");
        _sut.Select(null);
        Assert.IsNull(_sut.SelectedNodeId);
    }

    [Test]
    public void Select_PublishesSelectionChangedEvent()
    {
        SelectionChangedEvent received = default;
        bool fired = false;
        _bus.Subscribe<SelectionChangedEvent>(e => { received = e; fired = true; });
        _sut.Select("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("a", received.SelectedNodeId);
    }

    [Test]
    public void Select_SameId_DoesNotRefire()
    {
        int count = 0;
        _bus.Subscribe<SelectionChangedEvent>(_ => count++);
        _sut.Select("a");
        _sut.Select("a");
        Assert.AreEqual(1, count);
    }
}
```

- [ ] **Step 12: Verify**

1. Switch to Unity Editor, wait for recompile.
2. `Window > General > Console` — must have no red errors. If any other call-site of `SelectedIds` / `Toggle` / `Clear` / `SelectionVisual.InSet` / `ActiveId` exists, the compiler will flag it; fix each by either:
   - Renaming `ActiveId` reference → `SelectedNodeId`.
   - Replacing `Toggle(id)` → `Select(id)`.
   - Replacing `Clear()` → `Select(null)`.
   - Removing dead `SelectionVisual.InSet` branches.
3. `Window > General > Test Runner > EditMode > Run All` — all green.

---

# Phase 1 — XRPromeonInteractable input swap

## Task 1.1: Swap input model (tap-trigger=select, hold-trigger=rotate, hold-grip=move)

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs`

- [ ] **Step 1: Rename state enum**

In `XRPromeonInteractable.cs` line 22, change:

```csharp
private enum State { Idle, TriggerPressed, TriggerMove, GripRotate }
```

to:

```csharp
private enum State { Idle, TriggerPressed, TriggerRotate, GripMove }
```

- [ ] **Step 2: Swap Idle-state grip handler**

In `ProcessInteractable` (around lines 95-100), the Idle case currently transitions to `GripRotate` on grip + captures rotation offset. Replace lines 95-101:

```csharp
                if (ni.selectInput.ReadWasPerformedThisFrame() && IsObjectSelected())
                {
                    Lock(ni);
                    CapturePositionOffset();
                    _state = State.GripMove;
                }
                break;
```

- [ ] **Step 3: Swap TriggerPressed → TriggerRotate transition**

In `ProcessInteractable` TriggerPressed case (around lines 111-115), the transition to "move-style" must become rotate. Replace lines 111-115:

```csharp
                if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
                {
                    CaptureRotationOffset();
                    _state = State.TriggerRotate;
                }
                break;
```

- [ ] **Step 4: Rename TriggerMove case to TriggerRotate and swap apply**

Replace the entire `case State.TriggerMove:` block (around lines 117-129) with:

```csharp
            case State.TriggerRotate:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    var pos = transform.position;
                    var rot = transform.rotation;
                    var scl = transform.localScale;
                    EndInteraction();
                    _gizmoController.CommitTransform(transform, pos, rot, scl);
                    break;
                }
                ApplyRotate();
                break;
```

- [ ] **Step 5: Rename GripRotate case to GripMove and swap apply**

Replace the entire `case State.GripRotate:` block (around lines 131-143) with:

```csharp
            case State.GripMove:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                {
                    var pos = transform.position;
                    var rot = transform.rotation;
                    var scl = transform.localScale;
                    EndInteraction();
                    _gizmoController.CommitTransform(transform, pos, rot, scl);
                    break;
                }
                ApplyMove();
                break;
```

- [ ] **Step 6: Verify**

1. Unity recompiles, no console errors.
2. EditMode tests pass.
3. (Manual user-side smoke test, optional now or deferred to Phase 3 §3.2 step 10-12): in VR — tap trigger on object selects, hold trigger past 0.5s rotates the object, hold grip moves it.

- [ ] **Step 7: Update memory entry**

Update `C:\Users\maksp\.claude\projects\S---02--Projects--02--Study--00--Repositories-PromeonLab\memory\project_interaction_input_model.md` — replace body with description of new model (tap-trigger=select, hold-trigger=rotate, hold-grip=move). No `MEMORY.md` index change needed.

---

# Phase 2 — Gizmo system

Tasks ordered for testability: pure-C# math first (strategies), then runtime glue (Hierarchy, Handle, Activator), then UI, then DI.

## Task 2.1: Enums

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoMode.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/HandleKind.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/AxisKind.cs`

- [ ] **Step 1: Create the Gizmo folder**

User-action: in Unity Project window, create folder `Assets/_App/Subsystems/VrInteraction/Gizmo/` and inside it `Strategies/` and `UI/`.

- [ ] **Step 2: Write GizmoMode.cs**

```csharp
public enum GizmoMode
{
    Move,
    Rotate,
    Scale,
}
```

- [ ] **Step 3: Write HandleKind.cs**

```csharp
public enum HandleKind
{
    MoveAxis,
    RotateRing,
    ScaleAxis,
    ScaleUniform,
}
```

- [ ] **Step 4: Write AxisKind.cs**

```csharp
public enum AxisKind
{
    X = 0,
    Y = 1,
    Z = 2,
}
```

- [ ] **Step 5: Verify** — Unity recompiles, no errors.

---

## Task 2.2: Gizmo events

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`

- [ ] **Step 1: Append 5 events**

Append to bottom of `AppEvents.cs`:

```csharp
public struct GizmoToolsPanelOpenedEvent { }
public struct GizmoToolsPanelClosedEvent { }
public struct GizmoModeChangedEvent      { public GizmoMode Mode; }
public struct GizmoDragStartedEvent      { public string TargetNodeId; }
public struct GizmoDragEndedEvent        { public string TargetNodeId; }
```

- [ ] **Step 2: Verify** — Unity recompiles, no errors.

---

## Task 2.3: GizmoConfig ScriptableObject

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoConfig.cs`

- [ ] **Step 1: Write GizmoConfig.cs**

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "GizmoConfig", menuName = "PromeonLab/Gizmo Config")]
public class GizmoConfig : ScriptableObject
{
    [SerializeField] private GameObject _gizmoPrefab;
    [SerializeField, Range(0.5f, 5f)]   private float _boundsCoefficient = 1.5f;
    [SerializeField, Range(0.01f, 1f)]  private float _minSize           = 0.1f;
    [SerializeField, Range(1f,    20f)] private float _maxSize           = 5f;

    public GameObject GizmoPrefab       => _gizmoPrefab;
    public float      BoundsCoefficient => _boundsCoefficient;
    public float      MinSize           => _minSize;
    public float      MaxSize           => _maxSize;
}
```

- [ ] **Step 2: Verify** — Unity recompiles. User-action: in Project window, right-click → `Create > PromeonLab > Gizmo Config` → save as `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoConfig.asset`. Drag `Vr3D_Gizmos.prefab` from `Assets/Resources/Prefabs/Gizmos/` into the `_gizmoPrefab` slot in the inspector.

---

## Task 2.4: IGizmoDragStrategy interface

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/Strategies/IGizmoDragStrategy.cs`

- [ ] **Step 1: Write the interface**

```csharp
using UnityEngine;

public interface IGizmoDragStrategy
{
    void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot);
    void UpdateDrag(Vector3 handPos, Quaternion handRot);
    void EndDrag();
}
```

Note: `handRot` is kept in the interface for future hand-rotation-driven strategies (not used by current 4 implementations). Cheap signature future-proofing — no extra storage.

- [ ] **Step 2: Verify** — Unity recompiles.

---

## Task 2.5: AxisMoveStrategy with tests (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/Strategies/AxisMoveStrategy.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/AxisMoveStrategyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class AxisMoveStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private AxisMoveStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position = Vector3.zero;
        _target.rotation = Quaternion.identity;
        _sut = new AxisMoveStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_AlongX_OnlyXChanges()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(3f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.position.x, 1e-4);
        Assert.AreEqual(0f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.z, 1e-4);
    }

    [Test]
    public void Drag_HandMovesPerpendicular_ProjectionIgnoresOffset()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 5f, 7f), Quaternion.identity);  // pure perpendicular motion
        Assert.AreEqual(0f, _target.position.x, 1e-4);
        Assert.AreEqual(0f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.z, 1e-4);
    }

    [Test]
    public void Drag_AlongY_OnlyYChanges()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(0f, 1f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 4f, 0f), Quaternion.identity);
        Assert.AreEqual(3f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.x, 1e-4);
    }

    [Test]
    public void Drag_TargetRotated45_AxisIsLocalNotWorld()
    {
        _target.rotation = Quaternion.Euler(0f, 45f, 0f);
        // local +X in world is (cos45, 0, -sin45)
        var dir = new Vector3(Mathf.Cos(Mathf.Deg2Rad * 45f), 0f, -Mathf.Sin(Mathf.Deg2Rad * 45f));
        _sut.BeginDrag(_target, AxisKind.X, dir * 1f, Quaternion.identity);
        _sut.UpdateDrag(dir * 3f, Quaternion.identity);
        // target should have moved 2 along local X = `dir * 2`
        Assert.AreEqual(dir.x * 2f, _target.position.x, 1e-4);
        Assert.AreEqual(0f,         _target.position.y, 1e-4);
        Assert.AreEqual(dir.z * 2f, _target.position.z, 1e-4);
    }
}
```

- [ ] **Step 2: Run tests, expect FAIL (compile error: AxisMoveStrategy not found)**

`Window > General > Test Runner > EditMode > Run All` → tests fail at compile.

- [ ] **Step 3: Write AxisMoveStrategy**

```csharp
using UnityEngine;

public class AxisMoveStrategy : IGizmoDragStrategy
{
    private Transform _target;
    private Vector3   _axisWorld;
    private Vector3   _originalTargetPos;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target            = target;
        _axisWorld         = LocalAxis(target, axis);
        _originalTargetPos = target.position;
        _distAtGrab        = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
        var delta   = distNow - _distAtGrab;
        _target.position = _originalTargetPos + _axisWorld * delta;
    }

    public void EndDrag()
    {
        _target = null;
    }

    private static Vector3 LocalAxis(Transform target, AxisKind axis)
    {
        switch (axis)
        {
            case AxisKind.X: return target.right;
            case AxisKind.Y: return target.up;
            default:         return target.forward;
        }
    }
}
```

- [ ] **Step 4: Run tests, expect PASS** — all 4 green.

---

## Task 2.6: AxisScaleStrategy with tests (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/Strategies/AxisScaleStrategy.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/AxisScaleStrategyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class AxisScaleStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private AxisScaleStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position   = Vector3.zero;
        _target.rotation   = Quaternion.identity;
        _target.localScale = Vector3.one;
        _sut = new AxisScaleStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_AwayFromCenter_DoublesAxisScale()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(2f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(1f, _target.localScale.y, 1e-4);
        Assert.AreEqual(1f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_TowardCenter_HalvesAxisScale()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(2f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0.5f, _target.localScale.x, 1e-4);
    }

    [Test]
    public void Drag_AtPivot_DistAtGrabClamped()
    {
        // grab AT pivot — _distAtGrab would be 0; must clamp to 0.01 to avoid div-by-zero
        Assert.DoesNotThrow(() =>
        {
            _sut.BeginDrag(_target, AxisKind.X, Vector3.zero, Quaternion.identity);
            _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        });
        Assert.IsFalse(float.IsNaN(_target.localScale.x));
        Assert.IsFalse(float.IsInfinity(_target.localScale.x));
    }

    [Test]
    public void Drag_PreservesOriginalScale()
    {
        _target.localScale = new Vector3(2f, 3f, 4f);
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(0f, 1f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 2f, 0f), Quaternion.identity);
        // Y doubles: 3 → 6. X, Z unchanged.
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(6f, _target.localScale.y, 1e-4);
        Assert.AreEqual(4f, _target.localScale.z, 1e-4);
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (AxisScaleStrategy missing)

- [ ] **Step 3: Write AxisScaleStrategy**

```csharp
using UnityEngine;

public class AxisScaleStrategy : IGizmoDragStrategy
{
    private const float MinFactor = 0.01f;

    private Transform _target;
    private Vector3   _axisWorld;
    private int       _axisIndex;
    private Vector3   _originalScale;
    private Vector3   _targetPosAtGrab;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _axisIndex       = (int)axis;
        _axisWorld       = LocalAxis(target, axis);
        _originalScale   = target.localScale;
        _targetPosAtGrab = target.position;
        _distAtGrab      = Mathf.Max(MinFactor, Vector3.Dot(handPos - _targetPosAtGrab, _axisWorld));
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = Vector3.Dot(handPos - _targetPosAtGrab, _axisWorld);
        var factor  = Mathf.Max(MinFactor, distNow / _distAtGrab);
        var scl     = _originalScale;
        scl[_axisIndex] = _originalScale[_axisIndex] * factor;
        _target.localScale = scl;
    }

    public void EndDrag() => _target = null;

    private static Vector3 LocalAxis(Transform target, AxisKind axis)
    {
        switch (axis)
        {
            case AxisKind.X: return target.right;
            case AxisKind.Y: return target.up;
            default:         return target.forward;
        }
    }
}
```

- [ ] **Step 4: Run, expect PASS**.

---

## Task 2.7: UniformScaleStrategy with tests (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/Strategies/UniformScaleStrategy.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/UniformScaleStrategyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class UniformScaleStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private UniformScaleStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position   = Vector3.zero;
        _target.localScale = Vector3.one;
        _sut = new UniformScaleStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_FartherFromCenter_ScalesAllAxesUp()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(2f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(2f, _target.localScale.y, 1e-4);
        Assert.AreEqual(2f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_CloserToCenter_ScalesDownProportionally()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(2f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0.5f, _target.localScale.x, 1e-4);
        Assert.AreEqual(0.5f, _target.localScale.y, 1e-4);
        Assert.AreEqual(0.5f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_AtPivot_ClampsAndDoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            _sut.BeginDrag(_target, AxisKind.X, Vector3.zero, Quaternion.identity);
            _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        });
        Assert.IsFalse(float.IsNaN(_target.localScale.x));
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Write UniformScaleStrategy**

```csharp
using UnityEngine;

public class UniformScaleStrategy : IGizmoDragStrategy
{
    private const float MinFactor = 0.01f;

    private Transform _target;
    private Vector3   _originalScale;
    private Vector3   _targetPosAtGrab;
    private float     _distAtGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _originalScale   = target.localScale;
        _targetPosAtGrab = target.position;
        _distAtGrab      = Mathf.Max(MinFactor, (handPos - _targetPosAtGrab).magnitude);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        var distNow = (handPos - _targetPosAtGrab).magnitude;
        var factor  = Mathf.Max(MinFactor, distNow / _distAtGrab);
        _target.localScale = _originalScale * factor;
    }

    public void EndDrag() => _target = null;
}
```

- [ ] **Step 4: Run, expect PASS**.

---

## Task 2.8: RingRotateStrategy with tests (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/Strategies/RingRotateStrategy.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/RingRotateStrategyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class RingRotateStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private RingRotateStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position = Vector3.zero;
        _target.rotation = Quaternion.identity;
        _sut = new RingRotateStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Rotate_AroundY_90Degrees()
    {
        // ring axis = Y (up). Grab at world +X, drag hand to world +Z.
        // Looking down -Y, that's CCW; SignedAngle(+X, +Z, +Y) = +90°.
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 0f, 1f), Quaternion.identity);
        // expected: rotation = AngleAxis(90, Y)
        var expected = Quaternion.AngleAxis(90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _target.rotation), 0.1f);
    }

    [Test]
    public void Rotate_ReverseDirection_NegativeAngle()
    {
        // grab at +X, drag to -Z → angle should be -90°.
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 0f, -1f), Quaternion.identity);
        var expected = Quaternion.AngleAxis(-90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _target.rotation), 0.1f);
    }

    [Test]
    public void Rotate_HandAtPivot_DoesNotWrite()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        var before = _target.rotation;
        Assert.DoesNotThrow(() => _sut.UpdateDrag(Vector3.zero, Quaternion.identity));
        Assert.AreEqual(0f, Quaternion.Angle(before, _target.rotation), 1e-4);
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Write RingRotateStrategy**

```csharp
using UnityEngine;

public class RingRotateStrategy : IGizmoDragStrategy
{
    private Transform  _target;
    private Vector3    _normalWorld;
    private Vector3    _targetPosAtGrab;
    private Vector3    _grabDirOnPlane;
    private Quaternion _originalRot;
    private bool       _validGrab;

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target          = target;
        _normalWorld     = LocalAxis(target, axis);
        _originalRot     = target.rotation;
        _targetPosAtGrab = target.position;

        var fromPivot     = handPos - _targetPosAtGrab;
        var grabOnPlane   = Vector3.ProjectOnPlane(fromPivot, _normalWorld);
        _validGrab        = grabOnPlane.sqrMagnitude > 1e-8f;
        _grabDirOnPlane   = _validGrab ? grabOnPlane.normalized : Vector3.zero;
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null || !_validGrab) return;
        var fromPivot   = handPos - _targetPosAtGrab;
        var nowOnPlane  = Vector3.ProjectOnPlane(fromPivot, _normalWorld);
        if (nowOnPlane.sqrMagnitude < 1e-8f) return;   // hand at pivot
        var nowDir      = nowOnPlane.normalized;
        var angle       = Vector3.SignedAngle(_grabDirOnPlane, nowDir, _normalWorld);
        _target.rotation = Quaternion.AngleAxis(angle, _normalWorld) * _originalRot;
    }

    public void EndDrag() => _target = null;

    private static Vector3 LocalAxis(Transform target, AxisKind axis)
    {
        switch (axis)
        {
            case AxisKind.X: return target.right;
            case AxisKind.Y: return target.up;
            default:         return target.forward;
        }
    }
}
```

- [ ] **Step 4: Run, expect PASS**.

---

## Task 2.9: BoundsFitter with tests (TDD)

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/BoundsFitter.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/BoundsFitterTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class BoundsFitterTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp() => _root = new GameObject("root");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void NoRenderers_ReturnsMinSize()
    {
        var size = BoundsFitter.ComputeSize(_root.transform, boundsCoefficient: 1.5f, minSize: 0.1f, maxSize: 5f);
        Assert.AreEqual(0.1f, size, 1e-4);
    }

    [Test]
    public void SingleCube_FitsToHalfExtentTimesCoefficient()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);   // 1×1×1, extents 0.5
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one;
        var size = BoundsFitter.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        // maxExtent = 0.5, * 1.5 = 0.75
        Assert.AreEqual(0.75f, size, 0.01f);
    }

    [Test]
    public void LargeMesh_ClampedToMaxSize()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one * 100f;   // extents = 50, *1.5 = 75 → clamp 5
        var size = BoundsFitter.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        Assert.AreEqual(5f, size, 1e-4);
    }

    [Test]
    public void TinyMesh_ClampedToMinSize()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one * 0.01f; // extents 0.005, *1.5 = 0.0075 → clamp 0.1
        var size = BoundsFitter.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        Assert.AreEqual(0.1f, size, 1e-4);
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Write BoundsFitter**

```csharp
using UnityEngine;

public static class BoundsFitter
{
    public static float ComputeSize(Transform target, float boundsCoefficient, float minSize, float maxSize)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers.Length == 0) return minSize;

        var combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        var e = combined.extents;
        var maxExtent = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
        return Mathf.Clamp(maxExtent * boundsCoefficient, minSize, maxSize);
    }
}
```

- [ ] **Step 4: Run, expect PASS**.

---

## Task 2.10: GizmoHierarchy — rename + move + extend API

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoHierarchy.cs` (rename of old file)
- Delete: `Assets/_App/Subsystems/SceneComposition/VrGizmoHierarchyController.cs` + `.meta`

- [ ] **Step 1: Cut & rename**

User-action (Unity Editor required for meta-file safety): move `Assets/_App/Subsystems/SceneComposition/VrGizmoHierarchyController.cs` → `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoHierarchy.cs` (drag in Project window — Unity preserves meta GUID, prefab references survive).

- [ ] **Step 2: Rewrite class fully**

Replace the entire content of `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoHierarchy.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

public class GizmoHierarchy : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private Transform _moveRoot;
    [SerializeField] private Transform _moveCenter;
    [SerializeField] private Transform _moveX;
    [SerializeField] private Transform _moveY;
    [SerializeField] private Transform _moveZ;

    [Header("Rotate")]
    [SerializeField] private Transform _rotateRoot;
    [SerializeField] private Transform _rotateX;
    [SerializeField] private Transform _rotateY;
    [SerializeField] private Transform _rotateZ;

    [Header("Scale")]
    [SerializeField] private Transform _scaleRoot;
    [SerializeField] private Transform _scaleCenter;
    [SerializeField] private Transform _scaleX;
    [SerializeField] private Transform _scaleY;
    [SerializeField] private Transform _scaleZ;

    private readonly Dictionary<Transform, TransformState> _defaultStates = new();

    private void Awake() => CacheInitialState();

    public void ShowMode(GizmoMode mode)
    {
        if (_moveRoot   != null) _moveRoot.gameObject  .SetActive(mode == GizmoMode.Move);
        if (_rotateRoot != null) _rotateRoot.gameObject.SetActive(mode == GizmoMode.Rotate);
        if (_scaleRoot  != null) _scaleRoot.gameObject .SetActive(mode == GizmoMode.Scale);
    }

    public void OnHandleGrabbed(GizmoHandle handle)
    {
        if (handle == null) return;
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:
                SetAsParent(handle.transform, new[] { _moveCenter, _moveX, _moveY, _moveZ });
                break;
            case HandleKind.RotateRing:
                SetAsParent(handle.transform, new[] { _rotateX, _rotateY, _rotateZ });
                break;
            // ScaleAxis / ScaleUniform: no re-parent
        }
    }

    public void OnHandleReleased(GizmoMode currentMode)
    {
        ResetHierarchy();
        ShowMode(currentMode);
    }

    public void ResetHierarchy()
    {
        foreach (var pair in _defaultStates)
        {
            var t = pair.Key;
            var s = pair.Value;
            if (t == null) continue;
            t.SetParent(s.Parent, false);
            t.localPosition = s.LocalPosition;
            t.localRotation = s.LocalRotation;
            t.localScale    = s.LocalScale;
        }
    }

    private static void SetAsParent(Transform newParent, Transform[] group)
    {
        foreach (var element in group)
        {
            if (element == null || element == newParent) continue;
            element.SetParent(newParent, worldPositionStays: true);
        }
    }

    private void CacheInitialState()
    {
        _defaultStates.Clear();
        Cache(_moveRoot); Cache(_moveCenter); Cache(_moveX); Cache(_moveY); Cache(_moveZ);
        Cache(_rotateRoot); Cache(_rotateX); Cache(_rotateY); Cache(_rotateZ);
        Cache(_scaleRoot); Cache(_scaleCenter); Cache(_scaleX); Cache(_scaleY); Cache(_scaleZ);
    }

    private void Cache(Transform t)
    {
        if (t == null || _defaultStates.ContainsKey(t)) return;
        _defaultStates.Add(t, new TransformState
        {
            Parent         = t.parent,
            LocalPosition  = t.localPosition,
            LocalRotation  = t.localRotation,
            LocalScale     = t.localScale,
        });
    }

    private class TransformState
    {
        public Transform  Parent;
        public Vector3    LocalPosition;
        public Quaternion LocalRotation;
        public Vector3    LocalScale;
    }
}
```

Note: this **renames** the class from `TransformGizmoHierarchyController` to `GizmoHierarchy`. The prefab `Vr3D_Gizmos.prefab` will lose its script reference until the user re-binds it (covered in Phase 3 user-action).

- [ ] **Step 3: Verify** — Unity recompiles. Console may show a "missing script" warning on `Vr3D_Gizmos.prefab` — expected, fixed in Phase 3.

---

## Task 2.11: GizmoHandle — rewrite

**Files:**
- Modify (replace fully): `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoHandle.cs` (move/rename from `Assets/_App/Subsystems/VrInteraction/GizmoHandle.cs`)
- Delete: `Assets/_App/Subsystems/VrInteraction/GizmoHandle.cs.meta` after move

- [ ] **Step 1: Move file**

User-action: in Project window, drag `Assets/_App/Subsystems/VrInteraction/GizmoHandle.cs` into `Assets/_App/Subsystems/VrInteraction/Gizmo/`.

- [ ] **Step 2: Rewrite content fully**

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GizmoHandle : XRBaseInteractable
{
    [SerializeField] private HandleKind _kind;
    [SerializeField] private AxisKind   _axis;

    public HandleKind Kind => _kind;
    public AxisKind   Axis => _axis;

    private GizmoActivator _activator;
    private NearFarInteractor _locked;
    private NearFarInteractor _lastHovering;

    private enum HandleState { Idle, Dragging }
    private HandleState _state;

    protected override void Awake()
    {
        base.Awake();
        _activator = GetComponentInParent<GizmoActivator>();
        // base.Awake auto-adds child colliders; keep just same-GO collider for hit-testing precision
        colliders.Clear();
        foreach (var c in GetComponents<Collider>())
            if (c != null && !colliders.Contains(c)) colliders.Add(c);
    }

    public override bool IsSelectableBy(IXRSelectInteractor _) => false;

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        if (_state == HandleState.Dragging && (_locked == null || !_locked.isActiveAndEnabled))
        {
            _activator?.OnHandleAborted();
            _state  = HandleState.Idle;
            _locked = null;
            return;
        }

        switch (_state)
        {
            case HandleState.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null || !IsPrimaryFor(ni)) break;
                if (ni.selectInput.ReadWasPerformedThisFrame())
                {
                    _locked = ni;
                    _state  = HandleState.Dragging;
                    var attach = ni.GetAttachTransform(this);
                    _activator?.OnHandleGrabbed(this, attach.position, attach.rotation);
                }
                break;

            case HandleState.Dragging:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                {
                    _activator?.OnHandleReleased();
                    _locked = null;
                    _state  = HandleState.Idle;
                    break;
                }
                var a = _locked.GetAttachTransform(this);
                _activator?.OnHandleDragged(a.position, a.rotation);
                break;
        }
    }

    private void UpdateLastHovering()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) { _lastHovering = ni; return; }
        }
    }

    private NearFarInteractor CurrentHoverer()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) return ni;
        }
        return _lastHovering != null && _lastHovering.isActiveAndEnabled ? _lastHovering : null;
    }

    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false;
        }
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);
        return false;
    }
}
```

- [ ] **Step 3: Verify** — Unity recompiles. Old prefab references on `Vr3D_Gizmos` handles (if any) may show missing script warnings — handled in Phase 3.

---

## Task 2.12: GizmoActivator — orchestrator

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/GizmoActivator.cs`

- [ ] **Step 1: Write GizmoActivator**

```csharp
using UnityEngine;
using VContainer;

public class GizmoActivator : MonoBehaviour
{
    [SerializeField] private GizmoConfig _config;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private GizmoController   _gizmoController;

    private bool       _panelOpen;
    private GizmoMode  _mode = GizmoMode.Move;
    private Transform  _target;
    private string     _targetNodeId;

    private GameObject     _instance;
    private GizmoHierarchy _hierarchy;
    private Collider       _originalTargetCollider;

    private bool                _dragActive;
    private GizmoHandle         _activeHandle;
    private IGizmoDragStrategy  _activeStrategy;
    private Vector3             _originalPos;
    private Quaternion          _originalRot;
    private Vector3             _originalScale;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, GizmoController gizmoController)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _gizmoController = gizmoController;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
        _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
        _bus.Subscribe<GizmoModeChangedEvent>(OnModeChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
        _bus.Unsubscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
        _bus.Unsubscribe<GizmoModeChangedEvent>(OnModeChanged);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_instance != null) Despawn();
    }

    private void Start()
    {
        // Initial target snapshot from current selection (if any).
        var id = _selection?.SelectedNodeId;
        if (id != null) _target = _graph?.GetNode(id)?.transform;
        _targetNodeId = id;
    }

    private void LateUpdate()
    {
        if (_instance == null || _dragActive) return;
        if (_target == null) { Despawn(); return; }
        _instance.transform.position = _target.position;
        _instance.transform.rotation = _target.rotation;
    }

    private void OnPanelOpened(GizmoToolsPanelOpenedEvent _)
    {
        _panelOpen = true;
        _mode      = GizmoMode.Move;
        RefreshVisibility();
    }

    private void OnPanelClosed(GizmoToolsPanelClosedEvent _)
    {
        if (_dragActive) return;
        _panelOpen = false;
        RefreshVisibility();
    }

    private void OnModeChanged(GizmoModeChangedEvent e)
    {
        if (_dragActive) return;
        _mode = e.Mode;
        if (_instance != null && _hierarchy != null) _hierarchy.ShowMode(_mode);
    }

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (_dragActive) return;
        _target       = (e.SelectedNodeId != null) ? _graph?.GetNode(e.SelectedNodeId)?.transform : null;
        _targetNodeId = e.SelectedNodeId;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool shouldShow = _panelOpen && _target != null;
        if (shouldShow && _instance == null)       Spawn();
        else if (!shouldShow && _instance != null) Despawn();
        else if (shouldShow && _instance != null)  { Despawn(); Spawn(); }
    }

    private void Spawn()
    {
        if (_config == null || _config.GizmoPrefab == null)
        {
            Debug.LogError("GizmoActivator: GizmoConfig missing or prefab null — gizmo disabled.");
            return;
        }
        _instance = Instantiate(_config.GizmoPrefab);
        _instance.transform.position = _target.position;
        _instance.transform.rotation = _target.rotation;

        _originalTargetCollider = _target.GetComponent<Collider>();
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = false;

        var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
        _instance.transform.localScale = Vector3.one * size;

        _hierarchy = _instance.GetComponent<GizmoHierarchy>();
        if (_hierarchy != null) _hierarchy.ShowMode(_mode);
    }

    private void Despawn()
    {
        if (_dragActive) AbortDrag();
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = true;
        _originalTargetCollider = null;
        if (_instance != null) Destroy(_instance);
        _instance  = null;
        _hierarchy = null;
    }

    public void OnHandleGrabbed(GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
        if (_dragActive || _target == null) return;
        _dragActive     = true;
        _activeHandle   = handle;
        _originalPos    = _target.position;
        _originalRot    = _target.rotation;
        _originalScale  = _target.localScale;
        _activeStrategy = ResolveStrategy(handle);
        _hierarchy?.OnHandleGrabbed(handle);
        _activeStrategy.BeginDrag(_target, handle.Axis, handPos, handRot);
        _bus.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
    }

    public void OnHandleDragged(Vector3 handPos, Quaternion handRot)
    {
        if (!_dragActive) return;
        if (_target == null) { OnHandleAborted(); return; }
        _activeStrategy?.UpdateDrag(handPos, handRot);
    }

    public void OnHandleReleased()
    {
        if (!_dragActive) return;
        _activeStrategy?.EndDrag();
        var currentMode = _mode;
        _hierarchy?.OnHandleReleased(currentMode);
        var finalPos   = _target.position;
        var finalRot   = _target.rotation;
        var finalScale = _target.localScale;
        // Restore to original so TransformCommand.ctor captures the correct _old snapshot.
        _target.position   = _originalPos;
        _target.rotation   = _originalRot;
        _target.localScale = _originalScale;
        _gizmoController.CommitTransform(_target, finalPos, finalRot, finalScale);
        // Refit after scale commits — bounds may have changed.
        if (_instance != null)
        {
            var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
            _instance.transform.localScale = Vector3.one * size;
        }
        EndDragInternal();
    }

    public void OnHandleAborted()
    {
        if (!_dragActive) return;
        AbortDrag();
    }

    private void AbortDrag()
    {
        if (_target != null)
        {
            _target.position   = _originalPos;
            _target.rotation   = _originalRot;
            _target.localScale = _originalScale;
        }
        _activeStrategy?.EndDrag();
        if (_hierarchy != null) _hierarchy.OnHandleReleased(_mode);
        EndDragInternal();
    }

    private void EndDragInternal()
    {
        var id = _targetNodeId;
        _activeStrategy = null;
        _activeHandle   = null;
        _dragActive     = false;
        _bus.Publish(new GizmoDragEndedEvent { TargetNodeId = id });
    }

    private IGizmoDragStrategy ResolveStrategy(GizmoHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:     return new AxisMoveStrategy();
            case HandleKind.ScaleAxis:    return new AxisScaleStrategy();
            case HandleKind.ScaleUniform: return new UniformScaleStrategy();
            case HandleKind.RotateRing:   return new RingRotateStrategy();
            default:                      return new AxisMoveStrategy();
        }
    }
}
```

- [ ] **Step 2: Verify** — Unity recompiles, no errors.

---

## Task 2.13: GizmoActivator state tests

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Tests/GizmoActivatorStateTests.cs`

These tests exercise the part of `GizmoActivator` that's testable without a real prefab — specifically the single-handle lock invariant in `OnHandleGrabbed` re-entrancy.

- [ ] **Step 1: Write tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class GizmoActivatorStateTests
{
    private GameObject _activatorGo;
    private GizmoActivator _sut;

    [SetUp]
    public void SetUp()
    {
        _activatorGo = new GameObject("activator");
        _sut         = _activatorGo.AddComponent<GizmoActivator>();
        // Strategies require a target — but for these tests we only exercise re-entrancy
        // guards which short-circuit before strategy runs.
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_activatorGo);

    [Test]
    public void OnHandleGrabbed_WithoutTarget_DoesNotThrow()
    {
        // _target is null (no selection wired) — call must early-return.
        Assert.DoesNotThrow(() => _sut.OnHandleGrabbed(null, Vector3.zero, Quaternion.identity));
    }

    [Test]
    public void OnHandleReleased_WithoutActiveDrag_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleReleased());
    }

    [Test]
    public void OnHandleDragged_WithoutActiveDrag_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleDragged(Vector3.zero, Quaternion.identity));
    }
}
```

Full state-table tests (panelOpen × target × mode) require event-bus + scene-graph + DI wiring which is heavy for unit scope. The above smoke tests cover the cheap-to-test guards; integration coverage is the manual smoke test in Phase 3.

- [ ] **Step 2: Run tests, expect PASS** (or FAIL with helpful message if guards missing — fix Activator if so).

---

## Task 2.14: GizmoToolsPanel UI

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/UI/GizmoToolsPanel.cs`

- [ ] **Step 1: Write GizmoToolsPanel**

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class GizmoToolsPanel : MonoBehaviour
{
    [SerializeField] private Button _moveButton;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _scaleButton;
    [Header("Optional visual feedback")]
    [SerializeField] private GameObject _moveActiveIndicator;
    [SerializeField] private GameObject _rotateActiveIndicator;
    [SerializeField] private GameObject _scaleActiveIndicator;

    private EventBus  _bus;
    private GizmoMode _current = GizmoMode.Move;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()
    {
        if (_moveButton   != null) _moveButton  .onClick.AddListener(() => SelectMode(GizmoMode.Move));
        if (_rotateButton != null) _rotateButton.onClick.AddListener(() => SelectMode(GizmoMode.Rotate));
        if (_scaleButton  != null) _scaleButton .onClick.AddListener(() => SelectMode(GizmoMode.Scale));
    }

    private void OnEnable()
    {
        _current = GizmoMode.Move;
        UpdateIndicators();
        if (_bus != null)
        {
            _bus.Subscribe<GizmoDragStartedEvent>(OnDragStarted);
            _bus.Subscribe<GizmoDragEndedEvent>(OnDragEnded);
            _bus.Publish(new GizmoToolsPanelOpenedEvent());
        }
    }

    private void OnDisable()
    {
        if (_bus != null)
        {
            _bus.Unsubscribe<GizmoDragStartedEvent>(OnDragStarted);
            _bus.Unsubscribe<GizmoDragEndedEvent>(OnDragEnded);
            _bus.Publish(new GizmoToolsPanelClosedEvent());
        }
    }

    private void SelectMode(GizmoMode mode)
    {
        _current = mode;
        UpdateIndicators();
        _bus?.Publish(new GizmoModeChangedEvent { Mode = mode });
    }

    private void UpdateIndicators()
    {
        if (_moveActiveIndicator   != null) _moveActiveIndicator  .SetActive(_current == GizmoMode.Move);
        if (_rotateActiveIndicator != null) _rotateActiveIndicator.SetActive(_current == GizmoMode.Rotate);
        if (_scaleActiveIndicator  != null) _scaleActiveIndicator .SetActive(_current == GizmoMode.Scale);
    }

    private void OnDragStarted(GizmoDragStartedEvent _) => SetButtonsInteractable(false);
    private void OnDragEnded  (GizmoDragEndedEvent   _) => SetButtonsInteractable(true);

    private void SetButtonsInteractable(bool value)
    {
        if (_moveButton   != null) _moveButton  .interactable = value;
        if (_rotateButton != null) _rotateButton.interactable = value;
        if (_scaleButton  != null) _scaleButton .interactable = value;
    }
}
```

- [ ] **Step 2: Verify** — Unity recompiles.

---

## Task 2.15: GizmoToolsPanelOpener

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/Gizmo/UI/GizmoToolsPanelOpener.cs`

- [ ] **Step 1: Write GizmoToolsPanelOpener**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class GizmoToolsPanelOpener : MonoBehaviour
{
    [SerializeField] private Button     _toggleButton;
    [SerializeField] private GameObject _subPanel;

    private void Awake()
    {
        if (_toggleButton != null) _toggleButton.onClick.AddListener(Toggle);
    }

    private void Toggle()
    {
        if (_subPanel == null) return;
        _subPanel.SetActive(!_subPanel.activeSelf);
    }
}
```

- [ ] **Step 2: Verify** — Unity recompiles.

---

## Task 2.16: UndoKeyHandler — block undo during drag

**Files:**
- Modify: `Assets/_App/Bootstrap/UndoKeyHandler.cs`

- [ ] **Step 1: Replace UndoKeyHandler**

```csharp
using UnityEngine;
using VContainer;

public class UndoKeyHandler : MonoBehaviour
{
    private CommandStack _commandStack;
    private EventBus     _bus;
    private bool         _dragActive;

    [Inject]
    public void Construct(CommandStack commandStack, EventBus bus)
    {
        _commandStack = commandStack;
        _bus          = bus;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<GizmoDragStartedEvent>(OnDragStarted);
        _bus.Subscribe<GizmoDragEndedEvent>(OnDragEnded);
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GizmoDragStartedEvent>(OnDragStarted);
        _bus.Unsubscribe<GizmoDragEndedEvent>(OnDragEnded);
    }

    private void OnDragStarted(GizmoDragStartedEvent _) => _dragActive = true;
    private void OnDragEnded  (GizmoDragEndedEvent   _) => _dragActive = false;

    private void Update()
    {
        if (_dragActive) return;
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.Z))
            _commandStack.Undo();
    }
}
```

- [ ] **Step 2: Verify** — Unity recompiles.

---

## Task 2.17: Bootstrap DI registration

**Files:**
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/SandboxSceneScope.cs`

- [ ] **Step 1: Update VrEditingSceneScope**

Open `Assets/_App/Bootstrap/VrEditingSceneScope.cs`. Add to top of file:

```csharp
[SerializeField] private GizmoConfig _gizmoConfig;
```

(merge with existing `_panelRegistry` declaration).

In `Configure(IContainerBuilder builder)`, after the existing `RegisterInstance(_panelRegistry)`:

```csharp
        if (_gizmoConfig != null) builder.RegisterInstance(_gizmoConfig);

        var gizmoActivator = Object.FindAnyObjectByType<GizmoActivator>(FindObjectsInactive.Include);
        if (gizmoActivator != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoActivator));

        var gizmoToolsPanel = Object.FindAnyObjectByType<GizmoToolsPanel>(FindObjectsInactive.Include);
        if (gizmoToolsPanel != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoToolsPanel));
```

- [ ] **Step 2: Update SandboxSceneScope**

Apply the same diff to `Assets/_App/Bootstrap/SandboxSceneScope.cs`:
- Add `[SerializeField] private GizmoConfig _gizmoConfig;`
- In `Configure`, register `_gizmoConfig`, find/inject `GizmoActivator` and `GizmoToolsPanel`.

- [ ] **Step 3: Verify** — Unity recompiles.

---

# Phase 3 — Manual prefab work + smoke test (user-action)

## Task 3.1: Prefab and scene wiring (user)

Each step here is a **user action in Unity Editor**. The plan describes _what_ needs to happen — the engineer is not expected to run any code for this task.

- [ ] **Step 1: Rebind GizmoHierarchy on Vr3D_Gizmos.prefab**

Open `Assets/Resources/Prefabs/Gizmos/Vr3D_Gizmos.prefab`. The root will show a "Missing Script" component (the renamed `TransformGizmoHierarchyController`). Remove that component, then `Add Component → GizmoHierarchy`. Drag all the existing Transforms into the matching SerializedField slots (Move Root, Move Center, Move X/Y/Z, Rotate Root, Rotate X/Y/Z, Scale Root, Scale Center, Scale X/Y/Z).

- [ ] **Step 2: Add GizmoHandle to each of the 10 handles**

On each of the 10 handle GameObjects under the prefab (Move X/Y/Z, Move Center, Rotate X/Y/Z, Scale X/Y/Z, Scale Center), add a `GizmoHandle` component and set:

| Handle GameObject | Kind | Axis |
|---|---|---|
| MoveGizmo (center) | MoveAxis | X (irrelevant — center is the parent shaft; treat as Move) |
| MoveGizmo_X | MoveAxis | X |
| MoveGizmo_Y | MoveAxis | Y |
| MoveGizmo_Z | MoveAxis | Z |
| RotateGizmo_X | RotateRing | X |
| RotateGizmo_Y | RotateRing | Y |
| RotateGizmo_Z | RotateRing | Z |
| ScaleGizmo (center) | ScaleUniform | X (unused) |
| ScaleGizmo_X | ScaleAxis | X |
| ScaleGizmo_Y | ScaleAxis | Y |
| ScaleGizmo_Z | ScaleAxis | Z |

Each handle GameObject must have a Collider for raycast. Verify Colliders exist; if missing, add `BoxCollider` sized to the handle visual.

> Note: in this prefab `MoveGizmo` (the shaft) is the "active when nothing else is grabbed" parent. For drag purposes it behaves like the X-arrow's drag — but in our model, the shaft itself shouldn't be grabbable to drive a move (only the three arrows do). If `MoveGizmo` has a visible collider, **remove or disable that collider** to avoid an undefined drag. Only `MoveGizmo_X/Y/Z` should be grabbable for Move mode. Same logic for Rotate (only rings grabbable) and Scale (3 axis cubes + 1 center cube grabbable).

- [ ] **Step 3: Create GizmoConfig asset**

In Project window: right-click `Assets/_App/Subsystems/VrInteraction/Gizmo/` → `Create → PromeonLab → Gizmo Config`. Name it `GizmoConfig.asset`. Drag `Vr3D_Gizmos.prefab` into the `_gizmoPrefab` slot. Leave defaults: BoundsCoefficient=1.5, MinSize=0.1, MaxSize=5.

- [ ] **Step 4: Create scene GO `_Gizmo` in VrEditing.unity**

Open `Assets/_App/Scenes/VrEditing.unity`. At scene root, create new empty GameObject named `_Gizmo`. Add `GizmoActivator` component. Drag `GizmoConfig.asset` into its `_config` slot.

On the `VrEditingSceneScope` GameObject in the same scene, drag `GizmoConfig.asset` into the `_gizmoConfig` field (new field from Task 2.17).

- [ ] **Step 5: Create scene GO `_Gizmo` in Sandbox.unity**

Open `Assets/_App/Scenes/Sandbox.unity`. Repeat Step 4 for this scene.

- [ ] **Step 6: Create GizmoToolsPanel prefab**

Duplicate one of the existing simple sub-panels in `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/` as a template. Save as `GizmoToolsPanel.prefab` in the same folder. Inside the panel:
- Three buttons: Move, Rotate, Scale (text labels).
- Add `GizmoToolsPanel` component to the prefab root.
- Drag the three buttons into Move/Rotate/Scale Button slots.
- Optionally: a small highlight/indicator GameObject per button — drag into the `*ActiveIndicator` slots.

- [ ] **Step 7: Add `Gizmo Tools` button to UserPanel**

Open `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab`. Add a new button labelled "Gizmo Tools". Place an instance of `GizmoToolsPanel.prefab` somewhere in the UserPanel hierarchy (initially `SetActive(false)`). Add `GizmoToolsPanelOpener` component to the new button. Drag the button into `_toggleButton` slot, drag the GizmoToolsPanel instance into `_subPanel`.

- [ ] **Step 8: Verify scene wiring**

Open `VrEditing.unity`, press Play. Console should be free of red errors. UserPanel should show the new "Gizmo Tools" button.

## Task 3.2: Smoke test (user)

In Quest 3 or via Editor + simulator:

- [ ] **Step 1**: Load `VrEditing` scene, select a model in the scene.
- [ ] **Step 2**: Open Gizmo Tools sub-panel → gizmo appears around the selected object, fitted to bounds, Move arrows visible.
- [ ] **Step 3**: Grip + drag the X arrow → object moves along local X only. Release → committed.
- [ ] **Step 4**: Click Rotate button in sub-panel → arrows hide, rings appear.
- [ ] **Step 5**: Grip + drag the Y ring → object rotates around local Y.
- [ ] **Step 6**: Click Scale → drag X-cube → x-scale only. Drag center cube → uniform scale.
- [ ] **Step 7**: Select a different object → gizmo migrates, mode preserved.
- [ ] **Step 8**: Close Gizmo Tools sub-panel → gizmo disappears.
- [ ] **Step 9**: Re-open → gizmo back in Move mode (default).
- [ ] **Step 10**: Hit Undo (Ctrl-Z in Editor; bound input in build) → each drag rolls back as one atomic step.
- [ ] **Step 11**: Phase 1 manual: outside Gizmo Tools, on a different selected object — tap trigger selects, hold trigger rotates, hold grip moves.
- [ ] **Step 12**: Repeat steps 1-7 in `Sandbox.unity`.

If any step fails, file the failure mode in `docs/developer-notes/2026-05-21-vr-gizmo-bugs.md` (new file) for follow-up.

---

# Phase 4 — Memory updates

## Task 4.1: Update memory entries

User-action (assistant performs file writes; user just confirms):

- [ ] **Step 1: Update `project_interaction_input_model.md`**

Path: `C:\Users\maksp\.claude\projects\S---02--Projects--02--Study--00--Repositories-PromeonLab\memory\project_interaction_input_model.md`

Update body to:

```
XRPromeonInteractable input model (post-2026-05-21):
- tap trigger (<tapWindow ≈ 0.5s) = select
- hold trigger (>tapWindow) = rotate
- hold grip = move
- XRI standard select-flow disabled (IsSelectableBy → false); manipulation requires the object to be already-selected
```

**Why:** New gizmo system inverts trigger/grip semantics. Hold-trigger now rotates (was move); hold-grip now moves (was rotate). Tap-trigger select unchanged.

**How to apply:** if you see anything in the codebase that says trigger=move or grip=rotate — that's stale, the swap happened.

- [ ] **Step 2: Add `project_gizmo_system.md`**

Path: `C:\Users\maksp\.claude\projects\S---02--Projects--02--Study--00--Repositories-PromeonLab\memory\project_gizmo_system.md`

Create with frontmatter:

```
---
name: gizmo-system
description: "3D gizmo system for VR object manipulation — manual toggle via UserPanel sub-panel, single-handle drag with axis constraints, commits via CommandStack"
metadata:
  node_type: memory
  type: project
---

VR gizmo system shipped 2026-05-21. Lives in `Assets/_App/Subsystems/VrInteraction/Gizmo/`.

**Activation:** UserPanel → "Gizmo Tools" button opens sub-panel with 3 buttons (Move/Rotate/Scale). Visible while sub-panel open AND object selected. Closes sub-panel = gizmo hides.

**Drag mechanics:** grip-hold on handle = drag, release = commit via `GizmoController.CommitTransform → TransformCommand → CommandStack`. Delta-math (target not reparented). Local axes (snapshot of `target.right/up/forward` at BeginDrag).

**Strategies (pure C#, EditMode-testable):** `AxisMoveStrategy`, `AxisScaleStrategy`, `UniformScaleStrategy`, `RingRotateStrategy`.

**Single-select hard-cleanup:** as part of this work, `SelectionManager` was reduced to single-select. `Toggle`, `SelectedIds`, `SelectedNodeIds`, `SelectionVisual.InSet` deleted.

**Why:** designed and shipped per `docs/superpowers/specs/2026-05-21-vr-gizmo-system-design.md`.

**How to apply:** when working with selection — use `ISelectionManager.SelectedNodeId` (string?), `Select(id)`, `Select(null)`. When working with gizmo — `GizmoActivator` is the orchestrator entry point. Memory entry [[interaction-input-model]] covers the related XRPromeonInteractable input swap.
```

- [ ] **Step 3: Update MEMORY.md index**

Path: `C:\Users\maksp\.claude\projects\S---02--Projects--02--Study--00--Repositories-PromeonLab\memory\MEMORY.md`

Append line:

```
- [Gizmo system](project_gizmo_system.md) — 3D gizmo via UserPanel sub-panel; grip-drag, axis-constrained; single-select after cleanup
```

- [ ] **Step 4**: User-action — review updated memory entries.

---

## Self-review (post-write)

**1. Spec coverage**

| Spec section | Plan tasks |
|---|---|
| §2 UX (toggle, 3 buttons, default Move) | 2.14 (Panel), 2.15 (Opener), 3.1 (manual wiring) |
| §3 Architecture / components | 2.1-2.17 |
| §4 Lifecycle (Spawn/Despawn/Follow/Fit) | 2.12 GizmoActivator |
| §5 Drag flow | 2.5-2.8 strategies + 2.12 Activator callbacks |
| §6 Hierarchy switching | 2.10 GizmoHierarchy |
| §7 Integrations (Phase 0 + Phase 1 + Undo guard) | 0.1 + 1.1 + 2.16 |
| §8 Edge cases | 2.12 Activator (single-handle lock, target-null abort, dragActive guards) |
| §9 Testing | 2.5/2.6/2.7/2.8/2.9/2.13 + 3.2 smoke |
| §10 File layout | Header table + 2.1-2.17 |
| §11 Phasing | Phase 0/1/2/3 sections |
| §12 Conventions compliance | implicit in all code blocks (no public fields, `[SerializeField] private`, file=type) |

No spec section is uncovered.

**2. Placeholder scan**

None of the forbidden patterns ("TODO", "implement later", "similar to task N", "add appropriate error handling") appear. Every step has actual code, exact paths, exact verification commands.

**3. Type consistency**

- `IGizmoDragStrategy.BeginDrag(Transform, AxisKind, Vector3, Quaternion)` — consistent across interface (2.4) and all 4 implementations (2.5-2.8) and call site (2.12 `ResolveStrategy` then `BeginDrag`).
- `GizmoHandle.Kind` and `.Axis` — defined in 2.11, consumed in 2.10 (`OnHandleGrabbed`) and 2.12 (`ResolveStrategy`).
- `GizmoHierarchy.OnHandleReleased(GizmoMode)` — 2.10 takes mode parameter; 2.12 passes `_mode`. Match.
- `GizmoActivator.OnHandleAborted()` — 2.12 defines public method; 2.11 (`GizmoHandle.ProcessInteractable`) calls it. Match.
- `BoundsFitter.ComputeSize` — 2.9 defines; 2.12 calls with same signature.
- `ISelectionManager.SelectedNodeId` — 0.1 step 1 defines; consumed in step 7 (XRPromeonInteractable) and step 8 (SceneOutlinerView) and 2.12 (Activator.Start). Match.

Plan complete and saved to `docs/superpowers/plans/2026-05-21-vr-gizmo-system.md`.

---

## Execution options

**1. Subagent-Driven (recommended)** — fresh subagent per task with two-stage review (spec compliance + code quality) between tasks. Best quality, isolated context per task.

**2. Inline Execution** — execute tasks in this same session with batch checkpoints. Faster start-up, but context accumulates.

Which approach?
