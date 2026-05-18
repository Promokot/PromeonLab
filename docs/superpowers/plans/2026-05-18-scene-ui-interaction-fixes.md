# Scene UI & Object Interaction Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **NEVER auto-commit.** The user commits manually. Every task ends with "stage changes; user will commit" — do not invoke `git commit`.

**Goal:** Make scene editing UI usable: inspector understands multi-select and live-rename, outliner stays in sync and sorts by name, empty-space tap deselects without breaking UI clicks, and 3D objects can be tap-selected and hold-grabbed via a new `XRPromeonInteractable`.

**Architecture:** Five surgical changes across three subsystems (`_Shared`, `SpatialUi`, `VrInteraction`). One new event (`NodeRenamedEvent`) decouples live UI feedback from autosave-triggering scene mutations. A new `XRBaseInteractable` subclass — `XRPromeonInteractable` — runs a Pressed→Dragging state machine to discriminate tap vs hold without inheriting `XRGrabInteractable`'s auto-parent semantics. Movement commits through the existing `CommandStack` via an extended `GizmoController.CommitTransform`.

**Tech Stack:** Unity 6000.3.7f1, C# 9, VContainer DI, MessagePipe-style `EventBus`, XR Interaction Toolkit 3.x (`NearFarInteractor`), TextMeshPro UI, NUnit Test Runner.

**Spec:** `docs/superpowers/specs/2026-05-18-scene-ui-interaction-fixes-design.md`

---

## File Structure

### New files

- `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs` — interface + `SingleDragStrategy` impl, extension seam for future group-drag
- `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` — tap-vs-hold interactable

### Modified files

- `Assets/_App/_Shared/Events/AppEvents.cs` — +1 struct (`NodeRenamedEvent`)
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs` — +`SetLabel(string)`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs` — alphabetical sort, `NodeRenamedEvent` subscription
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` — three-state branching, live rename, "Unnamed" fallback
- `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab` — manual: add `MultiState` GameObject
- `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` — `IsOverUI` guard
- `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs` — swap interactor type, take `GizmoController` dep
- `Assets/_App/Subsystems/VrInteraction/GizmoController.cs` — +`CommitTransform`

### Deleted files

- `Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs` (+`.meta`)

---

## Verification cadence

After every code change touching C# files:
1. Save the file.
2. Return to Unity Editor — wait for domain reload to finish.
3. Check `Window > General > Console`: zero compile errors.

"Stage changes" means `git add <paths>` only. **Do not run `git commit`** — the user commits manually after reviewing the staged diff.

---

## Task 1 — Add `NodeRenamedEvent`

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`

- [x] **Step 1.1 — Add the event struct**

After the `SelectionChangedEvent` line, add:

```csharp
public struct NodeRenamedEvent       { public string NodeId; public string NewName; }
```

- [x] **Step 1.2 — Verify compile in Unity Console**
- [ ] **Step 1.3 — Stage:** `git add Assets/_App/_Shared/Events/AppEvents.cs`

---

## Task 2 — Add `OutlinerItem.SetLabel`

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs`

- [x] **Step 2.1 — Add the method**

After `SetVisualState`, add:

```csharp
public void SetLabel(string newName)
{
    if (_label != null) _label.text = newName;
}
```

- [x] **Step 2.2 — Verify compile in Unity Console**
- [ ] **Step 2.3 — Stage:** `git add Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs`

---

## Task 3 — Outliner: alphabetical sort + `NodeRenamedEvent` subscription

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs`

- [x] **Step 3.1 — Add `using System;`** at top.

- [x] **Step 3.2 — Subscribe/unsubscribe to `NodeRenamedEvent` in `OnEnable`/`OnDisable`.**

- [x] **Step 3.3 — Add targeted-update handler:**

```csharp
private void OnNodeRenamed(NodeRenamedEvent e)
{
    if (_rowsRoot == null) return;
    foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
        if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
}
```

- [x] **Step 3.4 — Sort children alphabetically in `Rebuild`** before `AddRowsRecursive`:

```csharp
foreach (var list in byParent.Values)
    list.Sort((a, b) => string.Compare(
        a.DisplayName ?? "", b.DisplayName ?? "",
        StringComparison.OrdinalIgnoreCase));
```

