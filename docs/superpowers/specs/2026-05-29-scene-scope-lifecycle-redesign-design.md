# Scene & Scope Lifecycle Redesign — Design

> **Status:** design (awaiting review).
> Supersedes `2026-05-20-scene-loading-isolation.md` (the additive-load + `SetActiveScene` workaround is replaced by single-scene loading).

**Goal:** Replace additive scene loading with **single-scene loading + DontDestroyOnLoad infrastructure**, fix the persistent-panel ↔ scene-service lifetime mismatch via a root-scoped `SceneContext`, fix `ModeChangedEvent` ordering, remove the (docs-only) `FeatureLifetimeScope` concept, and bring documentation + the planning archive in line with the result.

**Why now:** Two real "orange" risks from the lifecycle review:
1. **Lifetime mismatch** — persistent UI panels (nested in the persistent `UserPanel`) hold direct references to scene-scoped services (`SceneGraph`, `SelectionManager`, `CommandStack`, …). When a mode scene unloads, those services are disposed but the panels keep dead references until re-injection.
2. **Ordering** — `ModeOrchestrator.TransitionTo` publishes `ModeChangedEvent` synchronously, before the new scene and its scope exist.

The user also wants the bootstrap scene to stop lingering after start (everything persistent moves to DDOL, exactly one mode scene loaded at a time), and `FeatureLifetimeScope` (which exists only in docs) removed.

**Tech stack:** Unity 6000.3.7f1, `UnityEngine.SceneManagement`, VContainer, custom `EventBus`. No new dependencies.

---

## Constraints

- Single runtime assembly (`_App.Runtime`), no namespaces for runtime code. VContainer DI (Root → Scene). Custom `EventBus` (`Publish<T>`/`Subscribe<T>`). Forbidden: `FindObjectOfType`/`GameObject.Find` at runtime (the sanctioned exception is `FindAnyObjectByType` + `c.Inject` inside `LifetimeScope.Configure`), singletons, `static` mutable state, generic type suffixes (`Manager`/`Handler`/`Controller`/…).
- `DontDestroyOnLoad` works only on **root** GameObjects.

---

## Section 1 — Loading model & persistence

**Current:** `Bootstrap` (build index 0) never unloads and holds all infrastructure; mode scenes load **additively** on top; `SetActiveScene` is called manually so the mode scene's `RenderSettings` win over bootstrap's.

**New:**
- All persistent infrastructure is grouped under **one root GameObject** `PersistentRoot` in `Bootstrap`: `RootLifetimeScope`, the XR rig (camera, controllers, `UserPanel` + all nested module panels, `WorldClickCatcher`, `VrKeyboard`, the head fade mesh), `PlayerSpawnApplier`, the `SceneTransitionRunner` (§3). `AppBootstrap.Start` calls `DontDestroyOnLoad(persistentRoot)`, then loads the first mode scene with `LoadSceneMode.Single` (via the runner — see §3).
  - Single root because DDOL only persists root GameObjects. (Manual scene edit: re-parent existing infra under `PersistentRoot`.)
- **A mode is the single loaded scene** on top of the DDOL set. A transition is `LoadSceneAsync(target, Single)`: the current mode scene unloads (its scene `LifetimeScope` disposes → `SceneContext` cleared, §2), the target loads.
- Single load ⇒ the loaded scene is automatically the active scene ⇒ its `RenderSettings` drive the frame ⇒ **render-bleed is solved structurally**. The manual `SetActiveScene` calls and the `scene-loading-isolation` workaround are removed. The DDOL infrastructure carries no Lights/skybox, so nothing bleeds.
- `Bootstrap` itself unloads after the first single load — its only job is to set up DDOL and kick the first load.

**Resolves the old rejection reason.** `scene-loading-isolation` rejected single + DDOL because VContainer's `FindAnyObjectByType` references (`UserPanel`, `AssetBrowserPanel`, `PlayerSpawnApplier`, `VrKeyboard`) "would be destroyed and re-created each transition." Marking those objects DDOL keeps them alive; `RootLifetimeScope` persists, so scene scopes still parent to it by `parentReference.TypeName = RootLifetimeScope`.

---

## Section 2 — `SceneContext` (variant B)

The root-scoped indirection that removes the lifetime mismatch. **Three new types:**

### `SceneContext` (root singleton)
A façade exposing the currently-loaded scene's scoped services as **read-only, nullable** properties:

