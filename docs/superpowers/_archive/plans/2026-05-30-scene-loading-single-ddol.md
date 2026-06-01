# Single-Scene Loading + DDOL Implementation Plan (Plan C of scene/scope redesign)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Checkbox (`- [ ]`) steps.
>
> **Parent spec:** `docs/superpowers/specs/2026-05-29-scene-scope-lifecycle-redesign-design.md` (§1, §3). **Depends on Plans A + B (DONE):** `SceneContext` exists and the data panels read scene services through it (so they survive the more aggressive scene unloads this plan introduces).
>
> **Git note:** the user (Promokot) commits manually. Do NOT run `git commit`; "Checkpoint" steps suggest messages.
>
> **Unity note:** no CLI build. The CONTROLLER compiles + runs tests via MCP-for-Unity (`run_tests` `_App.Tests` EditMode; `read_console` filter `CS`; stop Play before tests). Implementer subagents write code only. Steps marked **[MANUAL EDITOR]** require the user/controller to edit scenes/prefabs in the Unity Editor — they cannot be done by a code-only subagent.

**Goal:** Replace additive scene loading with single-scene loading where the bootstrap infrastructure (`RootLifetimeScope`, XR rig, `UserPanel`, keyboard, spawn applier) is `DontDestroyOnLoad`, exactly one mode scene is loaded at a time, transitions run asynchronously behind a VR head-fade, and `ModeChangedEvent` is published only after the new scene + its scope exist.

**Architecture:** A single persistent root GameObject (`PersistentRoot`) holds all infrastructure and is `DontDestroyOnLoad`-marked at boot. `AppBootstrap` then loads the first mode scene with `LoadSceneMode.Single`. `ModeOrchestrator` becomes pure policy (graph check + `CurrentMode`) and delegates the swap to `ISceneTransition`; the concrete `SceneTransitionRunner` (a persistent MonoBehaviour) does fade-out → `LoadSceneAsync(Single)` → fade-in and calls back so the orchestrator publishes `ModeChangedEvent` after the load. `HeadFade` (a head-attached unlit quad) provides the VR-safe fade. Single load makes the loaded scene the active scene automatically, so render-bleed (the reason for the old `scene-loading-isolation` workaround) disappears and `SetActiveScene` is removed.

**Tech Stack:** Unity 6000.3.7f1, `UnityEngine.SceneManagement`, VContainer, custom `EventBus`, NUnit EditMode.

---

## File Structure

- **Create** `Assets/_App/Scripts/SpatialUi/HeadFade.cs` — head-attached fade quad; `Coroutine`-free async alpha ramp via `FadeAsync`.
- **Create** `Assets/_App/Scripts/ModeOrchestrator/ISceneTransition.cs` — mechanism interface the orchestrator calls.
- **Create** `Assets/_App/Scripts/ModeOrchestrator/SceneTransitionRunner.cs` — persistent MonoBehaviour implementing `ISceneTransition` (fade + async single load).
- **Modify** `Assets/_App/Scripts/ModeOrchestrator/ModeOrchestrator.cs` — policy only; delegate to `ISceneTransition`; publish `ModeChangedEvent` after load; re-entrancy guard.
- **Modify** `Assets/_App/Scripts/Bootstrap/AppBootstrap.cs` — DDOL the persistent root; first load via the runner.
- **Modify** `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — register the `SceneTransitionRunner`/`HeadFade` instances + supply `ISceneTransition` to `ModeOrchestrator`.
- **Test** `Assets/_App/Tests/ModeOrchestrator/ModeOrchestratorTests.cs` — drive with a fake `ISceneTransition`.
- **[MANUAL EDITOR]** `Bootstrap.unity` scene + the XR rig prefab: group infra under `PersistentRoot`, add the `HeadFade` quad under the HMD camera, add the `SceneTransitionRunner` component, strip leftover Lights/skybox if any.

---

### Task 1: `HeadFade` (VR fade quad)

**Files:** Create `Assets/_App/Scripts/SpatialUi/HeadFade.cs`

A small unlit quad parented to the HMD camera, drawn on top, whose material alpha ramps 0↔1. Async API the runner awaits.

- [ ] **Step 1: Implement `HeadFade.cs`**
```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// A head-attached fade overlay for VR scene transitions. Lives on a small unlit quad parented to
// the HMD camera (so it covers both eyes uniformly — no 2D screen overlay). The transition runner
// awaits FadeAsync to black before loading and back to clear after.
public class HeadFade : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;   // unlit, transparent material on the quad
    [SerializeField] private float     _defaultDuration = 0.25f;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _mpb;
    private float _alpha;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        Apply(); // start clear
    }

    public void SetAlphaImmediate(float a)
    {
        _alpha = Mathf.Clamp01(a);
        Apply();
    }

    public async Task FadeAsync(float targetAlpha, CancellationToken token, float? duration = null)
    {
        float dur   = duration ?? _defaultDuration;
        float start = _alpha;
        float t     = 0f;
        if (dur <= 0f) { SetAlphaImmediate(targetAlpha); return; }
        while (t < dur)
        {
            token.ThrowIfCancellationRequested();
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.Lerp(start, Mathf.Clamp01(targetAlpha), Mathf.Clamp01(t / dur));
            Apply();
            await Task.Yield();
        }
        _alpha = Mathf.Clamp01(targetAlpha);
        Apply();
    }

    private void Apply()
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        var c = Color.black; c.a = _alpha;
        _mpb.SetColor(ColorId, c);
        _renderer.SetPropertyBlock(_mpb);
        _renderer.enabled = _alpha > 0.001f;
    }
}
```

- [ ] **Step 2 (controller): compile.** No `CS` errors.
- [ ] **Step 3: Checkpoint (user commits)** — `feat(transition): add HeadFade VR fade quad`

---

### Task 2: `ISceneTransition` + `SceneTransitionRunner`

**Files:** Create `Assets/_App/Scripts/ModeOrchestrator/ISceneTransition.cs`, `Assets/_App/Scripts/ModeOrchestrator/SceneTransitionRunner.cs`

- [ ] **Step 1: Implement `ISceneTransition.cs`**
```csharp
using System;