- [x] **Step 3.5 — Verify compile in Unity Console**
- [ ] **Step 3.6 — Stage:** `git add Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs`

---

## Task 4 — Inspector: three-state + live rename + "Unnamed" fallback

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs`

- [ ] **Step 4.1 — Add `_multiState` + `_multiCountLabel` serialized fields**

After `_content`, before `_nameField`:

```csharp
[SerializeField] private GameObject _multiState;
[SerializeField] private TMP_Text   _multiCountLabel;
```

- [ ] **Step 4.2 — Swap rename subscription**

Replace `OnEnable` body so it subscribes to BOTH callbacks:

```csharp
if (_nameField != null)
{
    _nameField.onValueChanged.AddListener(OnNameLiveEdit);
    _nameField.onEndEdit.AddListener(OnNameCommit);
}
```

Mirror unsubscribe in `OnDisable`.

- [ ] **Step 4.3 — Three-state `Refresh`**

Replace `Refresh` with the three-branch version. Pseudocode:

```csharp
var count = _selection.SelectedIds?.Count ?? 0;
var state = count == 0 ? InspectorState.Empty
          : count == 1 ? InspectorState.Single
                       : InspectorState.Multi;

_emptyState?.SetActive(state == InspectorState.Empty);
_content   ?.SetActive(state == InspectorState.Single);
_multiState?.SetActive(state == InspectorState.Multi);

if (state == InspectorState.Multi && _multiCountLabel != null)
    _multiCountLabel.text = $"Multiple Objects Selected ({count})";

if (state != InspectorState.Single) { _bound = null; return; }

_bound = _graph.GetNode(_selection.ActiveId);
if (_bound == null) return;

// existing single-bind code (name + transform labels)
```

Add private `enum InspectorState { Empty, Single, Multi }` inside the class.

- [ ] **Step 4.4 — Replace `OnNameChanged` with `OnNameLiveEdit` + `OnNameCommit`**

```csharp
private void OnNameLiveEdit(string newName)
{
    if (_bound == null) return;
    if (string.IsNullOrWhiteSpace(newName)) return;
    var trimmed = newName.Trim();
    _bound.SetDisplayName(trimmed);
    _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = trimmed });
}

private void OnNameCommit(string newName)
{
    if (_bound == null) return;
    string finalName;
    if (string.IsNullOrWhiteSpace(newName))
    {
        finalName = "Unnamed";
        _bound.SetDisplayName(finalName);
        if (_nameField != null) _nameField.SetTextWithoutNotify(finalName);
    }
    else
    {
        finalName = newName.Trim();
        _bound.SetDisplayName(finalName);
    }
    _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = finalName });
    _bus?.Publish(new SceneModifiedEvent());
}
```

- [ ] **Step 4.5 — Verify compile in Unity Console**
- [ ] **Step 4.6 — Stage:** `git add Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs`

---

## Task 5 — Prefab: add `MultiState` to `SceneInspectorModule.prefab`

Manual Unity Editor task.

- [ ] **Step 5.1** — Open `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab`.
- [ ] **Step 5.2** — Locate existing `EmptyState` / `Content` siblings (parent of inspector content).
- [ ] **Step 5.3** — Create sibling `MultiState` GameObject:
  - Child `CountLabel` with `TextMeshProUGUI` ("Multiple Objects Selected" placeholder, centered, matching empty-state styling).
  - Stretch `MultiState` to fill same rect.
- [ ] **Step 5.4** — On root with `SceneInspectorView`: drag `MultiState` GO → `Multi State` slot, drag `CountLabel` text → `Multi Count Label` slot.
- [ ] **Step 5.5** — Disable `MultiState` GameObject by default.
- [ ] **Step 5.6** — Save prefab (`Ctrl+S`).
- [ ] **Step 5.7** — Stage: `git add Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab`

After this task: Item 1 + Item 2 are fully functional. Recommend smoke test in Editor Play mode before continuing.

---

## Task 6 — `WorldClickCatcher`: UI-hit guard

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs`

- [ ] **Step 6.1 — Add usings:** `UnityEngine.EventSystems`, `UnityEngine.XR.Interaction.Toolkit.UI`.

- [ ] **Step 6.2 — Add `IsOverUI` helper:**

