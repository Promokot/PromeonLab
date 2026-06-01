# Audit 06 — Interaction Domain (VrInteraction + InputBindings + Gizmo + Outline + Selection)

**Date:** 2026-06-01
**Scope:** VrInteraction (RayInteractor model, Gizmo, masks, collider registration), InputBindings interaction surface, Outline system, Selection/Outliner.
**Method:** Read-only reconciliation of live C# against `docs/superpowers/` specs/plans/investigations + `docs/developer-notes/`.

---

## 1. Implemented reality (the LIVE model)

### 1.1 Direct-manipulation input model (LIVE)

`XRPromeonInteractable` (`Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs`) inherits
`XRBaseInteractable` purely for hover/registration; XRI standard select-flow is disabled
(`IsSelectableBy => false`, `:112`). It reads `NearFarInteractor` inputs directly in
`ProcessInteractable(Dynamic)` via a 4-state machine `{ Idle, TriggerPressed, TriggerRotate, GripMove }`
(`:27`).

LIVE mapping (`:125-190`):
| Gesture | Input | Action |
|---|---|---|
| **Tap trigger** (< `_tapWindow`=0.5s) | `activateInput` | `SelectionManager.Select(NodeId)` (`:155`) |
| **Hold trigger** (≥ tapWindow) + object already selected | `activateInput` | **Rotate** drag → `TriggerRotate` (`:158-162`, commit `:166-172`) |
| **Hold grip** + object already selected | `selectInput` | **Move** drag → `GripMove` (`:142-147`, commit `:179-185`) |

