# Stage 8→ Decisions Log & Action Plan

**Date:** 2026-06-03 · Branch `review-2026-06-03`. Records the user's manual decisions after reading
reports `01`–`08`, maps them to the report codes, and lists open questions blocking the final plan.
**Nothing in here is executed yet** beyond Stage 4's 8-file archive (see `03`/`08`).

> Legend: ✅ decided · ❓ open question (pending user answer) · ⏸ parked.

---

## Stage 4 — dead/stub scripts

| Item | Decision |
|---|---|
| 4.A Bucket-A 8 files | ✅ Keep archived (already moved, recompiled clean). |
| 4.B Detachable group (5 files) | ✅ Move ALL to `_Archive`. Archive stays in-repo but is **ignored** in all future analysis/planning. Requires first removing the inert `SpatialPanelDetachable` component from `AnimatorPanelModule.prefab` (GUID `6193c7e4`). No logic to migrate — the class is 100% no-ops. ❓ also delete the orphaned link/lock/close buttons from the prefab? |
| 4.C `SceneClosedEvent` | ✅ Leave (published by live `SceneAutoSaver`). |
| 4.D PathProvider dead methods | ✅ Cut all 3: `AssetCatalogJson`, `ExportDir`, `AssetPath` (in-place; PathProvider stays). |

## Stage 7 — tests
- ✅ Cut `PathProviderTests.AssetPath_ReturnsExpectedPath` (pairs with 4.D). Verify no test references `AssetCatalogJson`/`ExportDir` before removing those methods.

---

## Stage 5 — responsibility / duplication  (codes = report `04`)

| Code | Decision |
|---|---|
| **A1** AnimationAuthoring god class | ✅ Implement. Split into façade + `AnimationClipBaker` + `AnimationPlaybackSampler` + `AnimationStore`. **This is the umbrella that absorbs B3 and B4.** |
| **B3** dual sampling paths | ✅ Implement (falls out of A1 — one float-param `Sample`). |
| **B4** animation schema delete-vs-migrate | ✅ Implement — "починить сериализацию": route animation load through the same migrate-not-delete policy as `SceneSerializer` (shared `VersionedJson.Load<T>` helper inside the new `AnimationStore`). |
| **B1** duplicated diamond mesh | ✅ Implement. Single `AppendDiamond(...)` + shared base-vertex/index constants. |
| **A2** GizmoActivator god class | ✅ Implement. Extract `GizmoHighlightPainter` + `GizmoDragSession`. (Enables the `GizmoActivator → GizmoDriver` rename, Stage 6.) |
| **A4** bone-mode split across panels | ✅ Implement — "унифицируем режим отдельно": extract a scene-scoped `BoneEditMode` service owning active-rig state; Inspector + Animator observe it. |
| **B6** Reference recipe not shared | ✅ Implement — "нормализуем обработку": add `ReferenceEntityBuilder.RecipeFromImage(w,h)`; builtin generator calls it (single source of reference recipe constants). |
| **B2** dual transform-commit protocol | ✅ Address, but **not** by unifying the commit helper — ❓ "пока в принципе вырезаем анду логику" → scope to confirm (see open questions). |
| A3, A5, B5, B7, B8 | ⏸ Not selected this round (left as-is). |

**Dependency note:** do **A1 first** (it restructures the file B3/B4 live in), then B1, then A2 (+ its rename), A4, B6, and the B2 undo-cut. B2's cut may interact with A2 (both touch GizmoActivator drag/commit) — sequence A2 before/with B2.

---

## Stage 6 — renames  (codes = report `05`).  ✅ recorded; ❓ where noted

