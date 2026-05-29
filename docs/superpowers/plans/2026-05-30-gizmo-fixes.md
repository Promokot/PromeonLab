# Gizmo Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (mixes code + prefab/material editing + in-VR verification). Checkbox steps.
>
> **Root-cause reference:** `docs/superpowers/investigations/2026-05-30-gizmo.md` (read it; this is the actionable breakdown).
>
> **Git note:** user commits manually — no auto-commit.
> **Unity note:** controller compiles via MCP; **[MANUAL EDITOR]** steps (materials, `Vr3D_Gizmos.prefab`, `GizmoToolsModule.prefab`, `DefaultGizmoConfig.asset`) need the Editor; behaviour verified **in VR by the user**.
>
> **Dependency note:** item 3.1 (highlight) overlaps the outline see-through plan (`2026-05-30-outline-see-through.md`) — QuickOutline appends materials to the gizmo handle renderers, which is *why* the current highlight fails. Doing the outline plan first is not required, but the base-slot-identification fix here must account for QuickOutline material order regardless.

**Goal:** (3) make the gizmo a roughly fixed size (±50%) instead of scaling to the object's bounding box; (3.1) make the grabbed handle highlight by swapping its base mesh material to yellow (currently broken because QuickOutline owns the material array); (3.2) add a 4th toolbar button toggling global vs local (bounds-center) origin.

**Tech Stack:** Unity 6000.3.7f1, VContainer, custom `EventBus`, URP materials, uGUI VR panel.

---

## Task 1 (item 3): Fixed-size ±50% gizmo

**Files:** `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoConfig.cs`, `BoundsFitter.cs`; `[MANUAL]` `DefaultGizmoConfig.asset`.

Decision: **Policy B** (gentle bounds influence, hard-clamped to ±50% — minimal diff, keeps both call sites). See report §3.

- [ ] **Step 1:** In `GizmoConfig.cs` add:
```csharp
    [SerializeField] private float _normalSize = 0.4f;
    [SerializeField, Range(0f, 0.5f)] private float _sizeLeeway = 0.5f;
    public float NormalSize => _normalSize;
    public float SizeLeeway => _sizeLeeway;
```
(Leave `_boundsCoefficient`/`_minSize`/`_maxSize` for now; they become unused-or-secondary.)
- [ ] **Step 2:** In `BoundsFitter.ComputeSize` (`BoundsFitter.cs:5-17`) change the final clamp so the result is bounded to `[NormalSize*(1-leeway), NormalSize*(1+leeway)]`. Add `normalSize`/`leeway` params (or pass the config) and clamp:
```csharp
float lo = normalSize * (1f - leeway), hi = normalSize * (1f + leeway);
return Mathf.Clamp(maxExtent * boundsCoefficient, lo, hi);
```
- [ ] **Step 3:** Update the two call sites to pass the new clamp: `GizmoActivator.cs:133-134` (spawn) and `:221-225` (post-scale refit) — feed `_config.NormalSize`, `_config.SizeLeeway`.
- [ ] **Step 4 [MANUAL EDITOR]:** In `DefaultGizmoConfig.asset` set `_normalSize` (start 0.4, the user will tune down later) and `_sizeLeeway` 0.5.
- [ ] **Step 5 (controller): compile.** No `CS`.
- [ ] **Step 6 (user, VR): verify** large vs small objects → gizmo size near-constant within ±50%; scale-drag + post-release refit still work (report §Verification 1).
- [ ] **Checkpoint (user commits)** — `feat(gizmo): fixed-size ±50% instead of bounding-box scaling`

---

## Task 2 (item 3.1): Grabbed-part yellow highlight

**Files:** `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoHierarchy.cs`; `[MANUAL]` optional `Gizmo_Yellow.mat`.

Root cause (report §3.1): `ApplyHighlight` tints `sharedMaterials[0]`, but QuickOutline appended outline materials, so slot 0 isn't the base mesh material; the tint hits an outline pass or is reverted. Fix = identify the base slot by excluding outline shaders, scope to the handle's own renderer, keep the existing grab/release wiring.

