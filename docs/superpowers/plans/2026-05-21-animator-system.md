# Animator System v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **User memory rule:** `feedback_no_git_during_dev` — git commands are skipped throughout. Each task ends with **"Commit checkpoint"** for user-side manual commit. Do not run `git commit`, `git add`, or any git command.

**Goal:** Переработать систему анимации (data model v2, AnimatorPanel UI с fixed-step scrollable таймлайном, per-object ActionContainer, lazy bone tracks, copy/paste frame) + связку bones-mode → outliner blue + two-prefab outliner.

**Architecture:** Per-object `ActionContainer` (schema v2) с собственными TotalFrames/FPS. UGUI ScrollRect для таймлайна с фиксированным `FRAME_PX=30`. `AnimationClock.Configure(total, fps)` переключается за активным контейнером. Bones-visibility сигнал через новый event. Outliner получает два разных префаба для object/rig.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer DI, UGUI, NUnit Edit-mode tests, JsonUtility, MessagePipe-style EventBus (custom).

**Spec:** `docs/superpowers/specs/2026-05-21-animator-system-design.md`

---

## File Structure

### Create

**Events:**
- `Assets/_App/_Shared/Events/AnimationContainerChangedEvent.cs`
- `Assets/_App/_Shared/Events/BonesVisibilityChangedEvent.cs`
- `Assets/_App/_Shared/Events/ContainerChange.cs` (enum)
- `Assets/_App/_Shared/Events/KeyframeChange.cs` (enum)

**Data:**
- `Assets/_App/Subsystems/AnimationAuthoring/Data/ActionContainer.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboard.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboardEntry.cs`

**Services:**
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationClipboard.cs`

**UI Config:**
- `Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.cs`

**UI Views (root + toolbars + empty states):**
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorToolbarView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorTransportView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorEmptyStateView.cs`

**UI Timeline elements:**
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineRulerView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLanesView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLaneView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelinePlayheadView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineInputHandler.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineScrollSync.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TrackRowView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/RigOutlinerItem.cs`

**Tests:**
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/ActionContainerTests.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClipboardTests.cs`

### Modify

- `Assets/_App/Subsystems/AnimationAuthoring/Data/SceneAnimationData.cs` — переход на `List<ActionContainer>`
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs` — новый API
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs` — `Configure(total, fps)`
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs` — переписать под v2
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs` — добавить Configure тесты
- `Assets/_App/_Shared/Events/AppEvents.cs` — расширить `AnimationKeyframeChangedEvent`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` — publish `BonesVisibilityChangedEvent`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs` — two-prefab + bones state dict
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs` — убрать dual icon swap
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register `AnimationClipboard`

### Delete

- `Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs` (заменён `AnimatorPanelView`)

### Manual prefab work (user-side after code phase)

- `AnimatorPanel.prefab` — full restructure под mockup v4
- `OutlinerObject-Rig_ItemUI.prefab` — добавить `RigOutlinerItem`
- `OutlinerObject-Object_ItemUI.prefab` — упростить (один статичный icon)
- SO asset: `AnimatorPanelConfig.asset` создать через CreateAssetMenu
- Привязать `AnimatorPanelConfig` в `[SerializeField]` на `AnimatorPanelView`

---

## Phase 1: Events & Enums

### Task 1: ContainerChange enum

**Files:**
- Create: `Assets/_App/_Shared/Events/ContainerChange.cs`

- [ ] **Step 1: Create the enum file**

```csharp
public enum ContainerChange
{
    Added,
    Removed,
    LengthChanged,
    FpsChanged
}
```

- [ ] **Step 2: Verify compile in Unity**

Open Unity Editor, wait for compile. Expected: no errors in Console.

- [ ] **Step 3: Commit checkpoint (manual user-side)**

---

### Task 2: KeyframeChange enum

**Files:**
- Create: `Assets/_App/_Shared/Events/KeyframeChange.cs`

- [ ] **Step 1: Create the enum file**

```csharp
public enum KeyframeChange
{
    Added,
    Removed,
    Overwritten
}
```

- [ ] **Step 2: Verify compile in Unity**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

### Task 3: AnimationContainerChangedEvent

**Files:**
- Create: `Assets/_App/_Shared/Events/AnimationContainerChangedEvent.cs`

- [ ] **Step 1: Create event struct file**

```csharp
public struct AnimationContainerChangedEvent
{
    public string          OwnerNodeId;
    public ContainerChange Change;
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

### Task 4: BonesVisibilityChangedEvent

**Files:**
- Create: `Assets/_App/_Shared/Events/BonesVisibilityChangedEvent.cs`

- [ ] **Step 1: Create event struct file**

```csharp
public struct BonesVisibilityChangedEvent
{
    public string RigNodeId;
    public bool   Visible;
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

### Task 5: Extend AnimationKeyframeChangedEvent

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs:17`

- [ ] **Step 1: Replace existing event struct in AppEvents.cs**

Find line:
```csharp
public struct AnimationKeyframeChangedEvent { public string NodeId; }
```

Replace with:
```csharp
public struct AnimationKeyframeChangedEvent
{
    public string          NodeId;
    public string          OwnerNodeId;
    public int             Frame;
    public KeyframeChange  Change;
}
```

- [ ] **Step 2: Verify compile**

Expected: errors in `AnimationAuthoring.cs` (it publishes the old shape) and `AnimationModule.cs`. These will be fixed in later tasks. **Note errors but proceed.**

- [ ] **Step 3: Temporary fix to unblock build — update publish sites**

In `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`, find both occurrences:

```csharp
_bus.Publish(new AnimationKeyframeChangedEvent { NodeId = nodeId });
```

Replace each with:

```csharp
_bus.Publish(new AnimationKeyframeChangedEvent
{
    NodeId      = nodeId,
    OwnerNodeId = nodeId,
    Frame       = frame,
    Change      = KeyframeChange.Overwritten
});
```

(The full proper rewrite of these call sites happens in Phase 3. This is just to unblock compilation.)

- [ ] **Step 4: Verify compile**

Expected: no errors (AnimationModule still consumes only `NodeId`, that's fine — new fields are additive).

- [ ] **Step 5: Commit checkpoint**

---

## Phase 2: Data Model v2

### Task 6: AnimKeyData stays unchanged — verify

**Files:**
- Read-only: `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimKeyData.cs`

- [ ] **Step 1: Verify no changes needed**

Open file, confirm structure:
```csharp
[Serializable]
public class AnimKeyData
{
    public int        Frame;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
}
```

Expected: matches. No changes.

- [ ] **Step 2: Commit checkpoint** (no-op task — just a verification)

---

### Task 7: Extend AnimTrackData

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimTrackData.cs`
- Test: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs`

- [ ] **Step 1: Write failing test for trim**

Add to `AnimationDataTests.cs`:

```csharp
[Test]
public void TrimKeysAfter_RemovesKeysBeyondFrame()
{
    var track = new AnimTrackData { NodeId = "n1" };
    track.UpsertKey(5,  Vector3.zero, Quaternion.identity, Vector3.one);
    track.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);
    track.UpsertKey(25, Vector3.zero, Quaternion.identity, Vector3.one);

    track.TrimKeysAfter(15);

    Assert.AreEqual(2,  track.Keys.Count);
    Assert.AreEqual(5,  track.Keys[0].Frame);
    Assert.AreEqual(15, track.Keys[1].Frame);
}
```

- [ ] **Step 2: Run test, verify FAIL**

Open Unity Test Runner (`Window > General > Test Runner`), run `AnimationDataTests`. Expected: `TrimKeysAfter_RemovesKeysBeyondFrame` fails with compile error (method not defined).

- [ ] **Step 3: Add TrimKeysAfter method to AnimTrackData**

Add to `AnimTrackData.cs`:

```csharp
public void TrimKeysAfter(int maxFrame)
{
    for (int i = Keys.Count - 1; i >= 0; i--)
        if (Keys[i].Frame > maxFrame) Keys.RemoveAt(i);
}
```

- [ ] **Step 4: Run test, verify PASS**

Run `AnimationDataTests`. Expected: all pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 8: Create ActionContainer

**Files:**
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/ActionContainer.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Tests/ActionContainerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `ActionContainerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class ActionContainerTests
{
    [Test]
    public void Defaults_60Frames_24Fps()
    {
        var c = new ActionContainer();
        Assert.AreEqual(60, c.TotalFrames);
        Assert.AreEqual(24, c.Fps);
        Assert.AreEqual(0,  c.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_CreatesNew()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t = c.GetOrCreateTrack("rig");
        Assert.AreEqual("rig", t.NodeId);
        Assert.AreEqual(1, c.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_ReturnsExisting()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t1 = c.GetOrCreateTrack("rig");
        var t2 = c.GetOrCreateTrack("rig");
        Assert.AreSame(t1, t2);
    }

    [Test]
    public void FindTrack_ReturnsNullWhenMissing()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        Assert.IsNull(c.FindTrack("missing"));
    }

    [Test]
    public void HasAnyKeyAtFrame_TrueWhenAnyTrackHas()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t = c.GetOrCreateTrack("rig");
        t.UpsertKey(7, Vector3.zero, Quaternion.identity, Vector3.one);

        Assert.IsTrue (c.HasAnyKeyAtFrame(7));
        Assert.IsFalse(c.HasAnyKeyAtFrame(8));
    }

    [Test]
    public void TruncateToTotalFrames_DropsKeysBeyondTotal()
    {
        var c = new ActionContainer { OwnerNodeId = "rig", TotalFrames = 10 };
        var t = c.GetOrCreateTrack("rig");
        t.UpsertKey(5,  Vector3.zero, Quaternion.identity, Vector3.one);
        t.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);

        c.TruncateToTotalFrames();

        Assert.AreEqual(1, t.Keys.Count);
        Assert.AreEqual(5, t.Keys[0].Frame);
    }

    [Test]
    public void TruncateToTotalFrames_RemovesEmptyTracks()
    {
        var c = new ActionContainer { OwnerNodeId = "rig", TotalFrames = 10 };
        var t = c.GetOrCreateTrack("bone:rig:hand");
        t.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);

        c.TruncateToTotalFrames();

        Assert.AreEqual(0, c.Tracks.Count);
    }

    [Test]
    public void ExistingTrackNodeIds_ReturnsAllNodeIds()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        c.GetOrCreateTrack("rig");
        c.GetOrCreateTrack("bone:rig:hand");

        var ids = c.ExistingTrackNodeIds();
        Assert.AreEqual(2, ids.Count);
        Assert.Contains("rig",            (System.Collections.ICollection)ids);
        Assert.Contains("bone:rig:hand",  (System.Collections.ICollection)ids);
    }
}
```

- [ ] **Step 2: Run test, verify FAIL**

Test Runner → `ActionContainerTests`. Expected: compile errors (ActionContainer not defined).

- [ ] **Step 3: Implement ActionContainer**

Create `Assets/_App/Subsystems/AnimationAuthoring/Data/ActionContainer.cs`:

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class ActionContainer
{
    public string             OwnerNodeId;
    public int                Fps         = 24;
    public int                TotalFrames = 60;
    public List<AnimTrackData> Tracks     = new();

    public AnimTrackData FindTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        return null;
    }

    public AnimTrackData GetOrCreateTrack(string nodeId)
    {
        var existing = FindTrack(nodeId);
        if (existing != null) return existing;
        var track = new AnimTrackData { NodeId = nodeId };
        Tracks.Add(track);
        return track;
    }

    public bool HasAnyKeyAtFrame(int frame)
    {
        foreach (var t in Tracks)
            if (t.HasKey(frame)) return true;
        return false;
    }

    public IReadOnlyList<string> ExistingTrackNodeIds()
    {
        var ids = new string[Tracks.Count];
        for (int i = 0; i < Tracks.Count; i++) ids[i] = Tracks[i].NodeId;
        return ids;
    }

    public void TruncateToTotalFrames()
    {
        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            Tracks[i].TrimKeysAfter(TotalFrames);
            if (Tracks[i].Keys.Count == 0) Tracks.RemoveAt(i);
        }
    }
}
```

- [ ] **Step 4: Run test, verify PASS**

Test Runner → `ActionContainerTests`. Expected: all 8 pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 9: Rewrite SceneAnimationData to v2

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Data/SceneAnimationData.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs`

- [ ] **Step 1: Update existing JsonRoundTrip test to v2 shape**

In `AnimationDataTests.cs`, replace `SceneAnimationData_JsonRoundTrip` with:

```csharp
[Test]
public void SceneAnimationData_JsonRoundTrip_v2()
{
    var data = new SceneAnimationData();
    var c    = data.CreateContainer("rig", 60, 24);
    var t    = c.GetOrCreateTrack("rig");
    t.UpsertKey(5, new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), Vector3.one);

    var json   = UnityEngine.JsonUtility.ToJson(data);
    var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);

    Assert.AreEqual(2, loaded.schemaVersion);
    Assert.AreEqual(1, loaded.Containers.Count);
    Assert.AreEqual("rig", loaded.Containers[0].OwnerNodeId);
    Assert.AreEqual(24,    loaded.Containers[0].Fps);
    Assert.AreEqual(60,    loaded.Containers[0].TotalFrames);
    Assert.AreEqual(1,     loaded.Containers[0].Tracks.Count);
    Assert.AreEqual(5,     loaded.Containers[0].Tracks[0].Keys[0].Frame);
    Assert.AreEqual(1f,    loaded.Containers[0].Tracks[0].Keys[0].Position.x, 0.001f);
}
```