```csharp
private static bool IsOverUI(NearFarInteractor interactor)
{
    var ray = interactor.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
    if (ray != null && ray.TryGetCurrentUIRaycastResult(out var uiRaycast))
        if (uiRaycast.gameObject != null) return true;

    var module = EventSystem.current != null
        ? EventSystem.current.currentInputModule as XRUIInputModule
        : null;
    if (module != null)
    {
        for (int i = 0; i < 4; i++)
        {
            if (module.GetPointerEventData(i)?.pointerCurrentRaycast.gameObject != null)
                return true;
        }
    }
    return false;
}
```

- [ ] **Step 6.3 — In `Check`, after the `justPressed` early-return, add `if (IsOverUI(interactor)) return;`** and remove the old `Graphic`-in-parent guard inside the hovered loop (UI is now canonically guarded by `IsOverUI`).

- [ ] **Step 6.4 — Verify compile in Unity Console**
- [ ] **Step 6.5 — Stage:** `git add Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs`

---

## Task 7 — `GizmoController.CommitTransform`

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/GizmoController.cs`

- [ ] **Step 7.1 — Refactor `CommitMove`:**

```csharp
public void CommitTransform(
    UnityEngine.Transform   target,
    UnityEngine.Vector3     newPosition,
    UnityEngine.Quaternion  newRotation,
    UnityEngine.Vector3     newScale)
{
    var cmd = new TransformCommand(target, newPosition, newRotation, newScale);
    _commands.Execute(cmd);
}

public void CommitMove(UnityEngine.Transform target, UnityEngine.Vector3 newPosition)
    => CommitTransform(target, newPosition, target.rotation, target.localScale);
```

- [ ] **Step 7.2 — Verify compile in Unity Console** (`GizmoHandle` still calls `CommitMove`, forwards through).
- [ ] **Step 7.3 — Stage:** `git add Assets/_App/Subsystems/VrInteraction/GizmoController.cs`

---

## Task 8 — `IDragStrategy` + `SingleDragStrategy`

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs`

- [ ] **Step 8.1 — Create file:**

```csharp
using UnityEngine;

public enum DragMode { PositionOnly, SixDof }

public interface IDragStrategy
{
    void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode);
}

public class SingleDragStrategy : IDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        self.position = targetPos;
        if (mode == DragMode.SixDof)
            self.rotation = targetRot;
    }
}
```

- [ ] **Step 8.2 — Verify compile in Unity Console**
- [ ] **Step 8.3 — Stage:** `git add Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs.meta`

---

## Task 9 — `XRPromeonInteractable`

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs`

- [ ] **Step 9.1 — Create file:**

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

[RequireComponent(typeof(Collider))]
public class XRPromeonInteractable : XRBaseInteractable
{
    [SerializeField] private float _tapWindow     = 0.25f;
    [SerializeField] private float _moveThreshold = 0.03f;
    [SerializeField] private float _nearDistance  = 0.30f;

    private ISelectionManager _selectionManager;
    private GizmoController   _gizmoController;
    private IDragStrategy     _dragStrategy = new SingleDragStrategy();

    private enum State { Idle, Pressed, Dragging }
    private State              _state;
    private float              _pressTime;
    private Vector3            _attachStartPos;
    private Vector3            _grabPosOffset;
    private Quaternion         _grabRotOffset;
    private DragMode           _dragMode;
    private IXRSelectInteractor _selectingInteractor;

    [Inject]
    public void Construct(ISelectionManager selectionManager, GizmoController gizmoController)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        _selectingInteractor = args.interactorObject;
        _state               = State.Pressed;
        _pressTime           = Time.time;

        var attach = _selectingInteractor.GetAttachTransform(this);
        _attachStartPos = attach.position;
        _grabPosOffset  = transform.position - attach.position;
        _grabRotOffset  = Quaternion.Inverse(attach.rotation) * transform.rotation;

        var interactorTransform = (args.interactorObject as MonoBehaviour)?.transform;
        var distance            = interactorTransform != null
            ? Vector3.Distance(interactorTransform.position, transform.position)
            : float.MaxValue;
        _dragMode = distance <= _nearDistance ? DragMode.SixDof : DragMode.PositionOnly;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
        if (_selectingInteractor == null) return;

        var attach = _selectingInteractor.GetAttachTransform(this);

        if (_state == State.Pressed)
        {
            var moved = Vector3.Distance(attach.position, _attachStartPos);
            var held  = Time.time - _pressTime;
            if (held > _tapWindow || moved > _moveThreshold)
                _state = State.Dragging;
        }

        if (_state == State.Dragging)
        {
            var targetPos = attach.position + _grabPosOffset;
            var targetRot = attach.rotation * _grabRotOffset;
            _dragStrategy.Apply(transform, targetPos, targetRot, _dragMode);
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (_state == State.Pressed)
        {
            var sel = GetComponentInParent<Selectable>();
            if (sel != null && _selectionManager != null)
                _selectionManager.Toggle(sel.NodeId);
        }
        else if (_state == State.Dragging && _gizmoController != null)
        {
            _gizmoController.CommitTransform(transform,
                transform.position, transform.rotation, transform.localScale);
        }

        _state               = State.Idle;
        _selectingInteractor = null;
    }
}
```

