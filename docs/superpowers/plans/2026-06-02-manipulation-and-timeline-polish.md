# Manipulation & Timeline Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix three low-severity bugs (loop playhead frozen, stale rig outline, empty owner track in bone mode) and rework gizmo scale/rotate input to a uniform displacement-driven feel.

**Architecture:** Bugs are localized edits in `Animation`/`SpatialUi`/`VrInteraction`. The gizmo rework replaces pivot-relative magnitude math with a shared `GizmoDragSlider` (self-establishing 1D slider with a deadzone) feeding `exp`-scaled scale and linear rotation at constant gains from `GizmoConfig`.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer, custom `EventBus`, NUnit (EditMode). Compile/verify through the Unity MCP tools.

---

## CRITICAL PROJECT RULES (read before starting)

- **NEVER run any `git` command.** The user commits manually. Every "Checkpoint" means *stop and let the human commit*. Overrides the writing-plans default.
- **Compile/test via Unity MCP**: after editing, `mcp__unityMCP__refresh_unity` (force) → `mcp__unityMCP__read_console` (errors, **no `CS####`**) → `mcp__unityMCP__run_tests` (testMode EditMode) → poll `mcp__unityMCP__get_test_job`. Pin an instance via `mcpforunity://instances` + `set_active_instance` if several are listed.
- **Allowed pre-existing failures** (NOT regressions): `PathProviderTests` ×4 **and** `RingRotateStrategyTests` ×2 — *until Task 8 rewrites `RingRotateStrategyTests`*, after which only `PathProviderTests` ×4 remain allowed. Any *other* failure is yours to fix.
- Some tasks are **Unity-runtime integration** (panels, UI lifecycle) — they gate on clean compile + full suite green and are verified in-headset (Task 9). Do not fabricate unit tests for them.
- No namespaces on runtime gameplay code. One public type per file (nested fine). `[SerializeField] private` on MonoBehaviour/SO fields.

---

## File Structure

| File | New/Modify | Responsibility |
|---|---|---|
| `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs` | Modify | Bug 4 (owner track in bone mode); Bug 1 (subscribe `LoopFrameChangedEvent`). |
| `Assets/_App/Scripts/Animation/Events/LoopFrameChangedEvent.cs` | **New** | Display-only loop frame event. |
| `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` | Modify | Publish `LoopFrameChangedEvent` from the loop tick. |
| `Assets/_App/Scripts/VrInteraction/Selectable.cs` | Modify | Bug 2 (disable pre-existing outline at spawn). |
| `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoConfig.cs` | Modify | Add `DeadzoneMeters`/`ScaleGain`/`RotGain`. |
| `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/GizmoDragSlider.cs` | **New** | Self-establishing 1D slider (deadzone + signed displacement). |
| `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/AxisScaleStrategy.cs` | Modify | exp scale from slider; ctor `(gain, deadzone)`. |
| `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/UniformScaleStrategy.cs` | Modify | exp uniform scale from slider; ctor `(gain, deadzone)`. |
| `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/RingRotateStrategy.cs` | Modify | linear rotation from slider; ctor `(gain, deadzone)`. |
| `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` | Modify | `ResolveStrategy` passes config gains to the three strategies. |
| `Assets/_App/Tests/Animation/AnimationAuthoringLoopFrameTests.cs` | **New** | `PublishLoopFrameIfChanged` behavior. |
| `Assets/_App/Tests/VrInteraction/GizmoDragSliderTests.cs` | **New** | slider deadzone/lock/displacement. |
| `Assets/_App/Tests/VrInteraction/AxisScaleStrategyTests.cs` | Rewrite | new exp/slider behavior. |
| `Assets/_App/Tests/VrInteraction/UniformScaleStrategyTests.cs` | Rewrite | new exp/slider behavior. |
| `Assets/_App/Tests/VrInteraction/RingRotateStrategyTests.cs` | Rewrite | new linear/slider behavior. |

`_App.Runtime` already declares `[assembly: InternalsVisibleTo("_App.Tests")]` — `internal` members are test-visible; no new InternalsVisibleTo file.

---

## Task 1: Bug 4 — "Add animation" creates the owner track in bone mode

Integration (depends on `SceneContext`/UI). Gate: clean compile + full suite green; verified in-headset.

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

- [ ] **Step 1: Replace `OnAddAnimationClicked`**