- Trigger checked before grip → wins same-frame ties (`:133-148`).
- Manipulation gated on `IsObjectSelected()` (`:231-235`); a hold on an unselected object only tap-selects.
- `_lastHovering` 1-frame jitter fallback (`:193-211`); `Lock`/`EndInteraction` for single-interactor lock (`:237-244`).
- Offsets are **local-space** to the attach transform (`:246-271`) — position swings with the ray sweep (this diverges from the spec's world-space `transform.position - attach.position`; see §3).
- Commit path goes through `GizmoController.CommitTransform` → `TransformCommand` → `CommandStack` (`GizmoController.cs:30-38`).
- `IsPrimaryFor` (`:213-229`) uses XRI nearest-hit: ray path = `ray.TryGetCurrent3DRaycastHit` + `colliders.Contains(hit.collider)`; near path = `interactablesHovered[0] == this`. **No resolver / no parallel RaycastAll** — the v1 resolver is reverted.

### 1.2 Empty-space deselect (LIVE)

`WorldClickCatcher.cs` polls both interactors each `Update`, fires on `activateInput` (trigger) edge,
early-returns if over UI (`TryGetCurrentUIRaycastResult`, `:48-49`) or if anything is hovered
(`interactablesHovered.Count > 0`, `:34`), else `SelectionManager.Select(null)` (`:36`). The
Selectable-in-hovered loop is **commented out** pending UI-guard confidence (`:27-32`).

### 1.3 Gizmo flow (LIVE)

- **Visibility:** gizmo shows ⟺ `GizmoToolsPanel` open AND a target selected (`GizmoActivator.RefreshVisibility:143-150`). Panel open/close publishes `GizmoToolsPanelOpened/ClosedEvent` (`GizmoToolsPanel.cs:40-61`); default tool = Move.
- **Activator** (`GizmoActivator.cs`) is a scene MonoBehaviour, DI-injected (`Construct:64`), subscribes to panel/mode/selection events. Spawns the prefab at fixed size (`GizmoConfig.FixedSize`, halved for bone proxies via `BoneSceneNodeMarker`, `CurrentSize:197-202`). **Bounds-fit is frozen** (commented `:163-164`).
- **Handle input:** `GizmoHandle.cs` is grip-driven (`selectInput.ReadValue() > 0.5f`, `:67`). Grab computes a **virtual hand point** = `controllerPos + forward * grabRayDistance` so wrist-twist moves the point along a sphere (`:77-84`, drag `:102-104`). State `{ Idle, Dragging }`; defensive abort on dead interactor (`:50-57`).
- **Drag is gizmo-primary:** the strategy mutates `_instance.transform`; the target follows per strategy type in `OnHandleDragged` (`:404-432`) — move syncs position, rotate syncs rotation, scale applies a ratio factor (`instance/instanceAtGrab → target = targetAtGrab*factor`). Strategies in `Gizmo/Strategies/` (`AxisMove`, `AxisScale`, `UniformScale`, `RingRotate`).
- **Commit:** on release, target is restored to original then `CommitTransform` captures the correct old-snapshot for undo (`:436-455`). `GizmoDragStarted/EndedEvent` bracket the drag (disables tool buttons, blocks undo).
- **Highlight:** per-submesh base **and** emission color captured/restored; hover darkens ×0.75, grab adopts `GizmoConfig.ActiveMaterial`/`OutlineConfig.GizmoActiveColor`; handles indexed by `GetComponentInParent<GizmoHandle>(includeInactive:true)` (`BuildParts:209-260`).

### 1.4 Outline system (LIVE)

Vendored `Outline.cs` (`Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs`), patched:
- **SO-fed lazy material build:** materials no longer `Resources.Load`-ed; `SetOutlineMaterials(mask, fill)` feeds the two forked `PromeonLab/Outline*` materials from `OutlineConfig` (`:53-59`), built lazily in `TryBuildMaterials` (`:211-227`) since the component is `AddComponent`-ed at runtime before injection.
- **Per-instance stencil ref:** `nextStencilRef` cycles 1..250 per instance; mask `Replace` + fill `NotEqual` test against THIS silhouette so overlapping outlines stop clipping (`:104-111`, applied `:418-421`). Comment confirms URP renderers use no stencil → range free.
- **Layered render priority:** `renderQueue = 3100/3110 + renderPriority*20` (`:423-425`). LIVE assignments: **selection = 0** (`Selectable.cs:36`), **bones = 1** (per OutlineConfig comments), **gizmo = 2** (`GizmoActivator.InstallHandleOutline:281`). Gizmo outline mode = `SilhouetteOnly` (`:278`).
- `EnsureMaterialPerSubmesh` + `CombineSubmeshes` cover multi-submesh meshes; `LoadSmoothNormals`/`Bake` guard `isReadable` (the documented QuickOutline patch).

`OutlineConfig` (`Data/OutlineConfig.cs`) is the single SO carrying mask/fill materials + axis/select/gizmo-active/bone colors. `Selectable.cs` drives the selection outline (enabled on Selected, width 6, priority 0); `SelectionVisualSync.cs` (scene IStartable) flips visual state across all graph nodes on `SelectionChangedEvent`.

### 1.5 Interaction context masks (LIVE — contextual, NOT resolver)

`InteractionMaskBinder.cs` (persistent, on the XR rig root, root-EventBus-driven) is the **live** disambiguation mechanism. It rewrites both hands' `SphereInteractionCaster.physicsLayerMask` + `CurveInteractionCaster.raycastMask` per context (`Apply:93-106`):
```
(_panelOpen && _hasSelection) → GizmoHandles   (gizmo modal)
_bonesMode                    → BoneProxies     (body excluded; ray passes through to bone)
otherwise                     → SceneObjects    (normal selection)
```
- UI layer is OR-ed into every mask (`_uiMask`, `:60`, `:102`) so uGUI raycasts never go dead.
- **Context reset on `ModeChangedEvent`** (`:85-91`): resets `_bonesMode/_panelOpen/_hasSelection` to false → fixes the re-entry "nothing clickable" bug (stale `GizmoHandles`/`BoneProxies` mask from prior session).
- Layers resolved by NAME via `InteractionLayers.UnityLayer` (`InteractionLayers.cs`), enum `InteractionLayer { GizmoHandles, BoneProxies, SceneObjects }` (`Data/InteractionLayer.cs`). `GameObjectInteractionLayerExtensions.SetInteractionLayer` is the single funnel; `InteractionLayerTag` is the prefab-authored variant.
- Self-tagging: `XRPromeonInteractable.ApplyInteractionLayer` (`:97-101`, default `SceneObjects`, settable to `BoneProxies` via `SetInteractionLayer`); `GizmoHandle.Awake` tags `GizmoHandles` (`:31`).

### 1.6 Collider registration timing (LIVE)

`XRPromeonInteractable.RegisterColliders` + `RefreshColliderRegistration` (`:35-62`): XRI builds its
collider→interactable map once at `OnEnable`/`RegisterInteractable` and never re-scans. The spawn
pipeline appends colliders (convex children, bone selector boxes) **after** registration, so the
binder re-registers (Unregister→Register) to rebuild the map (`:57-62`). Comment is now accurate
(spawn-time ordering, not manual rigging). `Awake` takes ownership of the collider list
(`colliders.Clear()` then `GetComponents` or `GetComponentsInChildren` per `_includeChildColliders`,
`:75-86`). `InteractionCapability.Apply` is the single entity-build funnel (Box/ConvexMesh/BoneBoxes,
idempotent, `:11-62`).

---

## 2. Doc ↔ code matches

| Doc | Code | Match |
|---|---|---|
| `specs/2026-05-31-interaction-layer-priority-design.md` (v2 contextual masks) | `InteractionMaskBinder`, `InteractionLayers`, `InteractionLayer`, `SetInteractionLayer`, `InteractionLayerTag` | ✅ Strong — v1 resolver dropped, `IsPrimaryFor` reverted to nearest-hit exactly as v2 specifies. |
| `specs/2026-06-01-interaction-context-reset-and-registration-cleanup-design.md` | `InteractionMaskBinder.OnModeChanged` reset (`:85-91`); `RefreshColliderRegistration` extraction (`:57-62`); dead RigRuntime/IkWizardPanel cluster removed | ✅ Marked "Implemented & verified"; code confirms all three parts. |
| `specs/2026-06-01-type-keyed-selection-colliders-design.md` | `ColliderKind {None,Box,ConvexMesh,BoneBoxes}`; `InteractionCapability.Apply` type-keyed (Box root collider, ConvexMesh per-renderer + register, BoneBoxes no-op) | ✅ `ColliderKind.cs` + `InteractionCapability.cs:31-62` match the design. |
| `plans/2026-05-31-outline-layered-masks-and-bone-toggle.md` | `Outline.cs` SO-fed lazy build + per-instance `_StencilRef` + `RenderPriority` renderQueue; selection=0/bones=1/gizmo=2 | ✅ All present and live. Supersedes the stencil portion of the see-through plan. |
| `developer-notes/2026-05-21-bone-outline-needs-click.md` (RESOLVED) | re-assert `OutlineMode` on enable → `needsUpdate` | ✅ Mechanism matches `Outline` setter behavior (`:27-33`). |
| Gizmo strategies (`vr-gizmo-system-design.md` §5 formulas) | `AxisMoveStrategy`, `RingRotateStrategy` etc. | ✅ Axis-projection / signed-angle math matches the spec formulas. |
| Single-select cleanup (`vr-gizmo-system-design.md` §Phase 0) | `SelectionManager.cs` (`Select(id?)`, `SelectedNodeId` only), `SelectionVisual {None,Selected}`, `Selectable` no `InSet` | ✅ Multi-select API fully removed. |

---

## 3. Drift / mismatches

1. **Input-rework spec uses multi-select API that no longer exists.**
   `specs/2026-05-18-interaction-input-rework-design.md` (and its plan) reference `SelectionManager.Toggle`, `SelectedIds.Contains`, `Clear()`, and state `GripRotate`. LIVE code is single-select (`SelectionManager.Select` / `SelectedNodeId`, `XRPromeonInteractable.cs:155,234`) and the grip state is **`GripMove`** (grip = move), with **hold-trigger = rotate** (`TriggerRotate`). The spec's mapping table (trigger-hold = move, grip-hold = rotate) is the **inverse** of the live behavior. → Spec is stale; the gizmo-system spec (§2 UX, "hold trigger = rotate, hold grip = move") is the one that matches code.

2. **Offset capture: spec says world-space, code uses local-space.**
   Spec (`interaction-input-rework-design.md` §Offsets) captures `transform.position - attach.position`. LIVE `CapturePositionOffset` uses `attach.InverseTransformPoint(transform.position)` (local-space, `XRPromeonInteractable.cs:246-251`) so the object swings with ray rotation. Intentional improvement, undocumented.

3. **`WorldClickCatcher` Selectable-in-hovered guard is commented out** (`:27-32`) and replaced by a blanket `interactablesHovered.Count > 0` early-return (`:34`). Spec shows the active Selectable loop. Minor; behavior is a superset guard.

4. **Gizmo spec's `_originalTargetCollider` (disable target collider while gizmo up) is gone.**
   `vr-gizmo-system-design.md` §Spawn caches+disables the target collider; the layer-priority plan (Task 11) removed it. LIVE `GizmoActivator` has no such field — the context mask (`GizmoHandles` only) makes it redundant. Gizmo spec is stale on this point.

5. **Gizmo handle input: spec §5 uses `selectInput.ReadWasPerformedThisFrame()` + `GetAttachTransform`.** LIVE uses raw `selectInput.ReadValue() > 0.5f` edge-tracking and a **virtual hand point** (`controllerPos + forward*dist`), not the attach transform (`GizmoHandle.cs:67-104`). Implementation evolved past the spec.

---

## 4. Planned-but-not-implemented

- **`type-keyed-selection-colliders` bone-box traversal (BoneBoxes path).** Spec/plan dated 2026-06-01, status "Approved (pending user review)". `ColliderKind.BoneBoxes` exists and `InteractionCapability` treats it as a no-op (built rig-side), but the rig-side selector-box builder (`RigEntityFactory` pure traversal, `ProxyRigRuntime._selectorColliders` + `RegisterSelectorColliders`) is referenced in code comments (`InteractionCapability.cs:8-9`) — **verify in the RigBuilder/AssetBrowser audit** whether the traversal + registration is fully wired or still partial. The Object→ConvexMesh path IS live in `InteractionCapability`.
- **Reusable `IDragStrategy` group-drag seam.** `SingleDragStrategy` is the only impl; `GroupDragStrategy` (mentioned across input-rework + gizmo specs as a future seam) is not implemented (expected — multi-grab is a non-goal).

---

## 5. Stale-doc candidates (DO NOT delete — flag only)

| Doc | Disposition | Reason |
|---|---|---|
| `plans/2026-05-31-interaction-layer-priority.md` | **SUPERSEDED-BY** `specs/...-interaction-layer-priority-design.md` v2 | Already carries an explicit SUPERSEDED banner (top of file). Describes the dead v1 `RayInteractionResolver` (no `XRRayInteractor` on the `NearFarInteractor` rig). No `RayInteractionResolver.cs` exists in code. ✅ correctly flagged. |
| `plans/2026-05-30-outline-see-through.md` | **SUPERSEDED-BY** `plans/2026-05-31-outline-layered-masks-and-bone-toggle.md` | The layered-masks plan's banner says it supersedes the stencil portion. See-through plan proposed per-category/pooled stencil + an open SPIKE; live code uses unique-per-instance stencil. |
| `specs/2026-05-18-interaction-input-rework-design.md` + `plans/2026-05-18-interaction-input-rework.md` | **OBSOLETE (partial)** | Multi-select API (`Toggle`/`SelectedIds`/`Clear`), `GripRotate` state, and inverted move/rotate mapping no longer match code (see §3.1). The component's existence + IsSelectableBy=>false + state-machine shape are still accurate; the API surface and mapping are dead. |
| `specs/2026-05-21-vr-gizmo-system-design.md` | **DONE w/ drift** | Gizmo system shipped, but the spec still documents `_originalTargetCollider` disable (removed, §3.4), `GetAttachTransform`-based handle input (replaced by virtual-hand, §3.5), and bounds-fit `FitToBounds` (frozen → `FixedSize`). Core architecture (Activator/Hierarchy/Handle/strategies/events) matches. |
| `specs/2026-05-17-scene-objects-selection-outliner-design.md` | **OBSOLETE (selection portion)** | Describes Blender-style multi-select + `Toggle`/`Clear`; single-select cleanup (gizmo Phase 0) removed it. Scene-binding/outliner portions may still hold — cross-check in SceneComposition audit. |
| `developer-notes/2026-05-31-gizmo-occluded-not-selectable.md` | **DONE (resolve banner stale)** | Logged "Open"; its Fix Option 1 (dedicated gizmo layer + ray priority) is exactly the live `InteractionMaskBinder` GizmoHandles-only context. Functionally resolved; note still says "Open. Not investigated." |
| `developer-notes/2026-05-21-bone-outline-needs-click.md` | **DONE** | Already marked RESOLVED 2026-05-31; matches live `Outline` mode re-assert. ✅ accurate. |

---

## 6. Rudimentary / dead code

- **`GizmoController.CommitMove`** (`GizmoController.cs:40-41`) — convenience wrapper; no caller found in this domain (all commits go through `CommitTransform`). Low-risk dead method.
- **Commented-out blocks (preserved per project convention):** `WorldClickCatcher.cs:27-32` (Selectable-hover guard) and `:40-47` (old `IsOverUI` via `XRRayInteractor`); `GizmoHandle.cs:68-69,91-92` (DIAGNOSTIC hold-input logs); `GizmoActivator.cs:163-164` (frozen bounds-fit). These are intentional TODO-preserve, not accidental dead code.
- **Verbose `Debug.Log` left in shipping path:** `GizmoActivator` Construct/OnPanelOpened/OnSelectionChanged/RefreshVisibility/OnHandleGrabbed (`:81,114,139,146,382`) and `GizmoHandle` GRIP DOWN/UP (`:72,95`) log every interaction. Functional but noisy for a release build.
- **`InputBindings/InputBindings.cs`** is a one-line comment stub (`:1`) — the subsystem's real content is `ControlsProfile.cs` + `Data/`. The interaction input model lives entirely in VrInteraction, not InputBindings; the InputBindings folder is the controls-binding/settings surface (out of this domain's interaction core).
- **`IDragStrategy` / `SingleDragStrategy`** (`IDragStrategy.cs`) — still used by `XRPromeonInteractable` direct-manipulation (`_dragStrategy`, `:24,263,270`); the parallel gizmo `IGizmoDragStrategy` is the separate gizmo path. Both live; not dead, but two near-parallel strategy interfaces coexist.

