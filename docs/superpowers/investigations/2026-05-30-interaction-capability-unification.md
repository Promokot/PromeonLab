# Interaction Capability Unification — Investigation

Date: 2026-05-30
Status: Research only (no code changed)
Related: `2026-05-30-gizmo.md`, `2026-05-30-outline-see-through.md`, `2026-05-30-project-review.md`

## Summary

Interaction capabilities are attached today **ad-hoc and bundled**: every interactive object gets a `Selectable` (visual/selection link) plus an `XRPromeonInteractable` (a monolithic XRI state machine that bundles tap-select + hold-trigger-rotate + grip-move into one class). There is no data-driven, per-object description of "this object is movable but not rotatable", no scale in the direct-hand path (scale exists only inside the gizmo), and the attach step is hand-written separately in each authoring path (rig proxy build does `AddComponent<Selectable>()` + `AddComponent<XRPromeonInteractable>()` per bone; the asset-import path does its own equivalent).

The user's analogy is `PromeonProxyRigBuilder`: it reads structure (bones, from a `SkinnedMeshRenderer` / `RigDefinition`) and **applies** components onto a target via a builder. A unified capability system should mirror that: a **declarative capability descriptor (ScriptableObject `*Profile`)** + a **single builder/factory that applies capability components**, callable from rig-build, asset-import, and a future import wizard alike.

**Recommendation:** composable capability MonoBehaviours (`Movable` / `Rotatable` / `Scalable`, alongside the existing `Selectable`) selected by a declarative `InteractionProfile` ScriptableObject and applied by one composer factory — the structural mirror of `RigDefinition` + `PromeonProxyRigBuilder`. `XRPromeonInteractable` stays as the slim XRI bridge but delegates "what can I do" to the capability components present.

---

## Current State (file:line — all verified)

### The monolith: `XRPromeonInteractable`
`Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs`
- Extends `XRBaseInteractable` (line 8); one internal `enum State { Idle, TriggerPressed, TriggerRotate, GripMove }` (line 22) and one `ProcessInteractable` switch (lines 78-143) implement **all** interaction modes in a single class.
- DI: `[Inject] Construct(ISelectionManager, GizmoController)` (lines 56-61) — the interactable depends directly on selection and the gizmo subsystem.
- XRI built-in select flow disabled: `IsSelectableBy(...) => false` (line 65); input read directly off `NearFarInteractor.activateInput`/`selectInput` (lines 87, 95, 104, 119, 132).
- **Gesture → behaviour mapping is hardcoded here:**
  - tap trigger (press+release within `_tapWindow`, line 10) → `_selectionManager.Select(node.NodeId)` (line 108).
  - hold trigger past `_tapWindow` → `TriggerRotate` (lines 111-115), applied by `ApplyRotate()` (lines 219-224).
  - grip while already selected → `GripMove` (lines 95-100), applied by `ApplyMove()` (lines 212-217).
- **Manipulation requires the object to already be selected:** `IsObjectSelected()` (lines 184-188) gates grip-move (line 95) and entry to rotate (line 111). There is no separate per-capability enable.
- **Release commits through the command system:** end-rotate (line 125) and end-move (line 138) call `_gizmoController.CommitTransform(transform, pos, rot, scl)` — so the final transform IS undoable. The *in-progress* drag mutates the transform directly via `_dragStrategy.Apply` (lines 216, 223).
- Collider-ownership logic (lines 38-54, `_includeChildColliders` line 15, `RegisterColliders` lines 30-36) deliberately overrides XRI's child-collider auto-discovery so nested rig proxies don't double-register. Any capability-attach factory must reproduce this policy.

### Already-extracted piece: `Selectable`
`Assets/_App/Scripts/VrInteraction/Selectable.cs`
- A thin component: caches the `SceneNode` (line 13), exposes `NodeId`/`Node` (lines 8-9), and drives the selection outline via `SetVisualState(SelectionVisual)` (lines 16-30), lazily adding a QuickOutline `Outline` (lines 32-35).
- This is **not** a capability gate — it is the selection/visual link. Naming-wise `Selectable` is taken by a visual component, which matters for any new capability scheme (a "can be selected" capability would need a different name, or this one would need repurposing).

