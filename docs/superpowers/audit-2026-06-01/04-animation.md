# Audit 2026-06-01 — Animation subsystem + Animator panel

Domain: `Assets/_App/Scripts/Animation/` + `SpatialUi` AnimatorPanel & timeline UI.
Method: full read of all 17 Animation files, 7 `AnimatorSub*` panels, `AnimatorPanel`,
`AnimatorPanelConfig`, timeline `Elements/` (`TimelineLane`, `TrackRow`) and
`Behaviors/Timeline*`, plus 5 specs / 5 plans / 1 investigation. Read-only; no code touched.

---

## 1. Implemented reality

**Data model (v2, fully built).** `SceneAnimationData` holds `List<ActionContainer>`
keyed by `OwnerNodeId` (`SceneAnimationData.cs:5-8`). Each `ActionContainer` owns
`Fps`/`TotalFrames` + `List<AnimTrackData>` with lazy track creation
(`ActionContainer.cs:19-26`) and `TruncateToTotalFrames` (`:42-49`). `AnimTrackData`
upserts/sorts full-transform `AnimKeyData` snapshots (`AnimTrackData.cs:11-21`). This
exactly matches the v2 design (`specs/2026-05-21-animator-system-design.md:34-90`).

**Authoring (real and complete).** `AnimationAuthoring` (`IStartable`/`IDisposable`,
not a MonoBehaviour) implements the entire v2 API: container CRUD
(`AnimationAuthoring.cs:43-75`), `SetTotalFrames`/`SetFps` (`:92-119`), per-track
`SetKey`/`DeleteKey`/`HasKey`/`GetKeyFrames` (`:159-225`), whole-frame
`SetKeyForFrame`/`DeleteAllKeysAtFrame` (`:227-281`), `CopyFrame`/`PasteFrame` via
`FrameClipboard` (`:283-328`), `NearestKeyBefore`/`After` (`:330-354`), and
`OwnerOf` bone-prefix resolution (`:35-41`). Persistence is real: debounced 200 ms
async save (`:123-139`), load with v1-discard / v2-load / v3-forward-guard
(`:381-425`). Playback sampling builds legacy `AnimationClip`s with 10 curves per track
and `SampleAnimation`s each node on `FrameChangedEvent` while playing
(`:359-379`, `:444-473`).

**Clock / transport (scrub + play/pause + speed; NO loop).** `AnimationClock`
(`ITickable`, root-scope) supports `Play`/`Pause`/`Stop`/`Seek`/`Configure`
(`AnimationClock.cs:37-77`). Frame advance uses `Time.deltaTime * Fps` so FPS *is* the
playback speed (`:19`). Playback **stops at `TotalFrames`** (`:23-28`) — there is no
loop. Scrub exists two ways: toolbar frame input + `TimelineScrubInput` pointer/drag →
`round(localX / FramePx)` → `Clock.Seek` (`TimelineScrubInput.cs:15-25`).

**AnimatorPanel sub-modules — all 7 wired and functional:**
- `AnimatorPanel` (`Panels/AnimatorPanel.cs`) — the brain: `[Inject]` of
  `EventBus`/`AnimationClipboard`/`SceneContext` (`:27-33`), subscribes to 6 events
  (`:38-43`), drives a 3-state machine NoSelection/NoContainer/Active (`Refresh`,
  `:207-242`), rebuilds ruler/rows/lanes/playhead (`:269-292`).
- `AnimatorSubToolbar` — frame/total/fps inputs + set/delete/copy/paste/remove buttons,
  interactable-state setters (`AnimatorSubToolbar.cs`). Wired `:64-75`.
- `AnimatorSubTransport` — 7 transport buttons + play/pause sprite swap
  (`AnimatorSubTransport.cs`). Wired `:77-87`.
- `AnimatorSubEmptyState` — NoSelection / NoContainer panels + Add-animation button
  (`AnimatorSubEmptyState.cs`). Wired `:89-93`.
- `AnimatorSubRuler` — pooled ticks + major-tick labels (`AnimatorSubRuler.cs:15-41`).
- `AnimatorSubLanes` + `TimelineLane` — pooled lanes, per-lane pooled key diamonds with
  selected/bone/object coloring (`AnimatorSubLanes.cs`, `TimelineLane.cs:30-54`).
- `AnimatorSubPlayhead` — X = `frame * FramePx` + frame label (`AnimatorSubPlayhead.cs`).
- Plus `TrackRow` (Object/Rig/Bone kinds, indent, has-key dot — `TrackRow.cs`) and
  `TimelineScrollSync` (mirrors vertical scroll between the two columns).

**Maturity:** single-object/single-bone-set keyframe authoring with persistence,
scrub, and play/pause is **production-ready**. Multi-container simultaneous playback,
loop, and NLA are absent.

---

## 2. Doc ↔ code matches

- v2 data-model + `AnimationAuthoring` API (`specs/2026-05-21-animator-system-design.md`)
  matches the code method-for-method (see §1). Strong match.
