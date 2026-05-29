# SceneContext Consumer Migration Implementation Plan (Plan B of scene/scope redesign)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Parent spec:** `docs/superpowers/specs/2026-05-29-scene-scope-lifecycle-redesign-design.md` (§2). **Depends on Plan A** (`2026-05-30-scene-context-foundation.md`, DONE — `SceneContext`, `SceneContextChangedEvent`, `SceneContextBinder` exist and are wired).
>
> **Scope of THIS plan:** migrate the four UserPanel data panels — `OutlinerPanel`, `InspectorPanel`, `PropertyPanel`, `AnimatorPanel` — to read scene services through `SceneContext` instead of holding directly-injected scene-scoped references. The gizmo/rig/interaction consumers (`GizmoActivator`, `BoneInspectorPanel`, `IkWizardPanel`, `WorldClickCatcher`, `UndoKeyHandler`) are deferred to a later plan (they're entangled with the gizmo/outline fix tracks).
>
> **Git note:** the user (Promokot) commits manually. Do NOT run `git commit`. "Checkpoint" steps suggest a message.
>
> **Unity note:** no CLI build. The CONTROLLER compiles + runs tests via MCP-for-Unity (`run_tests` assembly `_App.Tests` EditMode; `read_console` filter `CS`; stop Play mode before running tests). Implementer subagents write code only and do NOT drive Unity.

**Goal:** The four data panels stop holding directly-injected scene-scoped services and instead read them via the root `SceneContext`, rebuilding on `SceneContextChangedEvent` bind and clearing on unbind — so a persistent panel never dereferences a service that was disposed when its scene unloaded.

**Architecture:** `SceneContext.Graph` becomes the concrete `SceneGraph` (consumers need `.Nodes` and the `SceneNode`-returning `GetNode`). Each panel: swaps its `Construct` scene-service params for `SceneContext` (keeping root services like `EventBus`, `IAssetRegistry`, `AnimationClipboard`), replaces field reads with `_ctx.X`, guards on `_ctx.HasScene`, and subscribes to `SceneContextChangedEvent` to rebuild/clear. **Per-scene injection is unchanged** — the scene scopes already `c.Inject(panel)`, and the new `Construct` deps (all root singletons) resolve fine from the child scope. No scene-scope edits.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces), VContainer, custom `EventBus`, NUnit EditMode.

---

## File Structure

- **Modify** `Assets/_App/Scripts/Bootstrap/SceneContext.cs` — `Graph` property + `Bind` param type: `ISceneGraph` → `SceneGraph`.
- **Modify** `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs` — `Resolve<ISceneGraph>()` → `Resolve<SceneGraph>()`.
- **Modify** `Assets/_App/Tests/Bootstrap/SceneContextTests.cs` — drop `FakeSceneGraph`; use a real `SceneGraph`.
- **Modify** `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs`
- **Modify** `Assets/_App/Scripts/SpatialUi/Panels/InspectorPanel.cs`
- **Modify** `Assets/_App/Scripts/SpatialUi/Panels/PropertyPanel.cs`
- **Modify** `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

---

### Task 1: `SceneContext.Graph` → concrete `SceneGraph`

**Files:** `SceneContext.cs`, `SceneContextBinder.cs`, `SceneContextTests.cs`

Rationale: `OutlinerPanel` uses `SceneGraph.Nodes` and `Inspector`/`Animator` use the `SceneNode`-returning concrete `GetNode` — none of which exist on `ISceneGraph`. Exposing the concrete type is the pragmatic choice (the alternative, lifting `Nodes` + a `SceneNode` `GetNode` onto `ISceneGraph`, is a larger interface change for no current benefit).

- [ ] **Step 1: Edit `SceneContext.cs`** — change the `Graph` property type and the `Bind` first parameter from `ISceneGraph` to `SceneGraph`:

Property line:
```csharp
    public SceneGraph        Graph     { get; private set; }
```
`Bind` signature first param:
```csharp
    public void Bind(SceneGraph graph, ISelectionManager selection, CommandStack commands,
                     GizmoController gizmo, AnimationAuthoring authoring, AnimationClock clock,
                     IRigRuntime rig)
```
(Body of `Bind`/`Clear`/`HasScene` unchanged — `HasScene => Graph != null` still valid.)

- [ ] **Step 2: Edit `SceneContextBinder.cs`** — change the first resolve in `Start()`:
```csharp
            Resolve<SceneGraph>(),
```
(everything else unchanged).

- [ ] **Step 3: Edit `SceneContextTests.cs`** — remove the `FakeSceneGraph` nested class entirely, and construct a real `SceneGraph` (its constructor only stores fields, so nulls are safe). Replace the two `new FakeSceneGraph()` usages. Final test body:

```csharp
using NUnit.Framework;

