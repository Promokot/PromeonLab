# Render Quality Profile — Design

**Status:** Planned, not yet implemented.
**Branch:** `feat/render-quality-profile` (created from `dev` on 2026-06-04, dormant until work starts).
**NOT-FOR-THESIS** — this feature is auxiliary tooling, the same category as the thesis-screenshot tool. It must remain trivially removable before defense (see §Removal).

---

## Goal

Give the user a runtime switch between three URP rendering quality presets (**Low / Medium / High**) without touching the existing `Mobile_RPAsset.asset` / `PC_RPAsset.asset` files. The default for first launch is **Medium**. The choice persists across sessions. Both target platforms (Quest standalone + PC/Link) are covered.

### Why this exists

The default `Mobile_RPAsset` renders at `MSAA 2× / RenderScale 0.8`. On Quest 3 that produces visible edge aliasing on the skeletal-rig wireframes and on `SpatialUi` text. We want to ship higher-AA presets without losing the option to fall back to the current low-overhead settings (Quest 2, hot-room thermal throttling, profiling sessions).

### Non-goals (YAGNI)

- No shadow / opaque-texture / depth-texture differences between presets — only **MSAA + RenderScale**.
- No `Custom` preset, no per-knob sliders.
- No use of Unity's `QualitySettings` tier system — we swap `GraphicsSettings.defaultRenderPipeline` directly.
- No localisation of the dropdown labels (matches the rest of the project).
- No interaction with `XR Plugin Management → OpenXR → Eye Texture Resolution Scale` (composer-level supersampling stays at user/system default).

---

## Architecture

```
SettingsPanel ──dropdown──▶ RenderQualityService.Apply(preset)
                                    │
                                    ├─▶ QualityPresetStore.Save(preset)  ──▶ persistentDataPath/quality-preset.json
                                    │
                                    └─▶ GraphicsSettings.defaultRenderPipeline =
                                           QualityProfilesConfig.Resolve(platform, preset)
                                                  │
                                                  └─▶ one of 6 URP RP-assets
```

Cross-subsystem contract:
- **Read state:** subscribe to `QualityPresetChangedEvent` on the root `EventBus`. Do **not** call the service directly from foreign subsystems.
- **Mutate state:** only through `RenderQualityService.Apply(preset)`. The settings UI is the legitimate consumer (it has the service injected).

---

## RP-asset matrix

Six new files in `Assets/Settings/` alongside the existing two. The existing files are **not modified** — they remain the default selected in `ProjectSettings → Graphics → Default Render Pipeline` and `Quality → Render Pipeline Asset`, so an uninstalled / unused feature changes nothing on disk that Unity reads at boot.

| File | Platform | MSAA | RenderScale | Notes |
|---|---|---|---|---|
| `Mobile_RPAsset_Low.asset` | Quest (Android) | 2× | 0.8 | Bit-for-bit copy of current `Mobile_RPAsset` (new GUID). |
| `Mobile_RPAsset_Medium.asset` | Quest (Android) | 4× | 1.0 | Default preset. |
| `Mobile_RPAsset_High.asset` | Quest (Android) | 4× | 1.15 | Ceiling for Quest 3 on our scene complexity. |
| `PC_RPAsset_Low.asset` | PC / Link | 2× | 1.0 | Copy of current `PC_RPAsset`. |
| `PC_RPAsset_Medium.asset` | PC / Link | 4× | 1.0 | Default preset. |
| `PC_RPAsset_High.asset` | PC / Link | 4× | 1.2 | Headroom for desktop GPUs. |

Why a whole-asset swap instead of mutating fields at runtime:
- `UniversalRenderPipelineAsset` is a `ScriptableObject`. Setting fields on the live instance during Play Mode dirties the asset and Unity writes the mutation back to disk on exit — known editor footgun.
- Swapping the reference (`GraphicsSettings.defaultRenderPipeline = X`) is atomic, leaves all six source files immutable, and the diff between presets is a 2-line YAML delta that's trivially reviewable.

---

## New subsystem — `Assets/_App/Scripts/RenderQuality/`

Follows project convention (one folder per subsystem under `Scripts/`, `Data/` + `Events/` subfolders, no extra `.asmdef`).

```
RenderQuality/
├── QualityPreset.cs                  ← enum { Low, Medium, High }
├── RenderQualityService.cs           ← IStartable; on start reads store + applies; public Apply(preset)
├── Data/
│   └── QualityProfilesConfig.cs      ← ScriptableObject with 6 UniversalRenderPipelineAsset refs
│                                       + Resolve(RuntimePlatform, QualityPreset) → URPA
├── Storage/
│   ├── IQualityPresetStore.cs
│   └── QualityPresetStore.cs         ← Load/Save via PathProvider; JsonUtility; schemaVersion 1
└── Events/
    └── QualityPresetChangedEvent.cs  ← struct { QualityPreset Preset; }
```

Every new `.cs` file gets the header:
```csharp
// NOT-FOR-THESIS: render-quality preset feature.
// See docs/superpowers/specs/2026-06-04-render-quality-profile-design.md §Removal.
```

`QualityProfilesConfig.asset` lives in `Assets/Settings/` next to the RP-assets. A single instance, referenced by `RootLifetimeScope` via a serialized field (same pattern as `NavBarConfig` / `ControlsProfile`).

---

## DI — `RootLifetimeScope`