Remove the existing `GetOrCreateTrack_CreatesNewTrack` and `GetOrCreateTrack_ReturnsExistingTrack` tests (they tested the flat layout which is being removed).

Add these new container-level tests:

```csharp
[Test]
public void FindByOwner_ReturnsNullWhenMissing()
{
    var data = new SceneAnimationData();
    Assert.IsNull(data.FindByOwner("missing"));
}

[Test]
public void CreateContainer_AddsAndReturns()
{
    var data = new SceneAnimationData();
    var c    = data.CreateContainer("rig", 90, 30);

    Assert.AreEqual(1,    data.Containers.Count);
    Assert.AreEqual("rig", c.OwnerNodeId);
    Assert.AreEqual(90,    c.TotalFrames);
    Assert.AreEqual(30,    c.Fps);
}

[Test]
public void CreateContainer_DefaultArgs_60_24()
{
    var data = new SceneAnimationData();
    var c    = data.CreateContainer("rig");

    Assert.AreEqual(60, c.TotalFrames);
    Assert.AreEqual(24, c.Fps);
}

[Test]
public void RemoveContainer_RemovesByOwner()
{
    var data = new SceneAnimationData();
    data.CreateContainer("a");
    data.CreateContainer("b");

    data.RemoveContainer("a");

    Assert.AreEqual(1,   data.Containers.Count);
    Assert.AreEqual("b", data.Containers[0].OwnerNodeId);
}
```

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile errors (CreateContainer/FindByOwner/RemoveContainer not defined; Containers/Fps/TotalFrames at wrong level).

- [ ] **Step 3: Rewrite SceneAnimationData.cs**

Replace entire file content:

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class SceneAnimationData
{
    public int                   schemaVersion = 2;
    public List<ActionContainer> Containers    = new();

    public ActionContainer FindByOwner(string ownerNodeId)
    {
        foreach (var c in Containers)
            if (c.OwnerNodeId == ownerNodeId) return c;
        return null;
    }

    public ActionContainer CreateContainer(string ownerNodeId, int totalFrames = 60, int fps = 24)
    {
        var existing = FindByOwner(ownerNodeId);
        if (existing != null) return existing;
        var c = new ActionContainer
        {
            OwnerNodeId = ownerNodeId,
            TotalFrames = totalFrames,
            Fps         = fps
        };
        Containers.Add(c);
        return c;
    }

    public void RemoveContainer(string ownerNodeId)
    {
        for (int i = Containers.Count - 1; i >= 0; i--)
            if (Containers[i].OwnerNodeId == ownerNodeId) Containers.RemoveAt(i);
    }
}
```

- [ ] **Step 4: Verify compile (AnimationAuthoring.cs will have errors)**

Expected: many errors in `AnimationAuthoring.cs` (it uses old `_data.GetOrCreateTrack`, `_data.Fps`, etc.). These are fixed in Phase 3 — leave for now.

To unblock the test run, temporarily stub out the broken methods in `AnimationAuthoring.cs`: comment out the body of `SetKey`, `DeleteKey`, `HasKey`, `GetKeyFrames`, `OnFrameChanged`, `ApplyFrame`, `RebuildClip`, leaving only method signatures returning defaults. This is a TEMPORARY stub for Phase 2; the full implementation is restored in Phase 3.

```csharp
public void SetKey   (string nodeId, int frame) { /* PHASE3 */ }
public void DeleteKey(string nodeId, int frame) { /* PHASE3 */ }
public bool HasKey   (string nodeId, int frame) => false;
public IReadOnlyList<int> GetKeyFrames(string nodeId) => System.Array.Empty<int>();

private void OnFrameChanged(FrameChangedEvent e) { /* PHASE3 */ }
private void ApplyFrame   (int frame)            { /* PHASE3 */ }
```

Also stub `OnSceneOpened` body and `LoadAsync`/`SaveAsync` bodies to use new v2 shape:

In `LoadAsync(string sceneId, CancellationToken ct)`, replace body of the `try` block with:

```csharp
var json = await File.ReadAllTextAsync(path, ct);
_data    = JsonUtility.FromJson<SceneAnimationData>(json) ?? new SceneAnimationData();
```

(removing the `foreach (var track in _data.Tracks) RebuildClip(track);` line — Tracks no longer top-level)

In `EnsureData`: keep as is.

Also remove the `_clips` dictionary usage temporarily (`_clips.Clear()`, `_clips.Remove`, `_clips.TryGetValue`, `_clips[track.NodeId] = clip`) by commenting them out (`/* */`). The clips cache is reintroduced in Phase 3 with the new container-aware logic.

- [ ] **Step 5: Run tests, verify PASS**

`AnimationDataTests` and `ActionContainerTests` should all pass.

- [ ] **Step 6: Commit checkpoint**

---

## Phase 3: AnimationAuthoring API Rewrite

### Task 10: AnimationAuthoring — full API definition

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Write tests for OwnerOf helper**

Create `AnimationAuthoringTests.cs`:

```csharp
using NUnit.Framework;

public class AnimationAuthoringTests
{
    [Test]
    public void OwnerOf_BonePrefix_StripsToRigId()
    {
        Assert.AreEqual("rig", AnimationAuthoring.OwnerOf("bone:rig:hand"));
    }

    [Test]
    public void OwnerOf_RegularNode_ReturnsAsIs()
    {
        Assert.AreEqual("plain", AnimationAuthoring.OwnerOf("plain"));
    }

    [Test]
    public void OwnerOf_BoneWithoutName_ReturnsRigId()
    {
        Assert.AreEqual("rig", AnimationAuthoring.OwnerOf("bone:rig:"));
    }

    [Test]
    public void OwnerOf_Null_ReturnsNull()
    {
        Assert.IsNull(AnimationAuthoring.OwnerOf(null));
    }

    [Test]
    public void OwnerOf_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", AnimationAuthoring.OwnerOf(""));
    }
}
```

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile error (OwnerOf static method does not exist).

- [ ] **Step 3: Add OwnerOf static helper to AnimationAuthoring.cs**

In `AnimationAuthoring.cs`, add after the constructor:

```csharp
public static string OwnerOf(string nodeId)
{
    if (nodeId == null) return null;
    if (!nodeId.StartsWith("bone:")) return nodeId;
    var parts = nodeId.Split(':');
    return parts.Length >= 2 ? parts[1] : nodeId;
}
```

- [ ] **Step 4: Run tests, verify PASS**

Expected: 5 tests pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 11: Container CRUD API

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Write tests**

Add to `AnimationAuthoringTests.cs`:

```csharp
[Test]
public void HasContainer_FalseWhenMissing()
{
    var fix = new AuthoringFixture();
    Assert.IsFalse(fix.Authoring.HasContainer("any"));
}

[Test]
public void CreateContainer_AddsContainer()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");
    Assert.IsTrue(fix.Authoring.HasContainer("obj"));
}

[Test]
public void CreateContainer_PublishesAddedEvent()
{
    var fix = new AuthoringFixture();
    AnimationContainerChangedEvent? received = null;
    fix.Bus.Subscribe<AnimationContainerChangedEvent>(e => received = e);

    fix.Authoring.CreateContainer("obj");

    Assert.IsTrue(received.HasValue);
    Assert.AreEqual("obj", received.Value.OwnerNodeId);
    Assert.AreEqual(ContainerChange.Added, received.Value.Change);
}

[Test]
public void RemoveContainer_RemovesAndPublishesEvent()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");

    AnimationContainerChangedEvent? received = null;
    fix.Bus.Subscribe<AnimationContainerChangedEvent>(e => received = e);

    fix.Authoring.RemoveContainer("obj");

    Assert.IsFalse(fix.Authoring.HasContainer("obj"));
    Assert.IsTrue(received.HasValue);
    Assert.AreEqual(ContainerChange.Removed, received.Value.Change);
}

[Test]
public void GetContainer_ReturnsNullWhenMissing()
{
    var fix = new AuthoringFixture();
    Assert.IsNull(fix.Authoring.GetContainer("missing"));
}
```

Add fixture helper at top of test class:

```csharp
private class AuthoringFixture
{
    public EventBus           Bus       { get; } = new();
    public AnimationAuthoring Authoring { get; }

    public AuthoringFixture()
    {
        // Pass nulls for sceneGraph, paths, storage — test only in-memory ops
        Authoring = new AnimationAuthoring(
            clock     : null,
            sceneGraph: null,
            paths     : null,
            storage   : null,
            bus       : Bus);
        Authoring.InitForTest(); // bypass Start() which subscribes/loads
    }
}
```

(`InitForTest()` exposes the in-memory `_data = new SceneAnimationData()` initialization without touching files. Add it in Step 3.)

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile errors (HasContainer/CreateContainer/RemoveContainer/GetContainer/InitForTest not defined).

- [ ] **Step 3: Implement Container CRUD in AnimationAuthoring.cs**

Add after `OwnerOf`:

```csharp
// === Container CRUD ===

public bool HasContainer(string ownerNodeId) =>
    _data?.FindByOwner(ownerNodeId) != null;

public ActionContainer GetContainer(string ownerNodeId) =>
    _data?.FindByOwner(ownerNodeId);

public ActionContainer CreateContainer(string ownerNodeId)
{
    EnsureData();
    var c = _data.CreateContainer(ownerNodeId);
    _bus.Publish(new AnimationContainerChangedEvent
    {
        OwnerNodeId = ownerNodeId,
        Change      = ContainerChange.Added
    });
    RequestSave();
    return c;
}

public void RemoveContainer(string ownerNodeId)
{
    if (_data == null) return;
    if (_data.FindByOwner(ownerNodeId) == null) return;
    _data.RemoveContainer(ownerNodeId);
    _bus.Publish(new AnimationContainerChangedEvent
    {
        OwnerNodeId = ownerNodeId,
        Change      = ContainerChange.Removed
    });
    RequestSave();
}

// Test-only: bypass Start() side-effects
internal void InitForTest() => _data = new SceneAnimationData();
```

Also add minimal `RequestSave()` stub (full impl in Task 19):

```csharp
private void RequestSave() { /* no-op until Task 19 */ }
```

- [ ] **Step 4: Run tests, verify PASS**

Expected: 5 new tests pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 12: SetTotalFrames & SetFps

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Write tests**

Add to `AnimationAuthoringTests.cs`:

```csharp
[Test]
public void SetTotalFrames_UpdatesAndPublishesLengthChanged()
{
    var fix = new AuthoringFixture();
    var c   = fix.Authoring.CreateContainer("obj");

    AnimationContainerChangedEvent? last = null;
    fix.Bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.LengthChanged) last = e; });

    fix.Authoring.SetTotalFrames("obj", 30);

    Assert.AreEqual(30, c.TotalFrames);
    Assert.IsTrue(last.HasValue);
    Assert.AreEqual("obj", last.Value.OwnerNodeId);
}

[Test]
public void SetTotalFrames_TruncatesKeysBeyondNewLength()
{
    var fix = new AuthoringFixture();
    var c   = fix.Authoring.CreateContainer("obj");
    var t   = c.GetOrCreateTrack("obj");
    t.UpsertKey(50, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    t.UpsertKey(80, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    fix.Authoring.SetTotalFrames("obj", 60);

    Assert.AreEqual(1,  t.Keys.Count);
    Assert.AreEqual(50, t.Keys[0].Frame);
}

[Test]
public void SetTotalFrames_ClampsToMinimumOne()
{
    var fix = new AuthoringFixture();
    var c   = fix.Authoring.CreateContainer("obj");

    fix.Authoring.SetTotalFrames("obj", 0);
    Assert.AreEqual(1, c.TotalFrames);

    fix.Authoring.SetTotalFrames("obj", -5);
    Assert.AreEqual(1, c.TotalFrames);
}

[Test]
public void SetFps_UpdatesAndPublishesFpsChanged()
{
    var fix = new AuthoringFixture();
    var c   = fix.Authoring.CreateContainer("obj");

    AnimationContainerChangedEvent? last = null;
    fix.Bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.FpsChanged) last = e; });

    fix.Authoring.SetFps("obj", 60);

    Assert.AreEqual(60, c.Fps);
    Assert.IsTrue(last.HasValue);
}

[Test]
public void SetFps_ClampsToMinimumOne()
{
    var fix = new AuthoringFixture();
    var c   = fix.Authoring.CreateContainer("obj");
    fix.Authoring.SetFps("obj", 0);
    Assert.AreEqual(1, c.Fps);
}

[Test]
public void SetTotalFrames_NoContainer_NoOp()
{
    var fix = new AuthoringFixture();
    bool published = false;
    fix.Bus.Subscribe<AnimationContainerChangedEvent>(_ => published = true);

    fix.Authoring.SetTotalFrames("missing", 30);

    Assert.IsFalse(published);
}
```

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile errors (methods not defined).

- [ ] **Step 3: Implement SetTotalFrames & SetFps**

Add to `AnimationAuthoring.cs`:

```csharp
// === Length / FPS ===