// Mechanism behind ModeOrchestrator: load a scene single-mode behind a fade, then invoke onLoaded
// once the new scene (and its LifetimeScope) is live. Implemented by the persistent
// SceneTransitionRunner. Kept as an interface so ModeOrchestrator stays unit-testable.
public interface ISceneTransition
{
    bool IsTransitioning { get; }
    void Load(string sceneName, Action onLoaded);
}
```

- [ ] **Step 2: Implement `SceneTransitionRunner.cs`**
```csharp
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent MonoBehaviour (lives on PersistentRoot, survives DontDestroyOnLoad). Owns the head
// fade and the async single-scene load. A re-entrancy guard drops Load calls while a transition is
// already running. onLoaded fires after the new scene is loaded+activated (its LifetimeScope has
// built), before the fade-in — ModeOrchestrator publishes ModeChangedEvent there.
public class SceneTransitionRunner : MonoBehaviour, ISceneTransition
{
    [SerializeField] private HeadFade _fade;

    public bool IsTransitioning { get; private set; }

    private CancellationTokenSource _cts;

    public void Load(string sceneName, Action onLoaded)
    {
        if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return;
        IsTransitioning = true;
        _cts = new CancellationTokenSource();
        _ = RunAsync(sceneName, onLoaded, _cts.Token);
    }

    // Cold-boot helper: start fully black, load the first scene, fade in. Used by AppBootstrap.
    public void LoadInitial(string sceneName, Action onLoaded)
    {
        if (_fade != null) _fade.SetAlphaImmediate(1f);
        Load(sceneName, onLoaded);
    }

    private async System.Threading.Tasks.Task RunAsync(string sceneName, Action onLoaded, CancellationToken token)
    {
        try
        {
            if (_fade != null) await _fade.FadeAsync(1f, token);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
            {
                token.ThrowIfCancellationRequested();
                await System.Threading.Tasks.Task.Yield();
            }
            // Single load makes the new scene the active scene automatically; its LifetimeScope
            // built during the load. Now it is safe to announce the mode change.
            onLoaded?.Invoke();

            if (_fade != null) await _fade.FadeAsync(0f, token);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex) { Debug.LogError($"SceneTransitionRunner: load '{sceneName}' failed: {ex}"); }
        finally { IsTransitioning = false; }
    }

