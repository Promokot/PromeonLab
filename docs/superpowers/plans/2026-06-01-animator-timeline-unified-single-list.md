# Animator Timeline — Unified Single-List Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **PROJECT RULE — git:** NEVER run any `git` command. The user commits manually. "Checkpoint" = pause for review, no commit.
>
> **PROJECT RULE — Unity:** C# compiles in the Editor. After editing `.cs`: `mcp__unityMCP__refresh_unity` (compile=request, wait_for_ready=true) → `mcp__unityMCP__read_console` (types=["error"]), confirm no `CS####`. Acceptable noise: `MCP-FOR-UNITY: Client handler exited`, and (after closing a prefab stage) `SerializedObjectNotCreatableException` / `MissingReferenceException`. Run tests with `mcp__unityMCP__run_tests` + `mcp__unityMCP__get_test_job` (EditMode). Prefab edits via `mcp__unityMCP__manage_prefabs` / `manage_components`; verify by reading the prefab back, then visually in-headset.

**Goal:** Replace the animator's two-column (names + timeline) layout with one scroll area whose rows each hold the track name and the keyframe strip, scrolling on both axes, for the single-object case.

**Architecture:** A new `TimelineRow` (name segment + key strip) replaces `TrackRow`+`TimelineLane`. `AnimatorPanelModule.prefab` collapses to one both-axes `ScrollRect` over one content (ruler + rows VLG + playhead); `TimelineScrollSync` and the names column are removed. `FramePx` stays a fixed config step; `TrackNameWidth` (config) is the left offset shared by ruler, keys, playhead, and scrub.

**Tech Stack:** Unity 6000.3.7f1, uGUI World-Space Canvas + XRI `TrackedDeviceGraphicRaycaster`, VContainer, NUnit EditMode, MCP-for-Unity.

**Spec:** `docs/superpowers/specs/2026-06-01-animator-timeline-unified-single-list-design.md`

**SCOPE:** single object only (one track). Rigs stay as-is (no rows; must not crash). Do not try to fix rig bone-tracks.

---

## File Structure

| File / asset | Responsibility | Change |
|---|---|---|
| `Assets/_App/Scripts/SpatialUi/AnimatorPanelConfig.cs` | timeline config | add `TrackNameWidth = 100f` |
| `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` | authoring API | add `EnsureTrack(owner, trackNodeId)` |
| `Assets/_App/Scripts/SpatialUi/Elements/TimelineRow.cs` | one row | NEW — name + key strip (merges TrackRow + TimelineLane) |
| `Assets/_App/Content/Prefabs/UI/Elements/TimelineRow.prefab` | one row prefab | NEW |
| `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs` | panel | rows instead of lanes+trackrows; offsets; pre-create object track on Add |
| `Assets/_App/Scripts/SpatialUi/Behaviors/TimelineScrubInput.cs` | scrub | subtract `TrackNameWidth` before frame conversion |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | panel prefab | one both-axes ScrollRect; rows = TimelineRow; remove TracksColumn + TimelineScrollSync; offsets |
| retired: `TrackRow.cs`/`.prefab`, `TimelineLane.cs`/`.prefab`, `AnimatorSubLanes.cs`, `TimelineScrollSync.cs` | — | delete after GUID sweep (Task 7) |

---

## Task 1: Config — `TrackNameWidth`

**Files:** Modify `Assets/_App/Scripts/SpatialUi/AnimatorPanelConfig.cs`

- [ ] **Step 1: Add the field**

Under `[Header("Timeline metrics")]`, after `public float FramePx = 30f;`, add:
```csharp
    public float TrackNameWidth     = 100f;
```

- [ ] **Step 2: Compile**

`refresh_unity`; `read_console` types=["error"] → no `CS####`.

- [ ] **Step 3: Checkpoint** (no git).

---

## Task 2: `AnimationAuthoring.EnsureTrack` + pre-create object track on Add

