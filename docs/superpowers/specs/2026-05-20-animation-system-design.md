# Animation System — Design Spec

**Date:** 2026-05-20
**Scope:** SceneNode transform animation — timeline, keyframes, playback
**Status:** Approved

---

## Goal

Implement a v1 animation system that lets the user set keyframes on SceneNode transforms (position / rotation / scale), scrub a timeline, and play back the result. Data persists as `animation.json` in the scene directory.

---

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Animated targets | SceneNode (whole objects) only | Bones are Future Work |
| Runtime representation | `AnimationClip` + `AnimationCurve` per SceneNode | Unity handles interpolation; no custom lerp |
| Persistence | `animation.json` via `PathProvider.AnimationPath` | Stays in scene directory; not inside scene.json |
| One track per object | One `AnimTrackData` per nodeId | No per-property tracks exposed externally |
| UI panel base | Clone AssetBrowserModule panel prefab (same size/position) | Consistent look |
| Loop | Not in v1 | Future Work |
| Undo/Redo for keyframes | Not in v1 | CommandStack integration is Future Work |
| Bone animation | Not in v1 | Future Work |

---

## Data Model

### Persistence (`animation.json`)

```
SceneAnimationData
├── schemaVersion: 1
├── fps: 30
├── totalFrames: 120
└── tracks: List<AnimTrackData>
        ├── nodeId: string
        └── keys: List<AnimKeyData>
                ├── frame: int
                ├── position: Vector3
                ├── rotation: Quaternion
                └── scale: Vector3
```

- One `AnimTrackData` per nodeId (enforced — no duplicates).
- `AnimKeyData` stores the full transform snapshot; no partial keyframes.
- `RebuildClip` converts `keys` into 9 `AnimationCurve`s internally (`localPosition.x/y/z`, `localEulerAngles.x/y/z`, `localScale.x/y/z`). This is an internal detail; callers work with `frame` + `Transform`.

### Runtime

`Dictionary<string, AnimationClip>` — keyed by `nodeId`, rebuilt whenever keys change. `AnimationClip.SampleAnimation(gameObject, time)` applies the pose each frame during playback.

---

## Components

### `AnimationClock` — SceneLifetimeScope, `ITickable`

```
CurrentFrame : int        (read-only, clamped [0, TotalFrames])
TotalFrames  : int        (default 120)
Fps          : int        (default 30)
IsPlaying    : bool

Play()    → IsPlaying = true
Pause()   → IsPlaying = false  (frame preserved)
Stop()    → IsPlaying = false, CurrentFrame = 0
Seek(int) → CurrentFrame = clamp(frame, 0, TotalFrames)
```

On each `Tick()`: if `IsPlaying`, accumulate `Time.deltaTime * Fps`; publish `FrameChangedEvent { Frame }` when the integer frame changes; stop at `TotalFrames`.

### `AnimationAuthoring` — MonoBehaviour, SceneLifetimeScope

Replaces the empty placeholder at `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs`.

**Responsibilities:**
- Load / save `animation.json` (via `PathProvider` + `JsonUtility`)
- Maintain `_data: SceneAnimationData` + `_clips: Dictionary<string, AnimationClip>`
- Apply poses each frame during playback

**Key methods:**

```
SetKey(nodeId, frame)
  capture SceneGraph.GetNode(nodeId).transform
  → track.UpsertKey(frame, snapshot)
  → RebuildClip(nodeId)
  → SaveAsync()
  → Publish AnimationKeyframeChangedEvent { NodeId }

DeleteKey(nodeId, frame)
  → track.RemoveKey(frame)
  → RebuildClip(nodeId)  [drop clip if no keys remain]
  → SaveAsync()
  → Publish AnimationKeyframeChangedEvent { NodeId }
```

**Event subscriptions:**
- `SceneOpenedEvent` → `LoadAsync` animation.json; build all clips
- `FrameChangedEvent` → `ApplyFrame`: foreach track, `clip.SampleAnimation(go, frame / fps)`

**DI:** `AnimationClock`, `ISceneGraph`, `PathProvider`, `EventBus`

### `AnimationModule` — MonoBehaviour, UserPanel tab

**Layout** (based on AssetBrowserModule panel — same prefab size and world position offset):

```
┌─────────────────────────────────────┐
│  [◀◀]  [▶ Play]  [■ Stop]   Fr: 42 │
├─────────────────────────────────────┤
│  [════════|════════════════════]    │  ← Slider scrubber
│   ◆    ◆       ◆                   │  ← keyframe markers (selected node)
├─────────────────────────────────────┤
│  [Set Key]          [Delete Key]    │
└─────────────────────────────────────┘
```

**Scrubber:** Unity `Slider` (0..TotalFrames); `onValueChanged` → `AnimationClock.Seek()`; during playback moves to track `CurrentFrame`.

**Keyframe markers:** pooled `Image` objects, positioned at `frame / totalFrames * sliderWidth`; shown only for the currently selected SceneNode. Refreshed on `SelectionChangedEvent` and `AnimationKeyframeChangedEvent`.

**Delete Key** button: active only when selected node has a key at `CurrentFrame`.

**DI:** `AnimationClock`, `AnimationAuthoring`, `ISelectionManager`

---

## New Events

| Event | Fields | Published by | Consumed by |
|---|---|---|---|
| `AnimationKeyframeChangedEvent` | `NodeId: string` | `AnimationAuthoring` | `AnimationModule` |

Existing `FrameChangedEvent` is reused for clock ticks.

---

## Integration Points

| Location | Change |
|---|---|
| `PathProvider` | Add `AnimationPath(sceneId)` → `scenes/{sceneId}/animation.json` |
| `AnimationAuthoring.cs` | Full implementation (replaces placeholder) |
| `AnimationPlayback.cs` | Delete placeholder — logic now in `AnimationAuthoring` + `AnimationClock` |
| `SceneLifetimeScope` | Register `AnimationClock` as `ITickable` + singleton |
| `NavBarConfig` SO | Add entry `"animation"`, exclusive group `tools`, visible in `VrEditing` |
| `UserPanel` prefab | Add `NavBarBinding` entry: button + `AnimationModule` panel |
| `_Shared/Events/` | Add `AnimationKeyframeChangedEvent.cs` |

---

## File Map

```
Assets/_App/Subsystems/AnimationAuthoring/
├── AnimationAuthoring.cs         ← replaces placeholder
├── AnimationClock.cs             ← ITickable service
├── Data/
│   ├── SceneAnimationData.cs
│   ├── AnimTrackData.cs
│   └── AnimKeyData.cs
└── UI/
    └── AnimationModule.cs

Assets/_App/_Shared/Events/
└── AnimationKeyframeChangedEvent.cs
```

---

## Out of Scope (v1)

- Loop playback
- Undo / Redo for Set Key / Delete Key
- Bone (BoneProxy) animation
- Delete entire track from UI
- Editing `TotalFrames` / `Fps` from UI
- Scrubber tick labels
