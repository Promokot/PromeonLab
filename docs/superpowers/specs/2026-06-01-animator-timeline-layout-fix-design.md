# Animator Timeline — Layout & Coordinate Fix (design)

**Date:** 2026-06-01
**Status:** Approved (pending user spec review)
**Scope:** Fix the three reported animator-timeline defects: (1) frame count ≠ config, (2) track/lane
structure, (3) keyframes mis-positioned. Pure layout + a small data-default change. No new features.

## Problem (as reported)

1. **Frame count mismatch** — the ruler's frame count does not match `AnimatorPanelConfig`.
2. **Track structure** — the lanes area should be a vertical layout group (like the scene-node list).
   Concern: full-width stretch will make absolute keyframe placement hard.
3. **Keyframes mis-placed** — keys spawn but don't land on their frame / expected offset.

## Root causes (verified in code + prefab)

- **Lanes overlap.** `AnimatorPanelModule.prefab` → `…/TimelineContent/LanesContent` carries
  `AnimatorSubLanes` + `TimelineScrubInput` but **no `VerticalLayoutGroup`**, and
  `AnimatorSubLanes.GetOrCreate` never assigns a lane's Y. So every lane is instantiated at the same
  position and they stack on top of each other. The track **names** column
  (`…/TracksColumnContent`) *does* have a `VerticalLayoutGroup` + `ContentSizeFitter`, so names lay
  out correctly while lanes pile up → keys from different tracks overlap and don't match their row.
  **This is the core of issues 2 and 3.**
- **Frame count source.** `AnimatorPanel.ApplyContainerToClock`/`RebuildTimeline` use
  `container.TotalFrames` (`AnimatorPanel.cs:266,283,289`). `ActionContainer.TotalFrames` defaults to
  **60** (`ActionContainer.cs:9`). `SceneAnimationData.CreateContainer(owner, totalFrames=60, fps=24)`
  *already* accepts overrides (`SceneAnimationData.cs:17`), but `AnimationAuthoring.CreateContainer(owner)`
  calls it **without** them (`AnimationAuthoring.cs:52`), so every new container is 60 frames —
  while `AnimatorPanelConfig.DefaultTotalFrames` = **100**. The ruler shows 60, the config says 100.
  (Fps already agrees: 24 = 24.) **This is issue 1.**
- **Horizontal mapping is already correct.** Both the ruler (`AnimatorSubRuler.cs:26`) and keyframes
  (`TimelineLane.cs:39`) position at `f * _config.FramePx`. The formula the user proposed
  (`pos = frame * step`) is already implemented. Any residual horizontal drift is a shared-origin /
  pivot mismatch between `Ruler/Content` and each `lane._content`, not a math error.

## Design

### 1. Coordinate model (single source of truth — keep)
- Frame → X: `X(f) = f * FramePx`. Used by ruler ticks, keyframes, and playhead.
- Content width: `(TotalFrames + 1) * FramePx` (already set in `RebuildTimeline`).
- **Shared horizontal origin:** `Ruler/Content`, `LanesContent`, and each `lane._content` are all
  left-anchored so local `x = 0` maps to frame 0 at `TimelineContent`'s left edge. No left padding
  on any of them.
- **Centered pivot (decision):** the keyframe marker prefab and the ruler tick prefab both use
  `pivot.x = 0.5`, so `f * FramePx` centers the marker on the frame line.

### 2. Vertical structure — twin synced VLG columns (decision A)
- **Left (names):** `TracksColumnContent` — `VerticalLayoutGroup` + `ContentSizeFitter` (exists).
- **Right (lanes):** add a `VerticalLayoutGroup` to `LanesContent`, matched to the names column:
  same spacing, same per-row height, `childForceExpandWidth = true` (lane spans the full timeline
  width = `(TotalFrames+1)*FramePx`), `childControlHeight = false` (each lane carries a
  `LayoutElement.preferredHeight = rowHeight`). Row i (name) now aligns with lane i (keys).
- **Keys stay absolute:** keyframes are children of `lane._content`, not direct VLG children, so the
  VLG controls only the lane (height / stacking / width) and never touches keyframe X. This resolves
  the "stretch makes keys hard to place" concern.
- **Vertical scroll** of the two columns stays synced via the existing `TimelineScrollSync`.

### 3. Frame count — config as the single default source
- The data layer already supports it: `SceneAnimationData.CreateContainer(owner, totalFrames, fps)`
  takes overrides. Add a matching overload `AnimationAuthoring.CreateContainer(owner, totalFrames,
  fps)` that forwards them (keep the parameterless one for tests, forwarding the same data-layer
  defaults). `AnimatorPanel.OnAddAnimationClicked` calls the new overload with
  `_config.DefaultTotalFrames` / `_config.DefaultFps`. Ruler, clock, and toolbar then all agree.
- Optional tidy: align `ActionContainer`'s default (60) and `AnimationClock`'s default with the
  config value so any non-panel creation path is also consistent (low priority).

## Components touched

| File / asset | Change |
|---|---|
| `AnimatorPanelModule.prefab` → `LanesContent` | add `VerticalLayoutGroup` (+ `ContentSizeFitter` if needed), matched to names column |
| `AnimatorPanelModule.prefab` → lane prefab, `lane._content` | left-anchored x=0, full-width; `LayoutElement` for row height |
| `AnimatorPanelModule.prefab` → key marker + ruler tick prefabs | `pivot.x = 0.5` |
| `AnimatorPanelModule.prefab` → `Ruler/Content`, `LanesContent` | left-anchored, no left padding, shared origin |
| `AnimationAuthoring.CreateContainer` | accept optional `totalFrames`/`fps`; default container uses them |
| `AnimatorPanel.OnAddAnimationClicked` | pass `_config.DefaultTotalFrames`/`DefaultFps` |

## Out of scope (YAGNI / future)
- Pinned-ruler / corner-cell scroll architecture (ruler currently scrolls with content vertically) —
  acceptable at current track counts; revisit if many-track vertical scroll becomes a problem.
- Loop playback, `SceneModifiedEvent` on key mutation, NLA, multi-container (tracked in `docs/BACKLOG.md`).

## Verification
- **Unit (pure):** a small test that a freshly created container reports `TotalFrames ==
  AnimatorPanelConfig.DefaultTotalFrames` when created via the panel path (or via the new authoring
  overload). Frame→X is trivially `f*FramePx`; no test needed.
- **In-headset checklist:**
  1. Ruler frame count equals the configured `DefaultTotalFrames`.
  2. Two+ tracks: name rows and lanes line up one-to-one; no overlapping lanes.
  3. Set a key at frame N on a bone and on the rig: each diamond centers on tick N in its own lane.
  4. Scroll vertically: names and lanes stay aligned.
  5. Detached/floating panel still behaves (regression from the raycaster fix).

## Risks
- VLG `childForceExpandWidth` interacting with the horizontally-scrolling `TimelineContent` width —
  verify the lane width tracks `(TotalFrames+1)*FramePx` and doesn't clamp to viewport.
- Prefab anchor edits are done in Unity and can't be compile-checked; verify visually in-headset.
