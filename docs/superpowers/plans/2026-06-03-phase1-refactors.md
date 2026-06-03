# Phase 1 — Stage-5 Refactors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the six selected Stage-5 refactors (A1, B1, A2, B2, A4, B6) to reduce god-classes and duplicated logic without changing user-facing behavior, except removing the dead undo subsystem.

**Architecture:** Unity 6 / VContainer DI / EventBus / no-namespace runtime C#. A1 splits the 726-line `AnimationAuthoring` into a CRUD façade + static `AnimationClipBaker` + `AnimationPlaybackSampler` (`ITickable`) + `AnimationStorage`. B1/A2/B6 dedup/extract; B2 deletes the undo subsystem; A4 extracts a `BoneEditMode` service. Each block is independently committable.

**Tech Stack:** Unity 6000.3.7f1, VContainer, NUnit EditMode tests (run via MCP `run_tests`), C#.

**Spec:** `docs/superpowers/specs/2026-06-03-phase1-refactors-design.md`

---

## Conventions for every task

- **Run a test (MCP):** `run_tests` with `mode=EditMode`, `test_filter=<ClassName>`. Poll `get_test_job` until done.
- **After editing any `.cs`:** `refresh_unity` (`scope=scripts`, `compile=request`, `wait_for_ready=true`), then a second `refresh_unity` (`wait_for_ready=true`) to settle the domain reload, then `read_console` (`types=["error"]`). "Clean" = no `CS####` errors (the pre-existing `CS0618` glTFast warning in `GltfModelLoader` is unrelated).
- **Archive a file:** `manage_asset` `action=move` `path=_App/Scripts/<old>` `destination=_App/Scripts/_Archive/<name>`. The call returns `success:false` but usually succeeds — **always verify with Glob** `Assets/_App/Scripts/_Archive/*.cs`.
- **Commit:** plain message, **no AI-trace trailer**, e.g. `git commit -m "Split AnimationAuthoring: extract AnimationClipBaker"`. Branch is `review-2026-06-03`.
- No-namespace runtime code; one public type per file; file name == type name.

---

# BLOCK A1 — Split `AnimationAuthoring`

> Order: Baker → Storage → Sampler → façade re-wire. Source: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`.

## Task A1.1: Extract `AnimationClipBaker` (static)

**Files:**
- Create: `Assets/_App/Scripts/Animation/AnimationClipBaker.cs`
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` (remove `BuildClip` `:667-698`, `ApplyInterpolation` `:700-725`; replace call sites)
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringInterpolationTests.cs` → rename to `AnimationClipBakerTests.cs`

- [ ] **Step 1: Create the static class.** Move `BuildClip` (`:667-698`) and `ApplyInterpolation` (`:700-725`) **verbatim** into the new file as `public static` methods of `AnimationClipBaker` (they are already `static`-friendly; `BuildClip` is currently `private`, make it `public static`; `ApplyInterpolation` is already `internal static`, make it `public static`).

```csharp
using UnityEngine;

// Pure baking of a keyframe track into a legacy AnimationClip + interpolation tangents.
public static class AnimationClipBaker
{
    public static AnimationClip BuildClip(AnimTrackData track, int fps, InterpolationMode mode)
    {
        // ... body moved verbatim from AnimationAuthoring.BuildClip (was :667-698) ...
    }