public class SceneContextTests
{
    private static SceneGraph MakeGraph() => new SceneGraph(new EventBus(), null, null, null);

    [Test]
    public void New_HasNoScene()
    {
        var ctx = new SceneContext();
        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Bind_WithGraph_SetsHasSceneAndExposesServices()
    {
        var ctx   = new SceneContext();
        var graph = MakeGraph();
        ctx.Bind(graph, null, null, null, null, null, null);
        Assert.IsTrue(ctx.HasScene);
        Assert.AreSame(graph, ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Clear_NullsEverythingAndHasNoScene()
    {
        var ctx   = new SceneContext();
        ctx.Bind(MakeGraph(), null, null, null, null, null, null);
        ctx.Clear();
        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
    }
}
```
(`using UnityEngine;` is no longer needed — remove it.)

- [ ] **Step 4 (controller): compile + run tests.** Recompile; `read_console` filter `CS` → no errors. Stop Play mode if active, then `run_tests` `SceneContextTests` → 3/3 pass.

- [ ] **Step 5: Checkpoint (user commits)** — suggested: `refactor(scope): SceneContext.Graph exposes concrete SceneGraph`

---

### Task 2: Migrate `OutlinerPanel`

**File:** `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs`

Current Construct: `Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)`; fields `_graph`, `_selection`. It reads `_graph.Nodes` (Rebuild) and `_selection.Select`/`_selection.SelectedNodeId` (ApplyHighlight, row callback). `Rebuild()` early-returns if `_graph == null` but does NOT clear existing rows.

- [ ] **Step 1: Replace fields + Construct.** Remove `private SceneGraph _graph;` and `private ISelectionManager _selection;`. Add `private SceneContext _ctx;` (keep `private EventBus _bus;`). New Construct:
```csharp
    [Inject]
    public void Construct(EventBus bus, SceneContext ctx)
    {
        _bus = bus;
        _ctx = ctx;
    }
```

- [ ] **Step 2: Replace service reads.**
  - In `Rebuild()`: change the guard `if (_rowsRoot == null || _objectRowPrefab == null || _rigRowPrefab == null || _graph == null) return;` → use `_ctx.Graph` in place of `_graph`; and change `foreach (var pair in _graph.Nodes)` → `foreach (var pair in _ctx.Graph.Nodes)`.
  - In `AddRowsRecursive`, the row callback `() => _selection.Select(node.NodeId)` → `() => _ctx.Selection?.Select(node.NodeId)`.
  - In `ApplyHighlight()`: guard `if (_rowsRoot == null || _selection == null) return;` → `if (_rowsRoot == null || _ctx.Selection == null) return;`; and `var selectedId = _selection.SelectedNodeId;` → `var selectedId = _ctx.Selection.SelectedNodeId;`.

- [ ] **Step 3: Add a row-clearing helper + bind/unbind handling.** Extract the existing "destroy rows" loop into a helper and subscribe to `SceneContextChangedEvent`:
```csharp
    private void ClearRows()
    {
        if (_rowsRoot == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        if (e.HasScene) Rebuild();
        else            ClearRows();
    }
```
In `OnEnable`, add as the first subscription: `_bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);`. In `OnDisable`, add the matching `_bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);`. (Keep the existing `Rebuild()` call at the end of `OnEnable` — it now no-ops safely when no scene is bound because `Rebuild` guards on `_ctx.Graph`.)

- [ ] **Step 4 (controller): compile.** Recompile; `read_console` filter `CS` → no errors.

- [ ] **Step 5 (controller + user): in-editor verify.** Enter Play → VrEditing → open the Outliner: it lists the scene's nodes (as before). Return to MainMenu, re-enter VrEditing: list rebuilds (no stale rows, no `NullReferenceException` in console referencing `OutlinerPanel`).

- [ ] **Step 6: Checkpoint (user commits)** — `refactor(outliner): read scene services via SceneContext`

---

### Task 3: Migrate `InspectorPanel`

**File:** `Assets/_App/Scripts/SpatialUi/Panels/InspectorPanel.cs`

Current Construct: `Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, IAssetRegistry registry)`. Uses `_graph.GetNode` (returns `SceneNode`), `_graph.RemoveNode`, `_selection.SelectedNodeId`/`Select`. `IAssetRegistry` is root-scoped — keep it.

- [ ] **Step 1: Replace fields + Construct.** Remove `private SceneGraph _graph;` and `private ISelectionManager _selection;`. Add `private SceneContext _ctx;` (keep `_bus`, `_registry`). New Construct:
```csharp
    [Inject]
    public void Construct(EventBus bus, SceneContext ctx, IAssetRegistry registry)
    {
        _bus      = bus;
        _ctx      = ctx;
        _registry = registry;
    }
