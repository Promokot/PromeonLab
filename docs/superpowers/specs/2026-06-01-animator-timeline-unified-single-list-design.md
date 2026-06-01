# Animator Timeline — Unified Single-List Redesign (design)

**Date:** 2026-06-01
**Status:** Approved (pending user spec review)
**Scope:** Restructure the animator timeline into ONE vertical scroll list whose rows each contain the
track name *and* the keyframe strip, eliminating the second column and its scroll-sync. Targets the
**single-object** case only. Rigs (multi bone-track) are explicitly deferred.

## Context

Today the panel is two columns: `TracksColumn` (names, own ScrollRect, VLG) and `TimelineColumn`
(ruler + lanes + playhead, own ScrollRect), kept aligned by `TimelineScrollSync`. The previous fix
made keyframes land correctly, but the two-column + sync structure is the remaining pain. The user
wants: one area = the timeline, the track name folded into each lane, a single scroll list with
nothing to synchronize, and the ruler drawn with a small left offset under which the lanes' key
strips begin.

**Rig status (deferred):** for rigs, track rows currently *do not appear at all* (bone tracks are
created only on keying, and that path is separately broken). This redesign does **not** fix rigs; it
scopes to a single object (exactly one track) and must not regress the rig path (rigs keep showing
nothing — no crash).

## Decisions (locked with the user)

1. **One unified row prefab** (`TimelineRow`) = `NameSegment` (fixed width) + `KeyStrip` (keys). It
   replaces the separate `TrackRow` + `TimelineLane`.
2. **The name-column width is a config value** (`AnimatorPanelConfig.TrackNameWidth`) so it's trivial
   to tune. The ruler's left offset and each row's key-strip left both use it.
3. **Pre-create the object's track on Add** so its (empty) row is visible immediately — but only for a
   plain object, not a rig. Bone tracks still appear on keying (rig path, deferred).
4. **Fit-to-width, no horizontal scroll:** `FramePx` is computed = `keyStripWidth / TotalFrames`, so
   the whole animation always fits the visible width. Config `FramePx` becomes an unused fallback.

## Design

### Layout — one vertical scroll list
```
Toolbar (frame / total / fps / set / del / copy / paste / remove)
Ruler            ← offset left by TrackNameWidth; ticks at frame*FramePx
ScrollRect (VERTICAL only) → Content (VerticalLayoutGroup):
   TimelineRow:  [ NameSegment (TrackNameWidth) | KeyStrip (fills rest): ◆  ◆     ◆ ]
   (single object → exactly one row)
Playhead         ← offset left by TrackNameWidth; at currentFrame*FramePx
Transport (prev / play-pause / next / ...)
```
- Remove `TracksColumn`, `TimelineColumn`, the second `ScrollRect`, and `TimelineScrollSync`. One
  `ScrollRect` (vertical) over a `VerticalLayoutGroup` content holds the rows.
