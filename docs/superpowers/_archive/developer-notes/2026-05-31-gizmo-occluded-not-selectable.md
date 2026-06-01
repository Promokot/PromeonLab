# Bug: Gizmo handle not grabbable when occluded by another collider

**Date logged:** 2026-05-31
**Status:** Open. Not investigated in depth — logged for later.
**Severity:** Workflow annoyance — gizmo becomes unusable when the object sits behind/under
scene geometry (e.g. on or below the floor).

## Symptom

When a gizmo handle is visually behind an occluding collider (most common: the **floor**), the
ray cannot grab the handle. The ray "hits" the floor instead of the gizmo, so grip-down does
nothing on the gizmo. The gizmo can be seen (its see-through outline renders through the object),
but it cannot be interacted with.

## Why it happens (mechanism)

Gizmo grab does NOT use the XRI select flow. Each `GizmoHandle` decides whether it is the grab
target by asking the ray interactor for its **single nearest 3D raycast hit** and checking whether
that hit collider belongs to the handle:

- `GizmoHandle.IsPrimaryFor` → `ray.TryGetCurrent3DRaycastHit(out var hit)` then
  `colliders.Contains(hit.collider)` (`GizmoHandle.cs:134-138`).
- Same pattern for scene objects in `XRPromeonInteractable.cs:171-173`.

`XRRayInteractor` returns the **closest** collider along the ray. If the floor (or any other
collider) is closer to the controller than the gizmo handle, `hit.collider` is the floor, so
`colliders.Contains(hit.collider)` is false → the handle never enters Dragging. There is no
"ignore occluders for the gizmo" or priority concept; nearest collider wins unconditionally.

## Direction for a fix (not yet validated)

Options, roughly in order of cleanliness:

1. **Dedicated gizmo physics layer + raycast priority.** Put gizmo handle colliders on their own
   layer and make the gizmo's hit-test win over scene geometry — either a separate raycast that
   only sees the gizmo layer and is checked first, or `XRRayInteractor` hit sorting that prefers
   the gizmo layer.
2. **Per-handle "is my collider anywhere along the ray" instead of "is my collider the nearest
   hit."** Use a layer-masked `Physics.Raycast`/`RaycastAll` against only the gizmo layer to decide
   `IsPrimaryFor`, so an occluding floor on the Default layer no longer steals the hit.
3. **Render-on-top already exists for the outline; mirror it for hit-testing.** The see-through
   outline makes the gizmo *visible* through occluders (QuickOutline ZTest); the interaction layer
   should get the same "gizmo wins" treatment so visible == grabbable.

## Related

- See-through rendering / outline priority: `docs/superpowers/investigations/2026-05-30-outline-see-through.md`
- Gizmo subsystem internals: `docs/superpowers/investigations/2026-05-30-gizmo.md`