```

- [ ] **Step 2: Replace service reads.** Throughout the file:
  - `_selection.SelectedNodeId` → `_ctx.Selection.SelectedNodeId`, `_selection?.Select(...)` → `_ctx.Selection?.Select(...)`, `_selection.Select(...)` → `_ctx.Selection?.Select(...)`.
  - `_graph.GetNode(...)` → `_ctx.Graph.GetNode(...)`, `_graph.RemoveNode(...)` → `_ctx.Graph.RemoveNode(...)`.
  - In `Refresh()`, change the guard `if (_selection == null || _graph == null) return;` → `if (!_ctx.HasScene) return;`.

- [ ] **Step 3: Add bind/unbind handling.** Add:
```csharp
    private void OnSceneContextChanged(SceneContextChangedEvent e) => Refresh();
```
`Refresh()` already shows the Empty state when there's no selection; with the new `!_ctx.HasScene` guard it returns early on unbind (panel keeps last frame but is hidden in MainMenu — acceptable). To actively clear on unbind, make the handler explicit:
```csharp
    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        if (e.HasScene) Refresh();
        else if (_emptyState != null)
        {
            _emptyState.SetActive(true);
            if (_content   != null) _content.SetActive(false);
            if (_boneState != null) _boneState.SetActive(false);
        }
    }
```
In `OnEnable` add `_bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);` (first), and the matching unsubscribe in `OnDisable`.

- [ ] **Step 4 (controller): compile.** No `CS` errors.

- [ ] **Step 5 (controller + user): verify.** VrEditing → select an object: inspector shows its transform/name; select a bone: bone state shows; delete works; rename works. Round-trip to MainMenu and back: no stale data, no NRE referencing `InspectorPanel`.

- [ ] **Step 6: Checkpoint (user commits)** — `refactor(inspector): read scene services via SceneContext`

---

### Task 4: Migrate `PropertyPanel`

**File:** `Assets/_App/Scripts/SpatialUi/Panels/PropertyPanel.cs`

Current Construct: `Construct(EventBus bus, ISceneGraph sceneGraph)`; it is `IStartable, IDisposable` (scene-scope-driven). `OnSelectionChanged` does `var go = _sceneGraph.GetNode(id)` (interface → `GameObject`) then `go.transform`. After migration `_ctx.Graph.GetNode` returns **`SceneNode`** (concrete) — `.transform` still works (SceneNode is a MonoBehaviour), so rename the local to avoid the misleading `go` name.

- [ ] **Step 1: Replace field + Construct.** Remove `private ISceneGraph _sceneGraph;`. Add `private SceneContext _ctx;` (keep `_bus`). New Construct:
```csharp
    [Inject]
    public void Construct(EventBus bus, SceneContext ctx)
    {
        _bus = bus;
        _ctx = ctx;
    }
```

- [ ] **Step 2: Subscribe to context changes.** Change `Start`/`Dispose`:
```csharp
    public void Start()
    {
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        if (!e.HasScene) ClearDisplay();
    }
```

- [ ] **Step 3: Update `OnSelectionChanged`.** Replace its body's graph access and guard against no scene:
```csharp
    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (e.SelectedNodeId == null || _ctx.Graph == null) { ClearDisplay(); return; }
        var node = _ctx.Graph.GetNode(e.SelectedNodeId);
        if (node == null) return;
        var t = node.transform;
        _positionText.text = $"Pos: {t.position:F2}";
        _rotationText.text = $"Rot: {t.eulerAngles:F1}";
        _scaleText.text    = $"Scl: {t.localScale:F2}";
    }
```

- [ ] **Step 4 (controller): compile.** No `CS` errors.

- [ ] **Step 5 (controller + user): verify.** VrEditing → select an object: PropertyPanel shows pos/rot/scale; deselect: shows "—". Round-trip to MainMenu and back: no NRE referencing `PropertyPanel`.

- [ ] **Step 6: Checkpoint (user commits)** — `refactor(property): read scene graph via SceneContext`

---

### Task 5: Migrate `AnimatorPanel`

**File:** `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

Current Construct: `Construct(EventBus bus, AnimationAuthoring authoring, AnimationClock clock, ISelectionManager selection, AnimationClipboard clipboard, SceneGraph graph)`. `_bus` and `_clipboard` are root-scoped (keep). `_authoring`, `_clock`, `_selection`, `_graph` are scene-scoped → via `_ctx`. The panel is only visible in VrEditing/Sandbox (a live scene), so `_ctx` services are non-null whenever it's usable; still add a top-level guard in `Refresh`.