    private void OnDestroy() => _cts?.Cancel();
}
```

- [ ] **Step 3 (controller): compile.** No `CS` errors.
- [ ] **Step 4: Checkpoint (user commits)** — `feat(transition): add ISceneTransition + SceneTransitionRunner (async single load + fade)`

---

### Task 3: Refactor `ModeOrchestrator` (policy + ordering fix) — TDD

**Files:** Modify `Assets/_App/Scripts/ModeOrchestrator/ModeOrchestrator.cs`; Test `Assets/_App/Tests/ModeOrchestrator/ModeOrchestratorTests.cs`

The orchestrator stops calling `SceneManager` directly. It validates the transition, sets `CurrentMode`, asks `ISceneTransition` to load, and publishes `ModeChangedEvent` in the `onLoaded` callback (after the scene exists).

- [ ] **Step 1: Write the failing tests** — create `Assets/_App/Tests/ModeOrchestrator/ModeOrchestratorTests.cs`:
```csharp
using NUnit.Framework;

public class ModeOrchestratorTests
{
    private class FakeTransition : ISceneTransition
    {
        public string LastScene;
        public System.Action Pending;
        public bool IsTransitioning { get; set; }
        public void Load(string sceneName, System.Action onLoaded) { LastScene = sceneName; Pending = onLoaded; }
        public void CompleteLoad() { Pending?.Invoke(); Pending = null; }
    }

    private static ModeTransitionGraph AllowAllGraph()
    {
        // ModeTransitionGraph is a ScriptableObject; create an in-memory instance with all
        // transitions allowed for the test. If its API differs, adapt to expose IsAllowed==true.
        var g = ScriptableObject.CreateInstance<ModeTransitionGraph>();
        return g;
    }

    [Test]
    public void TransitionTo_PublishesModeChanged_OnlyAfterLoadCompletes()
    {
        var bus = new EventBus();
        var fake = new FakeTransition();
        var sut = new ModeOrchestrator(bus, AllowAllGraph(), fake);

        bool fired = false;
        bus.Subscribe<ModeChangedEvent>(_ => fired = true);

        sut.TransitionTo(AppMode.VrEditing);
        Assert.AreEqual("VrEditing", fake.LastScene);
        Assert.IsFalse(fired, "ModeChangedEvent must NOT fire before the scene finishes loading");

        fake.CompleteLoad();
        Assert.IsTrue(fired, "ModeChangedEvent fires after onLoaded");
        Assert.AreEqual(AppMode.VrEditing, sut.CurrentMode);
    }

    [Test]
    public void TransitionTo_SameMode_NoOp()
    {
        var bus = new EventBus();
        var fake = new FakeTransition();
        var sut = new ModeOrchestrator(bus, AllowAllGraph(), fake);
        sut.TransitionTo(AppMode.MainMenu); // already MainMenu
        Assert.IsNull(fake.LastScene);
    }
}
```
> If `ModeTransitionGraph.IsAllowed` returns false for an empty SO, adjust `AllowAllGraph()` to populate it (read `ModeTransitionGraph.cs` for its serialized shape) so `MainMenu→VrEditing` is allowed. The test's intent: prove ordering (event after load) and the same-mode no-op.

- [ ] **Step 2: Run tests → FAIL** (ModeOrchestrator has no `ISceneTransition` constructor param yet).

- [ ] **Step 3: Rewrite `ModeOrchestrator.cs`**
```csharp
using UnityEngine;

public class ModeOrchestrator
{
    private readonly EventBus            _bus;
    private readonly ModeTransitionGraph _graph;
    private readonly ISceneTransition    _transition;

    private AppMode _current = AppMode.MainMenu;
    public AppMode CurrentMode => _current;

    public ModeOrchestrator(EventBus bus, ModeTransitionGraph graph, ISceneTransition transition)
    {
        _bus        = bus;
        _graph      = graph;
        _transition = transition;
    }

    public void TransitionTo(AppMode target)
    {
        if (_current == target) return;
        if (_transition.IsTransitioning) return;
        if (!_graph.IsAllowed(_current, target))
        {
            Debug.LogWarning($"Transition {_current} → {target} not allowed");
            return;
        }

        var prev = _current;
        _current = target;

        _transition.Load(SceneNameFor(target), () =>
            _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target }));
    }

    private static string SceneNameFor(AppMode mode) => mode switch
    {
        AppMode.MainMenu  => "MainMenu",
        AppMode.VrEditing => "VrEditing",
        AppMode.Sandbox   => "Sandbox",
        _                 => null,
    };
}
```
(The old `OnSceneLoadedSetActive`/`LoadScene`/`UnloadCurrentScene` + `SceneManager`/`using SceneManagement` are deleted — the runner owns scene mechanics now.)

- [ ] **Step 4: Run tests → PASS.**
- [ ] **Step 5: Checkpoint (user commits)** — `refactor(mode): ModeOrchestrator delegates to ISceneTransition; publish ModeChanged after load`

---

### Task 4: Refactor `AppBootstrap` (DDOL + first load via runner)

**Files:** Modify `Assets/_App/Scripts/Bootstrap/AppBootstrap.cs`

- [ ] **Step 1: Rewrite `AppBootstrap.cs`**
```csharp
using UnityEngine;