public void SetTotalFrames(string ownerNodeId, int frames)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;
    c.TotalFrames = Mathf.Max(1, frames);
    c.TruncateToTotalFrames();
    _bus.Publish(new AnimationContainerChangedEvent
    {
        OwnerNodeId = ownerNodeId,
        Change      = ContainerChange.LengthChanged
    });
    RequestSave();
}

public void SetFps(string ownerNodeId, int fps)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;
    c.Fps = Mathf.Max(1, fps);
    _bus.Publish(new AnimationContainerChangedEvent
    {
        OwnerNodeId = ownerNodeId,
        Change      = ContainerChange.FpsChanged
    });
    RequestSave();
}
```

(Requires `using UnityEngine;` already at top of file.)

- [ ] **Step 4: Run tests, verify PASS**

Expected: 6 new tests pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 13: Per-track SetKey / DeleteKey / HasKey / GetKeyFrames

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

The original `SetKey` captured the live `Transform` of the node and wrote keys. For unit tests we cannot use `SceneGraph.GetNode()` (no GameObjects). Refactor: split the API into a transform-capturing path (production) and a value-overload (testable).

- [ ] **Step 1: Write tests using value overloads**

Add to `AnimationAuthoringTests.cs`:

```csharp
[Test]
public void SetKey_WithExplicitValues_AddsKeyToTrack()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");

    fix.Authoring.SetKey("obj", 10,
        UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    var c = fix.Authoring.GetContainer("obj");
    var t = c.FindTrack("obj");
    Assert.IsNotNull(t);
    Assert.IsTrue(t.HasKey(10));
}

[Test]
public void SetKey_PublishesKeyframeChangedAdded()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");

    AnimationKeyframeChangedEvent? evt = null;
    fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);

    fix.Authoring.SetKey("obj", 5,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    Assert.IsTrue(evt.HasValue);
    Assert.AreEqual("obj", evt.Value.NodeId);
    Assert.AreEqual("obj", evt.Value.OwnerNodeId);
    Assert.AreEqual(5,     evt.Value.Frame);
    Assert.AreEqual(KeyframeChange.Added, evt.Value.Change);
}

[Test]
public void SetKey_OverwriteExisting_PublishesOverwritten()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");
    fix.Authoring.SetKey("obj", 5,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    AnimationKeyframeChangedEvent? evt = null;
    fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);
    fix.Authoring.SetKey("obj", 5,
        UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    Assert.AreEqual(KeyframeChange.Overwritten, evt.Value.Change);
}

[Test]
public void DeleteKey_RemovesAndPublishesEvent()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");
    fix.Authoring.SetKey("obj", 5,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    AnimationKeyframeChangedEvent? evt = null;
    fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);

    fix.Authoring.DeleteKey("obj", 5);

    Assert.IsFalse(fix.Authoring.HasKey("obj", 5));
    Assert.AreEqual(KeyframeChange.Removed, evt.Value.Change);
}

[Test]
public void DeleteKey_LastInTrack_RemovesTrack()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");
    fix.Authoring.SetKey("obj", 5,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    fix.Authoring.DeleteKey("obj", 5);

    var c = fix.Authoring.GetContainer("obj");
    Assert.AreEqual(0, c.Tracks.Count);
}

[Test]
public void SetKey_BoneNode_RoutesToParentRigContainer()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");

    fix.Authoring.SetKey("bone:rig:hand", 7,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    var c = fix.Authoring.GetContainer("rig");
    Assert.IsNotNull(c.FindTrack("bone:rig:hand"));
}

[Test]
public void HasKey_FalseWhenNoContainer()
{
    var fix = new AuthoringFixture();
    Assert.IsFalse(fix.Authoring.HasKey("missing", 5));
}

[Test]
public void GetKeyFrames_ReturnsFramesInOrder()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("obj");
    fix.Authoring.SetKey("obj", 5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("obj", 1,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("obj", 10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    var frames = fix.Authoring.GetKeyFrames("obj");
    Assert.AreEqual(3, frames.Count);
    Assert.AreEqual(1,  frames[0]);
    Assert.AreEqual(5,  frames[1]);
    Assert.AreEqual(10, frames[2]);
}
```

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile errors (SetKey value overload not defined).

- [ ] **Step 3: Implement per-track API**

Replace the stub `SetKey`/`DeleteKey`/`HasKey`/`GetKeyFrames` in `AnimationAuthoring.cs` with:

```csharp
// === Per-track keys ===

public void SetKey(string nodeId, int frame)
{
    var go = _sceneGraph?.GetNode(nodeId);
    if (go == null) return;
    SetKey(nodeId, frame, go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
}

public void SetKey(string nodeId, int frame, Vector3 pos, Quaternion rot, Vector3 scale)
{
    var owner = OwnerOf(nodeId);
    if (string.IsNullOrEmpty(owner)) return;
    EnsureData();
    var c = _data.FindByOwner(owner);
    if (c == null) return;

    var track    = c.GetOrCreateTrack(nodeId);
    bool existed = track.HasKey(frame);
    track.UpsertKey(frame, pos, rot, scale);

    _bus.Publish(new AnimationKeyframeChangedEvent
    {
        NodeId      = nodeId,
        OwnerNodeId = owner,
        Frame       = frame,
        Change      = existed ? KeyframeChange.Overwritten : KeyframeChange.Added
    });
    RequestSave();
}

public void DeleteKey(string nodeId, int frame)
{
    var owner = OwnerOf(nodeId);
    var c     = _data?.FindByOwner(owner);
    var track = c?.FindTrack(nodeId);
    if (track == null) return;

    if (!track.HasKey(frame)) return;
    track.RemoveKey(frame);
    if (track.Keys.Count == 0) c.Tracks.Remove(track);

    _bus.Publish(new AnimationKeyframeChangedEvent
    {
        NodeId      = nodeId,
        OwnerNodeId = owner,
        Frame       = frame,
        Change      = KeyframeChange.Removed
    });
    RequestSave();
}

public bool HasKey(string nodeId, int frame)
{
    var owner = OwnerOf(nodeId);
    return _data?.FindByOwner(owner)?.FindTrack(nodeId)?.HasKey(frame) ?? false;
}

public IReadOnlyList<int> GetKeyFrames(string nodeId)
{
    var owner = OwnerOf(nodeId);
    var track = _data?.FindByOwner(owner)?.FindTrack(nodeId);
    if (track == null) return System.Array.Empty<int>();
    var frames = new int[track.Keys.Count];
    for (int i = 0; i < track.Keys.Count; i++) frames[i] = track.Keys[i].Frame;
    return frames;
}
```

- [ ] **Step 4: Run tests, verify PASS**

Expected: all SetKey/DeleteKey/HasKey/GetKeyFrames tests pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 14: SetKeyForFrame (whole-frame with active node)

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

This is the user-facing "Set Key" action — captures active node + all existing tracks of the container.

- [ ] **Step 1: Write tests using value-snapshots**

The production version reads `Transform` of each track's node from `SceneGraph`. For tests we'll add a `SetKeyForFrame_Test` overload that takes a snapshot dict directly.

Add to `AnimationAuthoringTests.cs`:

```csharp
[Test]
public void SetKeyForFrame_ActiveOnly_WritesActiveTrack()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");

    var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
    {
        ["rig"] = (UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
    };
    fix.Authoring.SetKeyForFrame_Test("rig", "rig", 10, snapshots);

    Assert.IsTrue(fix.Authoring.HasKey("rig", 10));
}

[Test]
public void SetKeyForFrame_ActiveNodeLazyCreatesTrack()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");

    var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
    {
        ["bone:rig:hand"] = (UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
    };
    fix.Authoring.SetKeyForFrame_Test("rig", "bone:rig:hand", 5, snapshots);

    var c = fix.Authoring.GetContainer("rig");
    Assert.IsNotNull(c.FindTrack("bone:rig:hand"));
    Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand", 5));
}

[Test]
public void SetKeyForFrame_WritesActiveAndAllExistingTracks()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    // seed existing track on bone:rig:hand
    fix.Authoring.SetKey("bone:rig:hand", 0,
        UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
    {
        ["rig"]            = (UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
        ["bone:rig:hand"]  = (UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
    };
    fix.Authoring.SetKeyForFrame_Test("rig", "rig", 20, snapshots);

    Assert.IsTrue(fix.Authoring.HasKey("rig",             20));
    Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand",   20));
}

[Test]
public void SetKeyForFrame_NoContainer_NoOp()
{
    var fix = new AuthoringFixture();
    fix.Authoring.SetKeyForFrame_Test("missing", "missing", 5,
        new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>());

    Assert.IsFalse(fix.Authoring.HasContainer("missing"));
}
```

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile errors (SetKeyForFrame_Test not defined).

- [ ] **Step 3: Implement SetKeyForFrame + test overload**

Add to `AnimationAuthoring.cs`:

```csharp
// === Whole-frame operations ===

public void SetKeyForFrame(string ownerNodeId, string activeNodeId, int frame)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;

    var snapshots = new System.Collections.Generic.Dictionary<string, (Vector3, Quaternion, Vector3)>();

    // Always include active node (may lazy-create the track)
    if (!string.IsNullOrEmpty(activeNodeId) && _sceneGraph != null)
    {
        var go = _sceneGraph.GetNode(activeNodeId);
        if (go != null)
            snapshots[activeNodeId] = (go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
    }

    // All existing tracks of the container
    foreach (var t in c.Tracks)
    {
        if (snapshots.ContainsKey(t.NodeId)) continue;
        var go = _sceneGraph?.GetNode(t.NodeId);
        if (go == null) continue;
        snapshots[t.NodeId] = (go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
    }

    SetKeyForFrame_Test(ownerNodeId, activeNodeId, frame, snapshots);
}

internal void SetKeyForFrame_Test(
    string ownerNodeId, string activeNodeId, int frame,
    System.Collections.Generic.Dictionary<string, (Vector3 Pos, Quaternion Rot, Vector3 Scale)> snapshots)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;

    // Active node first (lazy-create if absent)
    if (!string.IsNullOrEmpty(activeNodeId) && snapshots.TryGetValue(activeNodeId, out var aSnap))
    {
        SetKey(activeNodeId, frame, aSnap.Pos, aSnap.Rot, aSnap.Scale);
    }

    // Other tracks already in container (skip active to avoid double-write)
    var existingIds = new System.Collections.Generic.List<string>(c.ExistingTrackNodeIds());
    foreach (var tid in existingIds)
    {
        if (tid == activeNodeId) continue;
        if (!snapshots.TryGetValue(tid, out var snap)) continue;
        SetKey(tid, frame, snap.Pos, snap.Rot, snap.Scale);
    }
}
```

- [ ] **Step 4: Run tests, verify PASS**

Expected: 4 new tests pass.

- [ ] **Step 5: Commit checkpoint**

---

### Task 15: DeleteAllKeysAtFrame

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Write tests**

Add:

```csharp
[Test]
public void DeleteAllKeysAtFrame_RemovesFromAllTracksAtFrame()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig",            10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("bone:rig:hand",  10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("rig",            20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    fix.Authoring.DeleteAllKeysAtFrame("rig", 10);

    Assert.IsFalse(fix.Authoring.HasKey("rig",            10));
    Assert.IsFalse(fix.Authoring.HasKey("bone:rig:hand",  10));
    Assert.IsTrue (fix.Authoring.HasKey("rig",            20));
}

[Test]
public void DeleteAllKeysAtFrame_NoOpForMissingFrame()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig", 10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    fix.Authoring.DeleteAllKeysAtFrame("rig", 99);

    Assert.IsTrue(fix.Authoring.HasKey("rig", 10));
}
```

- [ ] **Step 2: Run tests, verify FAIL**

- [ ] **Step 3: Implement DeleteAllKeysAtFrame**

```csharp
public void DeleteAllKeysAtFrame(string ownerNodeId, int frame)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;

    var trackIds = new System.Collections.Generic.List<string>(c.ExistingTrackNodeIds());
    foreach (var id in trackIds)
        DeleteKey(id, frame);
}
```

- [ ] **Step 4: Run tests, verify PASS**

- [ ] **Step 5: Commit checkpoint**

---

### Task 16: CopyFrame & PasteFrame

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboard.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboardEntry.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Create FrameClipboardEntry**

`Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboardEntry.cs`:

```csharp
using System;
using UnityEngine;

[Serializable]
public struct FrameClipboardEntry
{
    public string     TrackNodeId;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
}
```

- [ ] **Step 2: Create FrameClipboard**

`Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboard.cs`:

```csharp
using System.Collections.Generic;

public class FrameClipboard
{
    public string                    OwnerNodeId;
    public int                       SourceFrame;
    public List<FrameClipboardEntry> Entries = new();

    public bool IsEmpty => Entries == null || Entries.Count == 0;
}
```

- [ ] **Step 3: Write tests**

Add to `AnimationAuthoringTests.cs`:

```csharp
[Test]
public void CopyFrame_NoKeysAtFrame_ReturnsEmptyClipboard()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");

    var clip = fix.Authoring.CopyFrame("rig", 10);

    Assert.IsTrue(clip.IsEmpty);
}

[Test]
public void CopyFrame_ReturnsAllKeysAtFrame()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig",           10, UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("bone:rig:hand", 10, UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    var clip = fix.Authoring.CopyFrame("rig", 10);

    Assert.AreEqual(2,     clip.Entries.Count);
    Assert.AreEqual("rig", clip.OwnerNodeId);
    Assert.AreEqual(10,    clip.SourceFrame);
}

