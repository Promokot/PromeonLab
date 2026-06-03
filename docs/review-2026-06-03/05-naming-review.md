# Stage 6 — Naming Review

Date: 2026-06-03  
Scope: `Assets/_App/Scripts/**/*.cs`  
Convention source: `CLAUDE.md` §Naming + §Strictly Forbidden

---

## (A) Misleading Names

Names where the type name makes a promise the code does not keep, or the code does something the name does not suggest.

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `ErrorHandling` | `ErrorHandling/ErrorHandling.cs` | File contains only `public static class ErrorHandlingPlaceholder { }` — an empty stub. The class name repeats the folder name and declares nothing. A static placeholder is not a "handling" system. | `ErrorDispatcher` (reserved for the real implementation per BACKLOG) or delete/merge into `ErrorLevel.cs` | Low — no runtime references; just a stub file. |
| `InputBindings` | `InputBindings/InputBindings.cs` | File body is a single comment redirecting to other files. The class does not exist in the file — the file is a ghost/index comment. | Remove the file; it is not a compilable type. | Low — no class declared, no GUID reference to a concrete type. |
| `ExportPipeline` | `ExportPipeline/ExportPipeline.cs` | Same ghost pattern — three lines of comments pointing elsewhere. The subsystem is `ExportPipeline`, but no class named `ExportPipeline` is declared. | Remove the file; subsystem entry is `SceneExporter.cs`. | Low — same as above. |
| `GizmoActivator` | `VrInteraction/Gizmo/GizmoActivator.cs` | Name implies it activates/enables the gizmo widget (a simple toggle). The class actually owns the entire gizmo lifecycle: spawning, despawning, hover/grab coloring, drag-strategy dispatch, and CommitTransform. It is the gizmo's primary controller. | `GizmoPresenter` or `GizmoDriver` | HIGH — MonoBehaviour on the XR Rig variant prefab. Class name and filename must be changed together with all code references. |
| `BoundsFitter` | `VrInteraction/Gizmo/BoundsFitter.cs` | Named as if it resizes/fits something. Actual code: one static method `ComputeSize` that returns a `float`. It calculates a suggested scale scalar from renderer bounds — it fits nothing, it computes. The method itself is called from `GizmoActivator` but the frozen comment says "Bounds-fit frozen" (feature not in use). | `GizmoBoundsComputer` or collapse into `GizmoConfig` as a static helper | Low — pure static class, no Unity references. |
| `SelectionVisual` | `VrInteraction/SelectionVisual.cs` | Name reads as a MonoBehaviour or component that draws the visual. It is actually a two-value `enum` (`None`, `Selected`). | `SelectionVisualState` (follows `*State` pattern for enum options) | Low — enum, refactor all usages. |
| `InteractionLayerTag` | `VrInteraction/InteractionLayerTag.cs` | "Tag" is the wrong metaphor — Unity Tags are string labels, this is a layer assignment. The component applies a physics layer to a GameObject at Awake. | `InteractionLayerSetter` or `InteractionLayerMarker` | MEDIUM — MonoBehaviour; may be on prefabs. |
| `SingleDragStrategy` | `VrInteraction/IDragStrategy.cs` | "Single" conveys multiplicity (one object vs multi-select), but the class body shows it is the only concrete implementation and it simply applies position or rotation — not a "single" concept. Also violates one-public-type-per-file: `DragMode` enum, `IDragStrategy` interface, and `SingleDragStrategy` all live in one file. | `DirectDragStrategy`; split into separate files | Low — plain C# class. |

---

## (B) Imprecise / Unclear Responsibility Names