- [ ] **Step 1:** In `GizmoHierarchy.ApplyHighlight` (`:80-112`), stop assuming index 0. For the target renderer, find the first material whose `shader.name` does NOT contain `Outline` (skips `OutlineMask`/`OutlineFill`); that is the base slot. Tint/replace THAT slot; preserve all other slots (including the outline passes) unchanged. Store `{Renderer, originalSharedMaterials[], replacedIndex, tintedCloneOrMaterial}` so `ClearHighlight` (`:114-122`) restores exactly that index.
- [ ] **Step 2:** Scope to the handle's own renderer (or its direct mesh child), not a broad post-reparent `GetComponentsInChildren`. Keep taking the snapshot BEFORE `SetAsParent` (already the order at `:55-71`).
- [ ] **Step 3 (choose one):**
  - (a) **Serialized material (cleanest, "same material but yellow"):** add `[SerializeField] private Material _highlightMaterial;` and swap the base slot to it (no per-frame clone). `[MANUAL]` author `Gizmo_Yellow.mat` under `Content/Materials/Gizmo/` (copy a `Gizmo_*.mat`, set base color yellow, **matching ZTest** so it isn't see-through) and assign it on the `GizmoHierarchy` component in `Vr3D_Gizmos.prefab`.
  - (b) Keep the runtime clone but tint the correctly-identified base slot (no new asset).
  Recommend (a).
- [ ] **Step 4 (controller): compile.** No `CS`.
- [ ] **Step 5 (user, VR): verify** grab each handle (X/Y/Z move, center, rings, scale axes, uniform) → solid mesh turns yellow, reverts on release AND abort (`GizmoHandle.cs:48-55`); QuickOutline silhouette still renders (report §Verification 2). The `[GizmoActivator] OnHandleGrabbed` log confirms the grab path.
- [ ] **Open-question to confirm during impl (report Q2):** does QuickOutline rebuild `sharedMaterials` on its `Update`, reverting the swap? If so, apply the base-slot tint via `MaterialPropertyBlock` instead of array replacement. Check before finalizing.
- [ ] **Checkpoint (user commits)** — `fix(gizmo): highlight grabbed handle via base-material yellow swap`

---

## Task 3 (item 3.2): Global/Local origin toggle (4th button)

**Files:** create `GizmoOriginMode.cs`, `Events/GizmoOriginModeChangedEvent.cs`; modify `GizmoToolsPanel.cs`, `GizmoActivator.cs`, `BoundsFitter.cs`; `[MANUAL]` `GizmoToolsModule.prefab`.

- [ ] **Step 1:** Create `Assets/_App/Scripts/VrInteraction/GizmoOriginMode.cs`:
```csharp
public enum GizmoOriginMode { Global, Local }
```
And `Assets/_App/Scripts/VrInteraction/Events/GizmoOriginModeChangedEvent.cs`:
```csharp
public struct GizmoOriginModeChangedEvent { public GizmoOriginMode Mode; }
```
- [ ] **Step 2:** `BoundsFitter` — expose the bounds CENTER (it already computes the combined bounds at `:10-12`; currently only `.extents` used). Add a method or out-param returning `bounds.center` (world space) for the target.
- [ ] **Step 3:** `GizmoToolsPanel.cs` — add `[SerializeField] private Button _originButton;` (+ optional `_originActiveIndicator`), a `private GizmoOriginMode _currentOrigin = GizmoOriginMode.Global;`. In `Awake` add `_originButton.onClick.AddListener(ToggleOrigin)` (mirror `:21-26`). Add `ToggleOrigin()` that flips `_currentOrigin`, updates its indicator, and publishes `GizmoOriginModeChangedEvent { Mode = _currentOrigin }` (mirror `SelectMode` `:52-57`). Include `_originButton` in `SetButtonsInteractable` (`:69-74`). It is a TOGGLE, separate from the mutually-exclusive `_current` mode set.
- [ ] **Step 4:** `GizmoActivator.cs` — add `private GizmoOriginMode _originMode = GizmoOriginMode.Global;`. Subscribe to `GizmoOriginModeChangedEvent` in `Construct` (`:43-46`) and unsubscribe in `OnDestroy` (`:53-57`). Add `OnOriginModeChanged(e)` → set `_originMode`; if not dragging, re-place the instance (or `Despawn()/Spawn()`). Add a `PivotPosition()` helper: `Global` → `_target.position` (current); `Local` → world bounds-center from Step 2. Use it at the two position sets: `Spawn` (`:127`) and non-drag `LateUpdate` (`:75`).
  - **Scope note (report §3.2.3 + Q4):** v1 moves only the gizmo's VISUAL origin to the bounds center; drag math keeps pivoting on `target.position`. Full local-pivot rotation/scale (threading a pivot through `IGizmoDragStrategy.BeginDrag`) is a deferred follow-up — note it, don't build it now.
- [ ] **Step 5 [MANUAL EDITOR]:** In `GizmoToolsModule.prefab`, duplicate an existing button GO (e.g. `GizmoScaleBtn`) under the "Buttons Raw" `HorizontalLayoutGroup`, set its Label to "Global"/"Local", assign it to `GizmoToolsPanel._originButton` (+ indicator). Widen the panel `m_SizeDelta`/row if four buttons need room. **First confirm** the prefab-root `TransformGizmoHierarchyController` (report Q6) is dead/legacy before restructuring.
- [ ] **Step 6 (controller): compile.** No `CS`.
- [ ] **Step 7 (user, VR): verify** with an off-center mesh: Global = transform origin, Local = renderer bounds center; toggling mid-selection re-places without re-select; button disables during drag (report §Verification 3).
- [ ] **Checkpoint (user commits)** — `feat(gizmo): global/local origin toggle button`

---

## Self-Review

**Coverage:** 3 (sizing, Policy B clamp in `BoundsFitter` + `GizmoConfig` fields) → Task 1; 3.1 (base-slot yellow swap skipping outline shaders, own-renderer scope) → Task 2; 3.2 (`GizmoOriginMode` + event + 4th toggle button + bounds-center pivot, visual-only v1) → Task 3. **Placeholders:** none — exact files/lines/fields given; the two open questions (QuickOutline material rebuild Q2; local-pivot-during-drag Q4) are explicitly carried as confirm-during-impl / deferred, not silent gaps. **Type consistency:** `GizmoOriginMode`/`GizmoOriginModeChangedEvent` mirror the existing `GizmoMode`/`GizmoModeChangedEvent` pattern; `GizmoConfig.NormalSize`/`SizeLeeway` consumed by `BoundsFitter` + `GizmoActivator` call sites. **Cross-track:** 3.1 depends on understanding QuickOutline material order — coordinate with the outline plan.