- [ ] **Step 9.2 — Verify compile in Unity Console.** If a namespace cast fails, adjust usings (XRI 3.x splits across `Interactables`, `Interactors`, and root `UnityEngine.XR.Interaction.Toolkit`).
- [ ] **Step 9.3 — Stage:** `git add Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs.meta`

---

## Task 10 — `SelectionInteractorFactory` swap + delete legacy

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs`
- Delete: `Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs` (+`.meta`)

- [ ] **Step 10.1 — Rewrite the factory:**

```csharp
using UnityEngine;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly ISelectionManager _selectionManager;
    private readonly GizmoController   _gizmoController;

    public SelectionInteractorFactory(ISelectionManager selectionManager, GizmoController gizmoController)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
    }

    public void MakeInteractable(GameObject go, AssetCapabilities capabilities)
    {
        if ((capabilities & AssetCapabilities.Selectable) == 0)
            return;

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var sn  = go.GetComponent<SceneNode>();
        var sel = go.GetComponent<Selectable>() ?? go.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        var xri = go.GetComponent<XRPromeonInteractable>() ?? go.AddComponent<XRPromeonInteractable>();
        xri.Construct(_selectionManager, _gizmoController);
    }
}
```

(VContainer auto-resolves the new `GizmoController` constructor parameter — both scopes already register it as `Scoped`.)

- [ ] **Step 10.2 — Delete obsolete files:**

```bash
rm "Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs"
rm "Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs.meta"
```

(No remaining references — the new factory uses `XRPromeonInteractable`.)

- [ ] **Step 10.3 — Verify compile in Unity Console**
- [ ] **Step 10.4 — Stage:**

```bash
git add Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs
git add Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs
git add Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs.meta
```

(`git add` of a deleted file stages the deletion.)

---

## Manual smoke test (after all tasks staged + committed)

Build & Run on Quest 3, open VrEditing (or Sandbox) scene.

| Check | Steps |
|---|---|
| Multi-select inspector | Tap two objects → inspector says "Multiple Objects Selected (2)". Toggle off one → returns to single-bind. |
| Live rename | Edit field → outliner row updates per keystroke. Commit empty → both show "Unnamed". |
| Outliner sort | Spawn `Zebra`, `Apple`, `Mango` → outliner alphabetical. Rename → reorders on commit. |
| Deselect — empty | Select then tap empty world → deselect. |
| Deselect — UI safe | Select then tap UserPanel button or outliner row → selection survives. |
| Tap select 3D | Quick trigger pull on object → selection toggles, object stays put. |
| Hold grab — ray | Hold trigger + sweep ray → object slides along ray, rotation unchanged. Release → commits (Undo reverts). |
| Hold grab — hand | Direct-hand pinch + move → object follows hand 6DoF. Release → commits. |

Tuning knobs on `XRPromeonInteractable` component if behavior feels off: `_tapWindow`, `_moveThreshold`, `_nearDistance`.

---

## Spec coverage check

| Spec section | Task(s) |
|---|---|
| Item 1 — Multi-select inspector | T4 (code), T5 (prefab) |
| Item 2 — Live rename + targeted outliner update | T1, T2, T3 (sub), T4 |
| Item 3 — Empty-space deselect | T6 |
| Item 4 — `XRPromeonInteractable` | T7, T8, T9, T10 |
| Item 5 — Outliner alphabetical sort | T3 |
