# Outline see-through rendering investigation (2026-05-30)

READ-ONLY investigation. No project files were modified. Covers Bug 2 (overlapping see-through
outlines) and Bug 2.1 (bone outline needs a click). Companion to the gizmo investigation at
`docs/superpowers/investigations/2026-05-30-gizmo.md` — the "gizmo visible through an occluding
object" symptom is the *same rendering technique* analysed here.

## Summary

QuickOutline draws every outline with **a single, globally-shared stencil reference value of 1**,
hardcoded in both shader passes (`OutlineMask.shader:28` and `OutlineFill.shader:33`). The mask pass
writes `Ref 1` into the stencil buffer; the fill pass only draws where `stencil != 1`. This design
works for ONE outlined object at a time but collides as soon as two or more outlines overlap on
screen, because all instances share stencil value 1 — object A's mask suppresses object B's fill in
the overlap region. That is **Bug 2**.

**Bug 2.1** is a render-pipeline registration/ordering problem: when "Show Bones" enables many
`Outline` components in the same frame, the freshly appended outline materials (added in
`Outline.OnEnable`, `Outline.cs:102-113`) and the `needsUpdate`-driven ZTest setup
(`Outline.Update` → `UpdateMaterialProperties`, `Outline.cs:132-138, 302-338`) do not become visible
under URP+XR until an unrelated event (a click) forces the pipeline to re-evaluate the renderers. The
team already added two workaround hacks for this (`BumpOutlineNextFrame` and a self-assign material
nudge) in `PromeonProxyRigBuilder.cs`, which confirms the symptom but does not reliably fix it.

---

## Bug 2 — overlapping see-through outlines

### How QuickOutline renders an outline

For each `Outline` component, `Outline.OnEnable` appends two material instances to every child
renderer's material list (`Outline.cs:102-113`):

- **OutlineMask** (`OutlineMask.shader`) — queue `Transparent+100` (3100), `ColorMask 0` (writes no
  color), `ZWrite Off`, configurable `ZTest [_ZTest]`, and:
  ```
  Stencil { Ref 1  Pass Replace }      // OutlineMask.shader:27-30
  ```
  It stamps the object's silhouette into the stencil buffer as value **1**.

- **OutlineFill** (`OutlineFill.shader`) — queue `Transparent+110` (3110), extrudes vertices along
  smooth normals to make the rim, `ZWrite Off`, configurable `ZTest [_ZTest]`, and:
  ```
  Stencil { Ref 1  Comp NotEqual }     // OutlineFill.shader:32-35
  ```
  It draws the rim color **only where the stencil is NOT 1** — i.e. outside the silhouette the mask
  just stamped. This is what carves the fill into a thin border instead of flooding the whole object.

The "see-through" behaviour comes from the per-mode ZTest assignment in
`Outline.UpdateMaterialProperties` (`Outline.cs:302-338`):

- `OutlineHidden` → fill `ZTest Greater` (only the occluded part of the rim draws).
- `SilhouetteOnly` → mask `ZTest LessEqual`, fill `ZTest Greater`, width 0 (the bones use this:
  `PromeonProxyRigBuilder.cs:391`).
- `OutlineAll` → both `ZTest Always` (the selection outline uses the default mode = `OutlineAll`,
  since `Selectable` never sets `OutlineMode`, see `Selectable.cs:24-28`).

### Root cause: one global stencil ref shared by every outline

The stencil `Ref` is the literal constant `1` in BOTH shaders — it is **not** driven by a material
property and is **never varied per instance**. `Outline.cs` only ever sets `_ZTest`, `_OutlineColor`,
and `_OutlineWidth` (`Outline.cs:302-337`); it never touches a stencil ref. So every outlined object
in the scene writes and tests against the same stencil slot.