- Ruler and Playhead are positioned so their frame-0 sits at the key-strip's left edge
  (`x = TrackNameWidth` within the row's coordinate space).

### `TimelineRow` (new component + prefab `TimelineRow.prefab`)
Absorbs `TrackRow` + `TimelineLane`:
- `NameSegment`: left, RectTransform width = `TrackNameWidth` (set from config on Bind); TMP label,
  selection background, a `Button`/click → `onClick(trackNodeId)` (selects the track).
- `KeyStrip`: anchored to fill from `x = TrackNameWidth` to the row's right edge; holds the pooled
  key diamonds positioned at `anchoredPosition.x = frame * FramePx` (pivot.x = 0.5).
- API: `Bind(trackNodeId, displayName, isBone, onClick)`, `SetKeys(IReadOnlyList<int> frames,
  int currentFrame, float framePx)`, `SetActive(bool)`. Row height from `LayoutElement` (= prior
  `ROW_H` = 52).

### Computed `FramePx`
- `FramePx = max(1, keyStripWidth / TotalFrames)` where `keyStripWidth = rowWidth - TrackNameWidth`.
- Recomputed by `AnimatorPanel` whenever the timeline width changes (panel resize/grab) or
  `TotalFrames` changes, then ruler, all rows, and the playhead are repositioned with the new value.
  A lightweight resize hook (e.g. `OnRectTransformDimensionsChange` on the content, or recompute in
  `RebuildTimeline` plus a resize callback) drives this.

### `AnimatorPanel` changes
- Drop `_tracksColumnContent` and `RebuildTrackRows`.
- `RebuildLanes` → `RebuildRows`: instantiate one `TimelineRow` per `container.Tracks` entry under the
  single content; bind name (`_ctx.Graph?.GetNode(t.NodeId)?.DisplayName ?? t.NodeId`), selection
  click, and keys.
- `RebuildTimeline`: compute `FramePx`, set ruler offset = `TrackNameWidth`, rebuild ruler with the
  computed `FramePx`, rebuild rows, position playhead (offset + `currentFrame*FramePx`).
- `OnAddAnimationClicked`: after `CreateContainer(owner, DefaultTotalFrames, DefaultFps)`, if the
  owner is a plain object (not a rig), pre-create its track. Rig detection mirrors the existing
  panels (`GetNode(owner)?.GetComponentInChildren<ProxyRigRuntime>() != null` ⇒ rig ⇒ skip).

### Authoring — pre-create empty track
Add `AnimationAuthoring.EnsureTrack(string ownerNodeId, string trackNodeId)`: ensures the owner's
container has a track for `trackNodeId` (via the existing `ActionContainer.GetOrCreateTrack`), then
notifies the panel to rebuild its rows so the empty row appears. No keys are added. (Ruler/clock
already exist from container creation.) Notification mechanism: publish an
`AnimationContainerChangedEvent` for the owner (a dedicated `ContainerChange.TracksChanged` value is
the cleanest — the plan finalizes whether to add it or reuse `Added`); ordering must guarantee the
panel rebuilds **after** the track is added.

### Config
- Add `public float TrackNameWidth = 200f;` to `AnimatorPanelConfig`.
- `FramePx` stays as a field but is no longer the source of truth for spacing (kept as a harmless
  fallback / removed if unused after wiring — implementer's call).

### Data flow — unchanged
`AnimationAuthoring`, `SceneAnimationData`, `ActionContainer`, `GetKeyFrames`, selection, persistence
all unchanged. The row's `trackNodeId` is the track's `NodeId` exactly as before.

## Out of scope (deferred)
- **Rigs / bone tracks** — the "bone tracks don't appear" bug and multi-bone-row UX. Rigs keep
  current behavior (no rows); must not crash.
- Horizontal scroll / zoom for long timelines (fit-to-width chosen instead).
- Pinned ruler, loop, NLA, `SceneModifiedEvent` on keys (tracked in `docs/BACKLOG.md`).

## Components touched
| File / asset | Change |
|---|---|
| `AnimatorPanelConfig.cs` | add `TrackNameWidth` |
| `Scripts/SpatialUi/Elements/TimelineRow.cs` | new — merges TrackRow + TimelineLane |
| `Content/Prefabs/UI/Elements/TimelineRow.prefab` | new — NameSegment + KeyStrip |
| `AnimatorPanel.cs` | drop tracks-column/sync; `RebuildRows`; compute FramePx; offset ruler/playhead; pre-create object track on Add |
| `AnimationAuthoring.cs` | add `EnsureTrack(owner, trackNodeId)` |
| `AnimatorPanelModule.prefab` | collapse two columns → one ScrollRect+VLG; remove TimelineScrollSync; ruler/playhead offset |
| `TrackRow.cs` / `TimelineLane.cs` / `.prefab`, `AnimatorSubLanes.cs`, `TimelineScrollSync.cs` | retired/unused after the merge (delete in the plan once nothing references them) |

## Verification
- **Unit:** `EnsureTrack` creates a 0-key track on the owner's container (EditMode, like the existing
  CreateContainer test). FramePx math is trivial (`width/total`) — a 1-liner test optional.
- **Structural (EditMode):** `AnimatorPanelModule.prefab` has exactly one `ScrollRect` under the active
  state, no `TimelineScrollSync` component anywhere, and the row prefab has a `NameSegment`+`KeyStrip`.
- **In-headset (single object):**
  1. Select an object → Add → its (empty) row appears immediately under an offset ruler.
  2. Set keys at frames N/M → diamonds centered on ticks N/M in the row's key strip.
  3. Whole frame range fits the panel width; no horizontal scroll; name always visible.
  4. Resize/grab the panel → keys/ruler re-fit to the new width.
  5. Select a rig → no crash (rows simply absent, as before).

## Risks
- Computed `FramePx` on resize must not fight the VLG/layout pass (recompute after layout settles).
- Deleting `TimelineScrollSync`/`TrackRow`/`TimelineLane` must be done only after confirming no prefab
  GUID references remain (GUID sweep, as with prior cleanups).
- Prefab restructure is verified visually in headset; anchors can't be compile-checked.