### Keep / live, rename only
| Current | → New | Note |
|---|---|---|
| `GameObjectInteractionLayerExtensions` | `InteractionLayerExtensions` | **Live & important** (single funnel for collider→interaction layer). |
| `InteractionLayerTag` | `InteractionLayerSetter` | MonoBehaviour — check prefab refs. |
| `BoundsFitter` | `GizmoBoundsComputer` | pure static. |
| `SingleDragStrategy` | `DirectDragStrategy` | also split the 3 types out of `IDragStrategy.cs` into own files. |
| `IDragStrategy` | `IObjectDragStrategy` | disambiguate from `IGizmoDragStrategy`. |
| `GizmoActivator` | `GizmoDriver` | **after** the A2 split; ⚠ MonoBehaviour on XR Rig prefab. |
| `AssetSourceStore` | `ImportedSourceProvider` | |
| `ImportRenderProfile` | `ImportedAssetShaderProfile` | ScriptableObject — update `.asset` refs. |
| `WorldClickCatcher` | `EmptySpaceClickDeselector` | MonoBehaviour. |
| `PlayerSpawnApplier` | `XrRigRecenterer` | MonoBehaviour on XR Rig prefab; also update `FindAnyObjectByType<>` + `FallGuard [RequireComponent]`. |
| `VrInputFieldProxy` | `VrInputFieldFocusBridge` | MonoBehaviour. |
| `UnsavedChangesGuard` | `SceneDirtyTracker` | (covers both B & D listings.) |
| `RegionMember` | *(keep)* | ✅ decided to keep. |

### C1 — Factory cluster → "Fabricator"
| Current | → New |
|---|---|
| `ObjectEntityFactory` | `ObjectEntityFabricator` |
| `RigEntityFactory` | `RigEntityFabricator` |
| `ReferenceEntityFactory` | `ReferenceEntityFabricator` |
> ⚠ Caveat: uniform suffix fixes the *naming* inconsistency, but the *responsibility* imbalance remains
> (`ObjectEntityFactory` is a thin loader wrapper; `RigEntityFactory` builds the whole rig). Confirm you're OK leaving the responsibility split as-is and only normalizing the name.

### C2 — Surface → Panel
| `FileBrowserSurface` | `FileBrowserPanel` |
| `ImportWizardSurface` | `ImportWizardPanel` |

### C4 — list-element controllers → `Zone+ElementType_Item` (❓ confirm exact names)
Proposed mapping (an "Item" = a repeated row in a list/scroll):
| Current | → Proposed |
|---|---|
| `OutlinerItem` | `OutlinerNode_Item` |
| `RigOutlinerItem` | `OutlinerNode_Rig_Item` |
| `SceneItem` | `SceneListNode_Item` |
| `TimelineRow` | `TimelineRow_Item` |
| `BindingRow` | `BindingSectionRow_Item` |
| `BindingSectionCard` | `BindingSection_Item` |
| `LabAssetCard` | ❓ `LabAsset_Item` (not in your examples — confirm) |
> ⚠ `Name_Item` mixes PascalCase + underscore — unusual for C# type names; confirm it's the intended style. All are MonoBehaviours on prefabs (GUID-stable; file+class rename together).

### C5 — AnimatorSub* → common View pattern
| `AnimatorSubPlayhead` | `AnimatorPlayheadView` |
| `AnimatorSubRuler` | `AnimatorRulerView` |
| `AnimatorSubToolbar` | `AnimatorToolbarView` |
| `AnimatorSubTransport` | `AnimatorTransportView` |
| `AnimatorSubEmptyState` | ❓ `AnimatorEmptyStateView` (C5) **vs** `AnimatorEmptyStatePicker` (your item 12) — pick one. |

### D — junk-drawer
| `GltfModelLoader` | `GltfModelImporter` |
| `IAssetImportHandler` | `IAssetImporter` |
| `GltfImportHandler` | ❓ `GltfAssetImporter` (family rename) **vs** `ImportStrategyPicker` — recommend the family rename. |
| `ImageImportHandler` | `ImageAssetImporter` |

### C3 — DragHandle/GrabHandle
- ✅ Ignored: dead handles go to archive; `PanelGrabHandle` stays as-is.

---

## ⏸ Stage 9 — PARKED (not near-term)

Diploma-prep cleanup: a dedicated `dev-clean` mirror branch with **zero** AI traces — strip Claude-oriented
comments, "work-done" notices, scrub doc/spec folders, remove service annotations, so the project reads as
hand-authored. To be **planned and executed later**, after the Stage 4–7 changes land and stabilize.
(Also fold this into `docs/BACKLOG.md` when Stage 3 doc-consolidation runs.)

---

