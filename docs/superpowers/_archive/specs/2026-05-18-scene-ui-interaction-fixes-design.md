# Scene UI & Object Interaction Fixes

**Date:** 2026-05-18
**Status:** Approved (pending implementation)

## Goal

Fix four related issues in the scene editing UI:

1. **Inspector multi-select state** — currently silent when >1 object selected.
2. **Inspector rename bug** — rename via inspector field doesn't apply unless Enter pressed, and the outliner doesn't refresh live.
3. **No empty-space deselect** — selection persists until user re-clicks an outliner row; clicking empty space in 3D should clear selection.
4. **No way to interact with 3D objects** — `SelectionInteractor` only handles tap-select; no grab/move; users can't manipulate scene objects at all. Need a temporary unified interactable that supports both ray and direct hand.

## Constraints

- Unity 6000.3.7f1, URP, XR Interaction Toolkit 3.x, OpenXR + Meta OpenXR.
- VContainer DI; events via `EventBus`.
- All user-reversible scene mutations go through `CommandStack`.
- Must not break existing outliner row prefab contract or `SelectionManager` API.
- All custom XR Rig hooks live on the `User XR Origin (XR Rig)` prefab variant (base prefab is not touched). Refs in `WorldClickCatcher` are already wired there.

## Non-Goals

- Full editor-grade multi-grab (group transform) — design leaves an extension seam, but ships single-grab only.
- Rotation/scale gizmo UI rework — out of scope; rotation in drag handled inline per item 4.
- Per-property mixed-value editing in inspector multi-state — multi-state shows only the count, no field editing.
- Snap-to-surface / physics constraints during drag.

---

## Item 1 — Multi-select inspector state

### Current state

`SceneInspectorView` has two root GameObjects (`_emptyState`, `_content`) and reads only `_selection.ActiveId`. When user multi-selects, `ActiveId` still points to the last-added node, so the panel shows that single node's data — there is no signal to the user that multiple objects are selected.

### Change

Add a third root: `_multiState` (GameObject containing a TMP_Text labelled `_multiCountLabel`).

`Refresh()` becomes three-branch:

```csharp
var count = _selection.SelectedIds.Count;
_emptyState ?.SetActive(count == 0);
_content    ?.SetActive(count == 1);
_multiState ?.SetActive(count >  1);

if (count > 1 && _multiCountLabel != null)
    _multiCountLabel.text = $"Multiple Objects Selected ({count})";

if (count == 1) BindSingle(...);   // existing single-bind code
else            _bound = null;
```

Subscription to `SelectionChangedEvent` is unchanged — it already triggers `Refresh()`.

### Prefab change

`SceneInspectorModule.prefab` — add child `MultiState` GameObject with TMP_Text. Wire to new `_multiState` and `_multiCountLabel` SerializeField slots.

---

## Item 2 — Live rename + targeted outliner update

### Current state

`SceneInspectorView` subscribes only to `_nameField.onEndEdit`, which fires only on Enter/blur. `OnNameChanged` calls `_bound.SetDisplayName(newName.Trim())` and publishes `SceneModifiedEvent`. The outliner rebuilds on `SceneModifiedEvent` (full destroy + reinstantiate of all rows). Users see no feedback while typing; on slow-connect HMDs the commit may even appear to "not save" if focus is lost in an unexpected way.

### Change — events

Add to `_Shared/Events/AppEvents.cs`:

```csharp
public struct NodeRenamedEvent { public string NodeId; public string NewName; }
```

This is a UI-refresh hint, not a state-change signal — it intentionally does not trigger autosave.

### Change — inspector

`SceneInspectorView` subscribes to **both** TMP_InputField callbacks:

- `_nameField.onValueChanged` → `OnNameLiveEdit(text)`:
  - If `string.IsNullOrWhiteSpace(text)` → return (don't apply, don't publish). User may be mid-edit; we don't want to flicker the name to "Unnamed" between keystrokes.
  - Else `_bound.SetDisplayName(text.Trim())` and publish `NodeRenamedEvent { NodeId, NewName = text.Trim() }`. No `SceneModifiedEvent`.

- `_nameField.onEndEdit` → `OnNameCommit(text)`:
  - If `string.IsNullOrWhiteSpace(text)` → `_bound.SetDisplayName("Unnamed")`, call `_nameField.SetTextWithoutNotify("Unnamed")`, publish `NodeRenamedEvent { NodeId, NewName = "Unnamed" }`.
  - Always publish `SceneModifiedEvent` at the end of commit (marks scene dirty for autosave).

### Change — outliner

`OutlinerItem` gets `public void SetLabel(string newName)` that updates the row's TMP_Text without re-Binding.

`SceneOutlinerView` adds:

```csharp
_bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
...
private void OnNodeRenamed(NodeRenamedEvent e)
{
    foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
        if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
}
```

Full `Rebuild()` remains on `SceneModifiedEvent` — i.e., on structural changes (add/remove/reparent) and on rename commit (which also re-sorts; see Item 5).