Find the current method (around lines 181-192) and replace it with:

```csharp
    private void OnAddAnimationClicked()
    {
        if (_ctx.Authoring == null) return;
        var selected = _ctx.Selection?.SelectedNodeId;
        var owner    = AnimationAuthoring.OwnerOf(selected);
        if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
            owner = _boneModeRig;                         // bone mode, nothing selected → target the rig
        if (string.IsNullOrEmpty(owner)) return;

        _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
        _ctx.Authoring.EnsureTrack(owner, owner);         // owner track ALWAYS — object/rig's own transform
    }
```

This drops the old `if (!isBone && owner == selected)` guard so the owner track is always created, and adds the `_boneModeRig` fallback so Add works in bone mode even with nothing selected.

- [ ] **Step 2: Verify compile + suite**

`refresh_unity`, `read_console` (no `CS####`), run the FULL EditMode suite. Expect only the allowed pre-existing failures.

- [ ] **Step 3: Checkpoint** — stop; the user commits. (In-headset check is in Task 9.)

---

## Task 2: Bug 1 — timeline playhead follows the selected looping object

**Files:**
- Create: `Assets/_App/Scripts/Animation/Events/LoopFrameChangedEvent.cs`
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringLoopFrameTests.cs`

- [ ] **Step 1: Create the event**

Create `Assets/_App/Scripts/Animation/Events/LoopFrameChangedEvent.cs`:

```csharp
// Published by AnimationAuthoring's background loop as its integer frame advances. Display-only:
// the timeline playhead follows it for the selected owner. Kept separate from FrameChangedEvent,
// which is tied to the transport clock and drives clip sampling.
public struct LoopFrameChangedEvent
{
    public string OwnerNodeId;
    public int    Frame;
}
```

- [ ] **Step 2: Write the failing test**

Create `Assets/_App/Tests/Animation/AnimationAuthoringLoopFrameTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class AnimationAuthoringLoopFrameTests
{
    [Test]
    public void PublishLoopFrameIfChanged_PublishesOncePerIntegerFrame()
    {
        var bus = new EventBus();
        var captured = new List<LoopFrameChangedEvent>();
        bus.Subscribe<LoopFrameChangedEvent>(e => captured.Add(e));

        var authoring = new AnimationAuthoring(null, null, null, null, bus);
        authoring.InitForTest();

        authoring.PublishLoopFrameIfChanged("n1", 5.3f);  // → frame 5, publish
        authoring.PublishLoopFrameIfChanged("n1", 5.7f);  // still frame 5, no publish
        authoring.PublishLoopFrameIfChanged("n1", 6.1f);  // → frame 6, publish

        Assert.AreEqual(2, captured.Count);
        Assert.AreEqual("n1", captured[0].OwnerNodeId);
        Assert.AreEqual(5,    captured[0].Frame);
        Assert.AreEqual(6,    captured[1].Frame);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

`refresh_unity`, `read_console`: expect `CS1061`/`CS0246` — `PublishLoopFrameIfChanged` / `LoopFrameChangedEvent` referenced before the method exists (event created in Step 1, method not yet).

- [ ] **Step 4: Add the publish logic to `AnimationAuthoring`**

Add a field next to the other loop dictionaries (near `_loopCursors` / `_loopClips`):

```csharp
    private readonly Dictionary<string, int> _loopLastFrame = new();
```

Add the method (e.g. just below `SampleContainerAt`):

```csharp
    // Publishes a LoopFrameChangedEvent for an owner only when its integer frame changes, so the
    // playhead steps once per frame rather than every tick. Internal for EditMode testing.
    internal void PublishLoopFrameIfChanged(string owner, float cursor)
    {
        int frame = UnityEngine.Mathf.FloorToInt(cursor);
        if (_loopLastFrame.TryGetValue(owner, out var last) && last == frame) return;
        _loopLastFrame[owner] = frame;
        _bus.Publish(new LoopFrameChangedEvent { OwnerNodeId = owner, Frame = frame });
    }
```

In `Tick`, call it right after the cursor is advanced and sampled. The loop body currently reads:

```csharp
            float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
            _loopCursors[owner] = cursor;
            if (_loopClips.TryGetValue(owner, out var clips))
                SampleContainerAt(c, clips, cursor / Mathf.Max(1f, fps));
```

Add one line at the end of that body:

```csharp
            PublishLoopFrameIfChanged(owner, cursor);
```

In `StopLoopPlayback`, also clear the per-owner last frame so a fresh loop re-publishes its first frame:

```csharp
    public void StopLoopPlayback(string ownerNodeId)
    {
        _loopCursors.Remove(ownerNodeId);
        _loopClips.Remove(ownerNodeId);
        _loopLastFrame.Remove(ownerNodeId);
    }
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `AnimationAuthoringLoopFrameTests`. Expect 1 PASS.

- [ ] **Step 6: Subscribe in the panel**

In `AnimatorPanel.cs`, add the subscription next to the existing `FrameChangedEvent` subscribe (around line 41) and the matching unsubscribe (around line 61):

```csharp
        _bus.Subscribe<LoopFrameChangedEvent>(OnLoopFrameChanged);
```
```csharp
        _bus.Unsubscribe<LoopFrameChangedEvent>(OnLoopFrameChanged);
```

Add the handler next to `OnFrameChanged`:

```csharp
    private void OnLoopFrameChanged(LoopFrameChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;   // playhead follows only the selected owner
        if (_playhead != null) _playhead.SetFrame(e.Frame);
        if (_toolbar  != null) _toolbar.SetCurrentFrame(e.Frame);
    }
```

- [ ] **Step 7: Verify compile + suite**

`refresh_unity`, `read_console` (no `CS####`), run the FULL EditMode suite. Expect only allowed pre-existing failures.

- [ ] **Step 8: Checkpoint** — stop; the user commits.

---

## Task 3: Bug 2 — clear stale rig outline on fresh spawn

Integration (UI/visual). Gate: compile + suite; verified in-headset.

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Selectable.cs`

- [ ] **Step 1: Disable a pre-existing outline at spawn**

In `Selectable.cs`, add a `Start` method (the class currently has `Awake` and `Construct`; add `Start` after `Awake`):

```csharp
    private void Start()
    {
        // A pre-existing Outline (e.g. left enabled from bone display, or baked on a proxy) must not
        // show until this node is actually selected. Disable it without ADDING one to non-outlined
        // nodes (adding an Outline at spawn is expensive — smooth-normal baking).
        var existing = GetComponent<Outline>();
        if (existing != null) existing.enabled = false;
    }
```

- [ ] **Step 2: Verify compile + suite**

`refresh_unity`, `read_console` (no `CS####`), run the FULL EditMode suite. Expect only allowed pre-existing failures.

- [ ] **Step 3: Checkpoint** — stop; the user commits. (In-headset: enter bones on a rig → leave scene → re-enter → no stale blue outline. If insufficient, flag for follow-up diagnosis — low impact.)

---

## Task 4: GizmoConfig — drag-feel gains

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoConfig.cs`

- [ ] **Step 1: Add fields + accessors**

In `GizmoConfig.cs`, add after the `_activeMaterial` field block (before the public accessors), the new fields:

```csharp
    [Header("Drag feel (displacement-driven)")]
    [Tooltip("Controller travel (metres) before a drag direction locks; also the baseline so there is no pop at lock.")]
    [SerializeField, Range(0.001f, 0.1f)] private float _deadzoneMeters = 0.02f;
    [Tooltip("Scale gain: factor = exp(gain × metres). ~ln2/0.15 ≈ 4.62 → ×2 per 15 cm.")]
    [SerializeField, Range(0.5f, 20f)]    private float _scaleGain      = 4.62f;
    [Tooltip("Rotation gain: degrees per metre of controller displacement. 1200 → a full turn per 30 cm.")]
    [SerializeField, Range(60f, 3600f)]   private float _rotGain        = 1200f;
```

And add the accessors next to the existing ones:

```csharp
    public float DeadzoneMeters => _deadzoneMeters;
    public float ScaleGain      => _scaleGain;
    public float RotGain        => _rotGain;
```

- [ ] **Step 2: Verify compile**

`refresh_unity`, `read_console` (no `CS####`). (No test for SO getters; the full suite still passes with only allowed failures.)

- [ ] **Step 3: Checkpoint** — stop; the user commits.

---

## Task 5: `GizmoDragSlider`

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/GizmoDragSlider.cs`
- Test: `Assets/_App/Tests/VrInteraction/GizmoDragSliderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/VrInteraction/GizmoDragSliderTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class GizmoDragSliderTests
{
    [Test]
    public void InsideDeadzone_NotLocked_ReturnsFalse()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        Assert.IsFalse(sl.TryGetSignedDisplacement(new Vector3(0.01f, 0f, 0f), out var s));
        Assert.AreEqual(0f, s);
    }

    [Test]
    public void PastDeadzone_LocksDir_BaselinedAtDeadzone()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        Assert.IsTrue(sl.TryGetSignedDisplacement(new Vector3(1.02f, 0f, 0f), out var s));
        Assert.AreEqual(1.0f, s, 1e-4f);   // dot 1.02 − deadzone 0.02
    }

    [Test]
    public void PullBackPastStart_GivesNegative()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        sl.TryGetSignedDisplacement(new Vector3(1.02f, 0f, 0f), out _);            // lock +X
        Assert.IsTrue(sl.TryGetSignedDisplacement(new Vector3(-0.98f, 0f, 0f), out var s));
        Assert.AreEqual(-1.0f, s, 1e-4f);  // dot −0.98 − 0.02
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect `CS0246` — `GizmoDragSlider` does not exist.

- [ ] **Step 3: Create the slider**

Create `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/GizmoDragSlider.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Self-establishing 1D slider for gizmo drags. The first controller movement past the deadzone locks a
/// reference direction; afterwards it reports the signed displacement along that direction, baselined so
/// the value is 0 at the moment of lock (no pop). Replaces pivot-relative magnitude math so the rate is
/// uniform and there is no blow-up near the object center.
/// </summary>
public struct GizmoDragSlider
{
    private Vector3 _start;
    private Vector3 _refDir;
    private float   _deadzone;
    private bool    _locked;

    public void Begin(Vector3 handPos, float deadzone)
    {
        _start    = handPos;
        _deadzone = Mathf.Max(0f, deadzone);
        _refDir   = Vector3.zero;
        _locked   = false;
    }

    /// Returns false while still inside the deadzone (direction not yet established → apply no change).
    public bool TryGetSignedDisplacement(Vector3 handPos, out float s)
    {
        var delta = handPos - _start;
        if (!_locked)
        {
            if (delta.magnitude <= _deadzone) { s = 0f; return false; }
            _refDir = delta.normalized;
            _locked = true;
        }
        s = Vector3.Dot(handPos - _start, _refDir) - _deadzone;
        return true;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `GizmoDragSliderTests`. Expect 3 PASS.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 6: `AxisScaleStrategy` — exp scale from the slider

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/AxisScaleStrategy.cs`
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` (one line)
- Test: `Assets/_App/Tests/VrInteraction/AxisScaleStrategyTests.cs` (rewrite)

- [ ] **Step 1: Rewrite the test**

Replace the entire contents of `Assets/_App/Tests/VrInteraction/AxisScaleStrategyTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AxisScaleStrategyTests
{
    private GameObject _go;
    private Transform  _t;
    private AxisScaleStrategy _sut;

    // gain = ln2 per metre → factor 2 at s = 1; deadzone 0.02.
    private static readonly float Gain = Mathf.Log(2f);

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("target");
        _t  = _go.transform;
        _t.localScale = Vector3.one;
        _sut = new AxisScaleStrategy(Gain, 0.02f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void InsideDeadzone_NoChange()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0.01f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(1f, _t.localScale.x, 1e-4f);
    }

    [Test]
    public void PushAlongRefDir_DoublesAxis()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity); // s = 1 → factor 2
        Assert.AreEqual(2f, _t.localScale.x, 1e-3f);
        Assert.AreEqual(1f, _t.localScale.y, 1e-4f);
        Assert.AreEqual(1f, _t.localScale.z, 1e-4f);
    }

    [Test]
    public void PullBackPastStart_ShrinksBelowOriginal()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity);  // lock +X
        _sut.UpdateDrag(new Vector3(-0.98f, 0f, 0f), Quaternion.identity); // s = −1 → factor 0.5
        Assert.AreEqual(0.5f, _t.localScale.x, 1e-3f);
    }

    [Test]
    public void PreservesOtherAxesOriginalScale()
    {
        _t.localScale = new Vector3(2f, 3f, 4f);
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 1.02f, 0f), Quaternion.identity);  // s = 1 → ×2 on Y
        Assert.AreEqual(2f, _t.localScale.x, 1e-4f);
        Assert.AreEqual(6f, _t.localScale.y, 1e-3f);
        Assert.AreEqual(4f, _t.localScale.z, 1e-4f);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect compile errors — `AxisScaleStrategy` has no `(float, float)` constructor yet.

