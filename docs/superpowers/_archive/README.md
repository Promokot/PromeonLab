# Planning Archive

Implemented, superseded, and obsolete specs / plans / reports / notes, physically moved out of
the active `docs/superpowers/{specs,plans}` and `docs/{reports,session-reports,developer-notes}`
folders so the active folders show **only live work and current-state reference docs**.

Nothing here is deleted — it is project history (also in git). Re-archived 2026-06-01 after the
full project audit (`docs/superpowers/audit-2026-06-01/`); **swept again 2026-06-02** — all remaining
implemented specs and the animator spec/plan cycle were moved here, so `docs/superpowers/{specs,plans}`
are now **empty**. The living current-state references are root `CLAUDE.md`, `docs/BACKLOG.md`
(not-yet-implemented), and `audit-2026-06-01/`.

**What stays ACTIVE (not archived):**
- `specs/` and `plans/` — **empty**. Once a spec/plan is implemented it is archived here; current state
  lives in `CLAUDE.md` + `docs/BACKLOG.md` + the audit. (The only non-archived planning doc still in
  `docs/superpowers/` is the exporter handoff `exporter-scaffold-handoff.md`, which is outstanding work.)
- `audit-2026-06-01/` — the six reconciliation reports (current-state source of truth).
- `investigations/` — kept as research records.
- `docs/developer-notes/ui-conventions.md` — kept (partially updated 2026-06-01).
- `docs/reports/2026-05-21-vkr-implementation.md` — kept (academic write-up).

---

## specs/ (46 archived)

