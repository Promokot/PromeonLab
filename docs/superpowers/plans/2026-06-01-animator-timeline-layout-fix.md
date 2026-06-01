# Animator Timeline Layout & Coordinate Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **PROJECT RULE — git:** Do **not** run any `git` command. The user commits manually. Where this plan
> says "Checkpoint", just pause for review; never `git add`/`commit`.
>
> **PROJECT RULE — Unity:** C# compiles inside the Editor. After any `.cs` change, `refresh_unity`
> (compile) then `read_console` for `CS####` errors before continuing. Prefab edits are done via
> Unity MCP (`manage_prefabs` / `manage_components`) and verified structurally where possible, then
> in-headset. You cannot compile-check a prefab; visual checks are the user's in headset.

**Goal:** Fix the animator timeline so the ruler frame count matches the config, track lanes stack 1:1 with their name rows, and keyframes land centered on their frame's tick.

**Architecture:** One frame→pixel mapping (`X = frame * FramePx`) already exists in code; the fixes are (a) thread the config's default frame count into container creation, (b) give the lanes container a `VerticalLayoutGroup` matched to the names column so lanes stop overlapping, (c) align horizontal origins and use `pivot.x = 0.5` on key+tick markers.

**Tech Stack:** Unity 6000.3.7f1, uGUI (World-Space Canvas + XRI `TrackedDeviceGraphicRaycaster`), VContainer, NUnit (Unity Test Runner, EditMode), MCP-for-Unity.

**Spec:** `docs/superpowers/specs/2026-06-01-animator-timeline-layout-fix-design.md`

---

## File Structure

