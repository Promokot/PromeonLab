# Outline See-Through Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (shader/material + vendored-patch work with heavy in-VR verification — inline execution with checkpoints fits better than per-task subagents). Checkbox steps.
>
> **Root-cause reference:** `docs/superpowers/investigations/2026-05-30-outline-see-through.md` (read it; this plan is the actionable breakdown).
>
> **Git note:** user (Promokot) commits manually — no auto-commit.
> **Unity note:** controller compiles/inspects via MCP; **[MANUAL EDITOR]** steps (shader files, URP renderer inspection, material assignment) need the Editor. Heavy verification is **in-VR by the user**.
> **Vendored-patch caveat:** QuickOutline lives in `Assets/_App/ThirdParty/QuickOutline/`; reimport overwrites edits (there is already an `isReadable` guard patch). All vendored edits here MUST be recorded in the QuickOutline patch note so they can be re-applied.

**Goal:** Fix (Bug 2) overlapping see-through outlines clipping each other — caused by a single globally-shared stencil `Ref 1` in both QuickOutline shaders — by making the stencil ref per-outline-instance; and (Bug 2.1) bone see-through outline only appearing after a click — by forcing `UpdateMaterialProperties` on enable.

**Architecture:** Parameterise the stencil `Ref` as a shader property in forked copies of the two QuickOutline shaders (reimport-proof), assign a unique ref per active `Outline` instance, and fix the bone-toggle path app-side (re-assign `OutlineMode` to force the ZTest update) so the `BumpOutlineNextFrame`/self-assign hacks can be deleted.

**Tech Stack:** Unity 6000.3.7f1, URP, ShaderLab stencil, QuickOutline (vendored).

---

## Task 0 (SPIKE): URP stencil-ref budget

The per-outline ref pool must not collide with stencil bits/values URP already uses. The investigation flagged this as an open question needing runtime inspection.