### Drag strategy (existing seam, no scale)
`Assets/_App/Scripts/VrInteraction/IDragStrategy.cs`
- `enum DragMode { PositionOnly, RotationOnly }` (line 3) — **no scale mode**.
- `IDragStrategy.Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)` (lines 5-8); `SingleDragStrategy` (lines 10-17) sets `position` or `rotation`. Scale absent from the direct-hand path entirely.

### Selection + command plumbing
`Assets/_App/Scripts/SceneComposition/SelectionManager.cs` — single-selection: `SelectedNodeId` (line 9), `Select(string)` (line 16) publishes `SelectionChangedEvent` (line 20) on the `EventBus`. Implements `ISelectionManager`, `IStartable`, `IDisposable`.
`Assets/_App/Scripts/SceneComposition/TransformCommand.cs` — `ICommand` capturing old pos/rot/scale in ctor (lines 13-22), `Execute`/`Undo` (lines 24-36). The single reversible-transform command.
`Assets/_App/Scripts/VrInteraction/GizmoController.cs` — DI class (`IStartable`/`IDisposable`), tracks selection (lines 19-28), and `CommitTransform(...)` (lines 30-38) wraps a `TransformCommand` and calls `_commands.Execute(cmd)`. Both the direct-hand path and the gizmo commit through this one method. (`CommitMove` convenience at lines 40-41.)
`Assets/_App/Scripts/SceneComposition/SceneNode.cs` — node data: `NodeId`/`AssetRef`/`DisplayName`/`IsVisible` and notably **`IsLocked` (line 15) with `SetLocked` (line 38)** — an existing, capability-adjacent flag that nothing in the interaction path currently reads.

### The second manipulation surface: the gizmo
`Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs`
- DI-injected (`Construct`, lines 32-49); subscribes to `GizmoToolsPanelOpened/Closed`, `GizmoModeChanged`, `SelectionChanged` events (lines 43-46).
- Owns a full drag system: `OnHandleGrabbed/Dragged/Released` (lines 155-227), resolving a strategy per handle via `ResolveStrategy` (lines 257-267) → `AxisMoveStrategy` / `AxisScaleStrategy` / `UniformScaleStrategy` / `RingRotateStrategy` (under `Gizmo/Strategies/`, `IGizmoDragStrategy`).
- On release it restores the original transform then calls `_gizmoController.CommitTransform(...)` (line 219) so the gizmo edit is one undoable command.
- **So move/rotate/scale already exist fully — but inside the gizmo, behind a *different* strategy interface (`IGizmoDragStrategy`) than the direct-hand path (`IDragStrategy`).** Scale is gizmo-only.

### The analogy to mirror: `PromeonProxyRigBuilder`
`Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs`
- A `MonoBehaviour` builder that walks bone transforms (`BuildProxyHierarchy` line 236, `BuildProxyNode` line 264) and **applies** per-bone structure: mesh+outline (line 295), collider (line 296), `SceneNode` + `BoneSceneNodeMarker` (lines 299-301), then **the interaction attach**:
  - `proxyGo.AddComponent<Selectable>();` (line 305)
  - `proxyGo.AddComponent<XRPromeonInteractable>();` (line 306)
  with the comment (lines 303-304) noting DI is wired by `IObjectResolver.InjectGameObject` at spawn and colliders auto-discover in `XRPromeonInteractable.Awake`.
- `RigDefinition.cs` is the declarative data side: `[Serializable]` with `SchemaVersion = 1` (line 7), `AssetId`, `List<BoneRecord> Bones`, `List<IkChainRecord> IkChains` (lines 7-10).
- **Lines 305-306 are exactly the ad-hoc capability attach the user wants unified** — and the same two-line pattern is what the asset-import path repeats independently.