**Files:**
- Modify `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Modify `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs` (`OnAddAnimationClicked`)
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringEnsureTrackTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/Animation/AnimationAuthoringEnsureTrackTests.cs`:
```csharp
using NUnit.Framework;

public class AnimationAuthoringEnsureTrackTests
{
    [Test]
    public void EnsureTrack_AddsEmptyTrack_ToOwnerContainer()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        authoring.CreateContainer("obj1", 100, 24);

        authoring.EnsureTrack("obj1", "obj1");

        var c = authoring.GetContainer("obj1");
        Assert.IsNotNull(c.FindTrack("obj1"), "track should exist");
        Assert.AreEqual(0, c.FindTrack("obj1").Keys.Count, "track should have no keys");
    }

    [Test]
    public void EnsureTrack_NoContainer_DoesNotThrow()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        Assert.DoesNotThrow(() => authoring.EnsureTrack("missing", "missing"));
    }
}
```

- [ ] **Step 2: Run → expect FAIL**

`run_tests` EditMode `AnimationAuthoringEnsureTrackTests` → FAIL (no `EnsureTrack`).

- [ ] **Step 3: Implement `EnsureTrack`**

In `AnimationAuthoring.cs`, add after `CreateContainer(...)` overloads (near the `FinishCreate` helper):
```csharp
    public void EnsureTrack(string ownerNodeId, string trackNodeId)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        if (c.FindTrack(trackNodeId) != null) return;
        c.GetOrCreateTrack(trackNodeId);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.TracksChanged
        });
        RequestSave();
        RebuildActiveClips();
    }
```

- [ ] **Step 4: Add the `TracksChanged` enum value**

In `Assets/_App/Scripts/Animation/ContainerChange.cs`, add `TracksChanged` to the enum (append, do not reorder existing values):
```csharp
public enum ContainerChange { Added, Removed, LengthChanged, FpsChanged, TracksChanged }
```

- [ ] **Step 5: Handle `TracksChanged` in the panel**

In `AnimatorPanel.OnContainerChanged`, the `switch (e.Change)` (the part guarded by `e.OwnerNodeId != _activeOwner`) — add a case so a new track rebuilds the rows:
```csharp
            case ContainerChange.TracksChanged:
                RebuildTimeline();
                break;
```
(Place it alongside the existing `Removed`/`LengthChanged`/`FpsChanged` cases.)

- [ ] **Step 6: Pre-create the object track on Add (non-rig)**

Replace `AnimatorPanel.OnAddAnimationClicked` body with:
```csharp
    private void OnAddAnimationClicked()
    {
        if (_ctx.Authoring == null) return;
        var selected = _ctx.Selection?.SelectedNodeId;
        var owner = AnimationAuthoring.OwnerOf(selected);
        if (string.IsNullOrEmpty(owner)) return;
        _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);

        // Single object (owner == selected, not a bone, not a rig): show its row immediately.
        bool isBone = selected != null && selected.StartsWith("bone:");
        var ownerGo = _ctx.Graph?.GetNode(owner);
        bool isRig  = ownerGo != null && ownerGo.GetComponentInChildren<ProxyRigRuntime>() != null;
        if (!isBone && !isRig && owner == selected)
            _ctx.Authoring.EnsureTrack(owner, owner);
    }
```
(`ProxyRigRuntime` lives in `Assets/_App/Scripts/RigBuilder/` and is in the same `_App.Runtime` assembly — same is-rig check `InspectorPanel`/`OutlinerPanel` use.)

- [ ] **Step 7: Compile + run tests → PASS**

`refresh_unity`; `read_console` (no `CS####`). `run_tests` EditMode `AnimationAuthoringEnsureTrackTests` → both PASS. Also confirm `AnimationAuthoringCreateContainerTests` still PASS.

- [ ] **Step 8: Checkpoint** (no git).

---

## Task 3: `TimelineRow` component

**Files:** Create `Assets/_App/Scripts/SpatialUi/Elements/TimelineRow.cs`

This merges `TrackRow` (name/select) and `TimelineLane` (keys). It positions keys at `frame * _config.FramePx` inside `_keyStrip`, and sizes the name segment + key-strip offset from `_config.TrackNameWidth`.