- [ ] **Step 3: Rewrite the strategy**

Replace the entire contents of `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/AxisScaleStrategy.cs`:

```csharp
using UnityEngine;

public class AxisScaleStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // factor = exp(gain * metres)
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform _target;
    private int       _axisIndex;
    private Vector3   _originalScale;

    public AxisScaleStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target        = target;
        _axisIndex     = (int)axis;
        _originalScale = target.localScale;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        var factor = Mathf.Exp(_gain * s);
        var scl = _originalScale;
        scl[_axisIndex] = _originalScale[_axisIndex] * factor;
        _target.localScale = scl;
    }

    public void EndDrag() => _target = null;
}
```

- [ ] **Step 4: Update `GizmoActivator.ResolveStrategy`**

In `GizmoActivator.cs`, change the `ScaleAxis` case (currently `return new AxisScaleStrategy();`) to:

```csharp
            case HandleKind.ScaleAxis:    return new AxisScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `AxisScaleStrategyTests`. Expect 4 PASS.

- [ ] **Step 6: Checkpoint** — stop; the user commits.

---

## Task 7: `UniformScaleStrategy` — exp uniform scale from the slider

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/UniformScaleStrategy.cs`
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` (one line)
- Test: `Assets/_App/Tests/VrInteraction/UniformScaleStrategyTests.cs` (rewrite)

- [ ] **Step 1: Rewrite the test**

Replace the entire contents of `Assets/_App/Tests/VrInteraction/UniformScaleStrategyTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class UniformScaleStrategyTests
{
    private GameObject _go;
    private Transform  _t;
    private UniformScaleStrategy _sut;
    private static readonly float Gain = Mathf.Log(2f);

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("target");
        _t  = _go.transform;
        _t.localScale = Vector3.one;
        _sut = new UniformScaleStrategy(Gain, 0.02f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void PushAlongRefDir_ScalesAllAxesUp()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity); // s = 1 → ×2
        Assert.AreEqual(2f, _t.localScale.x, 1e-3f);
        Assert.AreEqual(2f, _t.localScale.y, 1e-3f);
        Assert.AreEqual(2f, _t.localScale.z, 1e-3f);
    }

    [Test]
    public void PullBackPastStart_ScalesAllAxesDown()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(-0.98f, 0f, 0f), Quaternion.identity); // s = −1 → ×0.5
        Assert.AreEqual(0.5f, _t.localScale.x, 1e-3f);
        Assert.AreEqual(0.5f, _t.localScale.y, 1e-3f);
        Assert.AreEqual(0.5f, _t.localScale.z, 1e-3f);
    }

    [Test]
    public void InsideDeadzone_NoChange()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0.01f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(1f, _t.localScale.x, 1e-4f);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect compile errors — no `(float, float)` constructor.