    public static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)
    {
        // ... body moved verbatim from AnimationAuthoring.ApplyInterpolation (was :700-725) ...
    }
}
```

- [ ] **Step 2: Replace call sites in `AnimationAuthoring.cs`.** Three calls to `BuildClip(...)` (in `RebuildActiveClips` `:124`, `StartLoopPlayback` `:185`, `RebuildLoopClips` `:203`) → `AnimationClipBaker.BuildClip(...)`. Delete the now-removed `BuildClip`/`ApplyInterpolation` method bodies from `AnimationAuthoring`.

- [ ] **Step 3: Re-point the test.** In `AnimationAuthoringInterpolationTests.cs`, change the two `AnimationAuthoring.ApplyInterpolation(...)` calls (`:12`, `:22`) to `AnimationClipBaker.ApplyInterpolation(...)`. Move the two `ApplyInterpolation_*` tests into a new `AnimationClipBakerTests` class (new file `AnimationClipBakerTests.cs`); leave `SetInterpolation_UpdatesValue_...` in `AnimationAuthoringInterpolationTests.cs` (it tests the façade). Rename old file only if it now holds just the façade test — keep it.

- [ ] **Step 4: Compile + test.** `refresh_unity` ×2 + `read_console` (clean). Run (MCP) `run_tests` EditMode `test_filter=AnimationClipBakerTests` → PASS; and `test_filter=AnimationAuthoringInterpolationTests` → PASS.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Split AnimationAuthoring: extract static AnimationClipBaker"`

## Task A1.2: Extract `AnimationStorage` (+ B4 non-destructive)

**Files:**
- Create: `Assets/_App/Scripts/Animation/AnimationStorage.cs`
- Create: `Assets/_App/Tests/Animation/AnimationStorageTests.cs`
- Modify: `AnimationAuthoring.cs` (remove `_saveCts`/`SAVE_DEBOUNCE_MS` `:25-26`, `RequestSave` `:253-258`, `DebouncedSave` `:260-269`, `SaveAsync` `:650-663`, `LoadAsync` `:601-648`; delegate to storage)
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`

- [ ] **Step 1: Write the failing B4 test first.** New `AnimationStorageTests.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class AnimationStorageTests
{
    private string _root;
    private PathProvider _paths;

    [SetUp] public void SetUp()
    {
        _root  = Path.Combine(Path.GetTempPath(), "animstore-" + System.Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "scenes", "s1"));
        _paths = new PathProvider(_root);
    }
    [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Test]
    public async Task LoadAsync_OldVersionFile_IsLeftOnDisk_AndReturnsEmpty()
    {
        var path = _paths.AnimationJson("s1");
        File.WriteAllText(path, "{\"schemaVersion\":1}");
        var sut  = new AnimationStorage(_paths);

        var data = await sut.LoadAsync("s1", CancellationToken.None);

        Assert.IsNotNull(data, "returns fresh data");
        Assert.AreEqual(0, data.Containers.Count, "empty");
        Assert.IsTrue(File.Exists(path), "B4: old file must NOT be deleted");
    }

    [Test]
    public async Task LoadAsync_MissingFile_ReturnsEmpty()
    {
        var sut  = new AnimationStorage(_paths);
        var data = await sut.LoadAsync("s1", CancellationToken.None);
        Assert.IsNotNull(data);
        Assert.AreEqual(0, data.Containers.Count);
    }
}
```

- [ ] **Step 2: Run it — expect FAIL** (`AnimationStorage` undefined). `run_tests` EditMode `test_filter=AnimationStorageTests` → FAIL.

- [ ] **Step 3: Create `AnimationStorage`.** Move `LoadAsync`/`SaveAsync`/`RequestSave`/`DebouncedSave` bodies from `AnimationAuthoring`. **B4 change:** in `LoadAsync`, in the `schemaVersion < 2` branch, **delete the `File.Delete(path)` block** (`:621-628`) — just log once and return a fresh `SceneAnimationData`; keep the `> 2` branch as-is.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Owns animation.json load/save + debounced write. Non-destructive on unsupported versions.
public class AnimationStorage : IDisposable
{
    private readonly PathProvider _paths;
    private CancellationTokenSource _saveCts;
    private const int SAVE_DEBOUNCE_MS = 200;

    [VContainer.Inject] public AnimationStorage(PathProvider paths) => _paths = paths;

    public async Task<SceneAnimationData> LoadAsync(string sceneId, CancellationToken ct)
    {
        var path = _paths.AnimationJson(sceneId);
        if (!File.Exists(path)) return new SceneAnimationData();
        try
        {
            var json   = await File.ReadAllTextAsync(path, ct);
            var loaded = JsonUtility.FromJson<SceneAnimationData>(json);
            if (loaded == null || loaded.schemaVersion < 2 || loaded.schemaVersion > 2)
            {
                Debug.LogWarning($"AnimationStorage: '{path}' has unsupported schemaVersion="
                    + $"{loaded?.schemaVersion ?? 0}. Opening empty; file left untouched.");
                return new SceneAnimationData();
            }
            if (loaded.Fps <= 0)
                loaded.Fps = loaded.Containers.Count > 0 ? Mathf.Max(1, loaded.Containers[0].Fps) : 24;
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogError($"AnimationStorage: load failed '{path}': {ex.Message}");
            return new SceneAnimationData();
        }
    }

    public void RequestSave(SceneAnimationData data, string sceneId)
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = DebouncedSave(data, sceneId, _saveCts.Token);
    }

    private async Task DebouncedSave(SceneAnimationData data, string sceneId, CancellationToken ct)
    {
        try { await Task.Delay(SAVE_DEBOUNCE_MS, ct); if (!ct.IsCancellationRequested) await SaveAsync(data, sceneId, ct); }
        catch (TaskCanceledException) { }
    }

    private async Task SaveAsync(SceneAnimationData data, string sceneId, CancellationToken ct)
    {
        if (data == null || string.IsNullOrEmpty(sceneId)) return;
        try
        {
            var path = _paths.AnimationJson(sceneId);
            await File.WriteAllTextAsync(path, JsonUtility.ToJson(data, true), ct);
        }
        catch (Exception ex) { Debug.LogError($"AnimationStorage: save failed: {ex.Message}"); }
    }

    public void Dispose() { _saveCts?.Cancel(); _saveCts?.Dispose(); }
}
```

