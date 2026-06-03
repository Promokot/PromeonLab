# Stage 4 — Dead & Stub Scripts Audit
**Date:** 2026-06-03  
**Scope:** `Assets/_App/Scripts/**/*.cs` (183 files) · `Assets/_App/Editor/**/*.cs` (5 files)  
**Method:** Read every suspect file; Grep type name across Scripts / Editor / Tests; `.meta` GUID search across `*.prefab`, `*.unity`, `*.asset`; verify DI registration in all `*LifetimeScope*` / Bootstrap files.

---

## ✅ EXECUTION STATUS — 2026-06-03 (main thread)

**ARCHIVED (8 Bucket-A files moved to `Assets/_App/Scripts/_Archive/` via Unity AssetDatabase — GUIDs preserved, project recompiled with NO errors / NO missing refs):**

```
ErrorHandling.cs · ErrorLevel.cs · ErrorOccurredEvent.cs · PanelDragHandle.cs
RigSerializer.cs · AssetCatalogData.cs · ExportPipeline.cs · InputBindings.cs
```

Re-verified before moving: both MonoBehaviour candidates' full GUIDs
(`a5609cfa…` PanelDragHandle, `a1e780ab…` DetachablePanelDragHandle) are absent from every
`*.prefab` / `*.unity` / `*.asset`. The other six are non-MonoBehaviour types (enum/struct/static/comment-only)
and cannot be GUID-referenced from serialized YAML at all; moving inside the `_App.Runtime` assembly
does not change compilation.

**Side effect:** the entire `ErrorHandling/` subsystem (3 files) is archived → `ErrorHandling/`
and `ErrorHandling/Events/` are now EMPTY folders (only `.meta` left). See manual-review item in `08`.

**HELD BACK (NOT archived — needs your decision, see `08`):** the detachable-panel feature group —
`SpatialPanelDetachable.cs` is still referenced by `AnimatorPanelModule.prefab` (GUID `6193c7e4`).
Archiving the group requires first removing the inert component from that prefab. Group:
`SpatialPanelDetachable.cs`, `DetachablePanelDragHandle.cs`, `PanelDetachedEvent.cs`,
`PanelLinkedEvent.cs`, `PanelClosedEvent.cs`.

> Conflict resolved: `PanelRegistry` / `UiPanelOrchestrator` are GONE from the codebase
> (Glob found no `.cs`); the only surviving mentions are stale `Assets/_App/Documentation/*.md`.

---

## Bucket A — SAFE to Archive (zero references everywhere)

These files have: no C# callers, no prefab/scene/asset GUID hits, no DI registration.

