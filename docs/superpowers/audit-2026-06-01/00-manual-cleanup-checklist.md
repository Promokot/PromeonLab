# Manual Cleanup Checklist — 2026-06-01

Items the audit found dead/rudimentary but that are **GUID-risky** (MonoBehaviour/SO referenced by
scenes/prefabs) or need a **product decision**. Do these inside Unity, with a GUID-reference sweep
first (memory note: check `.meta` GUID refs before deleting a `MonoScript`). The 3 safe files
(`ConstraintFreezePosition.cs`, `AnimationPlayback.cs`, `EditorPlaceholder.cs`) and the gizmo debug
logs were **already removed** automatically.

## Needs a product decision first

- [ ] **`PanelRegistry` + `UiPanelOrchestrator` panel system** — code path is wired in both scene
      scopes but `DefaultPanelRegistry.asset` is empty → runtime no-op (region model is the live
      system). **Decide:** delete or populate. If deleting, remove together:
      `SpatialUi/PanelRegistry.cs`, `SpatialUi/UiPanelOrchestrator.cs`,
      `Content/ScriptableObjects/DefaultPanelRegistry.asset`, the `_panelRegistry` serialized field +
      `RegisterInstance`/`Register<UiPanelOrchestrator>` lines in `VrEditingSceneScope.cs` and
      `SandboxSceneScope.cs`, and (then-dead) `PanelId.cs` / `SpatialPanel.Init`/`PanelId` plumbing.
      GUID sweep mandatory (`PanelRegistry` GUID `cf920f1c0606a6c4ca8cb6082d5abf0f`).
- [ ] **Placeholder subsystem stubs** — `InputBindings/InputBindings.cs`,
      `ExportPipeline/ExportPipeline.cs`, `ErrorHandling/ErrorHandling.cs` are near-empty.
      **Decide:** keep as roadmap markers (ExportPipeline + ErrorDispatcher are real backlog items)
      or delete. Kept by default for now (see `docs/BACKLOG.md`).

## Code edits (verify, then apply in Unity with compile check)

- [ ] **`GizmoController.CommitMove` + dead `_target` field** — no callers (commits go through
      `CommitTransform`). Remove method + the `SelectionChangedEvent` sub/unsub that only feeds `_target`.
- [ ] **`TransformCommand.cs`** — implements `ICommand` but has no constructor/callers; gizmo moves
      commit through `GizmoController.CommitTransform`. Confirm whether undo of gizmo moves is intended
      (if yes, this is *unfinished wiring*, not dead code) before removing.
- [ ] **`AppMode.Debug` enum value** — declared, never used. Remove or implement the debug overlay.
- [ ] **`PanelDragHandle.cs`** — off all prefabs (replaced by `PanelGrabHandle`); still references
      `UserPanel.SetDragging`/`MoveTo` so it compiles. Confirm unused on every prefab, then delete.
- [ ] **`AnimatorSubPlayhead.SetHeight`** — dead method (layout removed the call).
- [ ] **`AssetEntry.cs`** — `[Serializable]` record not referenced by any AssetBrowser code (libraries
      hold `*LabAsset`). Verify no StorageCore/scene-catalog consumer, then treat as dead.
- [ ] **`VrInputFieldProxy`** — resolves `EventBus` via `LifetimeScope.Find<RootLifetimeScope>()`
      service-locator in `Awake`; refactor to constructor/`[Inject]` DI.

## Stale internal docs (Plan D leftover — update to current model)

- [ ] `Assets/_App/Documentation/STRUCTURE.md` — references deleted files
      (`EditorPlaceholder.cs`, `ConstraintFreezePosition.cs`) and `RigRuntime`/`PromeonProxyRigBuilder`-era tree.
- [ ] `Assets/_App/Documentation/architecture_context.md` — still names `FeatureLifetimeScope`,
      `RigRuntime`, and the forbidden-suffix vocabulary (`UiPanelManager`, `ThumbnailService`, …).
- [ ] `Assets/_App/Documentation/conventions.md` — mirror the CLAUDE.md convention rewrites
      (suffix rule, `FindObjectOfType` carve-out, `public`-field/JsonUtility note) or it will re-drift.
- [ ] `docs/developer-notes/ui-conventions.md` — §4/§6 reference removed `NavBarBinding`/`DetachablePanel`;
      banner added 2026-06-01, sections still need a rewrite to the region model.

## Keep (do NOT remove — future features / by design)

- `RigSerializer.cs` (dormant; future bake tool), `IkChainRecord` / `RigDefinition.IkChains`,
  `BoneRecord.TranslationLocked`, `ColliderKind.None` — all reserved for backlog features.
- The 28 `FindAnyObjectByType` call sites inside `LifetimeScope.Configure` — sanctioned DI-bootstrap
  shim, now explicitly allowed by the CLAUDE.md carve-out.