---

## Item 3 — Empty-space deselect

### Current state

`WorldClickCatcher` already exists on `User XR Origin (XR Rig)` variant with `_leftInteractor` / `_rightInteractor` wired. It detects edge-triggered `isSelectActive` on `NearFarInteractor`, walks `interactablesHovered`, and clears selection if no hovered thing is a `Selectable` or a UI `Graphic`.

### The actual bug

XR UI clicks (Canvas buttons under `XRUIInputModule`) do **not** register in `interactablesHovered` — UI hits go through a different path. So when the user taps a button in `UserPanel` or a row in `SceneOutliner`, the catcher sees an empty `interactablesHovered` and calls `Clear()`. Result: selection is wiped every time the user navigates the UI.

### Change

Add a UI-hit guard to `WorldClickCatcher.Check()`:

```csharp
if (IsOverUI(interactor)) return;
```

Implementation of `IsOverUI`:

Preferred: dig out the active ray interactor from `NearFarInteractor` (XRI 3.x: `interactor.uiHoverInteractor` cast to `IUIHoverInteractor`, or `interactor.GetComponentInChildren<XRRayInteractor>()`), call `TryGetCurrentUIRaycastResult(out var raycastResult)` — if it returns true and `raycastResult.gameObject != null`, treat as UI hit.

Fallback if that API is awkward at runtime: query `XRUIInputModule` via `EventSystem.current.currentInputModule` and check `IsPointerOverGameObject(deviceId)` for the interactor's deviceId.

Whichever path is used, encapsulate as `private static bool IsOverUI(NearFarInteractor interactor)` so the rest of the file stays clean.

The existing `Graphic`-in-parent check inside the `interactablesHovered` loop becomes redundant once `IsOverUI` exists — remove it to avoid double-guarding the same case.

---

## Item 4 — `XRPromeonInteractable` (tap = select, hold = grab)

### Current state

`SelectionInteractor` (extends `XRSimpleInteractable`) only handles tap → `SelectionManager.Toggle(NodeId)`. No grab, no move. `XRGrabInteractable` exists only on `GizmoHandle`, not on scene objects.

### New component

`Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` — extends `XRBaseInteractable` (not `XRGrabInteractable`, to avoid inherited auto-parent grab semantics).

Naming makes it clear this is a project-customized variant.

### State machine

```
Idle ──selectEntered──► Pressed (timer + tracking attachTransform delta)
                       │
       ┌───────────────┴───────────────┐
       │ duration > _tapWindow OR
       │ attach-delta > _moveThreshold
       ▼
   Dragging ──selectExited──► CommitTransform → Idle
       │
       └ selectExited inside Pressed window → Toggle(NodeId) → Idle
```

Inspector knobs (SerializeField with defaults):

- `_tapWindow = 0.25f` (sec)
- `_moveThreshold = 0.03f` (m, world-space distance of attachTransform from its initial position at SelectEnter)

### Drag mode — near vs far

Detected once at `SelectEnter`:

- **Committed implementation:** distance heuristic — if `Vector3.Distance(interactor.transform.position, transform.position) <= 0.30f` at SelectEnter, treat as Near; else Far. 0.30 m is well outside typical direct-hand reach jitter and well inside the dead zone before ray-only interaction.
- **Optional substitution (no spec change required):** if a clean `NearFarInteractor` accessor in XRI 3.x surfaces which caster (near vs far) grabbed, the heuristic may be swapped for it inline — same `DragMode` output either way.

Modes:

- **Near (`DragMode.SixDof`):** every frame in `ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Dynamic)`, copy both position and rotation from `selectingInteractor.attachTransform`, preserving the grab offset captured at SelectEnter.
- **Far (`DragMode.PositionOnly`):** every frame copy only position (apply the position offset from attach to object captured at SelectEnter). Rotation is left at whatever the object had at SelectEnter.

### Commit

`GizmoController` API extended:

```csharp
public void CommitTransform(Transform target, Vector3 pos, Quaternion rot, Vector3 scale);
```

`CommitMove` becomes a thin wrapper that forwards `target.rotation` and `target.localScale` to `CommitTransform` (back-compat for `GizmoHandle`).

`XRPromeonInteractable.OnSelectExited` in Dragging state:

```csharp
_gizmoController.CommitTransform(transform, transform.position, transform.rotation, transform.localScale);
```

`TransformCommand` already accepts pos/rot/scale, so no command-stack changes.

### DI / factory

`SelectionInteractorFactory.MakeInteractable` updated:

```csharp
if (go.GetComponent<Collider>() == null)
    go.AddComponent<BoxCollider>();

var sn  = go.GetComponent<SceneNode>();
var sel = go.GetComponent<Selectable>() ?? go.AddComponent<Selectable>();
if (sn != null) sel.Init(sn);

// remove legacy selection interactor if present (defensive for old prefabs)
var legacy = go.GetComponent<SelectionInteractor>();
if (legacy != null) UnityEngine.Object.Destroy(legacy);

var xri = go.GetComponent<XRPromeonInteractable>() ?? go.AddComponent<XRPromeonInteractable>();
xri.Construct(_selectionManager, _gizmoController);
```