[Test]
public void PasteFrame_RestoresKeysAtTargetFrame()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig",           10, UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("bone:rig:hand", 10, UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    var clip = fix.Authoring.CopyFrame("rig", 10);

    fix.Authoring.PasteFrame("rig", 30, clip);

    Assert.IsTrue(fix.Authoring.HasKey("rig",           30));
    Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand", 30));
}

[Test]
public void PasteFrame_LazyCreatesMissingTrack()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("src");
    fix.Authoring.SetKey("src", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    var clip = fix.Authoring.CopyFrame("src", 5);

    fix.Authoring.CreateContainer("dst");
    // dst has no tracks yet — but clip entry's TrackNodeId == "src", not in dst.
    // We expect paste to still create that track (caller's responsibility to repoint TrackNodeId if needed).
    fix.Authoring.PasteFrame("dst", 5, clip);

    var c = fix.Authoring.GetContainer("dst");
    Assert.IsNotNull(c.FindTrack("src"));
}

[Test]
public void PasteFrame_NullClipboard_NoOp()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");

    fix.Authoring.PasteFrame("rig", 10, null);
    Assert.AreEqual(0, fix.Authoring.GetContainer("rig").Tracks.Count);
}
```

- [ ] **Step 4: Run tests, verify FAIL**

Expected: compile errors (CopyFrame/PasteFrame not defined).

- [ ] **Step 5: Implement CopyFrame & PasteFrame**

Add to `AnimationAuthoring.cs`:

```csharp
public FrameClipboard CopyFrame(string ownerNodeId, int frame)
{
    var clip = new FrameClipboard { OwnerNodeId = ownerNodeId, SourceFrame = frame };
    var c    = _data?.FindByOwner(ownerNodeId);
    if (c == null) return clip;

    foreach (var t in c.Tracks)
    {
        foreach (var k in t.Keys)
        {
            if (k.Frame != frame) continue;
            clip.Entries.Add(new FrameClipboardEntry
            {
                TrackNodeId = t.NodeId,
                Position    = k.Position,
                Rotation    = k.Rotation,
                Scale       = k.Scale
            });
            break;
        }
    }
    return clip;
}

public void PasteFrame(string ownerNodeId, int frame, FrameClipboard clip)
{
    if (clip == null || clip.IsEmpty) return;
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return;

    foreach (var e in clip.Entries)
        SetKey(e.TrackNodeId, frame, e.Position, e.Rotation, e.Scale);
}
```

- [ ] **Step 6: Run tests, verify PASS**

- [ ] **Step 7: Commit checkpoint**

---

### Task 17: NearestKeyBefore & NearestKeyAfter

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`

- [ ] **Step 1: Write tests**

```csharp
[Test]
public void NearestKeyBefore_ReturnsPreviousKey()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig",           5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("bone:rig:hand", 12, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("rig",           20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    Assert.AreEqual(12, fix.Authoring.NearestKeyBefore("rig", 15));
    Assert.AreEqual(5,  fix.Authoring.NearestKeyBefore("rig", 10));
}

[Test]
public void NearestKeyBefore_ReturnsNullIfNone()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 5));
    Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 0));
}

[Test]
public void NearestKeyAfter_ReturnsNextKey()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig", 5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    fix.Authoring.SetKey("rig", 20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

    Assert.AreEqual(20, fix.Authoring.NearestKeyAfter("rig", 5));
    Assert.AreEqual(5,  fix.Authoring.NearestKeyAfter("rig", 0));
}

[Test]
public void NearestKeyAfter_ReturnsNullIfNone()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    fix.Authoring.SetKey("rig", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
    Assert.IsNull(fix.Authoring.NearestKeyAfter("rig", 5));
    Assert.IsNull(fix.Authoring.NearestKeyAfter("rig", 100));
}

[Test]
public void NearestKey_EmptyContainer_ReturnsNull()
{
    var fix = new AuthoringFixture();
    fix.Authoring.CreateContainer("rig");
    Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 10));
    Assert.IsNull(fix.Authoring.NearestKeyAfter ("rig", 10));
}
```

- [ ] **Step 2: Run tests, verify FAIL**

- [ ] **Step 3: Implement**

```csharp
public int? NearestKeyBefore(string ownerNodeId, int frame)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return null;

    int? best = null;
    foreach (var t in c.Tracks)
        foreach (var k in t.Keys)
            if (k.Frame < frame && (!best.HasValue || k.Frame > best.Value))
                best = k.Frame;
    return best;
}

public int? NearestKeyAfter(string ownerNodeId, int frame)
{
    var c = _data?.FindByOwner(ownerNodeId);
    if (c == null) return null;

    int? best = null;
    foreach (var t in c.Tracks)
        foreach (var k in t.Keys)
            if (k.Frame > frame && (!best.HasValue || k.Frame < best.Value))
                best = k.Frame;
    return best;
}
```

- [ ] **Step 4: Run tests, verify PASS**

- [ ] **Step 5: Commit checkpoint**

---