- [ ] **Step 1: Replace fields + Construct.** Remove the four fields `private AnimationAuthoring _authoring;`, `private AnimationClock _clock;`, `private ISelectionManager _selection;`, `private SceneGraph _graph;`. Add `private SceneContext _ctx;` (keep `_bus`, `_clipboard`). New Construct:
```csharp
    [Inject]
    public void Construct(EventBus bus, AnimationClipboard clipboard, SceneContext ctx)
    {
        _bus       = bus;
        _clipboard = clipboard;
        _ctx       = ctx;
    }
```

- [ ] **Step 2: Replace service reads across the file.** Apply these exact substitutions everywhere they appear in the body:
  - `_authoring` → `_ctx.Authoring`
  - `_clock` → `_ctx.Clock`
  - `_selection` → `_ctx.Selection`
  - `_graph` → `_ctx.Graph`

  (These appear in `WireToolbar`, `WireTransport`, `OnAddAnimationClicked`, `OnRemove/Set/Delete/Copy/Paste/Prev/NextKey`, `OnPlayPauseClicked`, `Refresh`, `ApplyContainerToClock`, `RebuildTimeline`, `RebuildTrackRows`, `RebuildLanes`, `RefreshLaneKeys`, `RefreshKeyButtonStates`, `CurrentTotal`.) Note `AnimationAuthoring.OwnerOf(...)` is a STATIC call — leave it as `AnimationAuthoring.OwnerOf(...)` (do not rewrite the type name there).

- [ ] **Step 3: Guard `Refresh` + add bind/unbind handling.** At the very top of `Refresh()` add:
```csharp
        if (!_ctx.HasScene) { ShowEmpty(AnimatorSubEmptyState.State.NoSelection); return; }
```
Add the handler:
```csharp
    private void OnSceneContextChanged(SceneContextChangedEvent e) => Refresh();
```
In `OnEnable` add `_bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);` (first subscription), and the matching unsubscribe in `OnDisable`.

- [ ] **Step 4 (controller): compile.** No `CS` errors.

- [ ] **Step 5 (controller + user): verify.** VrEditing → select a rig/object with animation: the animator timeline populates; set/delete/copy/paste key, play/pause, scrub all work as before. Select nothing: empty state. Round-trip to MainMenu and back: no NRE referencing `AnimatorPanel`.

- [ ] **Step 6: Checkpoint (user commits)** — `refactor(animator): read scene services via SceneContext`

---

## Self-Review

**Spec coverage (§2):** consumers read via `SceneContext` (Tasks 2-5) ✓; never cache `_ctx.X` in a field (all reads go through `_ctx`) ✓; subscribe to `SceneContextChangedEvent` to rebuild on bind / clear on unbind (each task adds the handler) ✓; scene-resident consumers untouched (only these 4 persistent panels migrated) ✓; per-scene injection unchanged (no scope edits — new Construct deps are root singletons reachable from the child scope) ✓. Concrete-`SceneGraph` exposure (Task 1) is the documented resolution of the "concrete vs interface" note from Plan A's review.

**Placeholder scan:** none — every step gives exact Construct signatures, handler bodies, and substitution rules. The AnimatorPanel body uses substitution rules (not full reproduction) because it's a mechanical rename across ~25 call sites; the four rules are unambiguous and the static `AnimationAuthoring.OwnerOf` exception is called out.

**Type consistency:** `SceneContext.Bind(SceneGraph, ISelectionManager, CommandStack, GizmoController, AnimationAuthoring, AnimationClock, IRigRuntime)` — Task 1 changes only the first param type; binder `Resolve<SceneGraph>()` matches. Panels read `_ctx.Graph` (SceneGraph), `_ctx.Selection` (ISelectionManager), `_ctx.Authoring`/`_ctx.Clock` (concrete) — all match SceneContext property types. `SceneContextChangedEvent.HasScene` used consistently.

**Verification reality:** the four panels are UI MonoBehaviours (not unit-testable); correctness is gated by compile + in-editor smoke per task. The only unit tests are SceneContextTests (Task 1), kept green.

---

## Deferred to a later plan (not here)
`GizmoActivator` (god-object; coordinate with the gizmo fix track), `BoneInspectorPanel` + `IkWizardPanel` (mutually coupled; rig/outline track), `WorldClickCatcher`, `UndoKeyHandler` (confirm persistent-vs-scene-resident first). Plan C (loading model) and Plan D (docs/archive) still follow.