| File / asset | Responsibility | Change |
|---|---|---|
| `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` | authoring API | add `CreateContainer(owner, totalFrames, fps)` overload; DRY the post-create publish/save |
| `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs` | panel wiring | `OnAddAnimationClicked` passes config defaults |
| `Assets/_App/Tests/Animation/AnimationAuthoringCreateContainerTests.cs` | unit test | new — verifies config frames flow through |
| `Assets/_App/Tests/SpatialUi/AnimatorPanelModulePrefabTests.cs` | structural test | new — asserts `LanesContent` has a `VerticalLayoutGroup` |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | timeline layout | add VLG to `LanesContent`; left-anchor `Ruler/Content` + `LanesContent` |
| `Assets/_App/Content/Prefabs/UI/Elements/TimelineLane.prefab` | one lane | `LayoutElement.preferredHeight` = row height; left-anchor the keys `_content` |
| `Assets/_App/Content/Prefabs/UI/Elements/TimelineKeyDiamond.prefab` (the lane's `_keyPrefab`) | key marker | `pivot.x = 0.5` |
| `Assets/_App/Content/Prefabs/UI/Elements/TimelineTick.prefab` (ruler `_tickPrefab`) | ruler tick | `pivot.x = 0.5` |

---

## Task 1: Frame count flows from config (code + unit test)

**Files:**
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringCreateContainerTests.cs` (create)
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` (the `CreateContainer` region, ~lines 49-61)
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs:147-153` (`OnAddAnimationClicked`)

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/Animation/AnimationAuthoringCreateContainerTests.cs`:

```csharp
using NUnit.Framework;

public class AnimationAuthoringCreateContainerTests
{
    [Test]
    public void CreateContainer_WithExplicitFramesAndFps_UsesThem()
    {
        var bus = new EventBus();
        // clock/sceneGraph/paths/storage are not dereferenced on the CreateContainer path.
        var authoring = new AnimationAuthoring(null, null, null, null, bus);
        authoring.InitForTest();

        var c = authoring.CreateContainer("node1", 100, 24);

        Assert.AreEqual(100, c.TotalFrames);
        Assert.AreEqual(24, c.Fps);
        Assert.AreSame(c, authoring.GetContainer("node1"));
    }

    [Test]
    public void CreateContainer_Parameterless_KeepsDataLayerDefault()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();

        var c = authoring.CreateContainer("node2");

        Assert.AreEqual(60, c.TotalFrames); // ActionContainer default
    }
}
```

(`InitForTest()` is `internal`; `Assets/_App/Scripts/Animation/InternalsVisibleTo.cs` already exposes internals to `_App.Tests`.)

- [ ] **Step 2: Run the test, verify it fails to compile/fails**

In Unity: `run_tests` (EditMode) filtered to `AnimationAuthoringCreateContainerTests`, or Test Runner.
Expected: FAIL — `CreateContainer` has no `(string,int,int)` overload (compile error).

- [ ] **Step 3: Add the overload + DRY helper in `AnimationAuthoring.cs`**

Replace the existing `CreateContainer` method (lines ~49-61) with:

```csharp
public ActionContainer CreateContainer(string ownerNodeId)
{
    EnsureData();
    return FinishCreate(ownerNodeId, _data.CreateContainer(ownerNodeId));
}

public ActionContainer CreateContainer(string ownerNodeId, int totalFrames, int fps)
{
    EnsureData();
    return FinishCreate(ownerNodeId,
        _data.CreateContainer(ownerNodeId, Mathf.Max(1, totalFrames), Mathf.Max(1, fps)));
}

private ActionContainer FinishCreate(string ownerNodeId, ActionContainer c)
{
    _bus.Publish(new AnimationContainerChangedEvent
    {
        OwnerNodeId = ownerNodeId,
        Change      = ContainerChange.Added
    });
    RequestSave();
    RebuildActiveClips();
    return c;
}
```

(`Mathf` is available — `using UnityEngine;` is already at the top.)

- [ ] **Step 4: Pass config defaults from the panel**

In `AnimatorPanel.cs`, change `OnAddAnimationClicked` (lines 147-153):

```csharp
private void OnAddAnimationClicked()
{
    if (_ctx.Authoring == null) return;
    var owner = AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId);
    if (string.IsNullOrEmpty(owner)) return;
    _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
}
```

- [ ] **Step 5: Compile + run tests**

`refresh_unity` (compile) → `read_console` types=[error]: expect no `CS####`.
`run_tests` EditMode `AnimationAuthoringCreateContainerTests`: expect PASS (both tests).

- [ ] **Step 6: Checkpoint** (no git — pause for review).

---

## Task 2: Lanes container gets a VerticalLayoutGroup (prefab + structural test)

**Files:**
- Test: `Assets/_App/Tests/SpatialUi/AnimatorPanelModulePrefabTests.cs` (create)
- Modify: `AnimatorPanelModule.prefab` → `…/TimelineContent/LanesContent`

**Why:** `LanesContent` has no `VerticalLayoutGroup`, so `AnimatorSubLanes` stacks every lane at the
same position. The names column (`TracksColumnContent`) already uses a VLG (padding top/bottom **9**,
spacing **15**). Mirror its *vertical* params; differ on width (lanes span the full timeline width).

- [ ] **Step 1: Write the failing structural test**

Create `Assets/_App/Tests/SpatialUi/AnimatorPanelModulePrefabTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorPanelModulePrefabTests
{
    private const string PrefabPath =
        "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab";

    [Test]
    public void LanesContent_HasVerticalLayoutGroup()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"prefab not found at {PrefabPath}");

        var lanes = root.transform.Find(
            "ActiveStateRoot/Body/TimelineColumn/TimelineScroll/Viewport/TimelineContent/LanesContent");
        Assert.IsNotNull(lanes, "LanesContent path not found");

        var vlg = lanes.GetComponent<VerticalLayoutGroup>();
        Assert.IsNotNull(vlg, "LanesContent must have a VerticalLayoutGroup");
        Assert.IsTrue(vlg.childForceExpandWidth, "lanes must stretch to full timeline width");
        Assert.IsFalse(vlg.childControlHeight, "lane height comes from each lane's LayoutElement");
    }
}
```

This test lives in `_App.Tests`; it uses `UnityEditor` so the test assembly must be EditMode-only
(the `_App.Tests.asmdef` already runs in the Editor). If `_App.Tests` lacks an `Editor` platform
include, add `"Editor"` to its `includePlatforms` — verify by running.

- [ ] **Step 2: Run the test, verify it fails**

`run_tests` EditMode `AnimatorPanelModulePrefabTests`: expect FAIL ("LanesContent must have a VerticalLayoutGroup").

- [ ] **Step 3: Add the VerticalLayoutGroup to LanesContent**

```
manage_components action=add
  target="ActiveStateRoot/Body/TimelineColumn/TimelineScroll/Viewport/TimelineContent/LanesContent"
  search_method=by_path
  component_type="VerticalLayoutGroup"
```

Then set its properties to mirror the names column vertically + stretch width:

```
manage_components action=set_property component_type="VerticalLayoutGroup" target=<same path> properties={
  "padding": {"left":0,"right":0,"top":9,"bottom":9},
  "spacing": 15,
  "childAlignment": "UpperLeft",
  "childControlWidth": true,
  "childForceExpandWidth": true,
  "childControlHeight": false,
  "childForceExpandHeight": false,
  "childScaleWidth": false,
  "childScaleHeight": false
}
```

> If `manage_components` can't be targeted by path inside an unopened prefab, do it interactively:
> `manage_prefabs action=open_prefab_stage prefab_path=<prefab>` → `manage_components ... search_method=by_path`
> → `manage_prefabs action=save_prefab_stage` → `close_prefab_stage`.

Confirm the live names-column values before trusting the 9/15 above:
```
manage_prefabs action=open_prefab_stage prefab_path=<prefab>
# read TracksColumnContent's VerticalLayoutGroup padding.top/bottom + spacing; use the SAME numbers.
```

- [ ] **Step 4: Run the test, verify it passes**

`run_tests` EditMode `AnimatorPanelModulePrefabTests`: expect PASS.

- [ ] **Step 5: Checkpoint** (no git).

---

## Task 3: Lane height + keys-content origin (prefab `TimelineLane.prefab`)

**Files:** Modify `Assets/_App/Content/Prefabs/UI/Elements/TimelineLane.prefab`

**Why:** With a VLG on `LanesContent` and `childControlHeight=false`, each lane needs an explicit
height equal to the name-row height (so row i and lane i line up). And the keys parent (`_content`)
must have its local x=0 at the lane's left so `key.anchoredPosition.x = f*FramePx` maps to frame 0
at the timeline's left edge.

- [ ] **Step 1: Read the name-row height**

Open the prefab stage and read `TrackRow.prefab`'s effective row height (its `LayoutElement.preferredHeight`,
or RectTransform height). Call it `ROW_H`.
```
manage_prefabs action=get_info prefab_path="Assets/_App/Content/Prefabs/UI/Elements/TrackRow.prefab"
```