---

## Capabilities Implicit Today (catalogue)

| Capability | Where wired (verified) | Gesture | Undoable on commit? | Per-object toggle? |
|---|---|---|---|---|
| Select (tap) | `XRPromeonInteractable.cs:108` -> `SelectionManager.Select` | tap trigger | n/a | No (present whenever interactable is) |
| Move | `XRPromeonInteractable.cs:95-100,212-217` + `SingleDragStrategy` (PositionOnly) | hold grip (while selected) | Yes — `GizmoController.CommitTransform` on release (`:138`) | No — only the selection prerequisite |
| Rotate | `XRPromeonInteractable.cs:111-115,219-224` + `SingleDragStrategy` (RotationOnly) | hold trigger past `_tapWindow` | Yes — `CommitTransform` (`:125`) | No — only the selection prerequisite |
| Scale (direct hand) | — | — | — | **Does not exist** |
| Gizmo move/rotate/scale | `GizmoActivator.cs:257-267` + `Gizmo/Strategies/*` | gizmo handle drag | Yes — `CommitTransform` (`:219`) | implicit via gizmo panel/mode |
| Bone interaction | attached via `PromeonProxyRigBuilder.cs:305-306` (same `Selectable`+monolith) | same as move/rotate | same | No |
| "Locked" (latent) | `SceneNode.IsLocked` (`SceneNode.cs:15`) / `SetLocked` | — | — | flag exists but **unread by interaction code** |

Key inconsistencies surfaced:
1. One component (`XRPromeonInteractable`) grants select+move+rotate together — no granularity.
2. Scale lives only in the gizmo; the direct-hand path has no scale mode (`DragMode` enum, `IDragStrategy.cs:3`).
3. Two manipulation surfaces with two strategy interfaces (`IDragStrategy` vs `IGizmoDragStrategy`) that both ultimately funnel through `GizmoController.CommitTransform`.
4. The capability attach is duplicated per authoring path (`PromeonProxyRigBuilder.cs:305-306` and the import path).
5. `SceneNode.IsLocked` exists but is ignored — a half-started capability notion.

---

## Design Options

### Option A — Composable capability MonoBehaviours + declarative profile (RigBuilder mirror)
Add thin per-capability components: `Movable`, `Rotatable`, `Scalable` (names avoid forbidden suffixes `Manager`/`Handler`/`Controller`/etc.; `Selectable` already exists as the visual link, so a "can-select" gate would be a flag on the profile or a distinct name). `XRPromeonInteractable` keeps its input state machine but, instead of the hardcoded `IsObjectSelected()`-gated `ApplyMove`/`ApplyRotate`, it **queries which capability components are present** and only enters those states. Scale becomes a real direct-hand mode once `Scalable` + a scale gesture/strategy exist.

A declarative `InteractionProfile : ScriptableObject` (mirrors `RigDefinition`) lists which capabilities + tuning a profile grants. A composer factory (mirrors `PromeonProxyRigBuilder` + its per-node attach) exposes `Apply(GameObject target, InteractionProfile profile)`: it is the single place that adds collider (honoring the policy at `XRPromeonInteractable.cs:38-54`) + `XRPromeonInteractable` + the requested capability components. `PromeonProxyRigBuilder.cs:305-306` and the import path both call it instead of hand-wiring.

- **Fits conventions:** ScriptableObject-for-config with sanctioned `Profile` suffix; DI factory (no `FindObjectOfType` — components are already DI-injected via `IObjectResolver.InjectGameObject`, per the comment at `PromeonProxyRigBuilder.cs:303`); one-type-per-file. Capability components hold behaviour, not data, so no "MonoBehaviour as data container" violation. Cross-subsystem comms stay event-driven.
- **Tradeoffs:** several small components per object; the bridge does `GetComponent` per capability (cheap, cache on hover/lock). Most Unity-idiomatic and inspector-discoverable.
- **Migration cost:** Moderate. Split `ApplyMove`/`ApplyRotate` + their states out of the monolith into `Movable`/`Rotatable`; replace the implicit "selected => can manipulate" rule (`:95`,`:111`) with capability presence (optionally still requiring selection). Decide how direct-hand and gizmo share a scale strategy.