Names that are technically correct but so broad or vague that they do not communicate the specific role.

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `AssetSourceStore` | `AssetBrowser/AssetSourceStore.cs` | "Store" is vague (could mean storage, shop, data store). The class copies raw import files into `asset-libraries/sources/` and returns relative paths. It is a file-copy sink for imported raw assets. | `ImportedSourceStore` or `AssetSourceFiles` | Low — plain C# class, DI-injected by name. |
| `ImportRenderProfile` | `AssetBrowser/ImportRenderProfile.cs` | "Render" hints at rendering settings for displayed assets, but the class stores shader/material overrides used when building reference-image quads. It is not a general render profile — it maps `AssetType` to shader configuration. | `AssetShaderProfile` | MEDIUM — ScriptableObject; rename requires updating `.asset` file references. |
| `SceneContextBinder` | `Bootstrap/SceneContextBinder.cs` | "Binder" is a generic pattern word. The class populates the root-lifetime `SceneContext` from the incoming scene scope's services (Start) and clears it on scope dispose. It is the bridge that makes scene services accessible app-wide. | `SceneContextPopulator` or keep as-is (the Binder suffix at least implies two-directional linking) | Low — plain C# class. |
| `WorldClickCatcher` | `VrInteraction/WorldClickCatcher.cs` | "Catcher" is informal and suggests interception rather than the actual job: deselect when the user activates the trigger on empty space (no hovered interactable). | `EmptySpaceDeselector` or `BackgroundTapDeselect` | MEDIUM — MonoBehaviour; may be on scene GO. |
| `RegionMember` | `SpatialUi/RegionMember.cs` | "Member" describes membership without saying what the thing does: it is a MonoBehaviour that identifies a UI module by ID and delegates Show/Hide to an optional `IRegionSurface` sibling, or falls back to `SetActive`. It is the router's registration bridge. | `RegionModuleProxy` or `PanelRegionEntry` | HIGH — MonoBehaviour attached to panel prefab roots. |
| `PlayerSpawnApplier` | `Bootstrap/PlayerSpawnApplier.cs` | "Applier" is weak — applies what, to whom? The class teleports the XR rig to world origin on every scene load and on Respawn. | `XrRigRecenterer` or `PlayerSpawnPlacer` | MEDIUM — MonoBehaviour on the XR Rig prefab. |
| `VrInputFieldProxy` | `Bootstrap/VrInputFieldProxy.cs` | "Proxy" implies forwarding to another target, but the class simply publishes a `KeyboardFocusEvent` when a TMP InputField is pointer-downed, opening the VR keyboard. It is an input-field focus bridge. | `VrInputFieldFocusBridge` or `InputFieldKeyboardTrigger` | MEDIUM — MonoBehaviour added to each TMP InputField in VR. |
| `AppBootstrap` | `Bootstrap/AppBootstrap.cs` | Acceptable domain name, but "Bootstrap" is generic. The class does exactly one thing: marks `PersistentRoot` `DontDestroyOnLoad` and fires the first scene load. | `AppEntryPoint` or keep `AppBootstrap` (well-understood) | Low — MonoBehaviour in Bootstrap scene. |
| `AnimatorSubEmptyState` | `SpatialUi/Panels/AnimatorSubEmptyState.cs` | "EmptyState" describes a UI state concept, but the class actually shows/hides two alternative empty-state panels ("no selection" vs "no container") and exposes an add-animation callback. It is a sub-view/composite, not a single state. | `AnimatorEmptyStatePicker` or `AnimatorNoContentView` | MEDIUM — MonoBehaviour on prefab. |
| `UnsavedChangesGuard` | `StorageCore/UnsavedChangesGuard.cs` | "Guard" is a policy pattern name (gate-keeping), but this class only tracks a dirty boolean — it does not prevent navigation on its own. It is a dirty-state tracker. | `SceneDirtyTracker` | Low — plain C# class. |

---

## (C) Inconsistent-Vocabulary Clusters

Groups of scripts doing similar jobs but using different naming conventions for the suffix or the noun.

### C1 — Builder vs Factory (same subsystem, same pattern)

`AssetBrowser` has both `*EntityBuilder` and `*EntityFactory` for each asset type:

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `ObjectEntityFactory` | `AssetBrowser/ObjectEntityFactory.cs` | Thin wrapper over `GltfModelLoader.LoadAsync`. "Factory" implies it constructs rich objects; it only forwards to the loader. The *Builder already calls it as a helper. | `ObjectEntityLoader` | Low — plain C# class. |
| `RigEntityFactory` | `AssetBrowser/RigEntityFactory.cs` | Does significantly more than `ObjectEntityFactory` — builds the proxy rig, selector colliders, and all diamond meshes. The word "Factory" is inconsistent with `ObjectEntityFactory` and overlaps "Builder" vocabulary. The distinction between "Factory" (construction helper) and "Builder" (recipe-in / entity-out) is not immediately clear. | Keep `RigEntityFactory` but rename `ObjectEntityFactory` → `ObjectEntityLoader` and `ReferenceEntityFactory` → `ReferenceEntityLoader` to clarify the builder/factory relationship | Low — plain C# classes. |
| `ReferenceEntityFactory` | `AssetBrowser/ReferenceEntityFactory.cs` | Same factory/builder ambiguity. Builds a textured quad from scratch — arguably it is the true factory for reference images, but the pattern should be uniform. | `ReferenceEntityLoader` (thin wrapper) or `ReferenceQuadFactory` (if the creation work stays here) | Low — plain C# class. |

### C2 — Surface vs Panel (UI presentation layer vocabulary)

