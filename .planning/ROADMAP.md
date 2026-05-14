# PromeonLab Demo — Roadmap

Golden path: **MainMenu → create scene → import model → build rig → set keyframe → play animation**

Primary target: **XR Simulator** (no headset required for development).

---

## Phases

| # | Phase | Deliverable |
|---|---|---|
| 1 | [Foundation](phases/01-foundation.md) | XR Simulator boots, VContainer scopes initialize, all asmdefs compile |
| 2 | [SpatialUi + ModeOrchestrator](phases/02-spatial-ui.md) | ToolbarPanel visible in VR, ray clicks UGUI, MainMenu ↔ VrEditing transition works |
| 3 | [StorageCore + MainMenu](phases/03-storage-main-menu.md) | Create/open scene persists to JSON, ScenePickerView functional |
| 4 | [AssetBrowser + Model Loading](phases/04-asset-browser.md) | SimpleFileBrowser opens, pre-bundled skinned mesh loads into scene |
| 5 | [SceneComposition + VrInteraction](phases/05-scene-vr-interaction.md) | Select objects with ray, gizmo move/rotate, PropertyPanel shows transforms, Ctrl+Z undoes |
| 6 | [RigBuilder](phases/06-rig-builder.md) | Build Rig from skeleton, bones visible, IK chain configurable |
| 7 | [Animation Authoring + Playback](phases/07-animation.md) | Set keyframe on bone, scrub timeline, Play animates bone |
| 8 | [Integration + Stubs](phases/08-integration.md) | Golden path runs end-to-end, AR/Export show Coming Soon, errors surface as toasts |

## Stub map

| Subsystem | Status |
|---|---|
| EnvironmentMapping | Coming Soon panel — no implementation |
| ExportPipeline | Coming Soon dialog |
| NLA / NlaComposer | Slot reserved, not wired |
| ThumbnailService | Returns null, UI shows type icon |
| StorageMigrator | No-op |
| Material slot editing | Read-only in PropertyPanel |