- Migration policy (`<2` delete file + warn; `==2` load; `>2` error, don't touch)
  in the spec (`:92-99`) matches `AnimationAuthoring.cs:397-416` exactly.
- `AnimationClock.Configure` clamp-and-publish (spec `:198`) matches `AnimationClock.cs:66-77`.
- Sub-part role taxonomy + `Animator*` rename map
  (`specs/2026-05-29-spatialui-animation-refactor-design.md:46-68`) matches the on-disk
  `AnimatorPanel` / `AnimatorSub*` / `TimelineLane` / `TrackRow` / `TimelineScrubInput`
  / `TimelineScrollSync` filenames precisely.
- Animation folder merge (`AnimationAuthoring/`+`AnimationPlayback/` → `Animation/`,
  class names unchanged) is done: all 17 files live in `Scripts/Animation/`.
- Sandbox-null-services guard the investigation flagged is in the code with a matching
  comment (`AnimatorPanel.cs:209-212`).

---

## 3. Drift / mismatches

1. **CLAUDE.md line 58 lists non-existent key types.** `Animation` row names
   `ActionData`, `AnimationPlayback`, plus "NLA composition" and "playback transport
   (scrub/loop/speed)". Reality: there is no `ActionData` type (it is
   `ActionContainer`/`AnimTrackData`); `AnimationPlayback.cs` is an **empty placeholder**
   (`AnimationPlayback.cs:1-3`, declares only `AnimationPlaybackPlaceholder`); there is
   **no NLA** and **no loop** anywhere (grep: zero hits for `AnimationEvaluator`,
   `TrackRecorder`, `ActionData`, `loop`, `NLA`). Speed is real (= FPS), scrub is real,
   loop is not.

2. **CLAUDE.md line 73 event wiring is fictional.** `FrameChanged → AnimationEvaluator,
   TrackRecorder` — neither type exists. The real consumers of `FrameChangedEvent` are
   `AnimationAuthoring.OnFrameChanged` (`AnimationAuthoring.cs:359`) and `AnimatorPanel`
   (`AnimatorPanel.cs:40,105`).

3. **Animation events not listed in CLAUDE.md.** `AnimationContainerChangedEvent`,
   `AnimationKeyframeChangedEvent`, `PlaybackStateChangedEvent` exist and are published
   by `AnimationAuthoring`/`AnimationClock`, but the "Key events" list omits them.

4. **Scope drift: `AnimationClock` and `AnimationAuthoring` are scene-scoped, not root.**
   CLAUDE.md line 39 lists `AnimationClock` under `RootLifetimeScope`, and the v2 spec
   says "AnimationClock остаётся в RootLifetimeScope" (`...animator-system-design.md:194`).
   But the add-button investigation confirms both are registered in `VrEditingSceneScope`
   (`investigations/2026-05-30...:58-61`) and absent in Sandbox — i.e. scene-scoped via
   `SceneContext.Authoring/Clock`. The doc/spec claim of root-scope is stale.

5. **`AnimatorPanelConfig` default mismatch.** Code default `DefaultTotalFrames = 100`,
   `FramePx = 30`, `MajorTickInterval = 5` (`AnimatorPanelConfig.cs:8-11`), but spec/Clock
   defaults are 60 frames (`...animator-system-design.md:317`, `AnimationClock.cs:7-8`).
   Not a bug (SO overrides at runtime), but the 100 vs 60 default is inconsistent with
   every doc.

6. **`menuName` drift.** SO uses `"PromeonLab/Animator Panel Config"`
   (`AnimatorPanelConfig.cs:3`); spec specified `"VrAnimApp/Animator Panel Config"`
   (`...animator-system-design.md:316`). Cosmetic.

---

## 4. Planned-but-not-implemented

- **NLA composition** — claimed in CLAUDE.md, **never specced in detail and absent in
  code.** No clip-blending, no action layering, no multi-action timeline. Zero hits.
- **Loop playback** — explicitly "Future Work" in v1 spec
  (`specs/2026-05-20-animation-system-design.md:24,178`); still not implemented (clock
  hard-stops at `TotalFrames`). CLAUDE.md's "loop" claim is aspirational.
- **Multi-container / master timeline** — only one container plays at a time; the clock
  is reconfigured per active selection (`AnimatorPanel.ApplyContainerToClock`,
  `:256-267`). Listed as a known future risk in the spec (`...animator-system-design.md:547`).
- **Undo/Redo for animation actions** — out of scope in both specs
  (`specs/2026-05-20...:25,177`; `...animator-system-design.md:498`); no `ICommand`
  integration. Set/Delete/Remove-animation mutate directly, bypassing `CommandStack`.
- **`BonesVisibilityChangedEvent` wiring** — designed (`...animator-system-design.md:215,
  393-414`) but **not present** in the Animation folder; `AnimatorPanel` does not
  subscribe to it (only `SelectionChangedEvent`). Bones-mode → outliner-blue lives
  outside this domain; the animator-side subscription described in the spec was dropped.