| File | Kind | Evidence | Confidence | Recommendation |
|---|---|---|---|---|
| `Scripts/ErrorHandling/ErrorHandling.cs` | `static class` (plain) | Body is literally `public static class ErrorHandlingPlaceholder { }`. Grep for `ErrorHandlingPlaceholder` or `ErrorHandling\b`: zero hits outside own file. GUID `74d3789f`: no prefab/scene/asset refs. | **High** | Move to `_Archive` |
| `Scripts/ErrorHandling/ErrorLevel.cs` | `enum` | Only referenced from `ErrorOccurredEvent.cs` (also a dead file). No other callers found. GUID `42dc82f9`: no prefab/scene/asset refs. | **High** | Move to `_Archive` |
| `Scripts/ErrorHandling/Events/ErrorOccurredEvent.cs` | `struct` (event) | Never subscribed to or published anywhere in runtime code — `ErrorDispatcher` noted in CLAUDE.md as "not implemented". GUID `7bc3ef8b`: no refs. | **High** | Move to `_Archive` |
| `Scripts/SpatialUi/Behaviors/PanelDragHandle.cs` | `MonoBehaviour` | File comment says "DEAD / LEGACY — on no prefab or scene." All operational methods are no-ops (`/* dead feature — no-op */`). Grep for `PanelDragHandle`: only appears in `SpatialPanelDetachable.cs`'s class *declaration line* (not as usage), and in `DetachablePanelDragHandle.cs` (also dead). GUID `a5609cfa`: no prefab/scene/asset refs. | **High** | Move to `_Archive` |
| `Scripts/RigBuilder/RigSerializer.cs` | `static class` (plain) | Grep `RigSerializer` across entire `Assets/_App`: zero matches outside own file. GUID `7a80781b`: no prefab/scene/asset refs. `RigDefinition` serialization has been folded into `SceneSerializer` inline JSON; this class is now unused. | **High** | Move to `_Archive` |
| `Scripts/StorageCore/AssetCatalogData.cs` | `class` (plain) | Grep `AssetCatalogData`: zero callers — only the class declaration itself. `PathProvider.AssetCatalogJson()` exists but is also uncalled (no one invokes that method). No tests, no DI. GUID `09f86f42`: no prefab/scene/asset refs. The `asset-catalog.json` file was part of an older per-scene catalog design that was superseded by the global asset-library. | **High** | Move to `_Archive` |
| `Scripts/ExportPipeline/ExportPipeline.cs` | comment-only file | File body is three comment lines — a namespace redirect. No type defined, zero compilation output. GUID `cc6c3051`: no refs. Purely a documentation comment masquerading as a source file. | **High** | Move to `_Archive` |
| `Scripts/InputBindings/InputBindings.cs` | comment-only file | File body is one comment line — `// InputBindings subsystem — data types live in Data/; см. ControlsProfile.cs`. No type defined. GUID `dbf0b738`: no refs. | **High** | Move to `_Archive` |

---

## Bucket B — STUB / Does-Nothing (describe whether referenced)

These files compile, but their runtime behaviour is entirely empty or their feature is disabled. Some are referenced by prefabs (see column).