| Doc | Status | Reason |
|---|---|---|
| `2026-05-14-demo-development-plan-design` | OBSOLETE | Earliest demo plan; overtaken by all subsequent milestones. |
| `2026-05-15-panel-consolidation-design` | OBSOLETE | Per-subsystem `SpatialUi.asmdef` / `_Shared/Interfaces` / `UI_Scripts/` premise contradicts the single-`_App.Runtime` no-namespace topology. |
| `2026-05-16-asset-browser-design` | OBSOLETE | `ILabAsset.SpawnAsync`, `AssetBrowserModule`, drag-and-drop, `AssetPropertiesView` — almost nothing matches shipped design. |
| `2026-05-16-asset-browser-spawn-filebrowser-design` | OBSOLETE | `AssetBrowserModule` + `FileBrowserVrAnchor` (deleted; now `FileBrowserSurface` + region router). |
| `2026-05-16-double-userpanel-cursor-fix-design` | SUPERSEDED | Targeted `UiPanelManager`/`DefaultPanelRegistry` double-spawn; UserPanel is now rig-persistent. |
| `2026-05-16-userpanel-filebrowser-fixes-design` | SUPERSEDED | Y-drift/FaceCamera logic rewritten by 2026-05-30 grab-and-lock. |
| `2026-05-16-userpanel-menu-button-fix-design` | DONE | Null-orchestrator fix; UserPanel now injected in `RootLifetimeScope`. |
| `2026-05-16-vr-keyboard-design` | DONE | Keyboard brain shipped; toggle migrated to region model (`UserPanelKeyboardToggle` deleted). |
| `2026-05-17-navbar-panel-system-design` | SUPERSEDED-BY region model | `NavBarBinding[]`/`ContextSlot`/`DetachablePanel`/`StartsEnabled` all replaced. |
| `2026-05-17-scene-objects-selection-outliner-design` | OBSOLETE (selection) | Multi-select removed (gizmo Phase 0 → single-select). |
| `2026-05-17-spatialui-scripts-reorganization-design` | PARTIALLY SUPERSEDED | Proposed `Scripts/{Panels,Views,Elements}`; final tree is `SpatialUi/{Panels,Elements,Behaviors,Events}`. |
| `2026-05-18-interaction-input-rework-design` | OBSOLETE | Multi-select API + **inverted** move/rotate mapping (live: hold-trigger=rotate, hold-grip=move). |
| `2026-05-18-navbar-exclusive-groups-design` | SUPERSEDED-BY region model | `ExclusiveGroup` survived as region key; `HideAllPanels` mechanism gone. |
| `2026-05-18-scene-ui-interaction-fixes-design` | DONE | Bugfix batch, landed. |
| `2026-05-20-animation-system-design` | SUPERSEDED-BY `2026-05-21-animator-system-design` | v1 single-`SceneAnimationData` model replaced by per-`ActionContainer` v2. |
| `2026-05-20-promeon-bone-renderer-design` | SUPERSEDED-BY entity pipeline | `PromeonInteractableRigBuilder : BoneRenderer` + Animation-Rigging — class & package gone. |
| `2026-05-20-rig-builder-v2-fixes` | SUPERSEDED-BY entity pipeline Slice B | Mesh/follower technique survived; host builder + `RigRuntime` gone. |
| `2026-05-20-rig-builder-v2-proxy-skeleton` | SUPERSEDED-BY rig-builder-v2-fixes → entity pipeline | First proxy-skeleton/`BoneFollower` design on a now-deleted builder. |
| `2026-05-20-rig-interaction-polish` | SUPERSEDED-BY rig-bake-prefab → entity pipeline | `BoneInteractableFactory`/`SceneInspectorView` deleted. |
| `2026-05-20-scene-loading-isolation` | SUPERSEDED-BY single-scene+DDOL (Plan C) | Single load removes render-bleed; additive-era design. |
| `2026-05-21-animator-panel-layout-design` | DONE / stale paths | `AnimationModule.prefab` + old `*View` names; layout landed. |
| `2026-05-21-animator-panel-module-design` | DONE / stale paths | `AnimatorPanelModuleBuilder` with old `*View` names (now `AnimatorPanel`/`AnimatorSub*`). |
| `2026-05-21-player-anchor-fall-guard-design` | OBSOLETE | Anchor + `PlayerSpawnRequestedEvent` + `XRBodyTransformer` replaced by teleport-to-origin. |
| `2026-05-21-rig-bake-prefab-design` | SUPERSEDED-BY rig-slice-b (Approach A) | Bake-into-prefab premise reversed by "always build proxies at runtime." |
| `2026-05-21-vr-gizmo-system-design` | DONE w/ drift | Core gizmo arch matches; `_originalTargetCollider` disable, attach-transform input, bounds-fit all removed. See audit 06 for live behavior. |
| `2026-05-28-app-restructure-design` | DONE | `Scripts/Core` + `_App` layout is live. |
| `2026-05-29-scene-scope-lifecycle-redesign-design` | DONE | A+B+C landed (the `IRigRuntime Rig` property in §2 was never added). |
| `2026-05-29-spatialui-animation-refactor-design` | DONE (scope A) | Rename/relocate done; scope B (overlays→modules) is future. |
| `2026-05-31-asset-import-pipeline-design` | SUPERSEDED-BY `2026-06-01-asset-entity-builders` | `IAssetSpawner`/`Meta`/`asset-library` singular renamed/dropped downstream. |
| `2026-05-21-animator-system-design` | DONE | Per-`ActionContainer` animator v2; live state in `CLAUDE.md` + code (extended through 2026-06-02). |
| `2026-05-29-spatialui-region-model-design` | DONE | Region/nav-bar model (`PanelRegionRouter`) is live. |
| `2026-05-30-settings-panel-controls-bindings-design` | DONE | `ControlsProfile`/`SettingsPanel` shipped. |
| `2026-05-30-userpanel-grab-and-lock-design` | DONE | Grip-grab + triple-lock live. |
| `2026-05-31-interaction-layer-priority-design` | DONE | v2 contextual cast-masks (`InteractionMaskBinder`) live. |
| `2026-06-01-asset-entity-builders-and-capability-design` | DONE | `IAssetEntityBuilder` registry + capability apply live. |
| `2026-06-01-rig-in-entity-pipeline-design` | DONE | Rig flows through the entity builder pipeline. |
| `2026-06-01-rig-slice-b-runtime-proxy-design` | DONE | Runtime proxy-bone rig (`ProxyRigRuntime`) live. |
| `2026-06-01-rig-leaf-bone-axis-design` | DONE | Leaf-bone axis handling shipped. |
| `2026-06-01-builtin-recipe-bake-design` | DONE | Builtin recipe bake shipped. |
| `2026-06-01-type-keyed-selection-colliders-design` | DONE | Type-keyed selection colliders live. |
| `2026-06-01-interaction-context-reset-and-registration-cleanup-design` | DONE | Mask reset on `ModeChanged` + collider re-registration live. |
| `2026-06-01-bone-pose-persistence-design` | DONE | schema-v3 `NodeData.BonePoses` live. |
| `2026-06-01-animator-timeline-layout-fix-design` | SUPERSEDED-BY `2026-06-01-animator-timeline-unified-single-list` | Two-column layout fix, replaced by the single-list redesign. |
| `2026-06-01-animator-timeline-unified-single-list-design` | DONE | Single-list `TimelineRow` timeline live. |
| `2026-06-01-animator-playback-and-refresh-improvements-design` | DONE | Scene-fps, live-track refresh, VR input commit, config metrics (loop superseded by 2026-06-02 per-object loop). |
| `2026-06-02-animator-selected-track-keying-and-interpolation-design` | DONE | Rig owner-track, selected-track keying, Linear/Stepped interpolation, scrub preview, per-object background loop (+ 2026-06-02 polish: keep-empty-tracks, live loop-clip rebuild, ruler scrub, bone-mode timeline visibility). |