- [ ] **Step 2: Set the lane's LayoutElement height**

On the `TimelineLane.prefab` root, ensure a `LayoutElement` exists and set `preferredHeight = ROW_H`,
`minHeight = ROW_H`, `flexibleHeight = 0`:
```
manage_prefabs action=open_prefab_stage prefab_path="Assets/_App/Content/Prefabs/UI/Elements/TimelineLane.prefab"
manage_components action=add  target="TimelineLane" search_method=by_name component_type="LayoutElement"   # skip if present
manage_components action=set_property component_type="LayoutElement" target="TimelineLane" properties={"minHeight": ROW_H, "preferredHeight": ROW_H, "flexibleHeight": 0}
```

- [ ] **Step 3: Left-anchor the keys content**

Find the lane's keys container (the RectTransform wired to `TimelineLane._content`). Set it to a
left-anchored full-height strip so keys position from the left edge:
- `anchorMin = (0, 0)`, `anchorMax = (0, 1)`, `pivot = (0, 0.5)`, `anchoredPosition = (0, 0)`,
  `sizeDelta.x = 0` (width grows with content; keys are absolutely placed children, so x is what matters).

```
manage_components action=set_property component_type="RectTransform" target="TimelineLane/<keysContentChildName>" search_method=by_path properties={
  "anchorMin": [0,0], "anchorMax": [0,1], "pivot": [0,0.5], "anchoredPosition": [0,0]
}
```
(Replace `<keysContentChildName>` with the actual child name shown by `get_hierarchy`.)

- [ ] **Step 4: Save + sanity**

`save_prefab_stage` → `close_prefab_stage`. `refresh_unity`; `read_console` (no errors).