Two scripts implementing `IRegionSurface` are named `*Surface`; the rest of the main UI panels are `*Panel`. Neither is wrong in isolation, but mixing them within the same region-router system creates inconsistency.

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `FileBrowserSurface` | `SpatialUi/Behaviors/FileBrowserSurface.cs` | Wraps the `SimpleFileBrowser` dialog; implements `IRegionSurface`. Called a "Surface" but acts as the panel-level integration point for the file browser. All other tab-level modules are called `*Panel`. | `FileBrowserPanel` | MEDIUM — MonoBehaviour; may be in prefab/scene. |
| `ImportWizardSurface` | `SpatialUi/Behaviors/ImportWizardSurface.cs` | Same — implements `IRegionSurface` and shows a wizard UI. Should be `*Panel` for consistency with `AssetBrowserPanel`, `ExportPanel`, etc. | `ImportWizardPanel` | MEDIUM — MonoBehaviour on a prefab. |

Note: `IRegionSurface` itself is fine as an interface name for the protocol, but the concrete implementations should follow the `*Panel` convention.

### C3 — DragHandle vs GrabHandle (two overlapping concepts)

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `DetachablePanelDragHandle` | `SpatialUi/Behaviors/DetachablePanelDragHandle.cs` | A UI pointer-drag handle for the dead detachable-panel feature. Uses "Drag". | Keep name (dead feature, low churn value) — or `DetachablePanelPointerHandle` to distinguish from XR grip. | Low — dead feature. |
| `PanelDragHandle` | `SpatialUi/Behaviors/PanelDragHandle.cs` | Legacy dead flat-screen pointer-drag handle for `UserPanel`. All operational code is commented out. Uses "Drag". | Keep (dead/legacy) | Low — dead feature. |
| `PanelGrabHandle` | `SpatialUi/Behaviors/PanelGrabHandle.cs` | The live XR grip-based grab handle. Uses "Grab". | Keep — but the distinction (`Drag` = pointer/flat, `Grab` = XR grip) should be documented as intentional if both were alive simultaneously. Currently the dead ones create confusion. |

Recommendation: when/if `DetachablePanelDragHandle` is revived, rename it `DetachablePanelGrabHandle` (XR grip, not pointer drag) to match the live naming.

### C4 — OutlinerItem suffix cluster vs Card/Row/Item inconsistency

The `Elements/` folder uses three different suffixes for list-row prefab controllers:

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `OutlinerItem` | `SpatialUi/Elements/OutlinerItem.cs` | Scene-node row in the outliner list. Uses `*Item`. | Consistent; keep. | — |
| `RigOutlinerItem` | `SpatialUi/Elements/RigOutlinerItem.cs` | Extends `OutlinerItem` for rig rows. Uses `*Item`. | Consistent; keep. | — |
| `SceneItem` | `SpatialUi/Elements/SceneItem.cs` | Scene picker row (a saved-scene button). Uses `*Item`. | Consistent; keep. | — |
| `TimelineRow` | `SpatialUi/Elements/TimelineRow.cs` | One track row in the animator timeline. Uses `*Row` instead of `*Item`. | `TimelineItem` — OR adopt `*Row` as the suffix for animator-specific list elements and document the distinction. | MEDIUM — MonoBehaviour on prefab. |
| `LabAssetCard` | `SpatialUi/Elements/LabAssetCard.cs` | Asset browser thumbnail. Uses `*Card` instead of `*Item`. | `LabAssetItem` or keep `*Card` and document that Card = thumbnail grid cell (spatial enough to deserve its own suffix). | MEDIUM — MonoBehaviour on prefab. |
| `BindingRow` | `SpatialUi/Elements/BindingRow.cs` | One binding entry in the settings list. Uses `*Row`. | `BindingItem` for consistency, or keep `*Row` if "row" is the chosen suffix for flat list entries. | MEDIUM — MonoBehaviour on prefab. |
| `BindingSectionCard` | `SpatialUi/Elements/BindingSectionCard.cs` | Section header + row container in the settings list. Uses `*Card`. | `BindingSectionHeader` or `BindingSection` — not really a card (no click, no icon). | MEDIUM — MonoBehaviour on prefab. |

Summary: the project has three suffixes for list-element prefab controllers (`*Item`, `*Row`, `*Card`). `*Item` is the majority convention (three uses). `*Row` and `*Card` appear in subsets without a documented rule.

### C5 — AnimatorSub* naming (consistent within itself, but "Sub" is unusual)