- [ ] **Step 4: Run the test — expect PASS.** `run_tests` EditMode `test_filter=AnimationStorageTests`.

- [ ] **Step 5: Delegate from `AnimationAuthoring`.** Add `AnimationStorage` constructor param; store `_storage`. Replace `RequestSave()` (the private method) with calls to `_storage.RequestSave(_data, _sceneId)` (update its ~9 call sites to pass nothing extra — keep a private `RequestSave()` wrapper that calls `_storage.RequestSave(_data, _sceneId)` to minimize churn). Replace `LoadAsync` body with `_data = await _storage.LoadAsync(sceneId, ct); _sceneId = sceneId;`. Delete `_saveCts`, `SAVE_DEBOUNCE_MS`, `DebouncedSave`, `SaveAsync`. In `Dispose`, drop the `_saveCts` lines (storage disposes itself).

- [ ] **Step 6: DI.** In `VrEditingSceneScope.cs`, before the `AnimationAuthoring` registration (`:48`), add: `builder.Register<AnimationStorage>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();`

- [ ] **Step 7: Compile + full Animation tests.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=Animation` (all Animation tests) → PASS.

- [ ] **Step 8: Commit.** `git add -A && git commit -m "Split AnimationAuthoring: extract AnimationStorage (non-destructive load)"`

## Task A1.3: Extract `AnimationPlaybackSampler` (+ B3 unified sampling)

**Files:**
- Create: `Assets/_App/Scripts/Animation/AnimationPlaybackSampler.cs`
- Modify: `AnimationAuthoring.cs` (remove `ITickable`; move `Tick`, `SampleContainerAt`, `ApplyFrame`, loop dicts + `StartLoopPlayback`/`StopLoopPlayback`/`RebuildLoopClips`/`AdvanceLoopCursor`/`PublishLoopFrameIfChanged`, `RebuildActiveClips`, `_clips`, `_activeContainerOwner`, `OnFrameChanged`/`OnPlaybackState` subscriptions)
- Modify: `VrEditingSceneScope.cs`
- Test: re-point `AnimationAuthoringLoopTests`, `AnimationAuthoringLoopFrameTests`, `AnimationAuthoringLoopRefreshTests`, `AnimationAuthoringLiveTrackTests`

- [ ] **Step 1: Create the sampler.** New `AnimationPlaybackSampler.cs`, `public class AnimationPlaybackSampler : VContainer.Unity.ITickable, System.IDisposable`. Move into it: the loop dictionaries (`:19-21`), `_clips` (`:18`), `_activeContainerOwner` (`:23`); methods `RebuildActiveClips` (`:118-125`), `SetActiveContainerOwner` (`:112-116`), `StartLoopPlayback`/`StopLoopPlayback`/`RebuildLoopClips`/`AdvanceLoopCursor` (`:180-214`), `SetInterpolation`'s loop-rebuild only stays in façade, `Tick` (`:523-556`), `SampleContainerAt` (`:558-567`), `PublishLoopFrameIfChanged` (`:571-577`), `ApplyFrame` (`:579-599`). Deps: `AnimationClock clock, ISceneGraph graph, EventBus bus`. Add `SceneAnimationData _data;` + `public void SetData(SceneAnimationData d){ _data = d; RebuildActiveClips(); }` + `public void OnDataChanged(string owner){ RebuildActiveClips(); if (owner != null) RebuildLoopClips(owner); }`. Subscribe `FrameChangedEvent`→`ApplyFrame(e.Frame)` and `PlaybackStateChangedEvent`→`if(e.Completed) ApplyFrame(0)` in a `Start`/ctor + unsubscribe in `Dispose`. (Make it also `IStartable` for the subscriptions, or subscribe in the ctor and unsubscribe in `Dispose`.)

- [ ] **Step 2: B3 — unify sampling.** In the sampler, make `ApplyFrame(int frame)` call the shared body:

```csharp
private void Sample(ActionContainer c, System.Collections.Generic.Dictionary<string, AnimationClip> clips, float seconds)
{
    foreach (var track in c.Tracks)
    {
        if (!clips.TryGetValue(track.NodeId, out var clip)) continue;
        var go = _graph?.GetNode(track.NodeId);
        if (go == null) continue;
        clip.SampleAnimation(go, seconds);
    }
}
```

Replace the body of `SampleContainerAt(c, clips, t)` with `Sample(c, clips, t)`. Replace the per-track loop inside `ApplyFrame` (`:592-598`) with `Sample(c, _clips, (float)frame / fps)`. Keep `ApplyFrame`'s early-out guards (`:581-589`) unchanged.

- [ ] **Step 3: Re-wire façade → sampler.** In `AnimationAuthoring`: add `AnimationPlaybackSampler _sampler` ctor param; remove `ITickable` from the class declaration and delete `Tick`. Replace each `RebuildActiveClips(); RebuildLoopClips(owner);` pair in CRUD methods with `_sampler.OnDataChanged(owner);` (and `RebuildActiveClips()` alone with `_sampler.OnDataChanged(null)`). Replace `SetActiveContainerOwner` body with `_sampler.SetActiveContainerOwner(ownerNodeId);`. Replace `StartLoopPlayback`/`StopLoopPlayback`/`IsLoopPlaying` public methods with delegations to `_sampler`. After load in `Start`/`LoadAsync`, call `_sampler.SetData(_data)`. Remove `OnFrameChanged`/`OnPlaybackState` subscriptions from the façade (they live in the sampler now). Keep `_clips`/loop state OUT of the façade.

- [ ] **Step 4: DI.** In `VrEditingSceneScope.cs`, add `builder.RegisterEntryPoint<AnimationPlaybackSampler>(Lifetime.Scoped).AsSelf();` (it is the `ITickable`).

- [ ] **Step 5: Re-point loop/sampling tests.** In `AnimationAuthoringLoopTests`, `AnimationAuthoringLoopFrameTests`, `AnimationAuthoringLoopRefreshTests`, `AnimationAuthoringLiveTrackTests`: construct an `AnimationPlaybackSampler` for the loop/sampling assertions (call `SetData(...)` to feed it the data the façade was previously initialised with). Where a test created the façade and called loop methods, route loop calls through the sampler (the façade still exposes `StartLoopPlayback`/`StopLoopPlayback`/`IsLoopPlaying` as delegations, so most tests may keep working — change only those that call the now-moved internal hooks `AdvanceLoopCursor`/`PublishLoopFrameIfChanged`, which are now on `AnimationPlaybackSampler`).

- [ ] **Step 6: Compile + tests.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=Animation` → PASS.