## Resolved (2026-06-03, second round)
1. **B2** → ✅ **Remove transform-undo entirely.** Drop `TransformCommand` calls from both `XRPromeonInteractable` and `GizmoActivator`; move/rotate/scale stop recording to `CommandStack`. The now-unused `TransformCommand.cs` then goes to `_Archive`. Transform undo to be reconsidered later.
2. **GltfImportHandler** → ✅ family rename: `IAssetImportHandler→IAssetImporter`, `GltfImportHandler→GltfAssetImporter`, `ImageImportHandler→ImageAssetImporter`.
3. **AnimatorSubEmptyState** → ✅ `AnimatorEmptyStateView` (joins the C5 View family).
4. **Detachable prefab** → ✅ remove ONLY the `SpatialPanelDetachable` (+`DetachablePanelDragHandle`) component(s); leave the link/lock/close button GameObjects (they were exclusive to this feature, referenced nowhere else — harmless dead UI, can be swept in Stage 9).

### Confirmed (2026-06-03, third round)
5. **C4** → ✅ `LabAssetCard → LabAsset_Item`; `Name_Item` underscore style adopted across all list elements.
6. **C1** → ✅ `*EntityFactory → *EntityFabricator` name-only (may grow later; unify the name now).

**Cadence:** execute phases 0→3 in order, with a STOP + per-phase clarification before each phase.

---

## EXECUTION PLAN (on branch `review-2026-06-03`, no commits)

> Run order chosen so refactors land before renames (rename the *final* structures), and lowest-risk
> first. After each phase: `refresh_unity` + `read_console` for compile errors; keep existing tests green.

### Phase 0 — finish Stage 4 (quick, low risk)
- Remove `SpatialPanelDetachable` (+`DetachablePanelDragHandle`) component from `AnimatorPanelModule.prefab` (MCP).
- Archive the 5-file detachable group → `_Archive`.
- Cut the 3 dead `PathProvider` methods + `PathProviderTests.AssetPath_…` (verify `AssetCatalogJson`/`ExportDir` have no other test refs first).

### Phase 1 — Stage 5 refactors (dependency-ordered)
1. **A1** split `AnimationAuthoring` → façade + `AnimationClipBaker` + `AnimationPlaybackSampler` + `AnimationStore`; **folds in B3** (one float `Sample`) **and B4** (`VersionedJson.Load` migrate-not-delete in `AnimationStore`). Existing `Animation*` tests are the safety net.
2. **B1** single `AppendDiamond(...)` in `RigEntityFactory`.
3. **A2** extract `GizmoHighlightPainter` + `GizmoDragSession` from `GizmoActivator`.
4. **B2** remove transform-undo (calls in `XRPromeonInteractable` + `GizmoActivator`); archive `TransformCommand.cs`. *(sequence right after A2 — same drag/commit code.)*
5. **A4** extract scene-scoped `BoneEditMode`; Inspector + Animator observe it.
6. **B6** `ReferenceEntityBuilder.RecipeFromImage(w,h)` shared with the builtin generator.

### Phase 2 — Stage 6 renames (after refactors; low→high risk)
- **Plain C# (global replace):** InteractionLayerExtensions · GizmoBoundsComputer · DirectDragStrategy(+split file) · IObjectDragStrategy · ImportedSourceProvider · SceneDirtyTracker · GltfModelImporter · IAssetImporter/GltfAssetImporter/ImageAssetImporter · *EntityFabricator ×3.
- **ScriptableObject (update `.asset` refs):** ImportedAssetShaderProfile.
- **MonoBehaviour (file+class together; prefab GUID stable):** InteractionLayerSetter · EmptySpaceClickDeselector · XrRigRecenterer (+`FindAnyObjectByType`+`FallGuard [RequireComponent]`) · VrInputFieldFocusBridge · FileBrowserPanel · ImportWizardPanel · AnimatorSub*→*View (incl. AnimatorEmptyStateView) · C4 list elements → *_Item · **GizmoDriver (last — after A2)**.
- **Keep:** RegionMember.

### Phase 3 — verify
- `run_tests` (EditMode) via MCP; `read_console`; confirm no errors / missing refs. Report back; you commit.
