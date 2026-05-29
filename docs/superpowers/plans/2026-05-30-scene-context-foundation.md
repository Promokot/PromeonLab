# SceneContext Foundation Implementation Plan (Plan A of scene/scope redesign)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Parent spec:** `docs/superpowers/specs/2026-05-29-scene-scope-lifecycle-redesign-design.md` (§2). This is the first of four plans (A foundation → B consumer migration → C loading model → D docs cleanup).
>
> **Git note (project rule):** the user (Promokot) commits manually. Do NOT run `git commit`. "Checkpoint" steps give a suggested message; leave the commit to the user.
>
> **Unity note:** there is no CLI build. Compile + run tests via the Unity Editor / MCP-for-Unity (`run_tests` assembly `_App.Tests`, EditMode; `read_console` for `CS` errors). After editing scripts, request a recompile and confirm no `CS####` before proceeding.

**Goal:** Introduce a root-scoped `SceneContext` that mirrors the live scene scope's services (or null when no scene is loaded), populated/cleared by a scene-scoped `SceneContextBinder`, so persistent UI can later read scene services without holding references that outlive the scene scope.

**Architecture:** `SceneContext` is a plain root singleton with read-only nullable properties. A `SceneContextBinder` (VContainer entry point, one per scene scope) fills it on scope build (`IStartable.Start`) and clears it on scope teardown (`IDisposable.Dispose`), publishing `SceneContextChangedEvent` each time. Scene scopes register different service sets (Sandbox has no animation/rig services), so the binder resolves each service defensively. **No consumer reads `SceneContext` yet** — after this plan the app behaves exactly as before; the payoff lands in Plan B.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces), VContainer DI, custom `EventBus`, NUnit EditMode tests (`_App.Tests`).

---

## File Structure

- **Create** `Assets/_App/Scripts/Bootstrap/SceneContext.cs` — root-singleton façade over the current scene's scoped services. One responsibility: hold + expose-or-null those references.
- **Create** `Assets/_App/Scripts/Bootstrap/Events/SceneContextChangedEvent.cs` — `struct` event (bound/unbound signal).
- **Create** `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs` — scene-scoped entry point; the single place that binds/clears `SceneContext`.
- **Modify** `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — register `SceneContext` as a singleton.
- **Modify** `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs` — register `SceneContextBinder` entry point.
- **Modify** `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs` — register `SceneContextBinder` entry point.
- **Create** `Assets/_App/Tests/Bootstrap/SceneContextTests.cs` — EditMode unit tests for `SceneContext`.

Service types referenced (already exist): `ISceneGraph`, `ISelectionManager`, `IRigRuntime`, `CommandStack`, `GizmoController`, `AnimationAuthoring`, `AnimationClock`.

---

### Task 1: `SceneContext` façade (TDD)

**Files:**
- Create: `Assets/_App/Scripts/Bootstrap/SceneContext.cs`
- Test: `Assets/_App/Tests/Bootstrap/SceneContextTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/_App/Tests/Bootstrap/SceneContextTests.cs`:

```csharp
using NUnit.Framework;

public class SceneContextTests
{
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
        var ctx = new SceneContext();
        var graph = new FakeSceneGraph();

        ctx.Bind(graph, null, null, null, null, null, null);