| File | Kind | What it does (or doesn't) | Referenced? | Confidence | Recommendation |
|---|---|---|---|---|---|
| `Scripts/SpatialUi/SpatialPanelDetachable.cs` | `MonoBehaviour` | "DEAD FEATURE" comment at top. All public methods are `{ /* dead feature — no-op */ }`. All real code commented out. Kept specifically to preserve serialized prefab field refs. | **YES — GUID `6193c7e4` appears in `AnimatorPanelModule.prefab`** (prefab has a reference to this component as `_dragHandle`). Cannot archive without first removing the component from the prefab. | High | **Do NOT archive** — referenced by prefab. Flag as "inert component on prefab"; either remove it from the prefab or leave as-is to avoid missing-ref. |
| `Scripts/SpatialUi/Behaviors/DetachablePanelDragHandle.cs` | `MonoBehaviour` | Real code exists (pointer/drag handlers are implemented), but it is only useful when `SpatialPanelDetachable` is live. `_panel.MoveDelta()` calls the dead no-op method on the inert `SpatialPanelDetachable`. Effectively does nothing useful. Only referenced from `SpatialPanelDetachable` (commented-out dead code). GUID `a1e780ab`: **no prefab/scene/asset refs**. | Only via `SpatialPanelDetachable` source code (`[SerializeField]` field, commented-out usage). | Medium | SAFE to archive once `SpatialPanelDetachable` is dealt with. Pair operation. |
| `Scripts/SpatialUi/Events/PanelDetachedEvent.cs` | `struct` (event) | Defined. Never published (all `Publish(new PanelDetachedEvent…)` calls are commented out in `SpatialPanelDetachable`). Never subscribed. GUID `d170f588`: no refs. | No — only defined | High | Move to `_Archive` with the detachable-feature group |
| `Scripts/SpatialUi/Events/PanelLinkedEvent.cs` | `struct` (event) | Defined. Never published (only in commented-out code). Never subscribed. GUID `e1564b54`: no refs. | No | High | Move to `_Archive` with the detachable-feature group |
| `Scripts/SpatialUi/Events/PanelClosedEvent.cs` | `struct` (event) | Defined. Never published (only in commented-out `SpatialPanelDetachable` code). Never subscribed. GUID `3fdcdc06`: no refs. | No | High | Move to `_Archive` with the detachable-feature group |
| `Scripts/SceneComposition/Events/SceneClosedEvent.cs` | `struct` (event) | Published once by `SceneAutoSaver`. **Never subscribed to** — no handler exists anywhere. The publish is live code, so file is not dead, but the event is currently inert. GUID `0d1a4e30`: no refs. | Published (not subscribed) | Medium | **Do NOT archive** — published by live code; subscriber may be added later. Flag as "unhandled event". |

---

## Bucket C — Suspicious-but-Referenced (do NOT archive; explanation below)

These look potentially dead at first glance but are actively referenced.

| File | Kind | Why it looked suspicious | What keeps it alive | Confidence (to leave alone) |
|---|---|---|---|---|
| `Scripts/VrInteraction/IDragStrategy.cs` | `interface` + `class` (plain) | Two types in one file; `IDragStrategy`/`SingleDragStrategy`/`DragMode` seemed like over-engineering. | `XRPromeonInteractable` (`_dragStrategy = new SingleDragStrategy()`) calls `.Apply()` with `DragMode`. Fully live. | High |
| `Scripts/Bootstrap/PlayerSpawnApplier.cs` | `MonoBehaviour` | No DI builder.Register call for it (unusual). | Registered via `FindAnyObjectByType<PlayerSpawnApplier>` + `RegisterInstance` in `RootLifetimeScope`. GUID `d6478f46` confirmed **in `User XR Origin (XR Rig).prefab`**. Also required by `FallGuard` (`[RequireComponent]`). | High |
| `Scripts/RigBuilder/BoneSelectorBoxPlanner.cs` | `static class` (plain) | No DI; not a MonoBehaviour; seemed isolated. | Referenced in `RigEntityFactory.cs` and `ColliderKind.cs`; has dedicated unit tests (`BoneSelectorBoxPlannerTests`). Fully live. | High |
| `Scripts/RigBuilder/TerminalBoneAxis.cs` | `enum` | Small enum, possibly vestigial. | Referenced in `RigDefinition`, `BuiltinLabAsset`, `RigEntityBuilder`, `RigEntityFactory`, `ImportWizardSurface`, `ImportConfirmedEvent`, tests. Fully live. | High |
| `Scripts/RigBuilder/IkChainRecord.cs` | `[Serializable] class` | IK is "serialized but no solver" per CLAUDE.md. | Field in `RigDefinition.IkChains`. Serialized to JSON; removing would be a breaking schema change. Keep as-is (future IK solver). | High |
| `Scripts/AssetBrowser/SavedAssetLibrary.cs` / `SavedLabAsset.cs` | `class` | "Saved" slice not implemented per BACKLOG. | Both registered in `RootLifetimeScope` (`RegisterEntryPoint<SavedAssetLibrary>`). `SavedLabAsset` used by `AssetRegistry` and tests. Infrastructure is in place; UI/spawn flow is the unimplemented part. | High |
| `Scripts/SpatialUi/Events/RegionChangedEvent.cs` | `struct` (event) | Might be over-defined. | Used by `PanelRegionRouter`, `AssetBrowserPanel`, and tests. | High |
| `Scripts/SpatialUi/SpatialPanel.cs` | `MonoBehaviour` (base class) | GUID not in any prefab. | Is the **base class** for `UserPanel`. Unity resolves the subclass GUID, not the base class GUID, in prefabs. | High |
| `Scripts/StorageCore/AssetCatalogData.cs` (PathProvider methods) | N/A — dead *methods* on live class | `PathProvider.AssetCatalogJson()`, `.ExportDir()`, `.AssetPath()` are uncalled in runtime. | These are dead **methods** on `PathProvider` (a fully-live class), not dead files. `PathProvider.cs` cannot be archived. Flag for method cleanup only. | High |
| `Scripts/VrInteraction/Gizmo/Strategies/AxisScaleStrategy.cs` / `UniformScaleStrategy.cs` | `class` (plain) | No direct construction visible in main code at first glance. | Both created in `GizmoActivator`; both have tests (`AxisScaleStrategyTests`, `UniformScaleStrategyTests`). | High |
| `Scripts/SceneComposition/Events/SceneSelectedEvent.cs` | `struct` (event) | Checked for subscribers. | Published by `MainMenuPanel`, subscribed by `ScenePickerPanel`. Live. | High |
| `Scripts/SpatialUi/Panels/PropertyPanel.cs` | `MonoBehaviour` | Registered only in Sandbox/VrEditing scopes. | Registered in `SandboxSceneScope` and `VrEditingSceneScope`. | High |

---

## Recommended `_Archive` Move List

The following files should be moved to `Assets/_App/Scripts/_Archive/` (create subfolder if needed). All have been confirmed: zero C# callers outside themselves, zero GUID hits in prefabs/scenes/assets, not DI-registered.

```
Assets/_App/Scripts/ErrorHandling/ErrorHandling.cs
Assets/_App/Scripts/ErrorHandling/ErrorLevel.cs
Assets/_App/Scripts/ErrorHandling/Events/ErrorOccurredEvent.cs
Assets/_App/Scripts/SpatialUi/Behaviors/PanelDragHandle.cs
Assets/_App/Scripts/RigBuilder/RigSerializer.cs
Assets/_App/Scripts/StorageCore/AssetCatalogData.cs
Assets/_App/Scripts/ExportPipeline/ExportPipeline.cs          ← comment-only pseudo-file
Assets/_App/Scripts/InputBindings/InputBindings.cs            ← comment-only pseudo-file
```

### Detachable-Feature Group (archive as a set, conditionally)

These three events are dead because `SpatialPanelDetachable` never publishes them. They can be archived now since they have no GUID refs. `DetachablePanelDragHandle.cs` can also be archived once `SpatialPanelDetachable` is cleaned up.

```
Assets/_App/Scripts/SpatialUi/Events/PanelDetachedEvent.cs     ← GUID clean
Assets/_App/Scripts/SpatialUi/Events/PanelLinkedEvent.cs       ← GUID clean
Assets/_App/Scripts/SpatialUi/Events/PanelClosedEvent.cs       ← GUID clean
Assets/_App/Scripts/SpatialUi/Behaviors/DetachablePanelDragHandle.cs  ← GUID clean; pair with SpatialPanelDetachable
```

> **IMPORTANT:** `SpatialPanelDetachable.cs` itself **cannot** be archived without first removing the component from `AnimatorPanelModule.prefab` (GUID `6193c7e4` is referenced there). Moving it while the prefab reference exists will cause a missing-script error.

---

## Dead/Orphaned PathProvider Methods (method-level cleanup, not file archiving)

These methods on the live `PathProvider` class have zero callers in runtime code:

- `AssetCatalogJson(sceneId)` — per-scene catalog feature superseded
- `ExportDir(sceneId)` — legacy export path, unused since ZIP exporter writes to Documents
- `AssetPath(sceneId, relativePath)` — only appears in a unit test, never in production code

Recommendation: remove these three methods from `PathProvider.cs` and delete the corresponding test assertions in `PathProviderTests.cs`.

---

## Summary Statistics

| Bucket | Count | Notes |
|---|---|---|
| A — Safe to archive | 8 files | Confirmed zero refs (code + GUID) |
| B — Stub/inert | 6 files | 1 prefab-blocked (`SpatialPanelDetachable`), 1 live-publish-only (`SceneClosedEvent`), 4 safe to archive (detachable events + `DetachablePanelDragHandle`) |
| C — Suspicious but referenced | 13 entries | All confirmed live; no action needed |
| Dead PathProvider methods | 3 methods | In-place method removal, not file archiving |