---

## Summary

**LIVE interaction model:** direct-input on `NearFarInteractor` — tap-trigger = select, **hold-trigger = rotate**, **hold-grip = move** (gated on prior selection); empty-trigger in space = deselect. **Gizmo:** panel-open + selection spawns a fixed-size prefab; handles are grip-driven via a virtual-hand point; gizmo is drag-primary, target follows, commit through `CommandStack`. **Outline:** vendored QuickOutline patched to SO-fed lazy materials + unique per-instance stencil ref (anti-clip) + render-queue priority layering (selection 0 < bones 1 < gizmo 2). **Disambiguation is contextual cast-masks** (`InteractionMaskBinder` rewrites both casters' physics masks per context: GizmoHandles / BoneProxies / SceneObjects + always-on UI), **reset on `ModeChangedEvent`**; the v1 priority-resolver is dead and reverted. **Collider registration** re-indexes XRI's map post-spawn via Unregister→Register.

**Dead-approach docs (flag, don't delete):** interaction-layer-priority v1 plan (RayInteractionResolver — never existed), outline-see-through plan (superseded by layered-masks), interaction-input-rework spec/plan (multi-select API + inverted mapping), vr-gizmo-system-design (target-collider-disable + attach-transform input removed), scene-objects-selection-outliner (multi-select), gizmo-occluded note (resolved by context masks).

**Planned-not-implemented:** type-keyed `BoneBoxes` selector-box traversal — verify rig-side wiring in the RigBuilder audit; `GroupDragStrategy` seam (intentional non-goal).