- [ ] **Step 1: Write the component**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimelineRow : MonoBehaviour
{
    [SerializeField] private RectTransform       _nameSegment;
    [SerializeField] private TMP_Text            _nameLabel;
    [SerializeField] private Image               _activeBackground;
    [SerializeField] private RectTransform       _keyStrip;   // anchored left; keys live here
    [SerializeField] private RectTransform       _keyPrefab;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _keyPool = new();
    private string _trackNodeId;
    private bool   _isBone;

    public string TrackNodeId => _trackNodeId;

    public void Bind(string trackNodeId, string displayName, bool isBone, Action onClick)
    {
        _trackNodeId = trackNodeId;
        _isBone      = isBone;

        if (_nameLabel != null)
        {
            _nameLabel.text         = displayName;
            _nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_config != null)
        {
            if (_nameSegment != null)
            {
                var sd = _nameSegment.sizeDelta; sd.x = _config.TrackNameWidth; _nameSegment.sizeDelta = sd;
            }
            if (_keyStrip != null)
            {
                var om = _keyStrip.offsetMin; om.x = _config.TrackNameWidth; _keyStrip.offsetMin = om;
            }
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

    public void SetKeys(IReadOnlyList<int> frames, int currentFrame)
    {
        DeactivateAll();
        if (_keyStrip == null || _keyPrefab == null || _config == null) return;

        for (int i = 0; i < frames.Count; i++)
        {
            int f   = frames[i];
            var key = GetOrCreateKey(i);
            key.anchoredPosition = new Vector2(f * _config.FramePx, 0f);

            bool isSel = f == currentFrame;
            var img = key.GetComponent<Image>();
            if (img != null)
                img.color = isSel
                    ? _config.KeyColor_Selected
                    : (_isBone ? _config.KeyColor_Bone : _config.KeyColor_Object);

            float size = isSel ? 26f : 22f;
            key.sizeDelta = new Vector2(size, size);
            key.gameObject.SetActive(true);
        }
    }

    private RectTransform GetOrCreateKey(int idx)
    {
        while (_keyPool.Count <= idx)
        {
            var k = Instantiate(_keyPrefab, _keyStrip);
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

- [ ] **Step 2: Compile**

`refresh_unity`; `read_console` (no `CS####`). (`TextOverflowModes.Ellipsis` is the TMP enum; confirm no typo.)

- [ ] **Step 3: Checkpoint** (no git).

---

## Task 4: `TimelineRow.prefab`

**Files:** Create `Assets/_App/Content/Prefabs/UI/Elements/TimelineRow.prefab`

Build a row that is full-width (it will stretch to the content width via the rows VLG), height 52, with a left name segment and a key strip filling the rest.

- [ ] **Step 1: Inspect the donor prefabs**

Read the existing `TrackRow.prefab` and `TimelineLane.prefab` (`get_hierarchy` + component reads) to reuse their pieces:
- From `TrackRow.prefab`: the name `TMP_Text`, the active-background `Image`, the click `Button`.
- From `TimelineLane.prefab`: the keys container (its `_content`) and the key prefab reference (`TimelineKeyDiamond.prefab`).

- [ ] **Step 2: Build the prefab structure**

Create `TimelineRow.prefab` with this hierarchy (layer = UI / 5, all RectTransforms):
```
TimelineRow            (RectTransform; LayoutElement minHeight=52 preferredHeight=52 flexibleHeight=0; Image activeBackground; Button [whole-row click]; TimelineRow component)
├─ NameSegment         (RectTransform: anchorMin=(0,0) anchorMax=(0,1) pivot=(0,0.5) anchoredPosition=(0,0) sizeDelta.x=100; RectMask2D)
│   └─ NameLabel       (TMP_Text, overflowMode=Ellipsis, stretched to NameSegment)
└─ KeyStrip            (RectTransform: anchorMin=(0,0) anchorMax=(1,1) pivot=(0,0.5) offsetMin=(100,0) offsetMax=(0,0))
```
Wire the `TimelineRow` component fields: `_nameSegment`=NameSegment, `_nameLabel`=NameLabel, `_activeBackground`=the row Image, `_keyStrip`=KeyStrip, `_keyPrefab`=`Assets/_App/Content/Prefabs/UI/Elements/TimelineKeyDiamond.prefab`, `_config`=the `AnimatorPanelConfig` asset used elsewhere (find it: `manage_asset` search `t:AnimatorPanelConfig`).

Practical MCP route: `create_from_gameobject` is awkward for nested UI; instead duplicate `TimelineLane.prefab` as a base (it already has the key strip + key pool + config), then inside the prefab stage: add the `TimelineRow` component, add a `NameSegment`+`NameLabel` (copy the label from `TrackRow.prefab`), repoint the keys container as `_keyStrip`, set anchors/offsets above, and remove the old `TimelineLane` component. Save the stage.

- [ ] **Step 3: Structural test**

Add to `Assets/_App/Tests/SpatialUi/AnimatorPanelModulePrefabTests.cs` (created earlier):
```csharp
    [Test]
    public void TimelineRowPrefab_HasNameAndKeyStrip()
    {
        var row = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_App/Content/Prefabs/UI/Elements/TimelineRow.prefab");
        Assert.IsNotNull(row, "TimelineRow.prefab missing");
        Assert.IsNotNull(row.GetComponent<TimelineRow>(), "TimelineRow component missing");
        Assert.IsNotNull(row.transform.Find("NameSegment"), "NameSegment missing");
        Assert.IsNotNull(row.transform.Find("KeyStrip"), "KeyStrip missing");
    }
```

- [ ] **Step 4: Run → PASS**

`run_tests` EditMode `AnimatorPanelModulePrefabTests` → all PASS. `read_console` (no errors).

- [ ] **Step 5: Checkpoint** (no git).

---

## Task 5: `AnimatorPanel` rewire (rows, offsets, content width) + scrub offset

**Files:**
- Modify `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`
- Modify `Assets/_App/Scripts/SpatialUi/Behaviors/TimelineScrubInput.cs`

- [ ] **Step 1: Swap the lane field + rebuild method**

In `AnimatorPanel.cs`: replace the `[SerializeField] private AnimatorSubLanes _lanes;` field with `[SerializeField] private TimelineRow _rowPrefab;` and a content ref `[SerializeField] private RectTransform _rowsContent;`. Remove `_tracksColumnContent` and `_trackRowPrefab` and `_rowPool`-of-`TrackRow`; add a row pool:
```csharp
    private readonly List<TimelineRow> _rowPool = new();
```
Replace `RebuildTrackRows` + `RebuildLanes` + `RefreshLaneKeys` with one `RebuildRows`:
```csharp
    private void RebuildRows(ActionContainer c)
    {
        foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);
        if (_rowsContent == null || _rowPrefab == null) return;

        for (int i = 0; i < c.Tracks.Count; i++)
        {
            var t       = c.Tracks[i];
            var go      = _ctx.Graph?.GetNode(t.NodeId);
            var display = go != null ? go.DisplayName : t.NodeId;
            bool isBone = t.NodeId.StartsWith("bone:");

            var row = GetOrCreateRow(i);
            row.gameObject.SetActive(true);
            row.Bind(t.NodeId, display, isBone, () => _ctx.Selection.Select(t.NodeId));
            row.SetActive(t.NodeId == _ctx.Selection.SelectedNodeId);
            row.SetKeys(_ctx.Authoring.GetKeyFrames(t.NodeId), _ctx.Clock.CurrentFrame);
        }
    }

    private TimelineRow GetOrCreateRow(int idx)
    {
        while (_rowPool.Count <= idx)
        {
            var r = Instantiate(_rowPrefab, _rowsContent);
            r.gameObject.SetActive(false);
            _rowPool.Add(r);
        }
        return _rowPool[idx];
    }
```

- [ ] **Step 2: Update `RebuildTimeline` for offset + content width**

Replace `RebuildTimeline` with:
```csharp
    private void RebuildTimeline()
    {
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;

        float off = _config != null ? _config.TrackNameWidth : 0f;
        float px  = _config != null ? _config.FramePx : 30f;

        if (_timelineContent != null)
        {
            var size = _timelineContent.sizeDelta;
            size.x = off + (c.TotalFrames + 1) * px;
            _timelineContent.sizeDelta = size;
        }

        if (_timelineInput != null) { _timelineInput.MaxFrame = c.TotalFrames; _timelineInput.LeftOffset = off; }

        _ruler?.Rebuild(c.TotalFrames);          // ruler tick content is offset by `off` in the prefab (Task 6)
        RebuildRows(c);

        if (_playhead != null) _playhead.SetFrame(_ctx.Clock.CurrentFrame);
    }
```
Also update `OnFrameChanged`/`RefreshLaneKeys` callers: replace the old `RefreshLaneKeys()` calls with a per-row key refresh:
```csharp
    private void RefreshRowKeys()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;
        foreach (var row in _rowPool)
        {
            if (row == null || !row.gameObject.activeSelf) continue;
            row.SetKeys(_ctx.Authoring.GetKeyFrames(row.TrackNodeId), _ctx.Clock.CurrentFrame);
        }
    }
```
Find every former `RefreshLaneKeys()` call (in `OnFrameChanged`, `OnKeyframeChanged`, `RebuildTimeline`) and call `RefreshRowKeys()` instead. Remove the now-dead `_lanes`/`RebuildLanes`/`RebuildTrackRows`/`RefreshLaneKeys`/`GetOrCreateRow`(old TrackRow) members.

- [ ] **Step 3: Scrub offset**

In `TimelineScrubInput.cs`: add `public float LeftOffset { get; set; }` and subtract it before converting to a frame. Its frame calc (`round(localX / FramePx)`) becomes `round((localX - LeftOffset) / FramePx)`, clamped to `[0, MaxFrame]`. Read the file first; apply the minimal change to the x→frame line.

- [ ] **Step 4: Compile**

`refresh_unity`; `read_console` (no `CS####`). Expect compile errors pointing at any leftover references to removed members — fix them (remove dead wiring).

- [ ] **Step 5: Checkpoint** (no git).

---

## Task 6: `AnimatorPanelModule.prefab` restructure

**Files:** Modify `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab`

Goal hierarchy under `ActiveStateRoot`:
```
Body
└─ TimelineScroll (ScrollRect: horizontal=true, vertical=true) → Viewport(mask) → TimelineContent
     ├─ Ruler/Content        (offset left by TrackNameWidth so tick 0 sits at x=TrackNameWidth)
     ├─ RowsContent          (VerticalLayoutGroup + ContentSizeFitter vertical=PreferredSize, horizontal=Unconstrained) ← rows go here
     └─ Playhead             (overlay line at x = TrackNameWidth + currentFrame*FramePx)
```

- [ ] **Step 1: Remove the names column + sync**

Open the prefab stage. Delete `ActiveStateRoot/Body/TracksColumn` entirely. Remove the `TimelineScrollSync` component wherever it sits (search the hierarchy). Make `Body/TimelineColumn` (or its ScrollRect) fill the full Body width now that the left column is gone.

- [ ] **Step 2: Enable both-axes scroll**

On the timeline `ScrollRect` (`…/TimelineScroll`): set `horizontal = true`, `vertical = true`.

- [ ] **Step 3: Rows content**

Rename/repurpose the old `LanesContent` as `RowsContent` (it already has a `VerticalLayoutGroup`). Ensure it has a `ContentSizeFitter` with `verticalFit = PreferredSize`, `horizontalFit = Unconstrained`. Remove the `AnimatorSubLanes` and old `TimelineScrubInput` components from it if present; the new scrub input target is set in Step 5.

- [ ] **Step 4: Ruler offset**

Offset the ruler's tick area so frame 0 aligns with the key strips: set `…/TimelineContent/Ruler/Content` `offsetMin.x = TrackNameWidth` (100) (anchorMin=(0,0) anchorMax=(1,1)), OR keep its left anchor and set `anchoredPosition.x = 100`. The key invariant: a tick for frame f and a key for frame f share the same screen X, i.e. ruler-content-left == key-strip-left == TimelineContent x=100.

- [ ] **Step 5: Re-wire `AnimatorPanel` serialized fields**

On the `AnimatorPanel` component (root of the module): set the new fields from Task 5 — `_rowPrefab` = `TimelineRow.prefab`, `_rowsContent` = `RowsContent`, `_timelineContent` = `TimelineContent`, `_timelineInput` = the `TimelineScrubInput` (move it onto `TimelineContent` or `RowsContent`), `_ruler`/`_playhead`/`_toolbar`/`_transport`/`_emptyState`/`_activeStateRoot`/`_config` unchanged. Clear the removed fields (`_lanes`, `_tracksColumnContent`, `_trackRowPrefab`). Verify by reading the component back.

- [ ] **Step 6: Playhead offset**

Ensure the playhead's X uses `TrackNameWidth + frame*FramePx`. If `AnimatorSubPlayhead.SetFrame` sets `x = frame*FramePx`, give it the same `TrackNameWidth` offset: either add a `LeftOffset` it adds, or place the playhead's parent at x=100. Read `AnimatorSubPlayhead.cs`; apply the smallest change consistent with the ruler/keys (offset = 100).

- [ ] **Step 7: Save + verify**

Save the stage; `refresh_unity`; `read_console` (no `CS####`). Re-run `AnimatorPanelModulePrefabTests`. Update that test's "exactly one ScrollRect / no TimelineScrollSync" assertion (add it now):
```csharp
    [Test]
    public void AnimatorModule_HasNoTimelineScrollSync_AndSingleScroll()
    {
        var root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab");
        Assert.IsNull(root.GetComponentInChildren<TimelineScrollSync>(true), "TimelineScrollSync must be gone");
        var active = root.transform.Find("ActiveStateRoot");
        Assert.AreEqual(1, active.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Length, "exactly one ScrollRect");
    }
```
(If `TimelineScrollSync` is deleted in Task 7 before this compiles, gate this assertion on the type still existing, or run it before Task 7. Simplest: keep this test, and delete `TimelineScrollSync.cs` LAST in Task 7 — the test references the type, so instead assert by component-name string if the type is gone. Use: `Assert.IsFalse(root.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m && m.GetType().Name == "TimelineScrollSync"));` with `using System.Linq;` to avoid a hard type reference.)

- [ ] **Step 8: Checkpoint** (no git).

---

## Task 7: Delete retired files (GUID-checked)

**Files:** delete after sweep: `TrackRow.cs`/`.prefab`, `TimelineLane.cs`/`.prefab`, `AnimatorSubLanes.cs`, `TimelineScrollSync.cs` (+ their `.meta`).

- [ ] **Step 1: GUID sweep**

For each file, read its `.meta` guid and `Grep` the whole `Assets/` for that guid in `*.prefab`/`*.unity`/`*.asset`. Also `Grep` `Assets/_App/Scripts` for the type names (`TrackRow`, `TimelineLane`, `AnimatorSubLanes`, `TimelineScrollSync`). Expected after Tasks 5-6: zero references (the module prefab now uses `TimelineRow`; AnimatorPanel no longer names them).

- [ ] **Step 2: Delete only the unreferenced ones**

Delete each `.cs`/`.prefab` whose guid + type name have zero remaining references, plus its `.meta`. If anything still references a file, STOP and report — do not delete it.

- [ ] **Step 3: Compile + full EditMode run**

`refresh_unity` (compile=request, scope=all); `read_console` (no `CS####`). `run_tests` EditMode `_App.Tests` → the animator tests PASS (pre-existing `PathProviderTests`/`RingRotateStrategyTests` failures are unrelated and expected).

- [ ] **Step 4: Checkpoint** (no git).

---

## Task 8: In-headset verification (user)

Hand to the user; cannot be automated:
- [ ] Select an object → Add → its empty row appears immediately; ruler offset by the name width (100).
- [ ] Set keys at frames N/M → diamonds centered on ticks N/M in the row's key strip.
- [ ] Horizontal scroll pans to later frames and back; vertical scroll works (mainly relevant once multiple rows exist).
- [ ] A long object name truncates with "…" in the name segment; never overflows the key strip.
- [ ] Scrubbing the timeline seeks to the correct frame (offset-corrected).
- [ ] Select a rig → no crash (no rows, as before).

If keys and ticks disagree horizontally, the offset invariant broke — recheck Task 6 Step 4 (ruler content left) vs the row's `KeyStrip.offsetMin.x` (both must equal `TrackNameWidth`).

---

## Self-Review notes
- **Spec coverage:** TrackNameWidth(100)+ellipsis → T1,T3,T4; fixed FramePx + both-axes scroll → T5,T6; pre-create object track on Add → T2; TimelineRow merge → T3,T4; one-scroll/no-sync restructure → T6; deletions → T7; rig no-crash + headset → T8.
- **Type consistency:** `EnsureTrack(owner,trackNodeId)`, `ContainerChange.TracksChanged`, `TimelineRow.Bind/SetKeys/SetActive/TrackNodeId`, `_rowPrefab`/`_rowsContent`/`_timelineContent`/`_timelineInput`, `TimelineScrubInput.LeftOffset`, `_config.TrackNameWidth`/`FramePx` — consistent across tasks.
- **Placeholders:** prefab-specific child names/anchors are given concretely; donor-prefab reuse is an explicit procedure, not "TBD".
