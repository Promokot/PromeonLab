# QuickOutline — local patches (re-apply after package reimport)

QuickOutline is vendored. **Reimporting the package from the Asset Store overwrites these edits.**
Re-apply them by hand if you ever reimport. All edits are in `Scripts/Outline.cs`.

## 1. `isReadable` guards in `LoadSmoothNormals` / `Bake` (older patch)
Skip meshes whose `sharedMesh.isReadable == false` before reading vertices/normals or writing UVs —
runtime/proxy meshes can be non-readable and would throw. Guards exist in both `LoadSmoothNormals`
(MeshFilter and SkinnedMeshRenderer loops) and `Bake`.

## 2. Material source via app config + lazy build (2026-05-31)
Material creation moved OUT of `Awake` (which used `Resources.Load("Materials/Outline*")`) into a lazy
builder so app code can supply materials AFTER `AddComponent` (the component is added at runtime in
several places and the source is injected post-construction).

- New API: `public void SetOutlineMaterials(Material maskSource, Material fillSource)` — stores the
  two source materials and builds/append on the spot if already enabled.
- New serialized fields: `maskMaterialSource`, `fillMaterialSource` (for prefab-authored Outlines that
  have no app code feeding them).
- `Awake` no longer creates materials. `OnEnable` calls `TryBuildMaterials()` (no-op if no sources yet)
  then `AppendMaterials()`. The setter rebuilds for runtime-added components. `Update`/`OnDisable`/
  `OnDestroy` are guarded against un-built materials.
- The two source materials are the forked shaders `PromeonLab/OutlineMask` / `PromeonLab/OutlineFill`
  (in `Assets/_App/Content/Shaders/`), wrapped by the `OutlineConfig` ScriptableObject
  (`Assets/_App/Content/ScriptableObjects/DefaultPromeonOutlineConfig.asset`). NOTE: `Outline.cs`
  must NOT reference `OutlineConfig` directly — it lives in the `_App.Runtime` assembly, which already
  references `QuickOutline`; a back-reference would be circular. That is why `Outline` takes raw
  `Material`s, and app code reads them from the SO.

## 3. Per-instance stencil ref (2026-05-31)
The forked shaders expose `_StencilRef` (the stock shaders hardcode `Stencil Ref 1`, so all outlines
shared one stencil slot and clipped each other where silhouettes overlap). `Outline` assigns a unique
ref per instance from a static cyclic allocator `1..250` (`nextStencilRef`/`stencilRef`) and writes it
to both materials in `UpdateMaterialProperties`. Range is safe because the URP renderers
(`Assets/Settings/Mobile_Renderer.asset`, `PC_Renderer.asset`) declare no renderer features and
`overrideStencilState = 0`.

## 4. `RenderPriority` (2026-05-31)
New `public int RenderPriority` (+ serialized `renderPriority`). In `UpdateMaterialProperties` it offsets
the mask/fill `renderQueue` (`3100/3110 + renderPriority * 20`) so higher-priority outlines paint on
top. Consumers: selection = 0, bones = 1, gizmo = 2.

## 5. Whole-mesh outline on multi-submesh meshes (2026-05-31)
New `EnsureMaterialPerSubmesh()` called in `Awake` BEFORE `LoadSmoothNormals()`. It pads each renderer's
`sharedMaterials` (repeating the last) up to `mesh.subMeshCount`. Reason: on assets whose mesh has more
submeshes than materials (e.g. a toilet with 1 material, 2-3 submeshes), Unity maps the appended
mask/fill material slots to the last submesh only, so the outline covered just one piece. Padding makes
`materials.Length == subMeshCount`, so `CombineSubmeshes`' guard `subMeshCount > materials.Length` no
longer bails — the all-triangles combined submesh is added and the mask/fill align to it → whole-mesh
outline. No-op when `materials.Length >= subMeshCount` (single-submesh objects unaffected); repeating an
already-clamped material is visually identical. Known limit: a NON-readable multi-submesh mesh still
can't get a combined submesh (CombineSubmeshes needs `mesh.triangles`), so it would remain partial —
such a mesh needs Read/Write enabled in its import settings.

## 6. Real-submesh tracking — fixes material overdraw on shared meshes (2026-06-03) — SUPERSEDED by #7
**Do NOT re-apply this on a reimport — it is replaced by #7.** The `realSubmeshCounts` dictionary /
`RealSubmeshCount` helper described below were REMOVED; #7's per-instance mesh clone makes them
unnecessary. Kept here only for history.

Bug: a SECOND Outline instance on the same SHARED mesh painted the last material over the whole object
("materials shift") and triggered "This renderer has more materials than the Mesh has submeshes".
Cause: `CombineSubmeshes` permanently mutates the shared mesh (`subMeshCount++` + a combined all-triangles
submesh) ONCE per mesh (guarded by the static `registeredMeshes`), but patch #5's `EnsureMaterialPerSubmesh`
had NO such guard and padded materials up to the already-inflated `subMeshCount`, duplicating the last
REAL material onto the combined whole-mesh submesh → it overdraws everything.
Fix: a static `Dictionary<Mesh,int> realSubmeshCounts` + `RealSubmeshCount(mesh)` records the pristine
(pre-combine) submesh count the first time a mesh is seen. `EnsureMaterialPerSubmesh` pads only to that
REAL count (never the combined one); `CombineSubmeshes` uses the real count and bails if `subMeshCount > real`
(already combined). Residual: the appended fill still overflows by one slot → the perf-hint warning can
appear by +1; it is inherent to QuickOutline's overflow-fill and harmless.

## 7. Per-instance clone + idempotent append — fixes flat-fill on multi-submesh (2026-06-03)
Supersedes the accumulation-prone parts of #5/#6. Two changes:
- `AppendMaterials` is now idempotent via the pure static `Outline.WithOutlineMaterials(current, mask, fill)`
  (strips any existing mask/fill before re-appending). A double-append or missed `OnDisable` can no
  longer grow the material array. Unit-tested in `Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs`.
- `CloneMultiSubmeshMeshes()` (called first in `Awake`) swaps each multi-submesh renderer's mesh for a
  per-instance `Instantiate` clone, so `CombineSubmeshes` never mutates the shared imported asset. Clones
  are tracked in `ownedMeshes` and destroyed in `OnDestroy`. Single-submesh meshes are left shared.
  The static `realSubmeshCounts` guard from #6 is removed (clones make each instance self-contained), and
  `EnsureMaterialPerSubmesh` / `CombineSubmeshes` were reverted to their plain `mesh.subMeshCount` forms
  (safe now, because the multi-submesh mesh they see is a fresh, combined-once clone).
Net: multi-submesh meshes (Chair, Toilet) no longer flat-fill on select, and material/submesh counts
stay constant across repeated selects.
