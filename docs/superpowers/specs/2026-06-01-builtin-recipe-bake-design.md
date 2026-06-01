# Builtin Assets Through the Entity Pipeline (Edit-Time Recipe Bake) — Design

**Date:** 2026-06-01
**Status:** Approved (pending user review of this doc)

## Problem

Built-in assets only become selectable scene entities if their prefab was **manually
baked** with `SceneNode` + `Selectable` + `XRPromeonInteractable` + a collider on the
right interaction layer. The runtime build-once/restore-many pipeline
(`AssetEntityBuilderRegistry.RestoreAsync` → `InteractionCapability.Apply` from a recipe)
applies capability only for imported assets (`recipe != null`) and **skips built-in**
(`recipe == null`). Drop a "bare" model prefab into the built-in library SO and it spawns
but is not selectable.

Goal: built-in assets are **processed by type from the SO**, like imports — no per-prefab
manual capability baking.

## Decision Summary

| Decision | Choice |
|---|---|
| When the recipe is computed | **Edit-time bake**, stored in the SO (`BuiltinLabAsset._recipe`) |
| Bake trigger | **Buttons on a custom `BuiltinAssetLibrary` inspector** — "Bake All" + per-entry |
| Bake needs glTFast? | **No** — built-in source is a prefab (a Unity `GameObject`); bake instantiates it and reuses the synchronous measurement core |
| Existing manually-baked prefabs | User removes/recreates them as bare prefabs by hand — **no migration code** |
| Built-in without a recipe at runtime | **Not spawned** (registry throws; existing `AssetSpawner` catch logs it) — no fallback, no new logging |
| Reference (image) built-ins | **In scope** — a `Texture2D` slot on the entry; one-click editor generation creates a quad prefab + material assets, assigns `_prefab`, bakes `_recipe`. Runtime then treats it like Object/Rig (instantiate prefab + recipe). |
| `AssetType` cleanup | Out of scope (enum is already `{Object, Rig, Reference}`) |

## Architecture

### Recipe carrier

- `BuiltinLabAsset` (struct) gains a serialized field `AssetEntityRecipe _recipe` and a
  getter `Recipe`.
- `BuiltinLabAsset` also gains a serialized `Texture2D _image` slot, used only by
  `Reference` entries as the generation input (ignored for Object/Rig).
- The `Recipe` getter is **hoisted into `ILabAsset`** so consumers stop down-casting:
  - `ImportedLabAsset.Recipe` → existing recipe (unchanged).
  - `BuiltinLabAsset.Recipe` → the baked recipe (new).
  - `SavedLabAsset.Recipe` → `null` (Slice 3, not implemented).

### Shared "instance → recipe" core

The recipe-from-a-`GameObject` logic is the **single** place that decides collider +
skeleton, reused by both call sites:

- **Runtime `BuildAsync(path)`** (import): load glTF → temp `GameObject` → core → destroy.
- **Editor bake** (built-in): `Instantiate(prefab)` → core → destroy.

The core is synchronous and DI-light. It needs only:
- `IColliderStrategy.Measure(go, out kind, out center, out size)` — box by default.
- `RigDefinitionExtractor.FromSkinnedMesh(smr)` — skeleton or `null`.

Per type the core produces:
- **Object** — `selectable = true`, `interactionLayer = SceneObjects`, measured box collider.
- **Rig** — same, **plus** `recipe.rig` from the extractor; then the SO authoring fields
  `_terminalBonesAxis` / `_invertTerminalBonesAxis` are folded into
  `recipe.rig.TerminalBonesAxis` / `recipe.rig.InvertTerminalBonesAxis`. No skeleton →
  `recipe.rig` stays `null` (behaves as a static Object — graceful, same as import).
- **Reference** — does **not** measure an instance; instead it runs the image→prefab
  generation pass below and bakes the same recipe values as
  `ReferenceEntityBuilder.BuildAsync` (box collider, `spawnOffset` lift, `referenceAspect`,
  `referenceTwoSided`).