### Option B — Single descriptor component, data-driven flags
One binder MonoBehaviour referencing an `InteractionProfile` ScriptableObject with bool/enum flags (`CanSelect`, `CanMove`, `CanRotate`, `CanScale`, + tuning). `XRPromeonInteractable` keeps its state machine and self-gates each transition on the flags; the gizmo reads `profile.CanScale` etc. `SceneNode.IsLocked` could fold into this.

- **Fits conventions:** same ScriptableObject + DI fit; fewer components per object; a wizard assigns one asset reference.
- **Tradeoffs:** does not decompose the monolith — it parameterizes it. Adding a capability still edits the monolith + the flag set + both surfaces. Risks a "settings bag." Least composable.
- **Migration cost:** Lowest — promote the selection-gate into profile flags. But preserves current coupling and keeps scale special-cased.

### Option C — Capability interfaces resolved via DI / registry
Define `IMovable`, `IRotatable`, `IScalable` (and reuse selection); objects implement them. A scene-scoped capability registry (node-id -> capability set) is populated at attach and queried by both the direct-hand path and the gizmo, converging the two surfaces on one source of truth and potentially unifying `IDragStrategy` + `IGizmoDragStrategy`. Could emit a `CapabilitiesChangedEvent`.

- **Fits conventions:** strongest DI alignment; unifies the two manipulation surfaces; interfaces are the project's preferred boundary mechanism.
- **Tradeoffs:** most abstract; a runtime registry risks "mutable runtime state in a service" — must be scene-scoped and disposed like `SelectionManager`/`GizmoController`. Heaviest to stand up for only 3-4 capabilities.
- **Migration cost:** Highest. Best layered on top of A once many consumers exist.

---

## Recommendation

Adopt **Option A** (composable capability components + `InteractionProfile` ScriptableObject + one composer factory), and define lightweight **capability interfaces from Option C** (`IMovable`/`IRotatable`/`IScalable`) as the seam the bridge and gizmo both query.

Rationale:
1. **Direct structural mirror of the requested RigBuilder analogy:** `RigDefinition -> InteractionProfile`; `PromeonProxyRigBuilder` per-node attach (`:305-306`) -> `composer.Apply(target, profile)`. One concept applied identically at rig-build, asset-import, and the future wizard.
2. **Removes the "one component grants everything" limitation** in `XRPromeonInteractable` (select+rotate+move bundled at `:108`/`:111`/`:95`) and gives **direct-hand scale** a home, which today is gizmo-only (`IDragStrategy.cs:3` has no scale).
3. **Both surfaces already converge on `GizmoController.CommitTransform`** (`XRPromeonInteractable.cs:125,138` and `GizmoActivator.cs:219`), so routing capabilities through the same commit keeps undo consistent with zero new command types.
4. **Lowest-friction given existing structure:** `Selectable` is already split out (`Selectable.cs`), `SceneNode.IsLocked` already hints at a capability flag, and DI-on-spawn is already the norm (`PromeonProxyRigBuilder.cs:303`). The composer slots into the existing scene `LifetimeScope` next to `SelectionManager`/`GizmoController`.
5. Interfaces (vs concrete `GetComponent<Movable>`) let the gizmo and direct-hand path ask the same question, buying Option C's convergence without a registry until needed.

Naming caution: `Selectable` is taken by the visual/selection-link component (`Selectable.cs`), so the capability set should be `Movable`/`Rotatable`/`Scalable` with "selectable" expressed as a profile flag (or `Selectable` repurposed) — decide explicitly to avoid a collision.

