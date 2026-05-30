# Bug: Bone outline requires click to appear (Issue #1)

**Date logged:** 2026-05-21
**Status:** RESOLVED 2026-05-31 (verified in VR). Root cause + fix below.
**Severity:** UX nuisance — does not block workflow. Mesh visibility works.

## Resolution (2026-05-31)

Root cause: `Outline.OnEnable` re-appends the mask/fill materials but never sets `needsUpdate`; only
`Awake` (runs once) and the property setters do. "Show Bones" re-ENABLES a disabled `Outline`, so
`Awake` does not run again and `UpdateMaterialProperties` (which applies the `SilhouetteOnly` ZTest)
stays stale until a property setter is poked — which historically happened on the first click
(`SelectionChangedEvent` → `ApplyBoneOutlineColors` → `OutlineColor` setter).

Fix: in `PromeonProxyRigBuilder.SetBonesInteractive`, re-assert `outline.OutlineMode =
Outline.Mode.SilhouetteOnly;` right after `outline.enabled = true`. The mode setter sets
`needsUpdate`, so `UpdateMaterialProperties` runs the same frame and the see-through rim appears with
no click. The old `BumpOutlineNextFrame` coroutine and `mr.sharedMaterial = mr.sharedMaterial`
self-assign hacks were removed (no longer needed). Part of the larger outline rework — see
`docs/superpowers/plans/2026-05-31-outline-layered-masks-and-bone-toggle.md`.

## Symptom

After spawning a rig and toggling **Show Bones**:

- Proxy bone **meshes** appear immediately ✅
- Proxy bone **outlines** (QuickOutline) do NOT appear until any subsequent click happens (anywhere — on bone, on rig, on empty space)
- Once a click fires anywhere → outlines appear for all bones

## Trigger

`PromeonProxyRigBuilder.SetBonesInteractive(true)` →
- `mr.enabled = true` for each proxy ✅ (mesh now visible)
- `outline.enabled = true` for each proxy → **outline materials added but visual does not render until click**
- `ApplyBoneOutlineColors(null)` called inline → still no visual

After click → `SelectionChangedEvent` → `PromeonProxyRigBuilder.OnSelectionChanged` → `ApplyBoneOutlineColors(selectedIds)` → outlines appear.

The click-handler does **the exact same thing** as our inline call: `outline.OutlineColor = …`. There is no functional difference in code paths. Yet only the click-triggered call produces visible outlines.

## Attempts that did NOT fix it

1. **Immediate `outline.OutlineColor = X` poke** after `outline.enabled = true` in same frame — no effect.
2. **Coroutine + `WaitForEndOfFrame`** then poke `OutlineColor` — no effect.
3. **Material deduplication** before enabling Outline (strip stale `OutlineMask*` / `OutlineFill*` from `renderer.sharedMaterials`) — no effect on this bug, but kept as defensive cleanup.
4. **Direct `ApplyBoneOutlineColors(null)` call** inline in `SetBonesInteractive` — no effect.
5. **Coroutine + 1-frame `yield return null` + force disable→enable cycle on Outline** — kept in code (`BumpOutlineNextFrame`), result inconclusive at last test. User chose to move on.

## Diagnostic state captured

Single-frame snapshot from `SetBonesInteractive(true)` for `proxy_pelvis` (representative):

| Property | Value |
|---|---|
| `activeInHierarchy` | True |
| `layer` | Default |
| `mr.enabled` | True |
| `mr.isVisible` | False (expected — `isVisible` is stale until first post-render frame) |
| `mr.forceRenderingOff` | False |
| `mr.shadowCastingMode` | On |
| `mr.bounds` | Center (2.12, 1.01, 5.48), Extents (0.09, 0.06, 0.01) |
| `sharedMaterial` | `(Mat)EmissiveWarm` |
| `sharedMesh` | True |
| `meshBounds` | Center (-0.05, 0.00, 0.00), Extents (0.06, 0.01, 0.09) |
| `proxyRoot.layer` | Default |

All standard Unity rendering preconditions met. Nothing in the renderer/material/transform state explains the deferred visibility.

## Working hypothesis

The XR render pipeline (URP + OpenXR + XRI on Quest 3) defers re-evaluating newly-enabled renderers/outline shaders until an external "frame nudge" event arrives. XR input events (click) appear to be one such trigger. The mesh layer works because `mr.sharedMaterial = mr.sharedMaterial` brute-force material reassignment forces an invalidation. The outline layer uses QuickOutline's instanced mask/fill materials with stencil-buffer interactions — they may need a stronger nudge than property setters provide.

This is a guess. Not validated.

## Workaround in code

`PromeonProxyRigBuilder.SetBonesInteractive` and `BumpOutlineNextFrame`:

- Activate proxy GO + ProxyRig root explicitly (defensive against baked-inactive state).
- Strip stale outline materials before enable.
- Brute-force `mr.sharedMaterial = mr.sharedMaterial` to nudge render pipeline (this fixes mesh).
- `ApplyBoneOutlineColors(null)` inline.
- Coroutine 1-frame-later disable→enable Outline cycle + repeat `ApplyBoneOutlineColors`.

If the bump-coroutine proves unreliable in practice, it can be removed without code regression — the mesh works without it.

## Next investigation steps (if revisited)

1. Hook a frame-by-frame logger inside `Outline.UpdateMaterialProperties` to confirm it actually runs N+1 after enable. If it doesn't run, the Outline component itself isn't ticking — investigate why Update is skipping.
2. Try forcing camera render via `Camera.allCameras[i].Render()` after enable.
3. Test on Quest 3 device (vs Editor) — if it works on device but not in Editor, it's an Editor-XR-simulator quirk and may not need fixing.
4. Try replacing QuickOutline with a render-feature-based outline implementation (URP Renderer Feature). Removes dependency on `Outline.OnEnable` material-list manipulation entirely.

## Acceptance

This bug is **explicitly accepted** as-is. The mesh is functional. The outline UX glitch ("first click reveals outlines") is mild. Moving on to higher-priority work (Issue #2: save bone positions).