## plans/ (52 archived)

All implemented plans whose work has landed. The corresponding live-state reference is either the
matching kept spec or the audit reports. Grouped:

- **Asset/import:** `2026-05-16-asset-browser-spawn-filebrowser`, `2026-05-16-asset-spawn-button`, `2026-05-31-asset-spawn-service-and-persistence`, `2026-05-31-asset-import-gltf-and-wizard`, `2026-06-01-asset-entity-builders-slice1`, `2026-06-01-builtin-recipe-bake`.
- **Rig:** `2026-05-20-promeon-bone-renderer`, `2026-05-20-rig-builder-proxy-visual-split`, `2026-05-20-rig-builder-v2-fixes`, `2026-05-20-rig-builder-v2-proxy-skeleton`, `2026-05-20-rig-interaction-polish`, `2026-05-21-rig-bake-prefab`, `2026-06-01-rig-entity-pipeline-slice-a`, `2026-06-01-rig-slice-b-runtime-proxy`, `2026-06-01-rig-leaf-bone-axis`, `2026-06-01-bone-pose-persistence`.
- **Animation:** `2026-05-20-animation-system`, `2026-05-21-animator-panel-layout`, `2026-05-21-animator-panel-module`, `2026-05-21-animator-system`, `2026-05-29-spatialui-animation-refactor`, `2026-06-01-animator-timeline-layout-fix`, `2026-06-01-animator-timeline-unified-single-list`, `2026-06-01-animator-playback-and-refresh-improvements`, `2026-06-02-animator-selected-track-keying-and-interpolation`.
- **SpatialUi / panels:** `2026-05-15-panel-consolidation`, `2026-05-16-double-userpanel-cursor-fix`, `2026-05-16-userpanel-filebrowser-fixes`, `2026-05-16-userpanel-menu-button-fix`, `2026-05-17-navbar-panel-system`, `2026-05-29-spatialui-region-model`, `2026-05-29-spatialui-region-prefab-verification`, `2026-05-30-settings-panel-controls-bindings`, `2026-05-30-userpanel-grab-and-lock`.
- **Interaction / gizmo / outline:** `2026-05-17-scene-objects-selection-outliner`, `2026-05-18-interaction-input-rework`, `2026-05-18-scene-ui-interaction-fixes`, `2026-05-21-vr-gizmo-system`, `2026-05-30-gizmo-fixes`, `2026-05-30-outline-see-through` (superseded by layered-masks), `2026-05-30-outliner-scroll-fix`, `2026-05-31-outline-layered-masks-and-bone-toggle`, `2026-05-31-interaction-layer-priority` (v1 — superseded by v2 spec), `2026-06-01-type-keyed-selection-colliders`, `2026-06-01-interaction-context-reset-and-registration-cleanup`.
- **Architecture / scene-scope:** `2026-05-28-app-restructure`, `2026-05-30-scene-context-foundation` (Plan A), `2026-05-30-scene-context-consumer-migration` (Plan B), `2026-05-30-scene-loading-single-ddol` (Plan C).
- **Docs / cleanup:** `2026-05-30-project-cleanup` (dead-code removal landed; remaining BaseSceneScope/gizmo-undo items tracked in `docs/BACKLOG.md`), `2026-05-30-docs-and-archive-cleanup`.

## reports/ (3 archived)

- `2026-05-16-asset-browser-spawn-sfb-vr` — OBSOLETE (SFB-VR/AssetBrowserModule work; `FileBrowserVrAnchor` removed).
- `2026-05-16-double-userpanel-cursor-fixes` — DONE (references removed `UiPanelManager`).
- `2026-05-21-project-audit` — SUPERSEDED by `audit-2026-06-01/`.

## session-reports/ (2 archived)

- `2026-05-20-rig-builder-refactor` — OBSOLETE (`PromeonInteractableRigBuilder` no longer exists).
- `2026-05-20-scene-loading-isolation` — SUPERSEDED by single-scene load.

## developer-notes/ (7 archived)

- `2026-05-17-navbar-manual-wiring-guide`, `2026-05-17-scene-outliner-manual-setup`, `2026-05-17-xr-ui-bugfixes-and-navbar-design`, `vr-keyboard` — OBSOLETE manual-wiring guides for a model that is now DI-discovery-driven.
- `2026-05-17-scene-save-load-bugs` — bug list, resolved.
- `2026-05-21-bone-outline-needs-click` — DONE (resolved 2026-05-31).
- `2026-05-31-gizmo-occluded-not-selectable` — DONE (resolved by `InteractionMaskBinder` GizmoHandles context).