- **`SceneModifiedEvent` on key mutation** — spec says every `SetKey` should also publish
  `SceneModifiedEvent` for `UnsavedChangesGuard` (`...animator-system-design.md:473`);
  `AnimationAuthoring` publishes only animation events, never `SceneModifiedEvent`.

---

## 5. Stale-doc candidates (do NOT delete — flag only)

| Doc | Status | Reason |
|---|---|---|
| `specs/2026-05-20-animation-system-design.md` + `plans/2026-05-20-animation-system.md` | **SUPERSEDED-BY** `2026-05-21-animator-system-design.md` | v1 single-`SceneAnimationData` model (one fps/totalFrames, schemaVersion 1, `AnimationModule` panel) fully replaced by the v2 per-`ActionContainer` model now in code. v1 file paths (`Subsystems/AnimationAuthoring/…`) no longer exist. |
| `specs/2026-05-21-animator-panel-module-design.md` + `plans/...module.md` | **DONE / OBSOLETE paths** | `AnimatorPanelModuleBuilder.cs` builder approach; describes types by their *old* `*View` names (`AnimatorPanelView`, `TimelineRulerView`, …) which were renamed to `AnimatorPanel`/`AnimatorSub*` by the 05-29 refactor. Layout built; names stale. |
| `specs/2026-05-21-animator-panel-layout-design.md` + `plans/...layout.md` | **DONE / OBSOLETE** | Targets `AnimationModule.prefab` and a `SetHeight` removal in `AnimatorPanelView.RebuildTimeline`; current `AnimatorPanel.RebuildTimeline` (`:288-291`) already has no `SetHeight` call, and `AnimatorSubPlayhead.SetHeight` is now dead (see §6). Old `Subsystems/SpatialUi/...` paths. |
| `specs/2026-05-29-spatialui-animation-refactor-design.md` + `plans/...refactor.md` | **DONE** | Rename/relocate scope A is reflected on disk (folders `Panels/`/`Elements/`/`Behaviors/`, merged `Animation/`). Spec B (overlays→modules, VrKeyboard rename) remains future. |
| `investigations/2026-05-30-animation-add-button-debug.md` | **PARTIALLY-RESOLVED** | H1/H4 (Sandbox-null) are by-design. **H5 is still a live bug** (see §6) — `OnContainerChanged` drops the first `Added` event. Keep until H5 fixed. |

CLAUDE.md line 58 (`Animation` row) and line 73 (`FrameChanged` consumers) are **stale
and should be corrected** (see §3.1–3.3) but per constraints are NOT edited here.

---

## 6. Rudimentary / dead code & live bugs

- **`AnimationPlayback.cs` — dead placeholder.** Empty `AnimationPlaybackPlaceholder`
  class (`:1-3`); playback logic actually lives in `AnimationAuthoring` + `AnimationClock`.
  CLAUDE.md still advertises `AnimationPlayback` as a key type. Candidate for deletion.
- **`AnimatorSubPlayhead.SetHeight` — dead method.** No caller (the layout spec removed
  the call); kept "not harmful" per `...panel-layout-design.md:345`. `AnimatorSubPlayhead.cs:17-23`.
- **Live bug (investigation H5, still present): first `Add animation` looks like a no-op
  in VrEditing.** `OnAddAnimationClicked` (`AnimatorPanel.cs:147-153`) calls
  `CreateContainer`, which publishes `AnimationContainerChangedEvent{Added}`. But
  `OnContainerChanged` guards `if (e.OwnerNodeId != _activeOwner) return;`
  (`AnimatorPanel.cs:120`) and at create-time `_activeOwner` is still null (set null at
  `:220`/`:229`), so the `Added` branch at `:134-136` never runs until a later `Refresh`
  (e.g. reselect). The container is created and saved, but the UI does not transition to
  Active immediately. Fix candidate: accept `Added` when `_activeOwner == null` and the
  new owner matches the current selection's owner, then `Refresh()`.
- **Silent guards vs CLAUDE.md "don't swallow silently".** `OnAddAnimationClicked` has two
  bare log-free `return`s (`:149`, `:151`); `DebouncedSave` swallows `TaskCanceledException`
  silently (`AnimationAuthoring.cs:138`). Minor; flagged by the investigation (`:210`).
- **`KeyColor_Rig` effectively unused for icons.** `TrackRow.Bind` colors Rig rows with
  `KeyColor_Object` (Bone is the only special case, `TrackRow.cs:28-33`), so the distinct
  `KeyColor_Rig` config slot (`AnimatorPanelConfig.cs:14`) never drives the row icon.
- **`AnimationClipboard.IsEmpty`/persistence** — fine, but clipboard is a root singleton
  holding mutable runtime state in a normal class (not static), so it's within convention.