---

## Import-Wizard Fit

The composer factory is the single funnel all authoring paths share:
1. The import wizard (editor or in-VR) presents the imported asset and lets the user pick an `InteractionProfile` (or auto-suggests by asset type: static prop -> select-only; prop -> select+move+rotate; mechanism -> +scale).
2. The chosen profile reference is persisted with the asset record — per CLAUDE.md the registry is `asset-catalog.json`; the profile id is a serialized, `schemaVersion`-carrying field (and `RigDefinition.cs:7` shows the project's `SchemaVersion` convention to copy), with migrations only in `StorageMigrator`.
3. At instantiation the import path calls `composer.Apply(instance, profile)` instead of repeating the `AddComponent<Selectable>()` + `AddComponent<XRPromeonInteractable>()` pattern — the same call `PromeonProxyRigBuilder` would use at `:305-306`.
4. Profiles being ScriptableObjects means the wizard only assigns an asset reference; the default profile is resolved via DI from the scene `LifetimeScope` (no `Resources.Load`, no `FindObjectOfType`).

This makes "tag an asset with capabilities" a first-class, data-driven step exactly parallel to "tag a mesh with a rig definition."

---

## Open Questions (need runtime/product decisions)

1. **Reconcile the two manipulation surfaces.** Direct-hand uses `IDragStrategy` (no scale); the gizmo uses `IGizmoDragStrategy` (`AxisMove`/`AxisScale`/`RingRotate`/`UniformScale`). Should capabilities unify these behind one strategy abstraction, or keep two that both commit via `GizmoController`?
2. **Scale via direct hand.** No scale gesture/mode exists in the direct path (`DragMode`, `IDragStrategy.cs:3`). Does `Scalable` need a new (two-handed?) gesture, or stay gizmo-only? Note no two-handed input path exists today.
3. **Per-capability enable vs selection-gating.** Today move/rotate require prior selection (`XRPromeonInteractable.cs:95,:111`). Should capabilities be independently present/absent (e.g. "selectable but locked from moving"), and should this subsume `SceneNode.IsLocked` (`SceneNode.cs:15`), which is currently unread?
4. **`Selectable` naming collision.** `Selectable` is the visual/selection-link component. Is "can be selected" a profile flag, or do we rename/repurpose `Selectable`?
5. **Default profile / back-compat.** What capabilities should an asset get before the wizard runs, and for existing prefabs/scenes already carrying bare `Selectable` + `XRPromeonInteractable`?
6. **Bone vs object profiles.** Should rig bones use the same `InteractionProfile` system (they get the monolith via `PromeonProxyRigBuilder.cs:305-306`), or a bone-specific profile?
7. **Wizard location.** Editor-time (`Assets/_App/Editor`), in-VR runtime authoring, or both? Determines whether the composer must be runtime-safe (recommended either way).
8. **Multi-select interplay.** `SelectionManager` is single-select only (`SelectionManager.cs:9`). If capabilities ever drive group manipulation, the selection model must change first — coupled but out of scope.

---

## Files verified for this report

`VrInteraction/XRPromeonInteractable.cs`, `VrInteraction/IDragStrategy.cs`, `VrInteraction/Selectable.cs`, `VrInteraction/GizmoController.cs`, `VrInteraction/Gizmo/GizmoActivator.cs`; `SceneComposition/SelectionManager.cs`, `SceneComposition/TransformCommand.cs`, `SceneComposition/SceneNode.cs`; `RigBuilder/RigDefinition.cs`, `RigBuilder/PromeonProxyRigBuilder.cs`. Directory inventories for `VrInteraction/`, `VrInteraction/Gizmo/`, `SceneComposition/`, `RigBuilder/` confirmed via Glob. Not exhaustively read (would refine details, not the conclusion): the AssetBrowser import path, `Gizmo/Strategies/*` bodies, `SceneGraph`, `Bootstrap/*LifetimeScope*`.