Factory constructor signature gains `GizmoController` parameter; `SelectionInteractorFactory` registration in both scopes (`VrEditingSceneScope`, `SandboxSceneScope`) already resolves `GizmoController` from the container — no scope edits needed (other than the constructor parameter being picked up by VContainer auto-injection).

### Multi-grab — extension seam (no implementation)

`XRPromeonInteractable` keeps drag logic in `private void ApplyDrag()` that calls into a `IDragStrategy` field, defaulting to `new SingleDragStrategy()` (moves only `this.transform`). The interface stays minimal:

```csharp
interface IDragStrategy { void Apply(Transform self, Vector3 pos, Quaternion rot, DragMode mode); }
```

We ship only `SingleDragStrategy`. A future `GroupDragStrategy` (move all selected, applying delta) drops in without changes to the interactable. This is the only seam added — no other premature abstraction.

### Removal

`SelectionInteractor.cs` deleted (along with its `.meta`). No other file references it.

---

## Item 5 — Outliner: sort children by name

### Current state

`SceneOutlinerView.Rebuild` iterates `_graph.Nodes` (Dictionary insertion order) and groups by parent. Children of each parent show in spawn order.

### Change

Inside `AddRowsRecursive`, sort the children list before iterating:

```csharp
children.Sort((a, b) =>
    string.Compare(a.DisplayName ?? "", b.DisplayName ?? "",
        StringComparison.OrdinalIgnoreCase));
```

### Live-rename behavior

`NodeRenamedEvent` triggers only `SetLabel` on the single row — it does **not** re-sort. Resort happens only on `SceneModifiedEvent` (i.e., rename commit, add/remove, reparent). This avoids the row jumping under the cursor while the user is typing.

---

## Cross-cutting

### Files touched

| File | Action |
|---|---|
| `Assets/_App/_Shared/Events/AppEvents.cs` | +1 event (`NodeRenamedEvent`) |
| `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` | multi-state branch, live rename, Unnamed fallback |
| `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs` | NodeRenamedEvent sub, sort in Rebuild |
| `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs` | +`SetLabel(string)` |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab` | +MultiState GameObject |
| `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` | +UI-hit guard |
| `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` | **NEW** |
| `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs` | **NEW** (interface + SingleDragStrategy) |
| `Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs` | **DELETED** |
| `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs` | swap interactor type, take `GizmoController` dep |
| `Assets/_App/Subsystems/VrInteraction/GizmoController.cs` | +`CommitTransform(...)`, refactor `CommitMove` |

No scope file (`VrEditingSceneScope` / `SandboxSceneScope`) needs editing — VContainer resolves the new factory dep automatically.

### Testing

**EditMode unit tests:**

- `SceneInspectorViewTests` — verify three-state branching: empty → empty-state; single → content + binds; multi → multi-state + count label; live-rename empty input is ignored; commit-with-empty falls back to "Unnamed".
- `SceneOutlinerViewTests` — verify alphabetical sort in `Rebuild`, verify `NodeRenamedEvent` updates only the targeted row label without rebuilding.
- `XRPromeonInteractableTests` — mock `IXRSelectInteractor`:
  - short select (< tapWindow, no attach movement) → exactly one `Toggle(NodeId)`, no `CommitTransform`.
  - long hold → no `Toggle`, one `CommitTransform` with final pos/rot.
  - attach moves > moveThreshold within tapWindow → transitions to Dragging without firing `Toggle`.

**Manual on Quest 3:**

- Tap object in 3D → highlights and appears in inspector (matches outliner click behavior).
- Hold + move via direct hand → object follows hand with rotation; release commits as undoable transform.
- Hold + move via ray → object slides along ray axis, keeps original rotation; release commits.
- Tap empty space → deselect.
- Tap UI button in UserPanel/Outliner → selection NOT cleared.
- Edit name in inspector → outliner row text updates per keystroke; commit with empty field → "Unnamed".
- Multi-select two objects (tap two) → inspector shows "Multiple Objects Selected (2)".

### Risks

- **Distance heuristic edge case.** If the user's hand happens to be within 0.30 m of a far-targeted object at SelectEnter (rare — would require physically standing next to a remote-aimed object), it's treated as Near. Mitigation: 0.30 m is conservative; further tuning happens on Quest 3 manual pass.
- **`TryGetCurrentUIRaycastResult` path.** If this API isn't surfaced on `NearFarInteractor` directly, fall back to `XRUIInputModule.IsPointerOverGameObject(deviceId)` via `EventSystem.current`.
- **Outliner sort on rename-commit could move the active row** — by design; user sees the row jump on commit, not during typing. Acceptable UX.