- [ ] **Step 7: Commit.** `git add -A && git commit -m "Split AnimationAuthoring: extract AnimationPlaybackSampler, unify sampling (B3)"`

## Task A1.4: Trim façade + verify

**Files:** Modify `AnimationAuthoring.cs`; verify `SceneContext`/`SceneContextBinder` unchanged.

- [ ] **Step 1:** Confirm `AnimationAuthoring` now is `IStartable, IDisposable` only, holds `_data`/`_sceneId`, deps `(AnimationStorage, AnimationPlaybackSampler, ISceneGraph, EventBus)`, and keeps the full public CRUD/query/`CaptureForExport`/`OwnerOf`/`InitForTest` surface. (`InitForTest` should also `SetData` into a test sampler if the façade tests need sampling — otherwise leave as data-only.)
- [ ] **Step 2:** Confirm `SceneContext.Authoring` still resolves the façade (no change needed — same type).
- [ ] **Step 3: Compile + full suite.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode (all) → PASS.
- [ ] **Step 4: Commit.** `git add -A && git commit -m "Finish AnimationAuthoring split: façade is CRUD + orchestration"`

---

# BLOCK B1 — Single bone-diamond mesh

**Files:** Modify `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` (`:217-279`). Test: `RigEntityFactoryBuildProxyTests`.