Exact factoring (a static helper vs. a method on `IAssetEntityBuilder`) is an
implementation detail for the plan; the requirement is that runtime `BuildAsync` and the
editor bake call the **same** code so the recipe can never diverge by call site.

### Reference image → prefab generation (editor-only)

Runtime `ReferenceEntityFactory` builds a throwaway in-memory quad (`new Mesh` /
`new Material`). A built-in needs **asset-backed** equivalents persisted on disk. The
generation pass, for a Reference entry with a non-null `_image`:

1. Compute `aspect = tex.width / tex.height`.
2. Ensure a single shared centered-quad mesh asset exists at
   `Assets/_App/Content/Generated/References/ReferenceQuad.mesh` (built from the same
   vertices/UVs/triangles as `ReferenceEntityFactory.BuildCenteredQuad` — exposed as a
   shared static so the geometry can never diverge).
3. Create a material asset `…/Generated/References/{Id}_Mat.mat` with the same shader
   selection as `ReferenceEntityFactory.BuildMaterial` (Reference entry shader from
   `ImportRenderProfile` if present, else `Universal Render Pipeline/Unlit`; `_Cull = Off`
   when two-sided), `mainTexture = _image`.
4. Build a `GameObject` (`MeshFilter` = shared quad, `MeshRenderer` = the material,
   `localScale = (aspect, 1, 1)`), save it as `…/Generated/References/{Id}.prefab`, and
   **assign `_prefab`** on the entry.
5. Bake `_recipe` with the Reference values (box collider unit-local, `spawnOffset` lift,
   `referenceAspect = aspect`, `referenceTwoSided = true`).

Asset paths key off the entry `Id` (stable). Re-generating overwrites in place.

### Edit-time bake (editor-only, `Assets/_App/Editor/`)

Custom inspector on `BuiltinAssetLibrary`:
- **"Bake All"** — iterates entries:
  - Object/Rig: `Instantiate(prefab)` → run the core → write `_recipe` → `SetDirty` →
    destroy temp.
  - Reference: run the image→prefab generation pass (assigns `_prefab`, writes `_recipe`).
- **Per-entry button** — same for one entry (label adapts: "Bake" for Object/Rig,
  "Generate" for Reference).
- Skips Object/Rig entries with a `null` prefab and Reference entries with a `null`
  `_image`. "Bake All" does not abort on a bad entry; it continues. No chatty warning logs
  (editor result feedback only).

### Runtime (unification)

- `AssetEntityBuilderRegistry.RestoreAsync` reads `asset.Recipe` (interface getter) instead
  of casting to `ImportedLabAsset`.
- Built-in geometry still comes from the prefab (`Instantiate(b.Prefab)`); imported still
  loads from `SourceRef`. Only the **recipe source** changes for built-in.
- `ReferenceEntityBuilder.RestoreAsync` gains a built-in branch: instead of throwing, it
  `Instantiate`s the generated prefab (like Object/Rig). The imported branch (file load via
  the factory) is unchanged.
- `InteractionCapability.Apply` now runs for built-in too (recipe present). It is
  **idempotent** (`InteractionCapability.cs:18` skips if `XRPromeonInteractable` already
  present), so a leftover hand-baked prefab is never double-processed.
- **Rig** reads axis / invert / bone names from `recipe.rig` (like import), not from the SO
  fields directly.
- **No recipe (un-baked built-in) → not spawned:** the registry throws
  `NotSupportedException`. The existing `try/catch` in `AssetSpawner.SpawnCoreAsync` logs
  it; no new logging is added. (Plan must confirm the scene-load path,
  `SceneGraph.OnSceneOpenedAsync`, tolerates the throw without crashing the load.)

## Data Flow