### Task 18: LoadAsync — v1 discard + v2 load

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`

- [ ] **Step 1: Rewrite LoadAsync body**

Replace existing `LoadAsync` method with:

```csharp
private async Task LoadAsync(string sceneId, CancellationToken ct)
{
    _sceneId = sceneId;
    var path = _paths.AnimationJson(sceneId);

    if (!File.Exists(path))
    {
        _data = new SceneAnimationData();
        return;
    }

    try
    {
        var json    = await File.ReadAllTextAsync(path, ct);
        var loaded  = JsonUtility.FromJson<SceneAnimationData>(json);

        if (loaded == null || loaded.schemaVersion < 2)
        {
            Debug.LogWarning(
                $"AnimationAuthoring: discarding old animation data at '{path}' (schemaVersion={loaded?.schemaVersion ?? 0}). Starting fresh.");
            try { File.Delete(path); } catch (Exception delEx) {
                Debug.LogError($"AnimationAuthoring: failed to delete v1 file '{path}': {delEx.Message}");
            }
            _data = new SceneAnimationData();
            return;
        }

        if (loaded.schemaVersion > 2)
        {
            Debug.LogError(
                $"AnimationAuthoring: animation file '{path}' has schemaVersion={loaded.schemaVersion} (newer than supported 2). Opening empty in-memory data; file NOT touched.");
            _data = new SceneAnimationData();
            return;
        }

        _data = loaded;
    }
    catch (Exception ex)
    {
        Debug.LogError($"AnimationAuthoring: load failed '{path}': {ex.Message}");
        _data = new SceneAnimationData();
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors (uses existing `File`, `JsonUtility`, `Debug`, `Exception`).

- [ ] **Step 3: Manual test (sceneId with v1 file)**

Manual smoke: place a fake `animation.json` with `{"schemaVersion":1,"Fps":24,"TotalFrames":60,"Tracks":[]}` into `Application.persistentDataPath/scenes/<sceneId>/`. Open scene → check Console for warning, verify file removed, verify in-memory empty v2.

- [ ] **Step 4: Commit checkpoint**

---

### Task 19: Debounced SaveAsync

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`

- [ ] **Step 1: Replace RequestSave stub with debounced impl**

In `AnimationAuthoring.cs`:

Add fields:
```csharp
private CancellationTokenSource _saveCts;
private const int SAVE_DEBOUNCE_MS = 200;
```

Replace the stub `RequestSave`:
```csharp
private void RequestSave()
{
    _saveCts?.Cancel();
    _saveCts = new CancellationTokenSource();
    _ = DebouncedSave(_saveCts.Token);
}

private async Task DebouncedSave(CancellationToken ct)
{
    try
    {
        await Task.Delay(SAVE_DEBOUNCE_MS, ct);
        if (ct.IsCancellationRequested) return;
        await SaveAsync(ct);
    }
    catch (TaskCanceledException) { /* expected on debounce */ }
}
```

Restore `SaveAsync` body (it was kept):

```csharp
private async Task SaveAsync(CancellationToken ct)
{
    if (_data == null || string.IsNullOrEmpty(_sceneId)) return;
    try
    {
        var path = _paths.AnimationJson(_sceneId);
        var json = JsonUtility.ToJson(_data, prettyPrint: true);
        await File.WriteAllTextAsync(path, json, ct);
    }
    catch (Exception ex)
    {
        Debug.LogError($"AnimationAuthoring: save failed: {ex.Message}");
    }
}
```

Update `Dispose`:
```csharp
public void Dispose()
{
    _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);
    _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
    _saveCts?.Cancel();
    _saveCts?.Dispose();
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors. Tests still pass (debounce is async; tests use in-memory, never trigger save path because `_paths` is null in fixture — `SaveAsync` early-returns on null `_sceneId`).

- [ ] **Step 3: Run all AnimationAuthoringTests**

Expected: all green.

- [ ] **Step 4: Commit checkpoint**

---

### Task 20: Restore ApplyFrame for playback (use container's FPS)

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`

The previous code rebuilt a single `AnimationClip` per track using `_data.Fps`. Now FPS is per-container.

- [ ] **Step 1: Replace OnFrameChanged + ApplyFrame body**

Remove the temporary stubs added in Task 9. Replace with:

```csharp
private readonly Dictionary<string, AnimationClip> _clips = new();
private string _activeContainerOwner;   // updated externally by AnimatorPanelView via SetActiveContainer

public void SetActiveContainerOwner(string ownerNodeId)
{
    _activeContainerOwner = ownerNodeId;
    RebuildActiveClips();
}

private void RebuildActiveClips()
{
    _clips.Clear();
    if (string.IsNullOrEmpty(_activeContainerOwner)) return;
    var c = _data?.FindByOwner(_activeContainerOwner);
    if (c == null) return;
    foreach (var t in c.Tracks) RebuildClip(t, c.Fps);
}

private void OnFrameChanged(FrameChangedEvent e)
{
    if (_data == null || _clock == null || !_clock.IsPlaying) return;
    ApplyFrame(e.Frame);
}

private void ApplyFrame(int frame)
{
    if (string.IsNullOrEmpty(_activeContainerOwner)) return;
    var c = _data?.FindByOwner(_activeContainerOwner);
    if (c == null || c.Fps <= 0) return;

    float t = (float)frame / c.Fps;
    foreach (var track in c.Tracks)
    {
        if (!_clips.TryGetValue(track.NodeId, out var clip)) continue;
        var go = _sceneGraph?.GetNode(track.NodeId);
        if (go == null) continue;
        clip.SampleAnimation(go, t);
    }
}

private void RebuildClip(AnimTrackData track, int fps)
{
    var clip = new AnimationClip { legacy = true };
    var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
    var rx = new AnimationCurve(); var ry = new AnimationCurve();
    var rz = new AnimationCurve(); var rw = new AnimationCurve();
    var sx = new AnimationCurve(); var sy = new AnimationCurve(); var sz = new AnimationCurve();

    foreach (var k in track.Keys)
    {
        float t = (float)k.Frame / fps;
        px.AddKey(t, k.Position.x); py.AddKey(t, k.Position.y); pz.AddKey(t, k.Position.z);
        rx.AddKey(t, k.Rotation.x); ry.AddKey(t, k.Rotation.y);
        rz.AddKey(t, k.Rotation.z); rw.AddKey(t, k.Rotation.w);
        sx.AddKey(t, k.Scale.x);    sy.AddKey(t, k.Scale.y);    sz.AddKey(t, k.Scale.z);
    }

    clip.SetCurve("", typeof(Transform), "localPosition.x",   px);
    clip.SetCurve("", typeof(Transform), "localPosition.y",   py);
    clip.SetCurve("", typeof(Transform), "localPosition.z",   pz);
    clip.SetCurve("", typeof(Transform), "m_LocalRotation.x", rx);
    clip.SetCurve("", typeof(Transform), "m_LocalRotation.y", ry);
    clip.SetCurve("", typeof(Transform), "m_LocalRotation.z", rz);
    clip.SetCurve("", typeof(Transform), "m_LocalRotation.w", rw);
    clip.SetCurve("", typeof(Transform), "localScale.x",      sx);
    clip.SetCurve("", typeof(Transform), "localScale.y",      sy);
    clip.SetCurve("", typeof(Transform), "localScale.z",      sz);

    _clips[track.NodeId] = clip;
}
```

Also: after `RequestSave()` in `SetKey`, add `RebuildActiveClips();` if track's owner == active. To keep it simple, just always call `RebuildActiveClips()` after each mutation (cheap enough for our scale):

Find each call to `RequestSave();` in `SetKey`, `DeleteKey`, `SetTotalFrames`, `SetFps`, `CreateContainer`, `RemoveContainer`. After each, add:
```csharp
RebuildActiveClips();
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Run all tests**

Expected: all pass.

- [ ] **Step 4: Commit checkpoint**

---

## Phase 4: AnimationClock

### Task 21: AnimationClock.Configure

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `AnimationClockTests.cs`:

```csharp
[Test]
public void Configure_UpdatesTotalAndFps()
{
    _sut.Configure(90, 60);
    Assert.AreEqual(90, _sut.TotalFrames);
    Assert.AreEqual(60, _sut.Fps);
}

[Test]
public void Configure_ClampsCurrentFrameAndPublishesFrameChanged()
{
    _sut.Seek(100);   // assumes default totalFrames >= 100; default after Configure is 60
    _sut.Configure(120, 30);
    Assert.AreEqual(100, _sut.CurrentFrame);

    int received = -1;
    _bus.Subscribe<FrameChangedEvent>(e => received = e.Frame);
    _sut.Configure(50, 24);

    Assert.AreEqual(50, _sut.CurrentFrame);
    Assert.AreEqual(50, received);
}

[Test]
public void Configure_ClampsMinValues()
{
    _sut.Configure(0, 0);
    Assert.AreEqual(1, _sut.TotalFrames);
    Assert.AreEqual(1, _sut.Fps);
}
```

The previous default `TotalFrames` was 120. With our new default it's 60. Verify by updating the test setup that uses the default:

Find `Seek_ClampsToRange`:
```csharp
[Test]
public void Seek_ClampsToRange()
{
    _sut.Seek(-5);
    Assert.AreEqual(0, _sut.CurrentFrame);

    _sut.Seek(9999);
    Assert.AreEqual(_sut.TotalFrames, _sut.CurrentFrame);
}
```

No change needed (it asserts against `_sut.TotalFrames` dynamically).

- [ ] **Step 2: Run tests, verify FAIL**

Expected: compile error (Configure method not defined).

- [ ] **Step 3: Implement Configure**

In `AnimationClock.cs`:

Update defaults at top:

```csharp
public int  TotalFrames  { get; private set; } = 60;
public int  Fps          { get; private set; } = 24;
```

Add method after `Seek`:

```csharp
public void Configure(int totalFrames, int fps)
{
    TotalFrames = Mathf.Max(1, totalFrames);
    Fps         = Mathf.Max(1, fps);

    if (CurrentFrame > TotalFrames)
    {
        CurrentFrame = TotalFrames;
        _accumulated = TotalFrames;
        _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
    }
}
```

- [ ] **Step 4: Run tests, verify PASS**

- [ ] **Step 5: Commit checkpoint**

---

## Phase 5: AnimationClipboard service

### Task 22: AnimationClipboard

**Files:**
- Create: `Assets/_App/Subsystems/AnimationAuthoring/AnimationClipboard.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClipboardTests.cs`

- [ ] **Step 1: Write tests**

`AnimationClipboardTests.cs`:

```csharp
using NUnit.Framework;

public class AnimationClipboardTests
{
    [Test]
    public void DefaultIsEmpty()
    {
        var c = new AnimationClipboard();
        Assert.IsTrue(c.IsEmpty);
        Assert.IsNull(c.Current);
    }

    [Test]
    public void Set_SetsCurrent_NotEmpty()
    {
        var c = new AnimationClipboard();
        var clip = new FrameClipboard
        {
            OwnerNodeId = "x",
            SourceFrame = 5,
            Entries = new System.Collections.Generic.List<FrameClipboardEntry>
            {
                new FrameClipboardEntry { TrackNodeId = "x" }
            }
        };
        c.Set(clip);
        Assert.IsFalse(c.IsEmpty);
        Assert.AreSame(clip, c.Current);
    }

    [Test]
    public void Set_NullOrEmpty_RemainsEmpty()
    {
        var c = new AnimationClipboard();
        c.Set(null);
        Assert.IsTrue(c.IsEmpty);

        c.Set(new FrameClipboard { Entries = new System.Collections.Generic.List<FrameClipboardEntry>() });
        Assert.IsTrue(c.IsEmpty);
    }

    [Test]
    public void Clear_RemovesCurrent()
    {
        var c = new AnimationClipboard();
        c.Set(new FrameClipboard
        {
            OwnerNodeId = "x",
            Entries = new System.Collections.Generic.List<FrameClipboardEntry>
            {
                new FrameClipboardEntry { TrackNodeId = "x" }
            }
        });

        c.Clear();
        Assert.IsTrue(c.IsEmpty);
    }
}
```

- [ ] **Step 2: Run tests, verify FAIL**

- [ ] **Step 3: Implement AnimationClipboard**

```csharp
public class AnimationClipboard
{
    public FrameClipboard Current { get; private set; }
    public bool           IsEmpty => Current == null || Current.IsEmpty;

    public void Set(FrameClipboard clip)
    {
        if (clip == null || clip.IsEmpty) return;
        Current = clip;
    }

    public void Clear() => Current = null;
}
```

- [ ] **Step 4: Run tests, verify PASS**

- [ ] **Step 5: Commit checkpoint**

---

### Task 23: Register AnimationClipboard in RootLifetimeScope

**Files:**
- Modify: `Assets/_App/Bootstrap/RootLifetimeScope.cs:13`

- [ ] **Step 1: Add registration**

Find:
```csharp
builder.Register<EventBus>(Lifetime.Singleton);
```

Add immediately after:
```csharp
builder.Register<AnimationClipboard>(Lifetime.Singleton);
```

- [ ] **Step 2: Verify compile**

Expected: errors if `RootLifetimeScope` cannot find `AnimationClipboard`. Check `Bootstrap` asmdef references — open `Assets/_App/Bootstrap/Bootstrap.asmdef` (find the actual file via Glob if name differs). If not already referenced, add `Subsystems.AnimationAuthoring`.

- [ ] **Step 3: Find Bootstrap asmdef**

Run in Unity Editor or via Bash:
```bash
find Assets/_App/Bootstrap -name "*.asmdef"
```
Open the file, add `"Subsystems.AnimationAuthoring"` to `references` if not present.

- [ ] **Step 4: Verify compile**

Expected: no errors.

- [ ] **Step 5: Commit checkpoint**

---

## Phase 6: Outliner two-prefab + bones-mode wiring

### Task 24: Refactor OutlinerItem to base class (remove dual icon swap)

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs`

- [ ] **Step 1: Replace OutlinerItem.cs content**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OutlinerItem : MonoBehaviour
{
    [SerializeField] private TMP_Text      _label;
    [SerializeField] private Image         _highlight;
    [SerializeField] private LayoutElement _indentSpacer;
    [SerializeField] private Button        _button;

    public string NodeId { get; private set; }

    public virtual void Bind(SceneNode node, float indentPx, Action onClick)
    {
        NodeId      = node.NodeId;
        _label.text = node.DisplayName;
        if (_indentSpacer != null) _indentSpacer.preferredWidth = indentPx;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetVisualState(SelectionVisual state)
    {
        if (_highlight == null) return;
        _highlight.enabled = state != SelectionVisual.None;
        _highlight.color = state == SelectionVisual.Selected
            ? new Color(1f, 0.95f, 0.15f, 0.35f)
            : Color.clear;
    }

    public void SetLabel(string newName)
    {
        if (_label != null) _label.text = newName;
    }
}
```

(Note: removed `_iconObject`, `_iconRig`, and the rig-detection logic — that lives in `SceneOutlinerView` now.)

- [ ] **Step 2: Verify compile**

Expected: errors in `SceneOutlinerView.cs` (it still expects `OutlinerItem.Bind` to do icon swap). Fixed in Task 25.

Temporarily: in `SceneOutlinerView.AddRowsRecursive`, the call `row.Bind(node, ...)` still works (it just no longer swaps icons). Code compiles.

- [ ] **Step 3: Verify compile actually works**

Test in Unity. Expected: compiles (no API mismatch — Bind signature unchanged, just behavior changed).

- [ ] **Step 4: Commit checkpoint**

---

### Task 25: Create RigOutlinerItem

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/RigOutlinerItem.cs`

- [ ] **Step 1: Create class**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class RigOutlinerItem : OutlinerItem
{
    [SerializeField] private Image _backgroundTint;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Color _bonesOnColor  = new Color(0.31f, 0.51f, 0.94f, 1f);
    [SerializeField] private Color _bonesOffColor = Color.white;

    public void SetBonesMode(bool active)
    {
        var col = active ? _bonesOnColor : _bonesOffColor;
        if (_backgroundTint != null) _backgroundTint.color = col;
        if (_iconImage      != null) _iconImage     .color = col;
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

### Task 26: SceneOutlinerView — two-prefab + bones state dict

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs`

- [ ] **Step 1: Replace SceneOutlinerView.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class SceneOutlinerView : MonoBehaviour
{
    [SerializeField] private Transform       _rowsRoot;
    [SerializeField] private OutlinerItem    _objectRowPrefab;
    [SerializeField] private RigOutlinerItem _rigRowPrefab;
    [SerializeField] private float           _indentPx = 16f;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;

    private readonly Dictionary<string, bool> _bonesActiveByRig = new();

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
    {
        _bus       = bus;
        _graph     = graph;
        _selection = selection;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
        Rebuild();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    }

    private void OnModified(SceneModifiedEvent _)            => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _) => ApplyHighlight();

    private void OnNodeRenamed(NodeRenamedEvent e)
    {
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
            if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
    }

    private void OnBonesVisibilityChanged(BonesVisibilityChangedEvent e)
    {
        _bonesActiveByRig[e.RigNodeId] = e.Visible;
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<RigOutlinerItem>())
            if (row.NodeId == e.RigNodeId) row.SetBonesMode(e.Visible);
    }

    private void Rebuild()
    {
        if (_rowsRoot == null || _objectRowPrefab == null || _rigRowPrefab == null || _graph == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

        var byParent = new Dictionary<string, List<SceneNode>>();
        foreach (var pair in _graph.Nodes)
        {
            var p = GetParentId(pair.Value) ?? "";
            if (!byParent.TryGetValue(p, out var list))
                byParent[p] = list = new List<SceneNode>();
            list.Add(pair.Value);
        }
        foreach (var list in byParent.Values)
            list.Sort((a, b) => string.Compare(
                a.DisplayName ?? "", b.DisplayName ?? "",
                StringComparison.OrdinalIgnoreCase));
        AddRowsRecursive(null, 0, byParent);
        ApplyHighlight();
    }

    private string GetParentId(SceneNode n)
    {
        var p = n.transform.parent;
        if (p == null) return null;
        var pn = p.GetComponent<SceneNode>();
        return pn != null ? pn.NodeId : null;
    }

    private void AddRowsRecursive(string parentId, int depth,
                                   Dictionary<string, List<SceneNode>> byParent)
    {
        if (!byParent.TryGetValue(parentId ?? "", out var children)) return;
        foreach (var node in children)
        {
            var isRig = node.GetComponentInChildren<PromeonProxyRigBuilder>(includeInactive: true) != null;
            OutlinerItem row = isRig
                ? Instantiate(_rigRowPrefab, _rowsRoot)
                : Instantiate(_objectRowPrefab, _rowsRoot);

            row.Bind(node, depth * _indentPx, () => _selection.Select(node.NodeId));

            if (row is RigOutlinerItem rigRow
                && _bonesActiveByRig.TryGetValue(node.NodeId, out var bonesOn))
            {
                rigRow.SetBonesMode(bonesOn);
            }

            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

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
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

### Task 27: SceneInspectorView — publish BonesVisibilityChangedEvent

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs:247-275`

- [ ] **Step 1: Update OnShowBonesToggleChanged**

Find the existing method `OnShowBonesToggleChanged`:

```csharp
private void OnShowBonesToggleChanged(bool value)
{
    PromeonProxyRigBuilder rig = null;
    string                        rigNodeId = null;
    ...
    rig.SetBonesInteractive(value);

    // If bones get hidden while a bone is the active selection, jump the selection up to the rig
    if (!value && _boneTransform != null && !string.IsNullOrEmpty(rigNodeId))
        _selection?.Select(rigNodeId);
}
```

After `rig.SetBonesInteractive(value);` and BEFORE the selection-jump block, add:

```csharp
_bus?.Publish(new BonesVisibilityChangedEvent
{
    RigNodeId = rigNodeId,
    Visible   = value
});
```

The final method should look like:

```csharp
private void OnShowBonesToggleChanged(bool value)
{
    PromeonProxyRigBuilder rig = null;
    string                        rigNodeId = null;

    if (_bound != null)
    {
        rig       = _bound.GetComponentInChildren<PromeonProxyRigBuilder>(true);
        rigNodeId = _bound.NodeId;
    }
    else if (!string.IsNullOrEmpty(_boneRigId))
    {
        var rigNode = _graph.GetNode(_boneRigId);
        if (rigNode != null)
        {
            rig       = rigNode.GetComponentInChildren<PromeonProxyRigBuilder>(true);
            rigNodeId = rigNode.NodeId;
        }
    }

    if (rig == null) return;

    rig.SetBonesInteractive(value);

    _bus?.Publish(new BonesVisibilityChangedEvent
    {
        RigNodeId = rigNodeId,
        Visible   = value
    });

    if (!value && _boneTransform != null && !string.IsNullOrEmpty(rigNodeId))
        _selection?.Select(rigNodeId);
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

- [ ] **Step 3: Commit checkpoint**

---

## Phase 7: AnimatorPanelConfig

### Task 28: AnimatorPanelConfig ScriptableObject

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.cs`

- [ ] **Step 1: Create SO class**

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "VrAnimApp/Animator Panel Config")]
public class AnimatorPanelConfig : ScriptableObject
{
    [Header("Timeline metrics")]
    public float FramePx             = 30f;
    public int   MajorTickInterval   = 5;
    public int   DefaultTotalFrames  = 60;
    public int   DefaultFps          = 24;

    [Header("Key marker colors")]
    public Color KeyColor_Object   = new(0.18f, 0.50f, 0.95f, 1f);
    public Color KeyColor_Rig      = new(0.18f, 0.50f, 0.95f, 1f);
    public Color KeyColor_Bone     = new(0.33f, 0.29f, 0.72f, 1f);
    public Color KeyColor_Selected = new(0.95f, 0.69f, 0.13f, 1f);

    [Header("Track row")]
    public Color TrackRow_Active   = new(0.18f, 0.50f, 0.95f, 0.60f);
    public Color TrackRow_Inactive = Color.clear;

    [Header("Rig outliner row")]
    public Color RigRow_BonesOn    = new(0.31f, 0.51f, 0.94f, 1f);
    public Color RigRow_BonesOff   = Color.white;
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

## Phase 8: Timeline UI components

Most of these components are MonoBehaviours that are configured later in prefab. Code is written here; visuals connected in Phase 11 (manual prefab work).

### Task 29: TimelinePlayheadView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelinePlayheadView.cs`

- [ ] **Step 1: Create class**

```csharp
using TMPro;
using UnityEngine;

public class TimelinePlayheadView : MonoBehaviour
{
    [SerializeField] private RectTransform        _root;
    [SerializeField] private TMP_Text             _frameLabel;
    [SerializeField] private AnimatorPanelConfig  _config;

    public void SetFrame(int frame)
    {
        if (_root == null || _config == null) return;
        _root.anchoredPosition = new Vector2(frame * _config.FramePx, _root.anchoredPosition.y);
        if (_frameLabel != null) _frameLabel.text = frame.ToString();
    }

    public void SetHeight(float height)
    {
        if (_root == null) return;
        var size = _root.sizeDelta;
        size.y = height;
        _root.sizeDelta = size;
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 30: TimelineRulerView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineRulerView.cs`

- [ ] **Step 1: Create class**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimelineRulerView : MonoBehaviour
{
    [SerializeField] private RectTransform       _content;
    [SerializeField] private RectTransform       _tickPrefab;
    [SerializeField] private TMP_Text            _labelPrefab;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _tickPool  = new();
    private readonly List<TMP_Text>      _labelPool = new();

    public void Rebuild(int totalFrames)
    {
        if (_content == null || _tickPrefab == null || _config == null) return;

        DeactivateAll();

        int needed = totalFrames + 1;

        for (int f = 0; f < needed; f++)
        {
            var tick = GetOrCreateTick(f);
            tick.anchoredPosition = new Vector2(f * _config.FramePx, 0f);
            bool major = f % _config.MajorTickInterval == 0;
            var sz = tick.sizeDelta;
            sz.y = major ? 24f : 16f;
            tick.sizeDelta = sz;
            tick.gameObject.SetActive(true);

            if (major)
            {
                var lbl = GetOrCreateLabel(f);
                ((RectTransform)lbl.transform).anchoredPosition = new Vector2(f * _config.FramePx, 0f);
                lbl.text = f.ToString();
                lbl.gameObject.SetActive(true);
            }
        }
    }

    private RectTransform GetOrCreateTick(int idx)
    {
        while (_tickPool.Count <= idx)
        {
            var t = Instantiate(_tickPrefab, _content);
            t.gameObject.SetActive(false);
            _tickPool.Add(t);
        }
        return _tickPool[idx];
    }

    private TMP_Text GetOrCreateLabel(int idx)
    {
        while (_labelPool.Count <= idx)
        {
            var l = Instantiate(_labelPrefab, _content);
            l.gameObject.SetActive(false);
            _labelPool.Add(l);
        }
        return _labelPool[idx];
    }

    private void DeactivateAll()
    {
        foreach (var t in _tickPool)  if (t != null) t.gameObject.SetActive(false);
        foreach (var l in _labelPool) if (l != null) l.gameObject.SetActive(false);
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 31: TimelineLaneView (one lane: keys + grid)

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLaneView.cs`

- [ ] **Step 1: Create class**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimelineLaneView : MonoBehaviour
{
    [SerializeField] private RectTransform       _content;
    [SerializeField] private RectTransform       _keyPrefab;       // diamond
    [SerializeField] private Image               _activeBackground;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _keyPool = new();
    private string _trackNodeId;
    private bool   _isBone;

    public string TrackNodeId => _trackNodeId;

    public void Bind(string trackNodeId, bool isBone)
    {
        _trackNodeId = trackNodeId;
        _isBone      = isBone;
    }

    public void SetActive(bool active)
    {
        if (_activeBackground == null || _config == null) return;
        _activeBackground.color = active ? _config.TrackRow_Active : _config.TrackRow_Inactive;
    }

    public void SetKeys(IReadOnlyList<int> frames, int currentFrame)
    {
        DeactivateAll();
        if (_content == null || _keyPrefab == null || _config == null) return;

        for (int i = 0; i < frames.Count; i++)
        {
            int f   = frames[i];
            var key = GetOrCreateKey(i);
            key.anchoredPosition = new Vector2(f * _config.FramePx, 0f);

            var img = key.GetComponent<Image>();
            bool isSel = f == currentFrame;
            if (img != null)
            {
                img.color = isSel
                    ? _config.KeyColor_Selected
                    : (_isBone ? _config.KeyColor_Bone : _config.KeyColor_Object);
            }
            float size = isSel ? 26f : 22f;
            key.sizeDelta = new Vector2(size, size);

            key.gameObject.SetActive(true);
        }
    }

    private RectTransform GetOrCreateKey(int idx)
    {
        while (_keyPool.Count <= idx)
        {
            var k = Instantiate(_keyPrefab, _content);
            k.gameObject.SetActive(false);
            _keyPool.Add(k);
        }
        return _keyPool[idx];
    }

    private void DeactivateAll()
    {
        foreach (var k in _keyPool) if (k != null) k.gameObject.SetActive(false);
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 32: TimelineLanesView (orchestrator)

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLanesView.cs`

- [ ] **Step 1: Create class**

```csharp
using System.Collections.Generic;
using UnityEngine;

public class TimelineLanesView : MonoBehaviour
{
    [SerializeField] private RectTransform     _root;
    [SerializeField] private TimelineLaneView  _lanePrefab;

    private readonly List<TimelineLaneView> _lanePool = new();

    public IReadOnlyList<TimelineLaneView> Lanes => _lanePool;

    public void Rebuild(IReadOnlyList<(string TrackNodeId, bool IsBone)> tracks)
    {
        foreach (var l in _lanePool) if (l != null) l.gameObject.SetActive(false);

        if (_root == null || _lanePrefab == null) return;

        for (int i = 0; i < tracks.Count; i++)
        {
            var lane = GetOrCreate(i);
            lane.Bind(tracks[i].TrackNodeId, tracks[i].IsBone);
            lane.gameObject.SetActive(true);
        }
    }

    public TimelineLaneView FindLane(string trackNodeId)
    {
        foreach (var l in _lanePool)
            if (l != null && l.gameObject.activeSelf && l.TrackNodeId == trackNodeId) return l;
        return null;
    }

    private TimelineLaneView GetOrCreate(int idx)
    {
        while (_lanePool.Count <= idx)
        {
            var l = Instantiate(_lanePrefab, _root);
            l.gameObject.SetActive(false);
            _lanePool.Add(l);
        }
        return _lanePool[idx];
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 33: TimelineInputHandler (pointer → frame snap)

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineInputHandler.cs`

- [ ] **Step 1: Create class**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

public class TimelineInputHandler : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RectTransform        _content;
    [SerializeField] private AnimatorPanelConfig  _config;

    public System.Action<int> OnFrameRequested;
    public int                MaxFrame { get; set; } = 60;

    public void OnPointerDown(PointerEventData e) => HandleEvent(e);
    public void OnDrag       (PointerEventData e) => HandleEvent(e);

    private void HandleEvent(PointerEventData e)
    {
        if (_content == null || _config == null || OnFrameRequested == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _content, e.position, e.pressEventCamera, out var local)) return;

        int frame = Mathf.RoundToInt(local.x / _config.FramePx);
        frame = Mathf.Clamp(frame, 0, MaxFrame);
        OnFrameRequested(frame);
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 34: TimelineScrollSync

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineScrollSync.cs`

- [ ] **Step 1: Create class**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class TimelineScrollSync : MonoBehaviour
{
    [SerializeField] private ScrollRect _leftTracks;
    [SerializeField] private ScrollRect _rightTimeline;

    private bool _syncing;

    private void OnEnable()
    {
        if (_leftTracks    != null) _leftTracks   .onValueChanged.AddListener(OnLeftChanged);
        if (_rightTimeline != null) _rightTimeline.onValueChanged.AddListener(OnRightChanged);
    }

    private void OnDisable()
    {
        if (_leftTracks    != null) _leftTracks   .onValueChanged.RemoveListener(OnLeftChanged);
        if (_rightTimeline != null) _rightTimeline.onValueChanged.RemoveListener(OnRightChanged);
    }

    private void OnLeftChanged(Vector2 v)
    {
        if (_syncing || _rightTimeline == null) return;
        _syncing = true;
        _rightTimeline.verticalNormalizedPosition = v.y;
        _syncing = false;
    }

    private void OnRightChanged(Vector2 v)
    {
        if (_syncing || _leftTracks == null) return;
        _syncing = true;
        _leftTracks.verticalNormalizedPosition = v.y;
        _syncing = false;
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 35: TrackRowView (left column)

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TrackRowView.cs`

- [ ] **Step 1: Create class**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TrackRowKind { Object, Rig, Bone }

public class TrackRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text             _label;
    [SerializeField] private Image                _icon;
    [SerializeField] private Image                _activeBackground;
    [SerializeField] private GameObject           _hasKeyDot;
    [SerializeField] private LayoutElement        _indent;
    [SerializeField] private AnimatorPanelConfig  _config;

    public string NodeId { get; private set; }

    public void Bind(string nodeId, string displayName, TrackRowKind kind, bool hasKeys, int indentLevel, Action onClick)
    {
        NodeId         = nodeId;
        _label.text    = displayName;
        if (_hasKeyDot != null) _hasKeyDot.SetActive(hasKeys);
        if (_indent    != null) _indent.preferredWidth = indentLevel * 18f;

        if (_icon != null && _config != null)
        {
            _icon.color = kind switch
            {
                TrackRowKind.Bone => _config.KeyColor_Bone,
                _                 => _config.KeyColor_Object
            };
        }

        var btn = GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }
    }

    public void SetActive(bool active)
    {
        if (_activeBackground == null || _config == null) return;
        _activeBackground.color = active ? _config.TrackRow_Active : _config.TrackRow_Inactive;
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 36: AnimatorEmptyStateView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorEmptyStateView.cs`

- [ ] **Step 1: Create class**

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorEmptyStateView : MonoBehaviour
{
    [SerializeField] private GameObject _noSelectionPanel;
    [SerializeField] private GameObject _noContainerPanel;
    [SerializeField] private Button     _addAnimationButton;

    public Action OnAddAnimationClicked;

    public enum State { NoSelection, NoContainer }

    private void Awake()
    {
        if (_addAnimationButton != null)
            _addAnimationButton.onClick.AddListener(() => OnAddAnimationClicked?.Invoke());
    }

    public void Show(State state)
    {
        if (_noSelectionPanel != null) _noSelectionPanel.SetActive(state == State.NoSelection);
        if (_noContainerPanel != null) _noContainerPanel.SetActive(state == State.NoContainer);
    }

    public void HideAll()
    {
        if (_noSelectionPanel != null) _noSelectionPanel.SetActive(false);
        if (_noContainerPanel != null) _noContainerPanel.SetActive(false);
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 37: AnimatorToolbarView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorToolbarView.cs`

- [ ] **Step 1: Create class**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorToolbarView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _currentFrameInput;
    [SerializeField] private TMP_InputField _totalFramesInput;
    [SerializeField] private TMP_InputField _fpsInput;
    [SerializeField] private Button         _setKeyButton;
    [SerializeField] private Button         _deleteKeyButton;
    [SerializeField] private Button         _copyButton;
    [SerializeField] private Button         _pasteButton;
    [SerializeField] private Button         _removeAnimationButton;

    public Action<int> OnCurrentFrameSubmitted;
    public Action<int> OnTotalFramesSubmitted;
    public Action<int> OnFpsSubmitted;
    public Action      OnSetKey;
    public Action      OnDeleteKey;
    public Action      OnCopy;
    public Action      OnPaste;
    public Action      OnRemoveAnimation;

    private void Awake()
    {
        _currentFrameInput?.onEndEdit.AddListener(OnCurrentFrameEdit);
        _totalFramesInput ?.onEndEdit.AddListener(OnTotalFramesEdit);
        _fpsInput         ?.onEndEdit.AddListener(OnFpsEdit);
        _setKeyButton         ?.onClick.AddListener(() => OnSetKey?.Invoke());
        _deleteKeyButton      ?.onClick.AddListener(() => OnDeleteKey?.Invoke());
        _copyButton           ?.onClick.AddListener(() => OnCopy?.Invoke());
        _pasteButton          ?.onClick.AddListener(() => OnPaste?.Invoke());
        _removeAnimationButton?.onClick.AddListener(() => OnRemoveAnimation?.Invoke());
    }

    public void SetCurrentFrame(int frame)
    {
        if (_currentFrameInput != null) _currentFrameInput.SetTextWithoutNotify(frame.ToString());
    }

    public void SetTotalFrames(int frames)
    {
        if (_totalFramesInput != null) _totalFramesInput.SetTextWithoutNotify(frames.ToString());
    }

    public void SetFps(int fps)
    {
        if (_fpsInput != null) _fpsInput.SetTextWithoutNotify(fps.ToString());
    }

    public void SetSetKeyInteractable   (bool v) { if (_setKeyButton    != null) _setKeyButton   .interactable = v; }
    public void SetDeleteKeyInteractable(bool v) { if (_deleteKeyButton != null) _deleteKeyButton.interactable = v; }
    public void SetPasteInteractable    (bool v) { if (_pasteButton     != null) _pasteButton    .interactable = v; }

    private void OnCurrentFrameEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnCurrentFrameSubmitted?.Invoke(v);
    }

    private void OnTotalFramesEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnTotalFramesSubmitted?.Invoke(v);
    }

    private void OnFpsEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnFpsSubmitted?.Invoke(v);
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 38: AnimatorTransportView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorTransportView.cs`

- [ ] **Step 1: Create class**

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorTransportView : MonoBehaviour
{
    [SerializeField] private Button _prevKeyButton;
    [SerializeField] private Button _prevFrameButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _playPauseButton;
    [SerializeField] private Button _endButton;
    [SerializeField] private Button _nextFrameButton;
    [SerializeField] private Button _nextKeyButton;
    [SerializeField] private Image  _playPauseIcon;
    [SerializeField] private Sprite _playSprite;
    [SerializeField] private Sprite _pauseSprite;

    public Action OnPrevKey;
    public Action OnPrevFrame;
    public Action OnStart;
    public Action OnPlayPause;
    public Action OnEnd;
    public Action OnNextFrame;
    public Action OnNextKey;

    private void Awake()
    {
        _prevKeyButton  ?.onClick.AddListener(() => OnPrevKey?.Invoke());
        _prevFrameButton?.onClick.AddListener(() => OnPrevFrame?.Invoke());
        _startButton    ?.onClick.AddListener(() => OnStart?.Invoke());
        _playPauseButton?.onClick.AddListener(() => OnPlayPause?.Invoke());
        _endButton      ?.onClick.AddListener(() => OnEnd?.Invoke());
        _nextFrameButton?.onClick.AddListener(() => OnNextFrame?.Invoke());
        _nextKeyButton  ?.onClick.AddListener(() => OnNextKey?.Invoke());
    }

    public void SetPlaying(bool playing)
    {
        if (_playPauseIcon == null) return;
        _playPauseIcon.sprite = playing ? _pauseSprite : _playSprite;
    }
}
```

- [ ] **Step 2: Verify compile**

- [ ] **Step 3: Commit checkpoint**

---

### Task 39: AnimatorPanelView (root orchestrator)

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs`
- Possibly modify: `Assets/_App/Subsystems/SpatialUi/Subsystems.SpatialUi.asmdef` (verify reference)

- [ ] **Step 1: Verify SpatialUi asmdef references AnimationAuthoring**

Run via Bash to inspect:
```bash
find Assets/_App/Subsystems/SpatialUi -name "*.asmdef" -exec cat {} \;
```

If `Subsystems.AnimationAuthoring` is NOT in the `references` array, add it. Example final structure:

```json
{
    "name": "Subsystems.SpatialUi",
    "references": [
        "_Shared",
        "VContainer",
        "Subsystems.AnimationAuthoring",
        "...existing refs..."
    ],
    "autoReferenced": false
}
```

Save the asmdef file. Wait for Unity to recompile.

- [ ] **Step 2: Create class**

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class AnimatorPanelView : MonoBehaviour
{
    [SerializeField] private AnimatorPanelConfig    _config;
    [SerializeField] private RectTransform          _timelineContent;
    [SerializeField] private AnimatorToolbarView    _toolbar;
    [SerializeField] private AnimatorTransportView  _transport;
    [SerializeField] private AnimatorEmptyStateView _emptyState;
    [SerializeField] private GameObject             _activeStateRoot;
    [SerializeField] private TimelineRulerView      _ruler;
    [SerializeField] private TimelineLanesView      _lanes;
    [SerializeField] private TimelinePlayheadView   _playhead;
    [SerializeField] private TimelineInputHandler   _timelineInput;
    [SerializeField] private RectTransform          _tracksColumnContent;
    [SerializeField] private TrackRowView           _trackRowPrefab;

    private EventBus           _bus;
    private AnimationAuthoring _authoring;
    private AnimationClock     _clock;
    private ISelectionManager  _selection;
    private AnimationClipboard _clipboard;
    private SceneGraph         _graph;

    private string                   _activeOwner;
    private readonly List<TrackRowView> _rowPool = new();

    [Inject]
    public void Construct(EventBus bus, AnimationAuthoring authoring, AnimationClock clock,
                          ISelectionManager selection, AnimationClipboard clipboard, SceneGraph graph)
    {
        _bus       = bus;
        _authoring = authoring;
        _clock     = clock;
        _selection = selection;
        _clipboard = clipboard;
        _graph     = graph;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Subscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Subscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);

        WireToolbar();
        WireTransport();
        WireEmptyState();
        WireTimelineInput();

        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Unsubscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Unsubscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);
    }

    // ===== Wiring =====

    private void WireToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.OnCurrentFrameSubmitted = f => _clock?.Seek(Mathf.Clamp(f, 0, CurrentTotal()));
        _toolbar.OnTotalFramesSubmitted  = f => { if (_activeOwner != null) _authoring.SetTotalFrames(_activeOwner, f); };
        _toolbar.OnFpsSubmitted          = f => { if (_activeOwner != null) _authoring.SetFps(_activeOwner, f); };
        _toolbar.OnSetKey                = OnSetKeyClicked;
        _toolbar.OnDeleteKey             = OnDeleteKeyClicked;
        _toolbar.OnCopy                  = OnCopyClicked;
        _toolbar.OnPaste                 = OnPasteClicked;
        _toolbar.OnRemoveAnimation       = OnRemoveAnimationClicked;
    }

    private void WireTransport()
    {
        if (_transport == null) return;
        _transport.OnPrevFrame  = () => _clock?.Seek(Mathf.Max(0, _clock.CurrentFrame - 1));
        _transport.OnNextFrame  = () => _clock?.Seek(Mathf.Min(CurrentTotal(), _clock.CurrentFrame + 1));
        _transport.OnStart      = () => _clock?.Seek(0);
        _transport.OnEnd        = () => _clock?.Seek(CurrentTotal());
        _transport.OnPlayPause  = OnPlayPauseClicked;
        _transport.OnPrevKey    = OnPrevKeyClicked;
        _transport.OnNextKey    = OnNextKeyClicked;
    }

    private void WireEmptyState()
    {
        if (_emptyState == null) return;
        _emptyState.OnAddAnimationClicked = OnAddAnimationClicked;
    }

    private void WireTimelineInput()
    {
        if (_timelineInput == null) return;
        _timelineInput.OnFrameRequested = frame => _clock?.Seek(frame);
    }

    // ===== Event handlers =====

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_playhead != null) _playhead.SetFrame(e.Frame);
        if (_toolbar  != null) _toolbar.SetCurrentFrame(e.Frame);
        RefreshKeyButtonStates();
        RefreshLaneKeys();
    }

    private void OnPlaybackStateChanged(PlaybackStateChangedEvent e)
    {
        if (_transport != null) _transport.SetPlaying(e.IsPlaying);
    }

    private void OnContainerChanged(AnimationContainerChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;

        switch (e.Change)
        {
            case ContainerChange.Removed:
                Refresh();   // active state → NoContainer
                break;

            case ContainerChange.LengthChanged:
            case ContainerChange.FpsChanged:
                ApplyContainerToClock();
                RebuildTimeline();
                break;

            case ContainerChange.Added:
                Refresh();
                break;
        }
    }

    private void OnKeyframeChanged(AnimationKeyframeChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;
        RefreshLaneKeys();
        RefreshKeyButtonStates();
    }

    // ===== Action handlers =====

    private void OnAddAnimationClicked()
    {
        var owner = AnimationAuthoring.OwnerOf(_selection?.SelectedNodeId);
        if (string.IsNullOrEmpty(owner)) return;
        _authoring.CreateContainer(owner);
    }

    private void OnRemoveAnimationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        _authoring.RemoveContainer(_activeOwner);
    }

    private void OnSetKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var active = _selection?.SelectedNodeId ?? _activeOwner;
        _authoring.SetKeyForFrame(_activeOwner, active, _clock.CurrentFrame);
    }

    private void OnDeleteKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        _authoring.DeleteAllKeysAtFrame(_activeOwner, _clock.CurrentFrame);
    }

    private void OnCopyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var clip = _authoring.CopyFrame(_activeOwner, _clock.CurrentFrame);
        _clipboard.Set(clip);
        RefreshKeyButtonStates();
    }

    private void OnPasteClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner) || _clipboard.IsEmpty) return;
        _authoring.PasteFrame(_activeOwner, _clock.CurrentFrame, _clipboard.Current);
    }

    private void OnPrevKeyClicked()
    {
        var prev = _authoring.NearestKeyBefore(_activeOwner, _clock.CurrentFrame);
        if (prev.HasValue) _clock.Seek(prev.Value);
    }

    private void OnNextKeyClicked()
    {
        var next = _authoring.NearestKeyAfter(_activeOwner, _clock.CurrentFrame);
        if (next.HasValue) _clock.Seek(next.Value);
    }

    private void OnPlayPauseClicked()
    {
        if (_clock == null) return;
        if (_clock.IsPlaying) _clock.Pause();
        else                  _clock.Play();
    }

    // ===== State machine =====

    private void Refresh()
    {
        var selected = _selection?.SelectedNodeId;
        var owner    = AnimationAuthoring.OwnerOf(selected);
        var has      = !string.IsNullOrEmpty(owner) && _authoring.HasContainer(owner);

        if (string.IsNullOrEmpty(selected))
        {
            _activeOwner = null;
            ShowEmpty(AnimatorEmptyStateView.State.NoSelection);
            _authoring.SetActiveContainerOwner(null);
            _clock.Configure(_config.DefaultTotalFrames, _config.DefaultFps);
            return;
        }

        if (!has)
        {
            _activeOwner = null;
            ShowEmpty(AnimatorEmptyStateView.State.NoContainer);
            _authoring.SetActiveContainerOwner(null);
            _clock.Configure(_config.DefaultTotalFrames, _config.DefaultFps);
            return;
        }

        _activeOwner = owner;
        ShowActive();
        _authoring.SetActiveContainerOwner(_activeOwner);
        ApplyContainerToClock();
        RebuildTimeline();
        RefreshKeyButtonStates();
    }

    private void ShowEmpty(AnimatorEmptyStateView.State state)
    {
        if (_activeStateRoot != null) _activeStateRoot.SetActive(false);
        if (_emptyState != null) _emptyState.Show(state);
    }

    private void ShowActive()
    {
        if (_activeStateRoot != null) _activeStateRoot.SetActive(true);
        if (_emptyState != null) _emptyState.HideAll();
    }

    // ===== Timeline rebuild =====

    private void ApplyContainerToClock()
    {
        var c = _authoring.GetContainer(_activeOwner);
        if (c == null) return;
        _clock.Configure(c.TotalFrames, c.Fps);
        if (_toolbar != null)
        {
            _toolbar.SetTotalFrames(c.TotalFrames);
            _toolbar.SetFps(c.Fps);
            _toolbar.SetCurrentFrame(_clock.CurrentFrame);
        }
    }

    private void RebuildTimeline()
    {
        var c = _authoring.GetContainer(_activeOwner);
        if (c == null) return;

        if (_timelineContent != null && _config != null)
        {
            var size = _timelineContent.sizeDelta;
            size.x = (c.TotalFrames + 1) * _config.FramePx;
            _timelineContent.sizeDelta = size;
        }

        if (_timelineInput != null) _timelineInput.MaxFrame = c.TotalFrames;

        _ruler?.Rebuild(c.TotalFrames);
        RebuildTrackRows(c);
        RebuildLanes(c);
        RefreshLaneKeys();

        if (_playhead != null)
        {
            _playhead.SetFrame(_clock.CurrentFrame);
            // height set by external layout; if needed:
            _playhead.SetHeight((c.Tracks.Count + 1) * 52f);
        }
    }

    private void RebuildTrackRows(ActionContainer c)
    {
        if (_tracksColumnContent == null || _trackRowPrefab == null) return;
        foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);

        for (int i = 0; i < c.Tracks.Count; i++)
        {
            var t  = c.Tracks[i];
            var go = _graph?.GetNode(t.NodeId);
            var display = go != null ? go.DisplayName : t.NodeId;
            bool isBone = t.NodeId.StartsWith("bone:");
            var kind    = isBone ? TrackRowKind.Bone : (c.OwnerNodeId == t.NodeId ? TrackRowKind.Rig : TrackRowKind.Object);
            int indent  = isBone ? 1 : 0;

            var row = GetOrCreateRow(i);
            row.gameObject.SetActive(true);
            row.Bind(t.NodeId, display, kind, t.Keys.Count > 0, indent,
                () => _selection.Select(t.NodeId));

            row.SetActive(t.NodeId == _selection.SelectedNodeId);
        }
    }

    private TrackRowView GetOrCreateRow(int idx)
    {
        while (_rowPool.Count <= idx)
        {
            var r = Instantiate(_trackRowPrefab, _tracksColumnContent);
            r.gameObject.SetActive(false);
            _rowPool.Add(r);
        }
        return _rowPool[idx];
    }

    private void RebuildLanes(ActionContainer c)
    {
        if (_lanes == null) return;
        var list = new List<(string, bool)>(c.Tracks.Count);
        foreach (var t in c.Tracks)
            list.Add((t.NodeId, t.NodeId.StartsWith("bone:")));
        _lanes.Rebuild(list);

        foreach (var lane in _lanes.Lanes)
            if (lane != null && lane.gameObject.activeSelf)
                lane.SetActive(lane.TrackNodeId == _selection.SelectedNodeId);
    }

    private void RefreshLaneKeys()
    {
        if (_lanes == null || string.IsNullOrEmpty(_activeOwner)) return;
        var c = _authoring.GetContainer(_activeOwner);
        if (c == null) return;
        foreach (var t in c.Tracks)
        {
            var lane = _lanes.FindLane(t.NodeId);
            if (lane == null) continue;
            var frames = _authoring.GetKeyFrames(t.NodeId);
            lane.SetKeys(frames, _clock.CurrentFrame);
        }
    }

    private void RefreshKeyButtonStates()
    {
        if (_toolbar == null) return;
        bool hasContainer = !string.IsNullOrEmpty(_activeOwner);
        bool hasKey = hasContainer && (_authoring.GetContainer(_activeOwner)?.HasAnyKeyAtFrame(_clock.CurrentFrame) ?? false);
        _toolbar.SetSetKeyInteractable   (hasContainer);
        _toolbar.SetDeleteKeyInteractable(hasKey);
        _toolbar.SetPasteInteractable    (!_clipboard.IsEmpty);
    }

    private int CurrentTotal()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return _config?.DefaultTotalFrames ?? 60;
        return _authoring.GetContainer(_activeOwner)?.TotalFrames ?? _config?.DefaultTotalFrames ?? 60;
    }
}
```

- [ ] **Step 3: Verify compile**

Expected: no errors. (Heavy class — verify carefully.)

- [ ] **Step 4: Commit checkpoint**

---

## Phase 9: Bootstrap cleanup & delete old AnimationModule

### Task 40: Delete AnimationModule.cs

**Files:**
- Delete: `Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs`

- [ ] **Step 1: Delete file**

Run via Bash:
```bash
rm "Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs"
rm "Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs.meta"
```

- [ ] **Step 2: Verify compile**

Expected: errors **only** in prefabs that referenced `AnimationModule` (missing script warnings in Unity Editor). Code compiles. The prefab references are user-side and cleaned up in Phase 10.

- [ ] **Step 3: Commit checkpoint**

---

## Phase 10: Spec coverage gaps — verify before manual

### Task 41: Self-verify Phase 1-9 against acceptance criteria

**Files:**
- Read: `docs/superpowers/specs/2026-05-21-animator-system-design.md`

- [ ] **Step 1: Read acceptance criteria from spec**

Open spec, find `## Acceptance criteria` section.

- [ ] **Step 2: Run all Edit-mode tests**

In Unity: `Window > General > Test Runner` → Edit Mode → Run All.

Expected: all green. The following test classes must pass:
- `AnimationDataTests`
- `ActionContainerTests`
- `AnimationAuthoringTests`
- `AnimationClipboardTests`
- `AnimationClockTests` (with new Configure tests)

If any fails: stop and fix before continuing to Phase 11. **Do not proceed to manual prefab work with broken tests.**

- [ ] **Step 3: Commit checkpoint**

---

## Phase 11: Manual prefab work (user-side)

These tasks require Unity Editor interaction. The agentic worker should **stop** here and hand off to the user. Steps are described as a checklist for the user.

### Task 42: AnimatorPanelConfig SO asset

**User action required.**

- [ ] **Step 1: Create the SO asset**

In Unity Project window, right-click `Assets/_App/Subsystems/SpatialUi/Data/` → Create → `VrAnimApp` → `Animator Panel Config`. Name it `AnimatorPanelConfig.asset`.

- [ ] **Step 2: Leave default values, save**

(Defaults in the SO class are already reasonable: FramePx=30, MajorTickInterval=5, DefaultTotalFrames=60, DefaultFps=24.)

---

### Task 43: AnimatorPanel.prefab restructure

**User action required.**

- [ ] **Step 1: Open existing AnimatorPanel prefab**

Locate the prefab that uses the (now-deleted) `AnimationModule` component. Open in Prefab edit mode.

- [ ] **Step 2: Replace component setup**

Restructure the prefab to match the layout in spec section "Prefab structure":
- Root: `AnimatorPanelView` component
- `EmptyState_NoSelection` GameObject
- `EmptyState_NoContainer` GameObject with `AnimatorEmptyStateView` + "Add animation" Button
- `ActiveState` GameObject containing:
  - `ToolbarTop` with `AnimatorToolbarView` + input fields + buttons
  - `Body > TracksColumn > TracksColumnScroll > TracksColumnContent`
  - `Body > TimelineColumn > TimelineScroll > TimelineContent` with Ruler + LanesContent + Playhead
  - `ToolbarBottom` with `AnimatorTransportView` + transport buttons
- Attach `TimelineRulerView`, `TimelineLanesView`, `TimelinePlayheadView`, `TimelineInputHandler`, `TimelineScrollSync` components on appropriate GameObjects.

- [ ] **Step 3: Create supporting prefabs**

- `TrackRow.prefab` — a single track row UI matching `TrackRowView` SerializeFields
- `TimelineLane.prefab` — single lane UI matching `TimelineLaneView` SerializeFields
- `TimelineTick.prefab` — simple Image for ruler tick
- `TimelineTickLabel.prefab` — TMP_Text for ruler label
- `TimelineKeyDiamond.prefab` — Image rotated 45°, square

- [ ] **Step 4: Wire SerializeField references on AnimatorPanelView**

Drag references into all `[SerializeField]` slots. Verify no missing refs in the inspector.

- [ ] **Step 5: Smoke-test in Unity**

- Play Bootstrap scene
- Open a saved scene
- Select an object → expect Empty/NoContainer state with "Add animation"
- Click Add animation → expect Active state appears
- Drag playhead → expect snap to integer frames
- Click [+ key] → expect key diamond appears
- Total frames input 30 → keys beyond 30 should disappear

---

### Task 44: OutlinerObject-Rig_ItemUI.prefab — RigOutlinerItem

**User action required.**

- [ ] **Step 1: Open prefab**

`Assets/_App/Subsystems/SpatialUi/Prefabs/Items/OutlinerObject-Rig_ItemUI.prefab`

- [ ] **Step 2: Remove old OutlinerItem component, add RigOutlinerItem**

If the prefab has `OutlinerItem` at root, replace with `RigOutlinerItem` (since `RigOutlinerItem : OutlinerItem`, the SerializeFields of the base are still exposed).

Set:
- `_backgroundTint` → background Image
- `_iconImage` → rig icon Image
- `_bonesOnColor` → blue
- `_bonesOffColor` → white

Set existing base-class fields (`_label`, `_highlight`, `_indentSpacer`, `_button`).

- [ ] **Step 3: Remove _iconObject / _iconRig refs (no longer used)**

These fields were removed from `OutlinerItem.cs` in Task 24. The prefab will show missing references — clean them up.

---

### Task 45: OutlinerObject-Object_ItemUI.prefab — clean up

**User action required.**

- [ ] **Step 1: Open prefab**

`Assets/_App/Subsystems/SpatialUi/Prefabs/Items/OutlinerObject-Object_ItemUI.prefab`

- [ ] **Step 2: Verify OutlinerItem component**

The base `OutlinerItem` component should remain. Remove `_iconObject`/`_iconRig` references (these fields no longer exist on the component). Set the static object-icon directly on the icon child.

---

### Task 46: SceneOutlinerView serialized refs

**User action required.**

- [ ] **Step 1: Find prefab/scene with SceneOutlinerView**

Likely in `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/`.

- [ ] **Step 2: Wire two prefab refs**

- `_objectRowPrefab` → `OutlinerObject-Object_ItemUI.prefab`
- `_rigRowPrefab`    → `OutlinerObject-Rig_ItemUI.prefab`

---

### Task 47: Final smoke test

**User action required.**

- [ ] **Step 1: Bootstrap scene → MainMenu → open scene with rig**

- [ ] **Step 2: Select rig in outliner → row icon should be object-rig style**

- [ ] **Step 3: Toggle Show Bones in inspector**

Expected: rig row becomes blue.

- [ ] **Step 4: Open Animator panel**

Expected: empty state "no animation container yet" + Add button.

- [ ] **Step 5: Add animation → set keys → scrub timeline**

Expected: keys appear under playhead at integer frames.

- [ ] **Step 6: Change Total frames to 30, then back to 60**

Expected: keys > 30 dropped on the first change; not re-added on the second.

- [ ] **Step 7: Copy frame → seek → Paste frame**

Expected: keys reappear at new frame.

- [ ] **Step 8: Remove animation**

Expected: panel returns to empty state.

- [ ] **Step 9: Done — final commit checkpoint**

---

## Test File Map

| Test class | File | Tests |
|---|---|---|
| `AnimationDataTests`     | `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs`    | UpsertKey x3, RemoveKey, HasKey, TrimKeysAfter, FindByOwner, CreateContainer x2, RemoveContainer, JsonRoundTrip_v2 |
| `ActionContainerTests`   | `Assets/_App/Subsystems/AnimationAuthoring/Tests/ActionContainerTests.cs`  | Defaults, GetOrCreate x2, FindTrack, HasAnyKeyAtFrame, TruncateToTotalFrames x2, ExistingTrackNodeIds |
| `AnimationAuthoringTests`| `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs`| OwnerOf x5, HasContainer, CreateContainer x2, RemoveContainer, GetContainer, SetTotalFrames x4, SetFps x2, SetKey x4, DeleteKey x2, SetKeyForFrame x4, DeleteAllKeysAtFrame x2, CopyFrame x2, PasteFrame x3, NearestKey x5 |
| `AnimationClipboardTests`| `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClipboardTests.cs`| DefaultIsEmpty, Set, SetNull, Clear |
| `AnimationClockTests`    | `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs`   | (existing 7) + Configure x3 |

Total: ~55 Edit-mode tests.

---

## Spec coverage table

| Spec section | Implementing task(s) |
|---|---|
| Data Model (SceneAnimationData v2) | T9 |
| ActionContainer | T8 |
| AnimKeyData / AnimTrackData | T6, T7 |
| FrameClipboard / Entry | T16 |
| Migration (v1 discard) | T18 |
| AnimationAuthoring.OwnerOf | T10 |
| Container CRUD | T11 |
| SetTotalFrames / SetFps | T12 |
| SetKey / DeleteKey / HasKey / GetKeyFrames | T13 |
| SetKeyForFrame | T14 |
| DeleteAllKeysAtFrame | T15 |
| CopyFrame / PasteFrame | T16 |
| NearestKeyBefore / After | T17 |
| Debounced SaveAsync | T19 |
| ApplyFrame / RebuildClip per-container FPS | T20 |
| AnimationClock.Configure | T21 |
| AnimationClipboard service | T22 |
| RootLifetimeScope register | T23 |
| OutlinerItem refactor | T24 |
| RigOutlinerItem | T25 |
| SceneOutlinerView two-prefab + bones state | T26 |
| SceneInspectorView publish BonesVisibilityChangedEvent | T27 |
| AnimatorPanelConfig SO | T28 |
| TimelinePlayheadView | T29 |
| TimelineRulerView | T30 |
| TimelineLaneView | T31 |
| TimelineLanesView | T32 |
| TimelineInputHandler (snap) | T33 |
| TimelineScrollSync | T34 |
| TrackRowView | T35 |
| AnimatorEmptyStateView | T36 |
| AnimatorToolbarView | T37 |
| AnimatorTransportView | T38 |
| AnimatorPanelView (state machine) | T39 |
| Delete AnimationModule | T40 |
| Test gate | T41 |
| Manual prefabs (user) | T42-46 |
| Smoke test (user) | T47 |
| Events: AnimationContainerChangedEvent | T3 |
| Events: BonesVisibilityChangedEvent | T4 |
| Events: AnimationKeyframeChangedEvent extended | T5 |
| Enums | T1, T2 |
