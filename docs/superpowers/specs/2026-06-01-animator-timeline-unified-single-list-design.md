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
2. **The name-column width is a config value** (`AnimatorPanelConfig.TrackNameWidth`, default **100**)
   so it's trivial to tune. The ruler's left offset and each row's key-strip left both use it. The
   name label is masked to that width and **truncates with an ellipsis** (TMP `overflowMode = Ellipsis`)
   — it never overflows the segment.
3. **Pre-create the object's track on Add** so its (empty) row is visible immediately — but only for a
   plain object, not a rig. Bone tracks still appear on keying (rig path, deferred).
4. **Fixed numeric step + scroll on both axes (no division):** `FramePx` stays a fixed config value
   (`AnimatorPanelConfig.FramePx`) — exactly like `TrackNameWidth`, a strict editable offset. Ruler
   ticks, key diamonds, and playhead all use `frame * FramePx`. **No** fit-to-width division. A single
   `ScrollRect` scrolls the content **both horizontally** (to pan a long frame range) **and vertically**
   (to scroll through rows). Everything lives in one content, so there is **nothing to synchronize** —
   the ruler and names pan with the keys. (For the single-object case there is one row, so vertical
   scroll is effectively unused and the ruler never scrolls off; pinning the ruler for multi-row rigs
   is a deferred add.)

## Design

### Layout — one scroll area (both axes)
```
Toolbar (frame / total / fps / set / del / copy / paste / remove)
ScrollRect (HORIZONTAL + VERTICAL) → Viewport(mask) → Content (width = TrackNameWidth+(TotalFrames+1)*FramePx):
   [ Ruler strip ]        ← ticks start at x = TrackNameWidth, at frame*FramePx
   VerticalLayoutGroup of rows:
     TimelineRow:  [ NameSegment (TrackNameWidth) | KeyStrip: ◆  ◆     ◆ ]   (key at TrackNameWidth + frame*FramePx)
   (single object → exactly one row)
   Playhead overlay (full content height) at x = TrackNameWidth + currentFrame*FramePx
Transport (prev / play-pause / next / ...)
```
- Remove `TracksColumn`, `TimelineColumn`, the second `ScrollRect`, and `TimelineScrollSync`. One
  `ScrollRect` (horizontal + vertical) over a single content holds the ruler, the rows
  (`VerticalLayoutGroup`), and the playhead overlay. Everything pans together — nothing to sync.
- The viewport's mask clips off-screen frames/rows; the scrollbar (or drag) pans to reach them.
- `Content.width = TrackNameWidth + (TotalFrames+1) * FramePx`, set in `RebuildTimeline`;
  `Content` height grows with the rows (VLG + `ContentSizeFitter`).

### `TimelineRow` (new component + prefab `TimelineRow.prefab`)
Absorbs `TrackRow` + `TimelineLane`:
- `NameSegment`: left, RectTransform width = `TrackNameWidth` (set from config on Bind); TMP label
  with `overflowMode = Ellipsis` (masked to the segment so long names truncate with "…", never
  overflow), selection background, a `Button`/click → `onClick(trackNodeId)` (selects the track).
- `KeyStrip`: anchored left at `x = TrackNameWidth` within the row (the row spans the full content
  width); holds the pooled key diamonds positioned at `anchoredPosition.x = frame * FramePx`
  (pivot.x = 0.5). No per-strip mask is needed — the ScrollRect viewport clips off-screen content.
- API: `Bind(trackNodeId, displayName, isBone, onClick)`, `SetKeys(IReadOnlyList<int> frames,
  int currentFrame, float framePx)`, `SetActive(bool)`. Row height from `LayoutElement` (= prior
  `ROW_H` = 52).

### Fixed `FramePx` (no division)
- `FramePx` is the existing config value (`AnimatorPanelConfig.FramePx`). Every consumer uses
  `frame * FramePx`: ruler ticks (`AnimatorSubRuler`), key diamonds (`TimelineRow`/`KeyStrip`), and the
  playhead. No computation, no resize recompute.
