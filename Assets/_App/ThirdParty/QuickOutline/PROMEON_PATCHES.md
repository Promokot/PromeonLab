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