        Assert.IsTrue(ctx.HasScene);
        Assert.AreSame(graph, ctx.Graph);
        Assert.IsNull(ctx.Selection); // unbound services stay null (e.g. Sandbox animation/rig)
    }

    [Test]
    public void Clear_NullsEverythingAndHasNoScene()
    {
        var ctx = new SceneContext();
        ctx.Bind(new FakeSceneGraph(), null, null, null, null, null, null);

        ctx.Clear();

        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
    }

    // Minimal ISceneGraph stub: only used as a non-null reference. Implement members
    // with throwing/default bodies — the tests never call them.
    private sealed class FakeSceneGraph : ISceneGraph
    {
        // NOTE TO IMPLEMENTER: auto-generate the interface members (Rider/VS "Implement interface")
        // against the current ISceneGraph in Assets/_App/Scripts/SceneComposition/ISceneGraph.cs.
        // Each member: throw new System.NotImplementedException();  (none are invoked by these tests).
    }
}
```

> The `FakeSceneGraph` stub must implement the **current** `ISceneGraph` interface verbatim. Open `Assets/_App/Scripts/SceneComposition/ISceneGraph.cs`, and for every member emit `throw new System.NotImplementedException();` (or `=> default;`). The tests only need a non-null instance; no member is called.

- [ ] **Step 2: Run tests to verify they fail**

Run EditMode tests (Unity Test Runner or MCP `run_tests`, assembly `_App.Tests`, filter `SceneContextTests`).
Expected: FAIL — `SceneContext` does not exist (compile error / test not found).

- [ ] **Step 3: Write `SceneContext`**

Create `Assets/_App/Scripts/Bootstrap/SceneContext.cs`:

```csharp
// Root-scoped façade exposing the currently-loaded scene scope's services, or null when no
// scene scope is live. Persistent UI (UserPanel modules) will read scene services THROUGH this
// (Plan B) so it never holds a reference that outlives the scene scope. Populated/cleared only
// by SceneContextBinder.
public class SceneContext
{
    public ISceneGraph       Graph     { get; private set; }
    public ISelectionManager Selection { get; private set; }
    public CommandStack      Commands  { get; private set; }
    public GizmoController    Gizmo     { get; private set; }
    public AnimationAuthoring Authoring { get; private set; }
    public AnimationClock     Clock     { get; private set; }
    public IRigRuntime       Rig       { get; private set; }

    public bool HasScene => Graph != null;

    public void Bind(ISceneGraph graph, ISelectionManager selection, CommandStack commands,
                     GizmoController gizmo, AnimationAuthoring authoring, AnimationClock clock,
                     IRigRuntime rig)
    {
        Graph = graph; Selection = selection; Commands = commands;
        Gizmo = gizmo; Authoring = authoring; Clock = clock; Rig = rig;
    }