- A long range does not clip away: the single `ScrollRect` pans horizontally to reach later frames
  (and vertically through rows). `FramePx`/`TotalFrames` are plain config numbers the user tunes;
  whatever the product, the scroll reaches all of it.

### `AnimatorPanel` changes
- Drop `_tracksColumnContent` and `RebuildTrackRows`.
- `RebuildLanes` → `RebuildRows`: instantiate one `TimelineRow` per `container.Tracks` entry under the
  single content; bind name (`_ctx.Graph?.GetNode(t.NodeId)?.DisplayName ?? t.NodeId`), selection
  click, and keys.
- `RebuildTimeline`: set ruler offset = `TrackNameWidth`, rebuild ruler (config `FramePx`), rebuild
  rows, position playhead (offset + `currentFrame * FramePx`).
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
- Add `public float TrackNameWidth = 100f;` to `AnimatorPanelConfig`.
- `FramePx` remains the authoritative per-frame step (unchanged role).

### Data flow — unchanged
`AnimationAuthoring`, `SceneAnimationData`, `ActionContainer`, `GetKeyFrames`, selection, persistence
all unchanged. The row's `trackNodeId` is the track's `NodeId` exactly as before.

## Out of scope (deferred)
- **Rigs / bone tracks** — the "bone tracks don't appear" bug and multi-bone-row UX. Rigs keep
  current behavior (no rows); must not crash.
- Timeline **zoom** (changing `FramePx` live) — config-only for now.
- Pinned ruler (always-visible on vertical scroll), loop, NLA, `SceneModifiedEvent` on keys
  (tracked in `docs/BACKLOG.md`).

## Components touched
| File / asset | Change |
|---|---|
| `AnimatorPanelConfig.cs` | add `TrackNameWidth` |
| `Scripts/SpatialUi/Elements/TimelineRow.cs` | new — merges TrackRow + TimelineLane |
| `Content/Prefabs/UI/Elements/TimelineRow.prefab` | new — NameSegment + KeyStrip |
| `AnimatorPanel.cs` | drop tracks-column/sync; `RebuildRows`; set Content.width + ruler/playhead offset (config `FramePx`); pre-create object track on Add |
| `AnimationAuthoring.cs` | add `EnsureTrack(owner, trackNodeId)` |
| `AnimatorPanelModule.prefab` | collapse two columns → one ScrollRect+VLG; remove TimelineScrollSync; ruler/playhead offset |
| `TrackRow.cs` / `TimelineLane.cs` / `.prefab`, `AnimatorSubLanes.cs`, `TimelineScrollSync.cs` | retired/unused after the merge (delete in the plan once nothing references them) |

## Verification
- **Unit:** `EnsureTrack` creates a 0-key track on the owner's container (EditMode, like the existing
  CreateContainer test).
- **Structural (EditMode):** `AnimatorPanelModule.prefab` has exactly one `ScrollRect` under the active
  state, no `TimelineScrollSync` component anywhere, and the row prefab has a `NameSegment`+`KeyStrip`.
- **In-headset (single object):**
  1. Select an object → Add → its (empty) row appears immediately under an offset ruler.
  2. Set keys at frames N/M → diamonds centered on ticks N/M in the row's key strip.
  3. Ticks/keys use the fixed config `FramePx`; if the range is longer than the view, horizontal
     scroll pans to the later frames (and back); vertical scroll works when there are many rows.
  4. Long name → truncates with "…" inside the name segment, never overflows into the key strip.
  5. Select a rig → no crash (rows simply absent, as before).

## Risks
- The single `ScrollRect` is both-axes over a VLG content: `Content.width` is set in code
  (`TrackNameWidth + (TotalFrames+1)*FramePx`) while `Content` height comes from the VLG +
  `ContentSizeFitter` — confirm the fitter doesn't override the code-set width (use a horizontal
  fit of `Unconstrained`).
- Deleting `TimelineScrollSync`/`TrackRow`/`TimelineLane` must be done only after confirming no prefab
  GUID references remain (GUID sweep, as with prior cleanups).
- Prefab restructure is verified visually in headset; anchors can't be compile-checked.
