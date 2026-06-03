# Stage 8.1 — Review Summary & Manual-Review Checklist

**Date:** 2026-06-03 · **Branch:** `review-2026-06-03` (off `dev`; `main`/`dev` untouched) · **No commits made.**

This is the synthesis of the six analysis reports (`01`–`06`) plus the record of what was actually
executed. **Stage 8.2 is a hard PAUSE** — nothing below the "Awaiting your decision" line has been
applied to the project. Read this, then tell me which items to proceed with.

---

## A. What was actually DONE (executed this run)

| # | Action | Result |
|---|--------|--------|
| 1 | Created working branch `review-2026-06-03` from `dev`. | `main`/`dev` untouched. |
| 2 | Created `docs/review-2026-06-03/` and wrote 6 analysis reports + this summary. | All additive. |
| 3 | **Stage 4:** archived 8 dead/stub scripts → `Assets/_App/Scripts/_Archive/` via Unity AssetDatabase (GUIDs preserved). | Recompiled clean — **no missing refs, no errors.** |

Archived: `ErrorHandling.cs`, `ErrorLevel.cs`, `ErrorOccurredEvent.cs`, `PanelDragHandle.cs`,
`RigSerializer.cs`, `AssetCatalogData.cs`, `ExportPipeline.cs`, `InputBindings.cs`. (Details + evidence in `03`.)

**Headline finding (good news):** the architecture is *genuinely well-built*. Report `01` confirms
`CLAUDE.md` matches the real code on scope hierarchy, the `ModeOrchestrator` event ordering, schema
versions, the `SceneContext` façade, the ZIP exporter, and every subsystem responsibility. **Zero
`Strictly Forbidden` convention violations** in runtime code (all 28 `Find*` calls are inside the
sanctioned bootstrap shim). The problems below are tidy-up and polish, not structural rot.

---

## B. AWAITING YOUR DECISION (manual review before further modification)

Grouped by stage. Each item: what / why / risk / my recommendation. Nothing here is applied yet.

### Stage 3 — Centralize unimplemented features & cut from main docs  *(report `02`)*
- **26 unimplemented/aspirational items** found (NLA/master timeline, real FBX export, IK solver,
  `ErrorDispatcher`, Saved-library spawn / Slice 3, Redo, …), each with exact doc location.
- **Proposal:** make `docs/BACKLOG.md` the single home; cut the "planned/not-yet" sentences out of
  `CLAUDE.md` and the `audit-2026-06-01` files. Report `02` section (C) has the exact quotes + locations.
- **Risk:** edits to the authoritative `CLAUDE.md`. → *Recommend I do this after your OK.*

### Stage 5 — Responsibility / duplication refactors  *(report `04`)* — **proposals only**
High-severity (most likely to cause real animation/interaction artifacts):
1. `AnimationAuthoring` god class (6 concerns in one 26 KB file) → split into baker / sampler / store.
2. **Two sampling paths that must agree but are duplicated** (`SampleContainerAt` vs `ApplyFrame`) →
   pose "jumps" on play/scrub boundary. Collapse to one float-param method.
3. Duplicated bone-diamond mesh data (`BuildOrientedDiamondMesh` vs `BuildCombinedDiamondMesh`).
4. Two transform-commit conventions on one undo stack (`XRPromeonInteractable` vs `GizmoActivator`).
5. `GizmoActivator` god class → extract highlight painter + drag session.
- **Risk:** real refactors touching live animation/gizmo code. → *Plan carefully, one at a time, after the pause.*

### Stage 6 — Renames  *(report `05`)* — **proposals only**
- Misleading: `GizmoActivator` → `GizmoDriver`/`GizmoPresenter` (⚠ on XR Rig prefab — riskiest);
  `BoundsFitter` → `GizmoBoundsComputer`.
- Vocabulary inconsistency clusters: `*Factory` vs `*Builder` vs `*Loader`; `*Surface` vs `*Panel`;
  list-element `*Item`/`*Row`/`*Card`.
- **Risk:** renaming MonoBehaviours on prefabs (filename+class must change together; downstream refs update).
  GUID is stable so prefab links survive, but it needs care. → *After the pause.*

### Stage 7 — Tests  *(report `06`)* — **proposals only**
- **0 fully orphaned test files** (healthy). One dead-method test to cut: `PathProviderTests.AssetPath_ReturnsExpectedPath`.
- 15 brittle tests; biggest cluster = exact `schemaVersion ==` pins (break on any schema bump) → assert `>=`.

### Stage 4 leftovers — needs a prefab edit first
- **Detachable-panel group (5 files) HELD BACK:** archiving blocked by `SpatialPanelDetachable`
  (inert) still on `AnimatorPanelModule.prefab`. → *Remove the component from the prefab via MCP, then I archive all 5.*
- **Empty folders:** `ErrorHandling/` + `ErrorHandling/Events/` are now empty (whole subsystem archived).
  → *Remove the folders + drop the `ErrorHandling` row from CLAUDE.md's subsystem table?*
- **3 dead `PathProvider` methods** (`AssetCatalogJson`, `ExportDir`, `AssetPath`) — in-place removal,
  not file archiving (PathProvider is core/live). Paired with the test cut above.

### Doc hygiene (found along the way)
- `Assets/_App/Documentation/*.md` (`architecture_context.md`, `STRUCTURE.md`, `conventions.md`) are
  **stale** — they still reference the deleted `PanelRegistry`/`UiPanelOrchestrator`. Separate from `docs/`.
- `CLAUDE.md` self-contradiction: implies a `StorageMigrator` rule but migration is inline in
  `SceneSerializer.Deserialize` (no such class). Minor wording fix.
- `audit-2026-06-01/*.md` partly stale: still marks thumbnails + per-object Loop as "not implemented"
  (both shipped 2026-06-02); BACKLOG's "gizmo moves don't go through CommandStack" is also now resolved.

---

## C. Suggested order after you resume

1. Decide Stage 3 doc consolidation (lowest risk, high tidiness payoff).
2. Approve the detachable-prefab cleanup + empty-folder removal (finishes Stage 4 cleanly).
3. Approve Stage 7 test fixes (small, safe).
4. Pick which Stage 5 refactors to schedule (start with the dual-sampling-path fix — real artifact risk).
5. Pick which Stage 6 renames to do (defer prefab-attached MonoBehaviour renames until last).
6. **Stage 9** (diploma-prep cleanup → `dev-clean` branch) — plan only; I'll draft it when you say go.

> Reminder: per your rules I will not commit. When you're happy, you commit `review-2026-06-03` yourself.
