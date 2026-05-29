# Docs & Planning-Archive Cleanup Implementation Plan (Plan D of scene/scope redesign)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (this is doc work — inline execution fits better than per-task subagents). Checkbox steps.
>
> **Parent spec:** `docs/superpowers/specs/2026-05-29-scene-scope-lifecycle-redesign-design.md` (§4). **Run LAST**, after Plans A, B, C have landed and been verified — the docs must describe the final state.
>
> **Git note:** the user (Promokot) commits manually. No auto-commit.
>
> **Unity note:** doc/markdown edits only — no compile/tests needed. The one code-adjacent action (verifying `FeatureLifetimeScope` truly has no code) is a read-only grep.

**Goal:** Bring the project's authoritative docs in line with the implemented architecture (single + DDOL loading, `SceneContext`, `ModeOrchestrator` + `ISceneTransition`, no `FeatureLifetimeScope`), and move implemented/ superseded planning docs into an archive so the active `specs/`+`plans/` folders show only live work.

**Architecture:** Pure documentation + file moves. No runtime change.

---

## File Structure

- **Modify** `CLAUDE.md` (repo root) — scope table, app-modes, cross-subsystem/data-flow.
- **Modify** `Assets/_App/Documentation/conventions.md`, `Assets/_App/Documentation/architecture_context.md` — `FeatureLifetimeScope` + scope/loading model.
- **Modify** `.planning/docs/ARCHITECTURE.md` — scene loading + scopes.
- **Create** `docs/superpowers/_archive/README.md` and move implemented specs/plans under `docs/superpowers/_archive/{specs,plans}/`.
- **Modify** `docs/superpowers/specs/2026-05-20-scene-loading-isolation.md` — prepend a "Superseded by" banner before archiving.

---

### Task 1: Confirm `FeatureLifetimeScope` is docs-only

- [ ] **Step 1:** `Grep FeatureLifetimeScope` across `Assets/` (code). Expected: matches ONLY in `Assets/_App/Documentation/*` (and CLAUDE.md / .planning), never in a `.cs` file. This confirms removal is a docs-only edit. If a `.cs` match appears, STOP and report (the plan's premise changed).

---

### Task 2: Update `CLAUDE.md`

- [ ] **Step 1:** In the **VContainer Scope Hierarchy** table, remove the `FeatureLifetimeScope` row. Update the `RootLifetimeScope` row to mention it is `DontDestroyOnLoad` (lives under `PersistentRoot`) and now also owns `SceneContext`, `ModeOrchestrator`, `ISceneTransition`/`SceneTransitionRunner`. Update the `SceneLifetimeScope` description to note the per-mode scope is the loaded scene's own `LifetimeScope` (MainMenu/VrEditing/Sandbox) and that it binds `SceneContext` via `SceneContextBinder`.
- [ ] **Step 2:** Replace the sentence stating `FeatureLifetimeScope` is created/disposed by `ModeOrchestrator` on mode transitions with: mode transitions are single-scene loads (`LoadSceneMode.Single`) behind a VR fade; `ModeOrchestrator` is pure policy delegating to `ISceneTransition`; the persistent infra is `DontDestroyOnLoad`.
- [ ] **Step 3:** In the **Cross-Subsystem Communication** / events list, add `SceneContextChangedEvent` (DI-lifecycle: scene services bound/unbound) and note `ModeChangedEvent` now fires after the new scene has loaded.
- [ ] **Step 4: Checkpoint (user commits)** — `docs: update CLAUDE.md to single+DDOL + SceneContext model; drop FeatureLifetimeScope`

---

### Task 3: Update the other architecture docs

- [ ] **Step 1:** `Assets/_App/Documentation/conventions.md` — remove/replace `FeatureLifetimeScope` references; align the scope description with Task 2.
- [ ] **Step 2:** `Assets/_App/Documentation/architecture_context.md` — same; describe single+DDOL loading and `SceneContext`.
- [ ] **Step 3:** `.planning/docs/ARCHITECTURE.md` — update the scene-loading + scopes section to single+DDOL + `SceneContext` + `ModeOrchestrator`/`ISceneTransition`.
- [ ] **Step 4: Checkpoint (user commits)** — `docs: align conventions/architecture docs with scene-scope redesign`

---

### Task 4: Archive implemented & superseded planning docs

- [ ] **Step 1:** Create `docs/superpowers/_archive/specs/` and `docs/superpowers/_archive/plans/` and a `docs/superpowers/_archive/README.md` index (one line per archived doc: title — status: implemented/superseded — date).
- [ ] **Step 2:** Prepend to `docs/superpowers/specs/2026-05-20-scene-loading-isolation.md` a top banner: `> SUPERSEDED by docs/superpowers/plans/2026-05-30-scene-loading-single-ddol.md (single-scene loading replaces the additive + SetActiveScene workaround).` Then move it (and its plan `docs/superpowers/plans/2026-05-20-scene-loading-isolation.md`) into `_archive/`.
- [ ] **Step 3:** Move the following IMPLEMENTED spec+plan pairs into `_archive/` (verify each is actually shipped before moving — if any is partial, leave it):
  - `2026-05-29-spatialui-region-model` (+ `2026-05-29-spatialui-region-prefab-verification`)
  - the navbar series (`2026-05-17-navbar-panel-system`, `2026-05-18-navbar-exclusive-groups`)
  - `2026-05-16-userpanel-filebrowser-fixes`, `2026-05-16-asset-browser-spawn-filebrowser`
  - `2026-05-17-scene-objects-selection-outliner`, `2026-05-16-double-userpanel-cursor-fix`
  - `2026-05-21-player-anchor-fall-guard`
  - the completed scene/scope set once shipped: `2026-05-29-scene-scope-lifecycle-redesign-design` + plans `2026-05-30-scene-context-foundation`, `2026-05-30-scene-context-consumer-migration`, `2026-05-30-scene-loading-single-ddol`, and this Plan D itself (archive last).
- [ ] **Step 4:** Use Glob to confirm each moved file is gone from the active folder and present in `_archive/` (MCP/move return strings are unreliable — verify by listing). Update `.planning/docs/FILES.md`/`STRUCTURE.md` if they index these paths.
- [ ] **Step 5: Checkpoint (user commits)** — `docs: archive implemented/superseded specs and plans`

---

## Self-Review

**Spec coverage (§4):** `FeatureLifetimeScope` removed from docs after confirming no code (Tasks 1-3) ✓; CLAUDE.md + conventions + architecture_context + .planning ARCHITECTURE updated to the single+DDOL + SceneContext model (Tasks 2-3) ✓; implemented/superseded specs+plans archived with a README index and the isolation spec explicitly marked superseded (Task 4) ✓; `[RegionDBG]` debug-log removal is intentionally NOT here — it's grouped with the other dead-code hygiene in the separate Project Cleanup plan.

**Placeholder scan:** none — each step names the exact doc + the exact change. Doc prose is left to the executor to phrase (these are markdown edits, not code), with the required content specified.

**Ordering:** Task 4 archives the scene/scope set "once shipped" — so Plan D's own archival step runs only after C is verified and these docs updated. Run Plan D last in the milestone.

---

## End of scene/scope redesign plan set (A → B → C → D).
Remaining standalone plans (separate tracks): Gizmo, Outline see-through, Outliner scroll, Project cleanup / PanelRegistry.