    public void Clear()
    {
        Graph = null; Selection = null; Commands = null;
        Gizmo = null; Authoring = null; Clock = null; Rig = null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode tests filtered to `SceneContextTests`.
Expected: PASS (3 tests).

- [ ] **Step 5: Checkpoint (user commits)**

Suggested message:
```
feat(scope): add SceneContext root façade for scene-scoped services
```

---

### Task 2: `SceneContextChangedEvent`

**Files:**
- Create: `Assets/_App/Scripts/Bootstrap/Events/SceneContextChangedEvent.cs`

- [ ] **Step 1: Create the event struct**

Create `Assets/_App/Scripts/Bootstrap/Events/SceneContextChangedEvent.cs`:

```csharp
// DI-lifecycle signal: published when SceneContext is bound (HasScene = true) or cleared
// (HasScene = false). Distinct from SceneOpenedEvent (scene data) and ModeChangedEvent
// (panel visibility). Plan-B consumers subscribe to rebuild/clear their UI.
public struct SceneContextChangedEvent
{
    public bool HasScene;
}
```

- [ ] **Step 2: Compile**

Request a Unity recompile; `read_console` filtered to `CS`.
Expected: no `CS####` errors.

- [ ] **Step 3: Checkpoint (user commits)**

Suggested message:
```
feat(scope): add SceneContextChangedEvent
```

---

### Task 3: `SceneContextBinder` (scene-scoped entry point)

**Files:**
- Create: `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs`

> **Why no EditMode unit test here:** VContainer dispatches `IStartable.Start` / `IDisposable.Dispose` for entry points through its PlayerLoop-based dispatcher, which does not run in EditMode unit tests. The bind/clear *logic* is covered by `SceneContextTests` (Task 1); the binder's wiring is verified by the in-editor smoke test in Task 6. A temporary debug log makes that smoke observable and is removed at the end of Task 6.

- [ ] **Step 1: Write `SceneContextBinder`**

Create `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs`:

```csharp
using System;
using VContainer;
using VContainer.Unity;

// Scene-scoped entry point: the SINGLE place that fills SceneContext from the live scene scope
// and clears it when the scope is torn down (scene unload). Service sets differ per scope
// (Sandbox registers no AnimationAuthoring/AnimationClock/RigRuntime), so each is resolved
// defensively — an unregistered service resolves to null, which SceneContext exposes as null.
public class SceneContextBinder : IStartable, IDisposable
{
    private readonly IObjectResolver _resolver;
    private readonly SceneContext    _ctx;
    private readonly EventBus        _bus;

    public SceneContextBinder(IObjectResolver resolver, SceneContext ctx, EventBus bus)
    {
        _resolver = resolver;
        _ctx      = ctx;
        _bus      = bus;
    }

    public void Start()
    {
        _ctx.Bind(
            Resolve<ISceneGraph>(),
            Resolve<ISelectionManager>(),
            Resolve<CommandStack>(),
            Resolve<GizmoController>(),
            Resolve<AnimationAuthoring>(),
            Resolve<AnimationClock>(),
            Resolve<IRigRuntime>());

        // TEMP smoke log (removed in Task 6): confirms bind on scene load.
        UnityEngine.Debug.Log($"[SCTXDBG] SceneContext bound HasScene={_ctx.HasScene}");
        _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
    }

    public void Dispose()
    {
        _ctx.Clear();
        UnityEngine.Debug.Log("[SCTXDBG] SceneContext cleared");
        _bus.Publish(new SceneContextChangedEvent { HasScene = false });
    }

    private T Resolve<T>() where T : class
    {
        try { return _resolver.Resolve<T>(); }
        catch { return null; } // service not registered in this scope
    }
}
```

- [ ] **Step 2: Compile**

Request recompile; `read_console` filtered to `CS`.
Expected: no `CS####` errors.

- [ ] **Step 3: Checkpoint (user commits)**

Suggested message:
```
feat(scope): add SceneContextBinder entry point (bind on load, clear on unload)
```

---

### Task 4: Register `SceneContext` in `RootLifetimeScope`

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Add the registration**

In `RootLifetimeScope.Configure`, add `SceneContext` next to the other root singletons. Find:

```csharp
        builder.Register<EventBus>(Lifetime.Singleton);
```

and add immediately after it:

```csharp
        builder.Register<SceneContext>(Lifetime.Singleton);
```

- [ ] **Step 2: Compile**

Request recompile; `read_console` filtered to `CS`.
Expected: no `CS####` errors.

- [ ] **Step 3: Checkpoint (user commits)**

Suggested message:
```
feat(scope): register SceneContext singleton in RootLifetimeScope
```

---

### Task 5: Register `SceneContextBinder` in both scene scopes

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`

Both scopes already `using VContainer.Unity;` (needed for `RegisterEntryPoint`).

- [ ] **Step 1: VrEditingSceneScope — register the binder**

In `VrEditingSceneScope.Configure`, add the entry-point registration at the top of the method body, immediately after the first registration line. Find:

```csharp
        builder.RegisterInstance(_panelRegistry);
```

and add immediately after it:

```csharp
        // Scene-scoped: fills/clears the root SceneContext for this scene's lifetime.
        builder.RegisterEntryPoint<SceneContextBinder>();
```

- [ ] **Step 2: SandboxSceneScope — register the binder**

In `SandboxSceneScope.Configure`, find the same line:

```csharp
        builder.RegisterInstance(_panelRegistry);
```

and add immediately after it:

```csharp
        // Scene-scoped: fills/clears the root SceneContext for this scene's lifetime.
        builder.RegisterEntryPoint<SceneContextBinder>();
```

- [ ] **Step 3: Compile**

Request recompile; `read_console` filtered to `CS`.
Expected: no `CS####` errors.

- [ ] **Step 4: Checkpoint (user commits)**

Suggested message:
```
feat(scope): bind SceneContext per scene via SceneContextBinder entry point
```

---

### Task 6: In-editor verification + remove smoke logs

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs` (remove temp logs)

- [ ] **Step 1: Run the unit tests**

Run EditMode tests, assembly `_App.Tests`. Expected: `SceneContextTests` green; the existing `PanelRegionRouterTests` (and all others) still green — no regressions.

- [ ] **Step 2: Smoke test — bind on scene load**

Enter Play mode (Bootstrap scene), let MainMenu load, then transition into **VrEditing**. In the console expect:
```
[SCTXDBG] SceneContext bound HasScene=True
```
Transition into **Sandbox** as well and confirm `HasScene=True` there too (Sandbox binds graph/selection/commands/gizmo; animation/rig stay null — `HasScene` is still true because the graph is bound).

- [ ] **Step 3: Smoke test — clear on scene unload**

Transition back to **MainMenu** (unloads the editing scene). Expect:
```
[SCTXDBG] SceneContext cleared
```
appears when the editing scene scope disposes. (Order vs `ModeChangedEvent` is not asserted here — Plan C addresses transition ordering.)

- [ ] **Step 4: Remove the temporary smoke logs**

In `SceneContextBinder.cs`, delete the two `UnityEngine.Debug.Log("[SCTXDBG]...")` lines (in `Start` and `Dispose`), keeping the `Bind`/`Clear` + `Publish` calls.

- [ ] **Step 5: Compile + re-run tests**

Request recompile; `read_console` filtered to `CS` (no errors). Re-run `SceneContextTests` (still green).

- [ ] **Step 6: Checkpoint (user commits)**

Suggested message:
```
chore(scope): verify SceneContext bind/clear lifecycle; remove smoke logs
```

---

## Self-Review

**Spec coverage (§2 of parent spec):**
- `SceneContext` type with nullable read-only props + `HasScene` → Task 1. ✓
- `SceneContextChangedEvent` (bound/unbound) → Task 2. ✓
- `SceneContextBinder` (IStartable bind / IDisposable clear, single owner) → Task 3, registered Task 5. ✓
- Scene services stay `Scoped`; scene-resident consumers untouched → no scope service registrations changed (only added the binder entry point). ✓
- Per-scope service difference (Sandbox lacks animation/rig) → defensive `Resolve<T>()` in binder. ✓
- "No consumer reads SceneContext yet" → consumer migration is Plan B; this plan changes no consumer. ✓
- Distinction from `SceneOpenedEvent`/`ModeChangedEvent` → documented in the event file. ✓

**Placeholder scan:** The only deferred detail is the `FakeSceneGraph` member bodies, which depend on the live `ISceneGraph` and are explicitly instructed to be auto-generated as `NotImplementedException` stubs (never invoked) — this is a concrete instruction, not a TBD. No other placeholders.

**Type consistency:** `Bind(ISceneGraph, ISelectionManager, CommandStack, GizmoController, AnimationAuthoring, AnimationClock, IRigRuntime)` — same 7-arg order in `SceneContext.Bind` (Task 1), the binder call (Task 3), and the test (Task 1). `SceneContextChangedEvent.HasScene` used consistently. `RegisterEntryPoint<SceneContextBinder>()` matches the class name.

**Known limitation (intentional):** the binder lifecycle (Start/Dispose) is verified by smoke test, not EditMode unit test, because VContainer entry-point dispatch needs the PlayerLoop. Documented in Task 3.

---

## Next plans (not in this document)

- **Plan B** — migrate the ~6-9 persistent consumers (`OutlinerPanel`, `InspectorPanel`, `AnimatorPanel`, `PropertyPanel`, `BoneInspectorPanel`, `IkWizardPanel`, plus the confirmed-persistent of `WorldClickCatcher`/`GizmoActivator`/`UndoKeyHandler`) to read via `SceneContext` + subscribe to `SceneContextChangedEvent`; remove their direct scene-service injection.
- **Plan C** — loading model: additive → single + DDOL `PersistentRoot`, `ISceneTransition`/`SceneTransitionRunner` + head fade, `ModeChangedEvent` ordering fix.
- **Plan D** — remove `FeatureLifetimeScope` (docs), update CLAUDE.md/architecture docs, archive implemented specs/plans.