```csharp
builder.RegisterInstance(_qualityProfilesConfig);
builder.Register<IQualityPresetStore, QualityPresetStore>(Lifetime.Singleton);
builder.RegisterEntryPoint<RenderQualityService>();   // IStartable applies on startup
```

Root-lifetime is correct because rendering quality is app-global and outlives mode transitions.

---

## Platform detection

```csharp
bool isMobile = Application.platform == RuntimePlatform.Android;
```

Quest standalone build → `Android` → Mobile branch.
PC standalone player + Editor Play Mode + Link via PC → not Android → PC branch.

The platform check happens inside `QualityProfilesConfig.Resolve(platform, preset)`; the service stays platform-agnostic.

---

## Persistence

New `PathProvider` method:

```csharp
public string QualityPresetFile() => Path.Combine(_persistentRoot, "quality-preset.json");
```

File contents:
```json
{ "schemaVersion": 1, "preset": "Medium" }
```

- **First launch** (no file): service uses `Medium`, writes the file, applies the matching RP-asset.
- **Subsequent launches**: read file → apply preset.
- **Unknown / corrupted preset string**: fall back to `Medium`, overwrite the file. Log a single `Debug.LogWarning`.
- **Schema bump policy**: inline migration in `QualityPresetStore.Load` (same convention as `SceneSerializer`).

---

## UI

A new section in the existing `SettingsPanel` (no new world-space prefab):

- Title: **Render Quality**
- Control: dropdown with three options (`Low`, `Medium`, `High`).
- Position: below the `Controls` section.

The dropdown widget reuses whatever the panel already uses for control-binding rows; if no dropdown component exists in the current settings UI, we build a thin `SettingsDropdownRow` view inside `SettingsPanel/Views/` (subsystem-local).

On selection change → `RenderQualityService.Apply(selected)`. The service handles persistence + pipeline swap + event publish; the panel stays dumb.

---

## Tests — `Assets/_App/Tests/RenderQuality/`

1. **`QualityPresetStore_RoundTrip`** — `Save(Medium)` then `Load()` returns `Medium`.
2. **`QualityPresetStore_FirstRun_ReturnsDefault`** — no file on disk → returns `Medium` and creates the file.
3. **`QualityPresetStore_CorruptedFile_FallsBackToMedium`** — write garbage JSON → load returns `Medium` and rewrites the file.
4. **`QualityProfilesConfig_Resolve`** — `(Android, Low)` → mobileLow asset; `(WindowsPlayer, High)` → pcHigh asset; `(WindowsEditor, Medium)` → pcMedium asset; null fields throw a clear exception.

All under a single `_App.Tests` assembly per project convention.

---

## Runtime cost / known costs

- **Pipeline swap hitch.** `GraphicsSettings.defaultRenderPipeline = X` forces URP to rebuild internal render-graph state — expect a one-frame stall in VR. Magnitude not measured; documented here as an expected (and acceptable, since user-initiated) cost rather than a budgeted number.
- **No memory penalty.** Only one RP-asset is referenced live at a time; the other five are loaded but small SOs.
- **Editor pollution risk:** zero — we never mutate the SO fields, only swap the reference.

---

## Edge cases

| Case | Behaviour |
|---|---|
| User switches mid-frame while a scene-load fade is in progress | The service is root-lifetime and oblivious to mode transitions; swap proceeds normally. The pipeline rebuild may briefly stall the fade, no functional damage. |
| `QualityProfilesConfig` field is `null` for the resolved slot | `Resolve` throws `InvalidOperationException("RP asset not assigned for {platform}/{preset}")`. Caller (the service) logs and aborts the swap, leaves the previous asset in place. |
| Editor Play Mode | Treated as PC; service runs, applies PC-tier asset, restores nothing on exit (the existing `PC_RPAsset` is the project's Graphics-default — Unity reverts at Play-Mode exit because we only mutated `GraphicsSettings` at runtime, not the asset on disk). |
| Headless / batch mode | `Application.platform == LinuxEditor` etc. — Resolve returns the PC branch. No special-case. |

---

## Removal plan (post-thesis cleanup)

1. Delete the entire folder `Assets/_App/Scripts/RenderQuality/`.
2. Delete the six new RP-assets:
   - `Assets/Settings/Mobile_RPAsset_{Low,Medium,High}.asset`
   - `Assets/Settings/PC_RPAsset_{Low,Medium,High}.asset`
3. Delete `Assets/Settings/QualityProfilesConfig.asset`.
4. Remove the three registration lines from `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` (DI section).
5. Remove the `Render Quality` row from `SettingsPanel` (whichever prefab + script it lives on).
6. Remove `Assets/_App/Tests/RenderQuality/`.
7. Remove the `QualityPresetFile()` method from `PathProvider`.
8. Update `CLAUDE.md` if any wording referenced render-quality presets (currently none — this spec is the only mention).
9. `ProjectSettings/QualitySettings.asset` and `ProjectSettings/GraphicsSettings.asset` are untouched throughout the lifetime of this feature, so no rollback there.
10. Optional: delete this spec and the BACKLOG entry, or move them to `docs/superpowers/_archive/`.

Cross-check after removal: grep the repository for `RenderQuality`, `QualityPreset`, `quality-preset.json`, `Mobile_RPAsset_`, `PC_RPAsset_` → no hits.

---

## Open questions (none right now)

All gating decisions resolved in the brainstorm session (2026-06-04). Implementation can start on the existing `feat/render-quality-profile` branch when the user signals.