// Boot entry: marks the persistent infrastructure root as DontDestroyOnLoad, then loads the first
// mode scene single-mode through the transition runner (which fades in from black). After the first
// single load, the bootstrap scene itself unloads — only PersistentRoot survives.
public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    [SerializeField] private GameObject            _persistentRoot;     // the infra root to keep alive
    [SerializeField] private SceneTransitionRunner _transitionRunner;   // lives under _persistentRoot

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_persistentRoot != null) DontDestroyOnLoad(_persistentRoot);

        if (_transitionRunner != null)
            _transitionRunner.LoadInitial(MAIN_MENU_SCENE, null);
        else
            Debug.LogError("AppBootstrap: _transitionRunner not assigned — first scene will not load.");
    }
}
```
> `_persistentRoot` and `_transitionRunner` are assigned in the Editor (Task 6). `AppBootstrap` itself sits OUTSIDE `_persistentRoot` (it is a one-shot boot script; it dies when the bootstrap scene unloads after the first single load).

- [ ] **Step 2 (controller): compile.** No `CS` errors.
- [ ] **Step 3: Checkpoint (user commits)** — `refactor(bootstrap): DDOL persistent root + first load via transition runner`

---

### Task 5: Register transition in `RootLifetimeScope`

**Files:** Modify `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

`ModeOrchestrator` now needs `ISceneTransition`. The runner is a persistent MonoBehaviour found and registered like `UserPanel`.