- [ ] **Step 1: Add shared constant + helper.** Add to `RigEntityFactory`:

```csharp
private static readonly Vector3[] DIAMOND_BASE = { /* the 6 base vertices currently inlined at :220-238 */ };
private static readonly int[]     DIAMOND_TRIS = { 0,1,3, 0,3,2, /* ...the 24-index list from :228-238... */ };

private static void AppendDiamond(System.Collections.Generic.List<Vector3> verts,
                                  System.Collections.Generic.List<int> tris,
                                  Quaternion rot, float length, float width)
{
    int baseIndex = verts.Count;
    float w = width; // EffectiveWidth scaling as currently computed
    foreach (var v in DIAMOND_BASE)
        verts.Add(rot * new Vector3(v.x * w, v.y * length, v.z * w)); // match the existing scale math
    foreach (var t in DIAMOND_TRIS)
        tris.Add(baseIndex + t);
}
```

(Read the exact vertex/scale/winding currently in `BuildOrientedDiamondMesh` `:217-241` and replicate precisely — they must be byte-identical to today's output.)

- [ ] **Step 2: Call from both builders.** `BuildOrientedDiamondMesh` and `BuildCombinedDiamondMesh` build their vertex/index lists via `AppendDiamond(...)`; the only difference is the per-segment `rot`/`length`. Delete the duplicated literal arrays.

- [ ] **Step 3: Compile + test.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=RigEntityFactoryBuildProxyTests` → PASS. (Visually sanity-check a rig in Play mode if possible — bone silhouettes unchanged.)

- [ ] **Step 4: Commit.** `git add -A && git commit -m "Dedup bone-diamond mesh builder (B1)"`

---

# BLOCK A2 — Split `GizmoActivator`

**Files:** Create `GizmoHighlightPainter.cs`, `GizmoDragSession.cs` in `Assets/_App/Scripts/VrInteraction/Gizmo/`; modify `GizmoActivator.cs`. Tests: `GizmoActivatorStateTests`, `GizmoDragSliderTests`.

> Read `GizmoActivator.cs` fully before starting; preserve behavior exactly.

- [ ] **Step 1: Extract `GizmoHighlightPainter`.** Move the `GizmoPart` struct, `BuildParts`, and `RecolorHandle`/`DarkenHandle`/`RestoreHandle` + the base/emission color capture (`:199-363`) into `GizmoHighlightPainter`. Expose `void Build(GameObject gizmoRoot)`, `void Recolor(...)`, `void Darken(...)`, `void RestoreAll()`. `GizmoActivator` holds a `GizmoHighlightPainter _painter` and delegates.

- [ ] **Step 2: Compile + test.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=GizmoActivatorStateTests` → PASS. Commit: `git commit -am "Extract GizmoHighlightPainter from GizmoActivator (A2)"`.

- [ ] **Step 3: Extract `GizmoDragSession`.** Move `_dragActive`, original-pose snapshot, target-follow switch, and `OnHandleGrabbed`/`OnHandleDragged`/`OnHandleReleased` drag orchestration (`:375-449`) into `GizmoDragSession`. `GizmoActivator` delegates grab/drag/release callbacks to it. **Leave the `_gizmoController.CommitTransform(...)` call (`:444`) inside `GizmoDragSession` for now — B2 removes it next.**

- [ ] **Step 4: Compile + test.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=GizmoDragSliderTests` and `test_filter=GizmoActivatorStateTests` → PASS.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Extract GizmoDragSession from GizmoActivator (A2)"`

---

# BLOCK B2 — Remove the undo subsystem

**Files:** Archive 5 files; cut 1 test; edit 6.

- [ ] **Step 1: Remove the commit calls.** In `XRPromeonInteractable.cs`: delete `_gizmoController` field (`:23`), the `GizmoController gizmoController` param + assignment in `Construct` (`:104`), and the two `_gizmoController.CommitTransform(...)` lines (`:172`, `:185`). In `GizmoDragSession` (from A2; was `GizmoActivator.cs:444`): delete `_gizmoController` + its `Construct` param + the `CommitTransform` call.

- [ ] **Step 2: Strip `SceneContext`.** In `SceneContext.cs`: remove the `Commands` (`:9`) and `Gizmo` (`:10`) properties and their `Bind`/constructor params (`:16-17`). In `SceneContextBinder.cs`: remove `Resolve<CommandStack>()` (`:27`) and `Resolve<GizmoController>()` (`:28`) from the `Bind(...)` call.

- [ ] **Step 3: Strip DI.** In `VrEditingSceneScope.cs` and `SandboxSceneScope.cs`: remove the `CommandStack` registration, the `GizmoController` registration, and the `UndoKeyHandler` find/inject block (the `var undo = Object.FindAnyObjectByType<UndoKeyHandler>...` block).

- [ ] **Step 4: Compile.** `refresh_unity` ×2 + `read_console`. Expect errors only if a reference was missed — fix until clean. (Now nothing references `CommandStack`/`GizmoController`/`TransformCommand`/`ICommand`/`UndoKeyHandler`.)

- [ ] **Step 5: Cut the orphaned test.** `manage_asset` move `_App/Tests/SceneComposition/CommandStackTests.cs` → there is no test `_Archive`; instead **delete** it: `manage_asset action=delete path=_App/Tests/SceneComposition/CommandStackTests.cs`. Verify via Glob it's gone.

- [ ] **Step 6: Archive the 5 sources** (verify each via Glob after):
  - `_App/Scripts/SceneComposition/TransformCommand.cs` → `_Archive/TransformCommand.cs`
  - `_App/Scripts/Core/ICommand.cs` → `_Archive/ICommand.cs`
  - `_App/Scripts/SceneComposition/CommandStack.cs` → `_Archive/CommandStack.cs`
  - `_App/Scripts/Bootstrap/UndoKeyHandler.cs` → `_Archive/UndoKeyHandler.cs`
  - `_App/Scripts/VrInteraction/GizmoController.cs` → `_Archive/GizmoController.cs`

- [ ] **Step 7: Compile + full suite.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode (all) → PASS (no `CommandStackTests`).

- [ ] **Step 8: Commit.** `git add -A && git commit -m "Remove dead undo subsystem (TransformCommand/CommandStack/UndoKeyHandler/GizmoController)"`

---

# BLOCK A4 — Extract `BoneEditMode`

**Files:** Create `Assets/_App/Scripts/RigBuilder/BoneEditMode.cs`; create `Assets/_App/Tests/RigBuilder/BoneEditModeTests.cs`; modify `InspectorPanel.cs`, `VrEditingSceneScope.cs`, `SandboxSceneScope.cs`.

- [ ] **Step 1: Write the failing test.** New `BoneEditModeTests.cs`:

```csharp
using NUnit.Framework;

public class BoneEditModeTests
{
    [Test]
    public void Enter_SetsActiveRig_AndDeselects()
    {
        var bus = new EventBus();
        var sel = new SelectionManager(bus);
        var graph = new SceneGraph(bus); // use the same ctor the scene scope uses
        var sut = new BoneEditMode(sel, graph, bus);

        // With no rig node present, SetActive(on) must no-op gracefully (rig == null guard).
        sut.SetActive("missing-rig", true);
        Assert.IsFalse(sut.IsActive, "no rig → not active");
    }
}
```

(If `SelectionManager`/`SceneGraph` ctors differ, adjust to match; the assertion that matters is the null-rig guard and `IsActive` default `false`.)

- [ ] **Step 2: Run — expect FAIL** (`BoneEditMode` undefined). `run_tests` EditMode `test_filter=BoneEditModeTests`.

- [ ] **Step 3: Create `BoneEditMode`.**

```csharp
using UnityEngine;

// Owns "bone edit mode" for one rig at a time: toggles bone interactivity, manages selection
// hand-off, and announces visibility. Replaces duplicated state in Inspector/Animator panels.
public class BoneEditMode
{
    private readonly ISelectionManager _selection;
    private readonly ISceneGraph       _graph;
    private readonly EventBus          _bus;

    public string ActiveRigId { get; private set; }
    public bool   IsActive => !string.IsNullOrEmpty(ActiveRigId);

    [VContainer.Inject]
    public BoneEditMode(ISelectionManager selection, ISceneGraph graph, EventBus bus)
    {
        _selection = selection; _graph = graph; _bus = bus;
    }

    public void SetActive(string rigNodeId, bool on)
    {
        var rigNode = string.IsNullOrEmpty(rigNodeId) ? null : _graph.GetNode(rigNodeId);
        var rig     = rigNode != null ? rigNode.GetComponentInChildren<ProxyRigRuntime>(true) : null;
        if (rig == null) return;

        rig.SetBonesInteractive(on);
        _bus.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = on });

        if (on) { ActiveRigId = rigNodeId; _selection.Select(null); }
        else    { ActiveRigId = null;      _selection.Select(rigNodeId); }
    }
}
```

- [ ] **Step 4: Run — expect PASS.** `run_tests` EditMode `test_filter=BoneEditModeTests`.

- [ ] **Step 5: Use it from `InspectorPanel`.** Inject `BoneEditMode` (add to `InspectorPanel`'s `[Inject] Construct`). Replace the body of `OnShowBonesToggleChanged` (`:266-307`): keep the rig-resolution prelude but read the remembered rig from `_boneEditMode.ActiveRigId` instead of `_activeBoneRigId`; then call `_boneEditMode.SetActive(rigNodeId, value);` and delete the inline `SetBonesInteractive`/publish/selection block and the private `_activeBoneRigId` field.

- [ ] **Step 6: DI in BOTH scopes.** In `VrEditingSceneScope.cs` and `SandboxSceneScope.cs`, add (before the inspector inject): `builder.Register<BoneEditMode>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();`

- [ ] **Step 7: Compile + tests.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=BoneEditModeTests` → PASS; spot-run `test_filter=SceneContextTests` → PASS. (AnimatorPanel still observes `BonesVisibilityChangedEvent` — unchanged.)

- [ ] **Step 8: Commit.** `git add -A && git commit -m "Extract BoneEditMode service; Inspector delegates bone toggle (A4)"`

---

# BLOCK B6 — Share the Reference recipe

**Files:** Modify `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs`; modify `Assets/_App/Editor/ReferenceImagePrefabGenerator.cs`. Tests: `ReferenceEntityBuilderTests`, `ReferenceEntityFactoryQuadTests`, `ImportedLabAssetRecipeTests`.

- [ ] **Step 1: Add `RecipeFromImage`.** In `ReferenceEntityBuilder`, extract the recipe literal (`:31-47`) into a static factory:

```csharp
public static AssetEntityRecipe RecipeFromImage(float aspect)
{
    const float h = 1f, gap = 0.5f;
    return new AssetEntityRecipe
    {
        type               = AssetType.Reference,
        selectable         = true,
        interactionLayer   = InteractionLayer.SceneObjects,
        colliderKind       = ColliderKind.Box,
        colliderCenter     = Vector3.zero,
        colliderSize       = new Vector3(1f, h, 0.02f),
        spawnOffset        = new Vector3(0f, gap + h * 0.5f, 0f),
        referenceAspect    = aspect,
        referenceBottomGap = gap,
        referenceTwoSided  = true,
    };
}
```

`BuildAsync` becomes: compute `aspect` (the existing `:24-29` block), then `return Task.FromResult(RecipeFromImage(aspect));`.

- [ ] **Step 2: Call it from the baker.** In `ReferenceImagePrefabGenerator.Generate` (Editor), replace its inline reference-recipe construction with `ReferenceEntityBuilder.RecipeFromImage(aspect)` (read the file to find where it currently builds the recipe / box size; compute `aspect` from the source image the same way).

- [ ] **Step 3: Compile + tests.** `refresh_unity` ×2 + `read_console` clean. `run_tests` EditMode `test_filter=Reference` and `test_filter=ImportedLabAssetRecipeTests` → PASS.

- [ ] **Step 4: Commit.** `git add -A && git commit -m "Share Reference recipe via RecipeFromImage (B6)"`

---

# Final verification

- [ ] **Step 1:** `run_tests` EditMode (entire `_App.Tests` assembly) → all PASS.
- [ ] **Step 2:** `read_console` (`types=["error","warning"]`) — only the pre-existing `CS0618` glTFast warning remains.
- [ ] **Step 3:** Confirm `Assets/_App/Scripts/_Archive/` now also contains `TransformCommand.cs`, `ICommand.cs`, `CommandStack.cs`, `UndoKeyHandler.cs`, `GizmoController.cs`.
- [ ] **Step 4:** Report a summary; the user reviews and (per their instruction) the branch is committed block-by-block already.

---

## Plan self-review notes
- **Spec coverage:** A1 (Baker/Storage/Sampler/façade) = Tasks A1.1–A1.4; B3 = A1.3 Step 2; B4 = A1.2 Step 3; B1 = Block B1; A2 = Block A2; B2 = Block B2; A4 = Block A4; B6 = Block B6. All spec sections mapped.
- **Out of scope (not in plan, by design):** A3/A5/B5/B7/B8, all renames, Stage-3 doc edits, `GizmoActivator→GizmoDriver`.
- **Known read-at-execution points** (existing code whose exact bytes matter): `RigEntityFactory` diamond vertices (B1), `GizmoActivator` highlight/drag bodies (A2), `ReferenceImagePrefabGenerator` recipe construction (B6). The executor must open these and preserve/relocate exact logic.
- **Type consistency:** `AnimationStorage.LoadAsync/RequestSave`, `AnimationPlaybackSampler.SetData/OnDataChanged/SetActiveContainerOwner/Sample`, `BoneEditMode.SetActive/ActiveRigId/IsActive`, `ReferenceEntityBuilder.RecipeFromImage(float aspect)` are referenced consistently across tasks.