The stencil buffer is screen-space and shared across all draws in the frame. Consider two outlines
overlapping on screen (the exact scenario in the bug report — a selected object plus a second
object's see-through outline behind/through it):

1. Object A renders its OutlineMask: stencil = 1 over A's silhouette pixels.
2. Object B renders its OutlineMask: stencil = 1 over B's silhouette pixels (same value).
3. Object B's OutlineFill runs `Comp NotEqual` against `Ref 1`. In any pixel already stamped by
   **A's** mask (because A overlaps B on screen), the stencil is already 1, so B's fill is rejected
   there — even though that pixel belongs to B's rim, not A's.

Net effect: wherever a previously-selected object's silhouette overlaps a second object's outline
rim, the second outline is clipped away. Because the meshes draw in `Transparent+100/+110` with
`ZWrite Off`, draw order between the two instances is queue/sorting-dependent and not deterministic,
so the artifact is order-sensitive and looks like "the second see-through outline doesn't render once
something is already selected." This matches the report precisely:

- **Bones through mesh:** the skinned mesh selection outline (or the body) stamps stencil 1 over the
  torso region; the bone's `SilhouetteOnly` fill (ZTest Greater) is then `NotEqual 1`-rejected in
  that region → bones stop showing through where they overlap the already-outlined mesh.
- **Gizmo behind object:** the selected object's `OutlineAll` mask stamps 1; the gizmo's occluded
  outline fill is rejected in the overlap → same root cause as the gizmo investigation.

A secondary aggravator: there is a single shared `registeredMeshes` static set and the smooth-normal
UV3 bake mutates the *shared* mesh (`Outline.cs:201, 215, 234, 244`). This is a correctness/perf
concern for shared meshes but is NOT the cause of the overlap clipping — the stencil collision is.

### Proposed fix (Bug 2)

Make the stencil reference **per-outline** so two overlapping outlines never collide:

1. Promote the stencil `Ref` to a shader property in both shaders, e.g.
   ```
   Properties { ... _StencilRef("Stencil Ref", Float) = 1 ... }
   Stencil { Ref [_StencilRef] Pass Replace }   // mask
   Stencil { Ref [_StencilRef] Comp NotEqual }  // fill — same value, NotEqual
   ```
   The mask and fill of *the same* Outline instance must share the same ref; different instances must
   use different refs.
2. Assign a unique small ref per active Outline instance at enable time (e.g. a pool of values
   2..255, or cycle through a few values — overlaps only need *different* refs, not globally unique).
   Set it in `UpdateMaterialProperties`/`OnEnable` via
   `outlineMaskMaterial.SetFloat("_StencilRef", ref)` and the same on the fill material.
   - Reserve a couple of values or read-mask bits if other URP features (e.g. URP's
     `_StencilRef`/decals/SSAO) use the stencil buffer — check the URP renderer's stencil usage
     before picking the range. (Open question below.)
3. Alternative / additional: drop `ZWrite Off`-only transparent ordering ambiguity by ensuring the
   two instances' masks cannot bleed into each other — the per-ref fix already achieves this, but
   verifying render-queue separation (selected vs. bones vs. gizmo) helps determinism.

Because edits to the vendored shaders/`Outline.cs` are overwritten on package reimport (per the
QuickOutline-patched memory note), the safest place for the per-ref logic is the app layer if
possible, but the stencil ref MUST be parameterised in the shader — so this patch has to live in the
vendored shaders and be re-applied on reimport (document it alongside the existing `isReadable`
guard). Consider forking the two shaders into `Assets/_App/Content/Shaders/` and pointing the
material instances at the fork to make the patch reimport-proof.

---

## Bug 2.1 — bone outline needs a click

### Sequence when "Show Bones" is toggled on

`InspectorPanel.OnShowBonesToggleChanged` (`InspectorPanel.cs:247-269`) calls
`rig.SetBonesInteractive(true)` (`PromeonProxyRigBuilder.cs:94`). For every proxy bone GO this:

1. Activates the GO / ProxyRig (`:99-105`).
2. Enables the `MeshRenderer` (`:107-108`) — bones become visible immediately (matches report: mesh
   bodies appear at once).
3. Strips any leftover OutlineMask/OutlineFill materials (`:115-122`).
4. Enables the `Outline` component (`:124-125`) → `Outline.OnEnable` re-appends mask+fill materials
   (`Outline.cs:102-113`) and sets `needsUpdate = true` only in `Awake` (`Outline.cs:99`), NOT in
   `OnEnable`.
5. Self-assigns `mr.sharedMaterial = mr.sharedMaterial` (`:132-133`) as a pipeline nudge.
6. Calls `ApplyBoneOutlineColors(null)` (`:137`) which sets `OutlineColor` → that setter sets
   `needsUpdate = true` (`Outline.cs:38-39`).
7. Starts `BumpOutlineNextFrame` (`:142-157`) — a one-frame-later disable→enable cycle.

### Root cause

The bones use `OutlineMode = SilhouetteOnly` (`PromeonProxyRigBuilder.cs:391`). The *see-through*
(hidden) part of that mode depends entirely on `UpdateMaterialProperties` having run to set
`mask ZTest=LessEqual` and `fill ZTest=Greater` (`Outline.cs:332-336`). Critically:

- `Outline.OnEnable` re-appends the materials but does **not** set `needsUpdate`
  (`Outline.cs:102-113`). Only `Awake` (`:99`) and the property setters (`:31, 39, 47`) do.
- `Awake` runs once. On a re-enable (which is exactly what "Show Bones" triggers — the component was
  disabled, now enabled), `Awake` does NOT run again, so the only thing that schedules a
  `UpdateMaterialProperties` is `ApplyBoneOutlineColors` setting `OutlineColor` (`:137, 156`).

So the ZTest state CAN be stale on the freshly re-enabled material instances until `Update` consumes
`needsUpdate` (`Outline.cs:132-138`). Combined with the per-renderer material-list mutation in
`OnEnable`, URP+XR (single-pass instanced) defers re-evaluating the renderer's material set until the
pipeline is poked. The result: the bone's body (plain mesh renderer) shows immediately, but the
`SilhouetteOnly` see-through rim (which needs both the freshly-appended fill material AND the
LessEqual/Greater ZTest applied) does not appear until the next forced re-evaluation — which today
happens when the user clicks (a `SelectionChangedEvent` fires →
`PromeonProxyRigBuilder.OnSelectionChanged` → `ApplyBoneOutlineColors` → sets `OutlineColor` →
`needsUpdate` → `Update` → `UpdateMaterialProperties`, AND the click's interaction churns the
pipeline). The mesh outline (the selection outline) works fine because it is applied as a *response*
to a selection event, so its `UpdateMaterialProperties` is always driven by the same event that makes
it visible.

The existing `BumpOutlineNextFrame` (`:145-157`) and the self-assign nudge (`:132-133`) are
band-aids: they sometimes succeed but, as the memory note "bone_outline_bug" records, the bug is
still considered unsolved — confirming the nudge is unreliable. The likely reason it's unreliable:
the disable→enable in `BumpOutlineNextFrame` triggers `OnDisable`/`OnEnable` (material remove/append)
but STILL never sets `needsUpdate`, so the ZTest for `SilhouetteOnly` can remain whatever it was, and
the bump only helps when the pipeline happens to re-sort that frame.

### Proposed fix (Bug 2.1)

Primary fix — make the outline self-sufficient on enable instead of relying on external nudges:

1. In the vendored `Outline.cs`, set `needsUpdate = true` inside `OnEnable` (after re-appending the
   materials), so `UpdateMaterialProperties` always runs the frame the component is enabled and the
   `SilhouetteOnly` ZTest is applied immediately. (Reimport-overwrite caveat applies — document with
   the `isReadable` patch.)
2. App-side alternative that avoids editing the package: after `outline.enabled = true` in
   `SetBonesInteractive` (`PromeonProxyRigBuilder.cs:124-125`), explicitly re-assign the mode to
   force the setter's `needsUpdate`:
   `outline.OutlineMode = Outline.Mode.SilhouetteOnly;` — the `OutlineMode` setter sets
   `needsUpdate = true` (`Outline.cs:30-31`), guaranteeing `UpdateMaterialProperties` runs next
   `Update`. This is the cleanest reimport-safe fix and should let the team delete the
   `BumpOutlineNextFrame` coroutine and the self-assign hack.
3. Verify ordering: ensure the `Outline` component's `Update` runs at least once before the user can
   interact. Setting the mode (step 2) makes a one-frame `Update` sufficient; no click needed.

Note: once Bug 2's per-stencil-ref fix lands, the bone see-through outline will ALSO stop being
clipped by the selected mesh's outline, so both fixes are needed for "bones through mesh" to be fully
correct (2.1 makes it appear on toggle; 2 keeps it from being clipped where it overlaps the selected
mesh).

---

## Verification steps (in VR after fixing)

1. **Single outline still correct:** select one object → yellow `OutlineAll` outline renders normally
   (no regression from per-stencil-ref change).
2. **Overlapping see-through (Bug 2):** select object A (gets outline), then enable bones or select a
   second object B whose outline overlaps A on screen → B's see-through outline must render fully
   through/over A's silhouette with no clipped/missing rim in the overlap region. Move the head so
   the overlap region changes; the outline must stay complete.
3. **Gizmo through object (Bug 2):** with an object selected and outlined, move so the manipulation
   gizmo is occluded by that object → the gizmo's occluded outline must still show through. Cross-
   check against `docs/superpowers/investigations/2026-05-30-gizmo.md`.
4. **Bones on toggle (Bug 2.1):** in bone-editing mode press "Show Bones" → the see-through bone
   outline must appear in the SAME frame the bones become visible, WITHOUT any click. Toggle off/on
   repeatedly; it must appear every time.
5. **Bones through mesh (2 + 2.1 combined):** with the skinned mesh selected (mesh outlined), show
   bones → every bone's see-through silhouette must be visible through the mesh, including where bones
   overlap the mesh's outlined silhouette.
6. **No leftover hacks needed:** confirm removing `BumpOutlineNextFrame` and the
   `mr.sharedMaterial = mr.sharedMaterial` self-assign does not regress (after applying the OnEnable /
   mode-reassign fix).

## Open questions (need Unity runtime state)

- **URP stencil usage / free bit range:** does the active URP renderer (ProjectSettings/XR + URP
  renderer asset) already consume specific stencil ref values/bits (decals, SSAO, render-feature
  overrides)? The per-outline ref pool must avoid colliding with those. Needs inspection of the URP
  Renderer asset's stencil settings.
- **Single-pass-instanced behaviour:** confirm whether the deferred-until-click symptom is specific
  to single-pass-instanced stereo. If multi-pass behaves differently, the OnEnable `needsUpdate` fix
  may already be sufficient regardless of pipeline mode.
- **How many simultaneous outlines:** to size the stencil-ref pool, how many overlapping outlines can
  coexist at once (selected object + gizmo + N bones)? If N can exceed the available stencil values,
  the strategy may need ref-recycling by screen-space non-overlap rather than strict uniqueness.
- **Material instance count on bones:** verify the OnEnable material-append + the `SetBonesInteractive`
  strip logic (`PromeonProxyRigBuilder.cs:115-122`) actually prevents duplicate Outline materials
  accumulating across toggles at runtime (the strip relies on material name prefixes "OutlineMask"/
  "OutlineFill" set in `Outline.Awake`, `Outline.cs:92-93`).