- [ ] **Step 1: Register the runner as `ISceneTransition` before `ModeOrchestrator`.** Near the top of `Configure`, before `builder.Register<ModeOrchestrator>(...)`, add:
```csharp
        var transition = Object.FindAnyObjectByType<SceneTransitionRunner>(FindObjectsInactive.Include);
        if (transition != null)
            builder.RegisterInstance(transition).As<ISceneTransition>();
        else
            Debug.LogError("RootLifetimeScope: SceneTransitionRunner not found — mode transitions will fail.");
```
(`ModeOrchestrator`'s constructor `(EventBus, ModeTransitionGraph, ISceneTransition)` now resolves. `ModeTransitionGraph` is already registered via `RegisterInstance(_transitionGraph)`.)

- [ ] **Step 2 (controller): compile.** No `CS` errors. Run full `_App.Tests` — `ModeOrchestratorTests` green, no regressions beyond the known 7 baseline failures.
- [ ] **Step 3: Checkpoint (user commits)** — `feat(scope): register SceneTransitionRunner as ISceneTransition`

---

### Task 6: [MANUAL EDITOR] PersistentRoot, DDOL, fade quad, runner wiring

This task is Unity Editor work (cannot be done by a code-only subagent). Do it carefully and verify in the Hierarchy.

- [ ] **Step 1: Group infrastructure under `PersistentRoot`.** Open `Bootstrap.unity`. Create an empty root GameObject `PersistentRoot`. Re-parent under it everything that must survive scene loads: the `[RootScope]` (RootLifetimeScope) object, the XR rig (`User XR Origin (XR Rig)` with `UserPanel`, keyboard, etc.), `PlayerSpawnApplier`, and any other persistent infra. **Leave `AppBootstrap` OUTSIDE** `PersistentRoot` (it must die with the bootstrap scene). Verify nothing else references those objects by scene position.

- [ ] **Step 2: Add the fade quad.** Under the HMD camera (the Main Camera inside the XR rig), create a small Quad ~0.3 m in front, facing the camera, on a layer the camera renders. Give it an **unlit, transparent** material (URP/Unlit, Surface=Transparent, with `_BaseColor`). Add the `HeadFade` component; assign its `_renderer` to the quad's `MeshRenderer`. Disable the renderer by default (HeadFade does this when alpha≈0). Confirm it covers the view when alpha=1 (test by `SetAlphaImmediate(1)`), and does not z-fight or occlude UI when alpha=0.

- [ ] **Step 3: Add the runner.** On a child of `PersistentRoot` (e.g., the XR rig root or a dedicated `SceneTransition` GO), add the `SceneTransitionRunner` component; assign its `_fade` to the `HeadFade`.

- [ ] **Step 4: Wire `AppBootstrap`.** On the `AppBootstrap` object, assign `_persistentRoot` = the `PersistentRoot` GameObject and `_transitionRunner` = the `SceneTransitionRunner`.

- [ ] **Step 5: Strip residual visuals from Bootstrap.** Ensure the bootstrap scene has no Lights/Skybox/Fog that would have bled (per the superseded isolation spec) — with single load they no longer bleed, but bootstrap unloads anyway, so just confirm `PersistentRoot` carries no Lights.

- [ ] **Step 6: Build Settings.** Confirm `Bootstrap` is scene index 0 and `MainMenu`/`VrEditing`/`Sandbox` are in Build Settings.

- [ ] **Step 7: Save the scene + prefab.**

---

### Task 7: Verify + remove dead additive code

- [ ] **Step 1 (controller): grep for leftovers.** Confirm no remaining `LoadSceneMode.Additive`, `SetActiveScene`, or `UnloadSceneAsync` in `Assets/_App/Scripts` (the runner uses `Single`). Remove any now-dead helpers.
- [ ] **Step 2 (controller): compile + full tests.** No `CS`; `ModeOrchestratorTests` + `SceneContextTests` green; baseline 7 failures unchanged.
- [ ] **Step 3 (user, in VR/Play): verify**
  - Cold start: app fades in from black to MainMenu; only `PersistentRoot` + MainMenu present in the Hierarchy (bootstrap scene unloaded; the mode scene is the bold/active scene).
  - MainMenu → VrEditing: fade-out → load → fade-in; UserPanel/XR rig persist; SceneContext binds (panels populate); no render-bleed (VrEditing's lighting/skybox only).
  - VrEditing → Sandbox → MainMenu round-trips cleanly; only one mode scene loaded at a time; no double-lighting; `ModeChangedEvent`-driven UI (nav button visibility) updates after each load.
  - Rapid double-trigger of a transition does nothing harmful (re-entrancy guard).
- [ ] **Step 4: Checkpoint (user commits)** — `chore(scene): remove additive/SetActiveScene remnants; verify single+DDOL transitions`

---

## Self-Review

**Spec coverage (§1, §3):** single + DDOL `PersistentRoot` (Tasks 4, 6) ✓; one scene at a time via `LoadSceneMode.Single` (Task 2) ✓; render-bleed solved by single load, `SetActiveScene` removed (Task 7) ✓; policy/mechanism split `ModeOrchestrator`↔`ISceneTransition`/`SceneTransitionRunner` (Tasks 2, 3) ✓; async + VR head-fade (Tasks 1, 2, 6) ✓; `ModeChangedEvent` published after load via `onLoaded` (Task 3, proven by `ModeOrchestratorTests`) ✓; re-entrancy guard (Task 2 runner + Task 3 `IsTransitioning` check) ✓; bootstrap unloads after first load (Task 4) ✓; old rejection reason resolved because infra is DDOL (Tasks 4, 6) ✓.

**Placeholder scan:** the only soft spots are the `[MANUAL EDITOR]` steps (inherently editor actions, described precisely) and the `AllowAllGraph()` test helper (with an explicit instruction to adapt to `ModeTransitionGraph`'s real shape). No code-step placeholders.

**Type consistency:** `ISceneTransition { bool IsTransitioning; void Load(string, Action); }` — implemented by `SceneTransitionRunner`, consumed by `ModeOrchestrator` and the `FakeTransition` test double identically. `HeadFade.FadeAsync(float, CancellationToken, float?)` matches the runner's awaits. `ModeOrchestrator(EventBus, ModeTransitionGraph, ISceneTransition)` matches the Task-5 registration.

**Risks:** DDOL only persists root GOs → the single `PersistentRoot` (Task 6) is load-bearing; verify every infra object ends up under it. VContainer scene scopes still parent to `RootLifetimeScope` by `TypeName` — unchanged since root persists. Async transition: `onLoaded` runs after `LoadSceneAsync` completes (scene + scope built) — correct ordering. First-frame: `LoadInitial` starts fully black so no unloaded frame is shown.

---

## Next plan
**Plan D** — remove `FeatureLifetimeScope` (docs-only), update CLAUDE.md/architecture docs to the single+DDOL + SceneContext model, archive implemented specs/plans (incl. marking `2026-05-20-scene-loading-isolation` superseded by this plan).