- [ ] **Step 5: Checkpoint** (no git).

---

## Task 4: Centered pivots + shared horizontal origin (prefab)

**Files:** `TimelineKeyDiamond.prefab` (lane `_keyPrefab`), `TimelineTick.prefab` (ruler `_tickPrefab`),
`AnimatorPanelModule.prefab` (`Ruler/Content`, `LanesContent`).

**Why:** `f * FramePx` must mean the same X for a tick and a key, and the key must read as "on this
frame line." Use `pivot.x = 0.5` on both markers and put `Ruler/Content` + `LanesContent` at the same
left origin (TimelineContent x=0, no left padding).

- [ ] **Step 1: Confirm which key prefab the lane uses**

Read `TimelineLane.prefab`'s `_keyPrefab` reference (TimelineLane MonoBehaviour). It is one of
`TimelineKeyDiamond.prefab` / `KeyframeMarker.prefab`. Edit **that** one. Likewise confirm
`AnimatorSubRuler._tickPrefab` → `TimelineTick.prefab`.

- [ ] **Step 2: Set key marker pivot.x = 0.5**

```
manage_components action=set_property component_type="RectTransform" target="<root of the key prefab>" search_method=by_name properties={"pivot": [0.5, 0.5]}
```
(Do this on the key prefab asset's root RectTransform.)

- [ ] **Step 3: Set tick pivot.x = 0.5**

Same on `TimelineTick.prefab` root: `pivot = [0.5, 0.5]` (or `[0.5, 1]` if ticks hang from the top —
keep the existing y, change only x to 0.5).

- [ ] **Step 4: Left-anchor Ruler/Content and LanesContent**

In `AnimatorPanelModule.prefab`, ensure both share the timeline's left origin (x=0, no left padding):
- `…/TimelineContent/Ruler/Content`: `anchorMin=(0,0)`, `anchorMax=(0,1)`, `pivot=(0,0.5)`, `anchoredPosition=(0,0)`.
- `…/TimelineContent/LanesContent`: `anchorMin=(0,1)`, `anchorMax=(0,1)`, `pivot=(0,1)`,
  `anchoredPosition=(0, -RULER_H)` so lanes begin just below the ruler (`RULER_H` = the Ruler row height).

(Use `get_hierarchy` to confirm exact child names; read `RULER_H` from the Ruler RectTransform.)

- [ ] **Step 5: Save + sanity**

`save_prefab_stage` → `close_prefab_stage`; `refresh_unity`; `read_console` (no errors).
Re-run `AnimatorPanelModulePrefabTests` (still PASS).

- [ ] **Step 6: Checkpoint** (no git).

---

## Task 5: In-headset verification (manual — user)

Cannot be automated; the user runs it in VR. Hand this checklist to the user:

- [ ] Ruler frame count equals the configured `DefaultTotalFrames` (100, not 60).
- [ ] Select a rig, Add animation, expand bones: each track **name row** (left) lines up with its **lane** (right); no lanes overlapping.
- [ ] Set a key at frame N on a bone and on the rig: each diamond is **centered on tick N** in its own lane.
- [ ] Scroll the tracks vertically: names and lanes stay aligned (shared vertical scroll).
- [ ] Detach/float the animator panel: buttons still work (regression check from the raycaster fix).

If any horizontal drift remains, the culprit is a leftover left-offset/padding on `Ruler/Content`,
`LanesContent`, or the lane keys-content — recheck Task 3 Step 3 and Task 4 Step 4.

---

## Self-Review notes
- **Spec coverage:** issue 1 → Task 1; issue 2 → Tasks 2-3; issue 3 → Tasks 3-4. Out-of-scope items
  (pinned ruler, loop, NLA) intentionally absent.
- **Types:** `CreateContainer(string,int,int)`, `FinishCreate(string,ActionContainer)`,
  `_config.DefaultTotalFrames`/`DefaultFps`, `AnimatorPanelConfig` — all exist or are defined here.
- **Placeholders:** prefab pixel values (`ROW_H`, `RULER_H`, key/tick prefab choice, keys-content child
  name) are read live in-step with exact MCP commands — not left vague.