| Current name | File | Problem |
|---|---|---|
| `AnimatorSubEmptyState` | Panels | Uses `Sub` prefix to mean "sub-component of `AnimatorPanel`". |
| `AnimatorSubPlayhead` | Panels | Same. |
| `AnimatorSubRuler` | Panels | Same. |
| `AnimatorSubToolbar` | Panels | Same. |
| `AnimatorSubTransport` | Panels | Same. |

The `*Sub*` infix is internally consistent but uncommon. The pattern used elsewhere in the codebase for sub-views is either `*Panel` (top-level) or direct noun names. The established convention for partial-panel widgets in Unity projects is `*View` or `*Widget`, e.g. `AnimatorPlayheadView`, `AnimatorRulerView`. The `AnimatorSub*` cluster functions as a module group, which is a defensible pattern — flag as a style inconsistency rather than an error. If unified, the proposed rename would be `AnimatorPlayheadView`, `AnimatorRulerView`, `AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorEmptyStateView`.

### C6 — Strategy interfaces (two coexisting, differently scoped)

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `IDragStrategy` | `VrInteraction/IDragStrategy.cs` | Interface for `XRPromeonInteractable`'s object-drag (position/rotation copy). Named generically "drag strategy". | `IObjectDragStrategy` to distinguish from gizmo drags. | Low — interface. |
| `IGizmoDragStrategy` | `VrInteraction/Gizmo/Strategies/IGizmoDragStrategy.cs` | Interface for gizmo handle drags (axis move, ring rotate, scale). Correctly prefixed with `Gizmo`. | Consistent; keep. | — |

Both strategy interfaces do different things. The plain `IDragStrategy` could be confused with `IGizmoDragStrategy`; adding the `Object` prefix would disambiguate.

---

## (D) Junk-Drawer / Convention Violations

Names explicitly banned by `CLAUDE.md` or violating suffix rules.

| Current name | File | Problem | Proposed name | Risk |
|---|---|---|---|---|
| `GltfModelLoader` | `AssetBrowser/GltfModelLoader.cs` | "Loader" is a generic grab-bag suffix in this context. The class has one method: `LoadAsync` which wraps `GltfImport` + `InstantiateMainSceneAsync`. A domain noun exists: it is a glTF model importer. | `GltfModelImporter` | Low — plain C# class. |
| `GltfImportHandler` | `AssetBrowser/GltfImportHandler.cs` | `*Handler` is on the banned suffix list when used as a default. The specific domain role here is "picks the right import strategy for glTF/GLB files" — it is an import strategy picker. However, within the `IAssetImportHandler` interface family the suffix is a coherent contract (`ImageImportHandler` mirrors it). If the interface is `IAssetImportHandler`, the implementations should follow that pattern — flag as a marginal violation contingent on renaming the interface. | If `IAssetImportHandler` → `IAssetImporter`, then `GltfImportHandler` → `GltfAssetImporter` and `ImageImportHandler` → `ImageAssetImporter`. | Low — plain C# classes + one interface rename. |
| `ImageImportHandler` | `AssetBrowser/ImageImportHandler.cs` | Same as above — "Handler" suffix on a concrete import strategy. | `ImageAssetImporter` | Low — plain C# class. |
| `UnsavedChangesGuard` | `StorageCore/UnsavedChangesGuard.cs` | "Guard" is a specific pattern-role name (gate, policy enforcement). This class only tracks a `bool _isDirty` — it never prevents any action on its own. The name overpromises its enforcement role. | `SceneDirtyTracker` (also cross-listed in B) | Low — plain C# class. |
| `GameObjectInteractionLayerExtensions` | `VrInteraction/GameObjectInteractionLayerExtensions.cs` | Long but explicit. Extension-method files conventionally use the type they extend (`GameObjectExtensions`) or the domain (`InteractionLayerExtensions`). The current name concatenates both and is redundant since the method is already `SetInteractionLayer(this GameObject ...)`. | `InteractionLayerExtensions` | Low — plain static class; no runtime linkage by name. |

---

## Summary Table of Severity

| Severity | Count | Examples |
|---|---|---|
| HIGH (MonoBehaviour on shipped prefab) | 3 | `GizmoActivator`, `RegionMember`, `InteractionLayerTag` |
| MEDIUM (MonoBehaviour or ScriptableObject, requires prefab update) | ~12 | `WorldClickCatcher`, `PlayerSpawnApplier`, `FileBrowserSurface`, `ImportWizardSurface`, all `*Row`/`*Card`/`*Item` inconsistencies |
| LOW (plain C# class, rename + global replace) | ~15 | `SelectionVisual`, `BoundsFitter`, `ErrorHandling` stub, `GltfModelLoader`, factory/loader cluster |