```
ISceneGraph        Graph;
ISelectionManager  Selection;
CommandStack       Commands;
GizmoController     Gizmo;
AnimationAuthoring  Authoring;
AnimationClock      Clock;
IRigRuntime        Rig;
bool HasScene => Graph != null;
```

Populated/cleared only through `internal void Bind(...)` / `internal void Clear()`.

### `SceneContextChangedEvent` (struct)
Published when the context is bound (scene services available) and when cleared (no scene). Consumers read state from `SceneContext.HasScene`. This is a **DI-lifecycle signal**, distinct from `SceneOpenedEvent` (scene-data) and `ModeChangedEvent` (panel visibility) — the three coexist.

### `SceneContextBinder` (scene-scoped `IStartable, IDisposable`)
Registered as an entry point in each scene scope. The **single place** that manages the context:
- `Start` (after scope build): resolve the scene services → `ctx.Bind(...)` → publish `SceneContextChangedEvent`.
- `Dispose` (scene scope torn down on unload): `ctx.Clear()` → publish `SceneContextChangedEvent`.

### What does not change
Scene services stay registered `Scoped` in the scene scopes. **Scene-resident** consumers (`XRPromeonInteractable`, `BoneProxy`, and other scripts on spawned/rig objects) keep resolving services directly from the scene scope — they die with the scene, so they need no `SceneContext`.

### Consumer changes (persistent panels only)
The persistent consumers that today hold direct scene-service references:

| Consumer | Scene services it holds today |
|---|---|
| `OutlinerPanel` | graph, selection |
| `InspectorPanel` | graph, selection |
| `AnimatorPanel` | authoring, clock, selection, graph |
| `PropertyPanel` | sceneGraph |
| `BoneInspectorPanel` | rigRuntime, selection, sceneGraph |
| `IkWizardPanel` | rigRuntime, selection, sceneGraph |
| `WorldClickCatcher` | selection |
| `GizmoActivator` | graph, selection, gizmoController |
| `UndoKeyHandler` | commandStack |

> The exact persistent-vs-scene-resident status of `GizmoActivator`, `WorldClickCatcher`, and `UndoKeyHandler` is confirmed per-object during planning. Scene-resident ones are left untouched; only persistent ones migrate to `SceneContext`.

Each persistent consumer:
- takes `SceneContext` (plus root services like `EventBus`) in `Construct` instead of the individual scene services;
- reads via `_ctx.Graph?.…` and **never caches `_ctx.Graph` in a field** — the invariant that makes a stale direct reference impossible;
- subscribes to `SceneContextChangedEvent`: on bind → rebuild UI from the new scene; on unbind → clear UI.

Null-guards on use are inherent to "a persistent object can outlive scene services" and are required regardless of approach; in practice these consumers act on root `EventBus` events that only fire while a scene is live, so guards are mostly defensive.

---

## Section 3 — Transition sequencing (async + VR fade) & ordering fix

Separate **policy** from **mechanism** (this also makes `ModeOrchestrator` unit-testable — today it calls `SceneManager` directly):