```
EDIT TIME:
  Object/Rig: prefab → Instantiate → [shared core: Measure collider (+ extract skeleton,
              fold SO axis)] → recipe → write _recipe (SetDirty)
  Reference:  _image → [generation: shared quad mesh asset + material asset + prefab,
              localScale=(aspect,1,1)] → assign _prefab → bake recipe → write _recipe

RUNTIME (spawn / scene-load):
  spawn request → Registry.RestoreAsync(asset)
    recipe = asset.Recipe
    recipe == null && Builtin → throw (AssetSpawner catch logs)
    go = builder.RestoreAsync(...)        # builtin: Instantiate(prefab); rig: + BuildProxyRig from recipe.rig
    recipe != null → InteractionCapability.Apply(go, recipe...)   # idempotent
```

## Error Handling

- **Bake:** Object/Rig `null` prefab → skip entry, continue. Reference `null` `_image` →
  skip. Rig without a skeleton → `recipe.rig == null` (static-object behavior). "Bake All"
  never aborts on one bad entry.
- **Runtime:** un-baked built-in → registry throws → not spawned; existing `AssetSpawner`
  catch logs. No fallback path, no added warnings.

## Testing (EditMode; core is testable without the editor assembly)

1. `BuiltinLabAsset` round-trips its `AssetEntityRecipe` through `JsonUtility`
   (field serializes/deserializes).
2. Shared core, **Object**: a `GameObject` with a mesh → recipe with a `Box` collider sized
   to bounds, `selectable == true`, `interactionLayer == SceneObjects`.
3. Shared core, **Rig**: a `GameObject` with a `SkinnedMeshRenderer` → `recipe.rig`
   populated; given SO axis/invert inputs, they appear in `recipe.rig.TerminalBonesAxis` /
   `recipe.rig.InvertTerminalBonesAxis`.
4. `Registry.RestoreAsync`: built-in with `recipe != null` → capability applied
   (`XRPromeonInteractable` present after). With an `XRPromeonInteractable` already on the
   object → no duplication (idempotency).
5. `Registry.RestoreAsync`: built-in with `recipe == null` → throws.
6. Shared centered-quad mesh: `ReferenceEntityFactory.BuildCenteredQuad` (now a shared
   static) returns the expected vertices/UVs/triangles — guards the runtime-vs-generated
   geometry match. (Editor generation that writes asset files is verified manually in-VR,
   not unit-tested.)

Baseline stays at the 6 known pre-existing EditMode failures (PathProvider ×4 Windows `\`,
RingRotateStrategy ×2).

## Out of Scope / Future

- `AssetType` rudiment cleanup (enum already minimal).

## Affected Files (indicative; finalized in the plan)

- `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs` — add `Recipe` getter.
- `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` — `_recipe` field + getter;
  `_image` (`Texture2D`) slot + getter.
- `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs` — `Recipe => null`.
- `Assets/_App/Scripts/AssetBrowser/AssetEntityBuilderRegistry.cs` — read `asset.Recipe`;
  throw for un-baked built-in.
- `Assets/_App/Scripts/AssetBrowser/ObjectEntityBuilder.cs` / `RigEntityBuilder.cs` —
  extract/reuse the shared instance→recipe core.
- `Assets/_App/Scripts/AssetBrowser/ReferenceEntityBuilder.cs` — built-in branch
  (`Instantiate` generated prefab).
- `Assets/_App/Scripts/AssetBrowser/ReferenceEntityFactory.cs` — expose `BuildCenteredQuad`
  (and material config) as shared statics for editor reuse.
- `Assets/_App/Editor/` — `BuiltinAssetLibrary` custom inspector + bake + Reference
  image→prefab generation.
- `Assets/_App/Content/Generated/References/` — generated `ReferenceQuad.mesh`, per-entry
  `{Id}_Mat.mat` + `{Id}.prefab`.
- `Assets/_App/Tests/AssetBrowser/` — tests above.
- `Content/ScriptableObjects/Libraries/DefaultBuiltinAssetLibrary.asset` — re-baked recipes
  (after the user recreates bare prefabs).
</content>
</invoke>