- [ ] **Step 1 [MANUAL EDITOR]:** Open the active URP Renderer asset (ProjectSettings → Graphics / the URP asset → Renderer). Note any stencil usage (render features, decals, SSAO, `_StencilRef` overrides). Determine a safe value range for outline refs (e.g. 200–250) that nothing else writes/tests.
- [ ] **Step 2:** Record the chosen safe range in this plan + the QuickOutline patch note. If URP consumes the whole stencil byte unpredictably, fall back to a small recycling pool keyed on screen-space non-overlap (documented in the report's open questions).

---

## Task 1: Fork the two outline shaders with a `_StencilRef` property

- [ ] **Step 1 [MANUAL EDITOR / file]:** Copy `Assets/_App/ThirdParty/QuickOutline/Resources/OutlineMask.shader` and `OutlineFill.shader` into `Assets/_App/Content/Shaders/` (forked copies). Give each fork a distinct shader name (e.g. `PromeonLab/OutlineMask`, `PromeonLab/OutlineFill`) so they don't clash with the vendored ones.
- [ ] **Step 2:** In both forks add a property `_StencilRef("Stencil Ref", Float) = 1` and replace the hardcoded stencil block:
  - Mask (was `Stencil { Ref 1 Pass Replace }`, `OutlineMask.shader:27-30`) → `Stencil { Ref [_StencilRef] Pass Replace }`.
  - Fill (was `Stencil { Ref 1 Comp NotEqual }`, `OutlineFill.shader:32-35`) → `Stencil { Ref [_StencilRef] Comp NotEqual }`.
  Keep everything else identical (queues `Transparent+100/+110`, `ZWrite Off`, `ZTest [_ZTest]`, `ColorMask 0` on mask, the normal-extrude fill).
- [ ] **Step 3 (controller): refresh assets;** confirm the two forked shaders compile (no shader errors in console).
- [ ] **Step 4: Checkpoint (user commits)** — `feat(outline): fork QuickOutline shaders with per-instance _StencilRef`

---

## Task 2: Assign per-instance stencil ref in `Outline.cs` (vendored patch)

- [ ] **Step 1 [vendored patch]:** In `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs`, point the mask/fill material creation at the forked shaders (`Shader.Find("PromeonLab/OutlineMask")` / `"PromeonLab/OutlineFill"`) instead of the originals (find where `outlineMaskMaterial`/`outlineFillMaterial` are created from `Shader.Find(...)`).
- [ ] **Step 2:** Add a per-instance stencil ref. A static cyclic allocator within the chosen safe range (Task 0), e.g.:
```csharp
private static int _nextStencilRef = STENCIL_MIN; // STENCIL_MIN..STENCIL_MAX from Task 0
private int _stencilRef;
```
Assign `_stencilRef` once (in `Awake`), cycling within range. In `UpdateMaterialProperties` (`Outline.cs:302-338`) set it on BOTH materials (same value, so mask `Replace` and fill `NotEqual` use the same ref):
```csharp
outlineMaskMaterial.SetFloat("_StencilRef", _stencilRef);
outlineFillMaterial.SetFloat("_StencilRef", _stencilRef);
```
- [ ] **Step 3:** Record this edit (forked-shader names + `_StencilRef` allocation) in the QuickOutline patch note alongside the `isReadable` guard, so reimport can re-apply.
- [ ] **Step 4 (controller): compile.** No `CS` errors.
- [ ] **Step 5: Checkpoint (user commits)** — `feat(outline): per-instance stencil ref (fixes overlapping see-through clipping)`

---

## Task 3: Bug 2.1 — force ZTest update on bone-outline enable (app-side, reimport-safe)

- [ ] **Step 1:** In `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs`, in `SetBonesInteractive` right AFTER `outline.enabled = true` (`:124-125`), force the mode setter so `UpdateMaterialProperties` runs next `Update`:
```csharp
outline.enabled = true;
outline.OutlineMode = Outline.Mode.SilhouetteOnly; // setter sets needsUpdate=true → ZTest applied immediately
```
- [ ] **Step 2:** Delete the band-aids now made redundant: the `BumpOutlineNextFrame` coroutine + its start call (`:142-157`) and the `mr.sharedMaterial = mr.sharedMaterial` self-assign (`:132-133`). (Per the report these are unreliable and unnecessary once the mode is re-asserted.)
- [ ] **Step 3 (controller): compile.** No `CS` errors.
- [ ] **Step 4: Checkpoint (user commits)** — `fix(rig): bone see-through outline appears on Show Bones without a click`

---

## Task 4: Verify in VR (controller + user)

Follow the investigation's "Verification steps" §:
- [ ] Single outline unchanged (select one object → normal outline).
- [ ] **Bug 2:** select A, then enable bones / select overlapping B → B's see-through outline renders fully through A's silhouette; move head, overlap stays complete.
- [ ] **Gizmo through object:** with an outlined object occluding the gizmo, the gizmo's occluded outline shows through.
- [ ] **Bug 2.1:** "Show Bones" → see-through bone outline appears the SAME frame, no click; toggle off/on repeatedly — appears every time.
- [ ] **Combined:** skinned mesh selected + show bones → every bone silhouette visible through the mesh, including overlap regions.
- [ ] Confirm removing the hacks didn't regress.
- [ ] **Checkpoint (user commits)** — `test(outline): verify see-through outlines + bone toggle in VR`

---

## Self-Review

**Coverage:** Bug 2 (per-instance stencil ref via forked shaders + Outline.cs allocation) → Tasks 1-2; Bug 2.1 (mode re-assert on enable, remove hacks) → Task 3; URP stencil-range risk handled by the Task 0 spike. **Placeholders:** the stencil range is a spike output (Task 0), not a placeholder; shader fork content references the exact lines to change. **Risk:** vendored `Outline.cs` patch is reimport-fragile → documented in the patch note (Task 2 Step 3); single-pass-instanced behavior + simultaneous-outline count are open questions carried from the report (Task 0 / verification covers them). **Cross-track:** this fixes the gizmo-through-object symptom too (shared technique) — the gizmo *highlight* (3.1) is a separate plan.