- **`ModeOrchestrator`** (root, plain C#) — policy: validate the transition against `ModeTransitionGraph`, track `CurrentMode`, choose the scene name, delegate the swap.
- **`ISceneTransition`** ← **`SceneTransitionRunner`** (persistent `MonoBehaviour` on `PersistentRoot`, DDOL) — mechanism: owns the head fade mesh and the async sequence. Resolved at root via `FindAnyObjectByType` + `RegisterInstance` (same pattern as `UserPanel`).

**Sequence:**
1. `ModeOrchestrator.TransitionTo(target)`: graph check → `_current = target` → `_transition.LoadAsync(sceneName, onLoaded)`.
2. Runner coroutine: **fade-out** → `LoadSceneAsync(sceneName, Single)` (await) → old scene + scope dispose (`Binder.Clear` + event), new scene loads, its scope builds → `SceneContextBinder.Start` (`Bind` + `SceneContextChangedEvent`).
3. Runner invokes `onLoaded` → `ModeOrchestrator` publishes **`ModeChangedEvent`** (scene and context now exist — **ordering fixed**) → runner runs **fade-in**.

Event order is `SceneContextChanged(bound)` → `ModeChanged`: panels get data first, then the router applies visibility. `CurrentMode` is set before the load, so the new scope's build-callback (`router.ApplyMode(CurrentMode)`) already sees the right mode; the later `ModeChangedEvent` re-applies idempotently.

**VR fade:** `HeadFade` — an unlit quad/sphere parented to the HMD camera on `PersistentRoot`, alpha animated 0↔1, drawn on top. Because it rides the head it dims both eyes uniformly — no 2D screen overlay, no stereo conflict.

**Folded-in robustness fixes:**
- **Re-entrancy guard** in the runner: while a transition runs, further `TransitionTo` calls are ignored (or at most one queued).
- The runner awaits **its own `AsyncOperation`** rather than the global `sceneLoaded`, removing the old `OnSceneLoadedSetActive` "doesn't filter by scene name" fragility.
- `SetActiveScene` is removed (single load makes the loaded scene active automatically).
- Cold start (`AppBootstrap`) uses the same runner: start fully faded-out → load `MainMenu` → fade-in.

---

## Section 4 — Remove `FeatureLifetimeScope`, docs & planning cleanup

**`FeatureLifetimeScope` is docs-only (no code).** Remove its mentions and record the reality: the per-mode scope is the scene's own `LifetimeScope`; `RigRuntime`/animation live in the scene scope.

**Docs to update (targeted edits, not rewrites):**
- `CLAUDE.md` — scope-hierarchy table (drop the FeatureLifetimeScope row; describe single + DDOL, `PersistentRoot`, `SceneContext`, `ModeOrchestrator` + `ISceneTransition`); App Modes and Cross-Subsystem sections (add `SceneContextChangedEvent`; note a transition is a single load).
- `Assets/_App/Documentation/conventions.md`, `Assets/_App/Documentation/architecture_context.md` — same corrections.
- `.planning/docs/ARCHITECTURE.md` — scene loading + scopes.

**Archive implemented specs/plans** → move into `docs/superpowers/_archive/{specs,plans}/` (preserve structure) with a short `_archive/README.md` index. Verify each is actually implemented before moving (don't archive unfinished work):
- `2026-05-20-scene-loading-isolation` (spec + plan) → add a top-line `> Superseded by 2026-05-29-scene-scope-lifecycle-redesign` before archiving.
- Implemented: `2026-05-29-spatialui-region-model` (+ `region-prefab-verification`), the navbar series, `2026-05-16-userpanel-filebrowser-fixes`, `asset-browser-spawn-filebrowser`, `scene-objects-selection-outliner`, `double-userpanel-cursor-fix`, `player-anchor-fall-guard`.

**Minor code hygiene (folded in):** remove the leftover pre-existing `[RegionDBG]` debug logs (`PanelRegionRouter.RegisterModule`/`RegisterButton`, `RegionNavButton.OnClick`) — the region work is complete.

**Ordering:** docs + archival are the **last** step, after the code lands and is verified, so they reflect the final state.

---

## Testing

- **`SceneContext`** bind/clear and **`SceneContextBinder`** — EditMode unit tests with fakes.
- **`ModeOrchestrator`** — now unit-testable through a fake `ISceneTransition`: graph enforcement, `CurrentMode` update, and that `ModeChangedEvent` fires only after `onLoaded`.
- **Existing** `PanelRegionRouterTests` stay green.
- **Manual VR checks:** fade in/out on transition; panels clear on scene unbind and rebuild on bind; round-trip MainMenu ↔ VrEditing ↔ Sandbox; no render-bleed; no missing-script warnings; `Bootstrap` unloads after start (only the mode scene + DontDestroyOnLoad are present in the hierarchy).

---

## Risks & mitigations

- **DDOL requires root GameObjects** → single `PersistentRoot` (manual scene restructure; verify nothing critical stays outside it).
- **VContainer parent resolution** — scene scopes parent to `RootLifetimeScope` by type; since root persists under DDOL, this is unchanged. Verify scene scopes still resolve root singletons after the restructure.
- **`SceneTransitionRunner` is a persistent MonoBehaviour resolved at root** — register it (and the fade) before `ModeOrchestrator` is first resolved.
- **Async transition timing** — ensure `SceneContextBinder.Start` runs before `onLoaded`/`ModeChangedEvent` (it does: scope build completes within the `LoadSceneAsync` completion before the runner continues).
- **First-frame fade** — start cold in the faded-out state so the user never sees an unloaded frame.

---

## Out of scope

- Reworking scene-dependent panels to be scene-resident (the rejected "approach C"); the docked `UserPanel` layout stays, panels stay persistent and read through `SceneContext`.
- Baked lighting / reflection probes per scene.
- Any change to the region model, file browser, or UserPanel-default work already completed this milestone.