- [ ] **Step 3: Rewrite the strategy**

Replace the entire contents of `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/UniformScaleStrategy.cs`:

```csharp
using UnityEngine;

public class UniformScaleStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // factor = exp(gain * metres), applied to all axes
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform _target;
    private Vector3   _originalScale;

    public UniformScaleStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target        = target;
        _originalScale = target.localScale;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        _target.localScale = _originalScale * Mathf.Exp(_gain * s);
    }

    public void EndDrag() => _target = null;
}
```

- [ ] **Step 4: Update `GizmoActivator.ResolveStrategy`**

Change the `ScaleUniform` case to:

```csharp
            case HandleKind.ScaleUniform: return new UniformScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `UniformScaleStrategyTests`. Expect 3 PASS.

- [ ] **Step 6: Checkpoint** — stop; the user commits.

---

## Task 8: `RingRotateStrategy` — linear rotation from the slider

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/RingRotateStrategy.cs`
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` (one line)
- Test: `Assets/_App/Tests/VrInteraction/RingRotateStrategyTests.cs` (rewrite)

- [ ] **Step 1: Rewrite the test**

Replace the entire contents of `Assets/_App/Tests/VrInteraction/RingRotateStrategyTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class RingRotateStrategyTests
{
    private GameObject _go;
    private Transform  _t;
    private RingRotateStrategy _sut;

    // gain = 90 deg per metre → 90° at s = 1; deadzone 0.02.
    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("target");
        _t  = _go.transform;
        _t.position = Vector3.zero;
        _t.rotation = Quaternion.identity;
        _sut = new RingRotateStrategy(90f, 0.02f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void PushAlongRefDir_RotatesAroundAxis()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity); // s = 1 → 90°
        var expected = Quaternion.AngleAxis(90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _t.rotation), 0.2f);
    }

    [Test]
    public void PullBackPastStart_RotatesNegative()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity);  // lock +X, +90°
        _sut.UpdateDrag(new Vector3(-0.98f, 0f, 0f), Quaternion.identity); // s = −1 → −90°
        var expected = Quaternion.AngleAxis(-90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _t.rotation), 0.2f);
    }

    [Test]
    public void InsideDeadzone_DoesNotRotate()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        var before = _t.rotation;
        _sut.UpdateDrag(new Vector3(0.01f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0f, Quaternion.Angle(before, _t.rotation), 1e-4f);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect compile errors — no `(float, float)` constructor.

- [ ] **Step 3: Rewrite the strategy**

Replace the entire contents of `Assets/_App/Scripts/VrInteraction/Gizmo/Strategies/RingRotateStrategy.cs`:

```csharp
using UnityEngine;

public class RingRotateStrategy : IGizmoDragStrategy
{
    private readonly float _gain;       // degrees per metre
    private readonly float _deadzone;
    private GizmoDragSlider _slider;

    private Transform  _target;
    private Vector3    _axisWorld;
    private Quaternion _originalRot;

    public RingRotateStrategy(float gain, float deadzone)
    {
        _gain     = gain;
        _deadzone = deadzone;
    }

    public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
    {
        _target      = target;
        _axisWorld   = LocalAxis(target, axis);
        _originalRot = target.rotation;
        _slider.Begin(handPos, _deadzone);
    }

    public void UpdateDrag(Vector3 handPos, Quaternion handRot)
    {
        if (_target == null) return;
        if (!_slider.TryGetSignedDisplacement(handPos, out var s)) return;
        var angle = _gain * s;
        _target.rotation = Quaternion.AngleAxis(angle, _axisWorld) * _originalRot;
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

- [ ] **Step 4: Update `GizmoActivator.ResolveStrategy`**

Change the `RotateRing` case to:

```csharp
            case HandleKind.RotateRing:   return new RingRotateStrategy(_config.RotGain, _config.DeadzoneMeters);
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `RingRotateStrategyTests`. Expect 3 PASS.

- [ ] **Step 6: Full suite re-baseline**

Run the FULL EditMode suite. The 2 previously-failing `RingRotateStrategyTests` are now green. **From here the only allowed pre-existing failures are `PathProviderTests` ×4.** Confirm no others.

- [ ] **Step 7: Checkpoint** — stop; the user commits.

---

## Task 9: In-headset verification (user-performed)

- [ ] **Bug 1:** Play a looping object → its timeline playhead advances and wraps each loop; select a different object → that object's playhead does not move for the first one; pause → playhead stops and scrubs by controller.
- [ ] **Bug 2:** Enable bones on a rig → leave the scene → re-enter → the rig has no stale blue outline, nothing selected.
- [ ] **Bug 4:** Enter bone mode on a rig → Add animation → an owner row for the rig appears (timeline not empty).
- [ ] **Feature — scale:** Grab a scale handle, swipe in any direction past ~2 cm → that direction grows the object at a uniform rate; reverse past the start → shrinks smoothly down to small sizes (no slip/jump near center).
- [ ] **Feature — rotate ring:** Grab a ring, swipe → rotates around that ring's axis at a uniform °/cm, can keep swiping for large turns, no sudden spin near the center.
- [ ] **Direct trigger-rotate (unchanged):** confirm it still feels like a relative wrist-tilt follow.

Tune `GizmoConfig.ScaleGain` / `RotGain` / `DeadzoneMeters` in the inspector to taste.

---

## Notes
- Git not touched by the agent; the user commits manually.
- Out of scope (per spec): direct trigger-rotate math, grip-move feel, snapping, per-axis gains.
