# Audit 2026-06-01 — AssetBrowser + Import/Spawn Pipeline + Entity Builders

**Domain:** `Assets/_App/Scripts/AssetBrowser/` + entity builders/factories + import/spawn pipeline + the related SpatialUi surfaces.
**Method:** full read of all 35 `AssetBrowser/*.cs`, the two editor bake files, the SpatialUi panel/surfaces, DI registration, `PathProvider`, and all 11 listed specs/plans/reports.
**Bottom line:** Slice 1 (import + persistence + entity-builder capability) and the built-in recipe bake are **fully implemented and wired**. The architecture shipped under **renamed** types (`IAssetEntityBuilder`/`AssetEntityBuilderRegistry`) that **diverge from the names still printed in CLAUDE.md and three plans** (`IAssetSpawner`/`AssetSpawnerRegistry`). Thumbnails and the Saved-library spawn flow remain unimplemented.

---

## 1. Implemented reality

**Three `AssetSource` libraries** (`AssetSource.cs:1` — `Builtin`/`Imported`/`Saved`), each behind `IAssetLibrary` (`IAssetLibrary.cs`):
- `BuiltinAssetLibrary` — SO, read-only, `List<BuiltinLabAsset>` (`BuiltinAssetLibrary.cs:10`); Add/Remove throw.
- `ImportedAssetLibrary` — `IStartable`, loads/saves `imported-lib.json` (`ImportedAssetLibrary.cs:30-66`), library `schemaVersion = 2` (recipe-per-entry).
- `SavedAssetLibrary` — **fully implemented load/save/add/remove** (`SavedAssetLibrary.cs`), but spawn is inert (see §4).
- `AssetRegistry.Find(AssetRef)` resolves by source across all three (`AssetRegistry.cs:14-27`).

**Asset records** (pure data; `SpawnAsync` removed per the import-pipeline spec): `ILabAsset` (`ILabAsset.cs`) exposes `Id/DisplayName/Type/Source/SourceRef/Icon/Recipe`. `BuiltinLabAsset` (struct, `_prefab`+`_image`+`_recipe`+terminal-bone axis), `ImportedLabAsset` (class, `_sourceRef`+`_recipe`), `SavedLabAsset` (class, `_assetId`; `Recipe => null`).

**`AssetType = { Object, Rig, Reference }`** (`AssetType.cs:1`) — matches the taxonomy redesign (`Model→Object`, `Texture→Reference`).

**Import handlers** (`IAssetImportHandler`, keyed by extension): `GltfImportHandler` (`.glb/.gltf`, suggests `Object`, warns on external-buffer `.gltf`, `GltfImportHandler.cs:13-27`) and `ImageImportHandler` (`.png/.jpg/.jpeg`, suggests `Reference`). Both copy raw bytes via `AssetSourceStore.CopyAsync` and mint an `ImportedLabAsset`.

**Import pipeline** (`ImportPipeline.cs`, Root entry point): `FilePickedEvent` → pick handler → `ImportRequestedEvent`; `ImportConfirmedEvent` → copy source → **`IAssetEntityBuilder.BuildAsync` bakes the recipe once** → stamp wizard rig axis → `library.Add` + `SaveAsync` → `AssetImportedEvent`. Errors logged, not swallowed (`ImportPipeline.cs:86-89`).

**Wizard** (`ImportWizardSurface.cs`, `IRegionSurface`): file-name/name/type toggles + leaf-bone axis toggles (Rig); publishes `ImportConfirmedEvent`. Subscribes at DI time (panel starts inactive).

**Entity builders (build-once / restore-many)** behind `IAssetEntityBuilder` + `AssetEntityBuilderRegistry` (`AssetEntityBuilderRegistry.cs`): `ObjectEntityBuilder` (ConvexMesh collider), `RigEntityBuilder` (BoneBoxes + proxy rig, Object/ConvexMesh fallback when skeleton-less), `ReferenceEntityBuilder` (textured quad, aspect/two-sided, spawnOffset lift). Factories: `GltfModelLoader` (glTFast), `ObjectEntityFactory`, `RigEntityFactory` (builds proxy bones + selector colliders), `ReferenceEntityFactory`. Capability applied at a **single finalization point** via the static `InteractionCapability.Apply` (`InteractionCapability.cs:13`) keyed off `ColliderKind {None,Box,ConvexMesh,BoneBoxes}` (`ColliderKind.cs`).

**Spawn** (`AssetSpawner.cs`, scene-scoped in VrEditing+Sandbox): subscribes `AssetSpawnRequestedEvent` → `registry.RestoreAsync` (recipe `spawnOffset` applied once on fresh spawn) → `SceneGraph.AddNode` → `InjectGameObject`. `AssetBrowserPanel` publishes the request (camera-forward, 1.2 m, Y=0; faces player).

**Built-in recipe bake (editor)**: `BuiltinAssetLibraryEditor.cs` (custom inspector, "Bake All" + per-entry), `BuiltinRecipeBaker.cs` (reflection into struct private fields, isolated preview-scene measure), `ReferenceImagePrefabGenerator.cs` (image→shared quad mesh + per-id material + prefab under `Content/Generated/References/`). Shared `RecipeFromInstance` cores in `ObjectEntityBuilder`/`RigEntityBuilder` are reused by both runtime build and editor bake.

**Slice status:**
- **Slice 1 (import + persist + capability)** — **DONE** (shipped & VR-verified per plan headers).
- **Built-in-through-pipeline / recipe bake** — **DONE** (later addition, not an original numbered slice).
- **Slice 2 (runtime proxy rig)** — **DONE** in code (`RigEntityFactory.BuildProxyRig`, `ProxyRigRuntime`, BoneBoxes selectors) even though the import-pipeline spec marked it "BLOCKED".
- **Slice 3 (Saved library spawn)** — **NOT implemented** (records persist; no restore path).

---

## 2. Doc ↔ code matches

- **`2026-05-31-asset-import-pipeline-design.md`** — three-layer Source/Asset/Node model, glTFast-not-FBX decision, `AssetType {Object,Rig,Reference}`, import-and-spawn separated, "disappears after a session" fix, `IAssetImportHandler` keyed by extension: all match code.
- **`2026-06-01-asset-entity-builders-and-capability-design.md`** — `IAssetEntityBuilder` (exact `BuildAsync`/`RestoreAsync` signatures, `AssetEntityBuilderRegistry.cs:18-31`), `AssetEntityRecipe` (schemaVersion/type/selectable/interactionLayer/collider/reference/rig fields, `AssetEntityRecipe.cs`), build-once/restore-many principle, idempotent capability skip when `XRPromeonInteractable` present (`InteractionCapability.cs:22`), SceneNode-first ordering, rig no-bones→Object fallback (`RigEntityBuilder.cs:44-46,58-59`): all match.
- **`2026-06-01-builtin-recipe-bake-design.md`** — strongest match: `BuiltinLabAsset._recipe`+`_image`, `ILabAsset.Recipe` hoisted, `SavedLabAsset.Recipe => null`, registry throws for un-baked builtin (`AssetEntityBuilderRegistry.cs:27-29`), "Bake All"+per-entry inspector, Reference image→prefab generation, shared `BuildCenteredQuad`/`BuildMaterial` statics. All present.
- **Spawn-button plan** (`2026-05-16-asset-spawn-button.md`) — the *behavior* (camera-forward 1.2 m, Y=0, event-published, `SandboxSceneScope`, AssetSpawner scene-scoped) matches `AssetBrowserPanel.OnSpawnClicked` + scopes, despite renamed files.

---

## 3. Drift / mismatches (file:line)

1. **`IAssetSpawner` / `AssetSpawnerRegistry` no longer exist** — superseded by `IAssetEntityBuilder` / `AssetEntityBuilderRegistry`. Still named as live types in:
   - `CLAUDE.md:55` (AssetBrowser table: "per-`AssetType` spawning via `IAssetSpawner`/`AssetSpawnerRegistry`")
   - `CLAUDE.md:81` (event map: "spawns through `AssetSpawnerRegistry`")
   Grep confirms zero occurrences in `Scripts/`. **Highest-impact drift** (it's in the always-loaded project instructions).

2. **Storage path: singular vs plural + filenames.** Code (`PathProvider.cs:35,38,41`, `AssetSourceStore.cs:23`, `ILabAsset.cs:9`) uses **`asset-libraries/`** with **`imported-lib.json`** / **`saved-lib.json`** / **`asset-libraries/sources/`**. Docs say **`asset-library/`** with **`imported.json`** / **`saved.json`**:
   - `CLAUDE.md:89-92` and `:102`
   - `specs/2026-05-31-asset-import-pipeline-design.md:71-73,168-175`
   - `specs/2026-06-01-asset-entity-builders-and-capability-design.md:119,156-159`
   - all four `IAssetImportHandler`/spawner plans
   (User memory `reference_imported_lib_json_path` already reflects the real `imported-lib.json` name — so the **code** is canonical and the docs are stale.)

3. **`InteractionCapability.Attach` (spec) vs `.Apply` (code).** Spec names the method `Attach` and gives it an `IColliderStrategy` param (`asset-entity-builders…design.md:91-98,164`); code is `InteractionCapability.Apply(root, layer, colliderKind, center, size, selectable)` (`InteractionCapability.cs:13`) — flat primitives, no strategy object.

4. **`IColliderStrategy` / `BoundsBoxColliderStrategy` never shipped.** Both the entity-builders spec (`:109-113`) and the slice-1 plan (`:21-22`) introduce them; neither exists in code (grep: no files). Collider choice is hardcoded in the `RecipeFromInstance` cores and `ColliderKind` grew to `{Box,ConvexMesh,BoneBoxes}` instead of the spec's "single Box".

5. **`AssetEntityRecipe` is richer than spec'd.** Code adds `boneColliderDepth`, `spawnOffset`, and a full `RigDefinition rig` (`AssetEntityRecipe.cs:21-37`) — the spec recipe (`…design.md:65-88`) listed rig only as a Slice-2 "omitted/empty" stub. Not a defect; the spec under-describes the shipped shape.

6. **Record `Meta` field never materialized.** The import-pipeline spec repeatedly specifies the record as `{Id, DisplayName, Type, Source, SourceRef, Icon, Meta}` (`…design.md:48,193,239`). Shipped records carry no `Meta` — its role was absorbed by the per-record `AssetEntityRecipe`.

7. **`ImageImportHandler` accepts `.jpeg`** (`ImageImportHandler.cs:11`); docs/spec only enumerate `.png/.jpg`. Minor — code is a superset.

---

## 4. Planned-but-not-implemented

- **THUMBNAILS — CONFIRMED NOT IMPLEMENTED.** `grep -i thumbnail` across `Scripts/` = 0 hits. `Icon` exists on `AssetEntry`/`ILabAsset` and `LabAssetCard` binds it (`LabAssetCard.cs:44-45`), but **only `BuiltinLabAsset` can carry a `Sprite` (inspector-assigned)**; `ImportedLabAsset.Icon => null` (`ImportedLabAsset.cs:18`) and `SavedLabAsset.Icon => null` (`SavedLabAsset.cs:17`). No preview/thumbnail generation anywhere. Exactly the "later nicety" deferred in `…import-pipeline-design.md:230` ("Imported thumbnails: `Icon` may be `null` … preview generation is a later nicety"). Imported/Saved cards render with no icon.
- **Saved library spawn (Slice 3) — NOT implemented.** `SavedLabAsset.Recipe => null` (`SavedLabAsset.cs:18`) and `AssetEntityBuilderRegistry.RestoreAsync` only special-cases `null`-recipe for Builtin (throws) — a Saved asset would hit `Resolve(asset.Type).RestoreAsync` with a `null` recipe and no Builtin/SourceRef branch, i.e. unsupported. The *library persistence* (`SavedAssetLibrary`) is built and wired, but **no manual save-from-scene producer and no spawn consumer exist**. Matches `…import-pipeline-design.md:217-221` (Slice 3) and `…entity-builders…design.md:174-175` (out of scope).
- **`StorageMigrator` `Model→Object` / `Texture→Reference` migration** — specced (`…import-pipeline-design.md:83,201`) but not verified present in this domain; `AssetType {Object,Rig,Reference}` was instead re-spelled with **integer order preserved** (per the spawn-service plan §architecture), so no live migration code was needed. Flag for the StorageCore auditor.
- **Drag-and-drop spawn** (whole `2026-05-16-asset-browser-design.md` §5: `LabAssetCardDragHandler`, grab→release-outside-panel spawn) — **never implemented**; replaced by the explicit Spawn button. No `LabAssetCardDragHandler` in code.
- **`AssetPropertiesView` / per-type properties prefabs** (`2026-05-16-asset-browser-design.md` §4) — **not implemented**; `AssetBrowserPanel` shows a flat `_propertiesText` (`AssetBrowserPanel.cs:150-156`).

---

## 5. Stale-doc candidates (DO NOT delete — classification only)

| Doc | Verdict | One-line reason |
|---|---|---|
| `specs/2026-05-16-asset-browser-design.md` | **OBSOLETE** | `ILabAsset.SpawnAsync`, `AssetBrowserModule`, `_Shared/Interfaces/`, drag-and-drop, `AssetPropertiesView`, `asset-library` singular — almost nothing matches the shipped design. |
| `specs/2026-05-16-asset-browser-spawn-filebrowser-design.md` | **OBSOLETE** | `AssetBrowserModule` + `FileBrowserVrAnchor` + `Plugins/SimpleFileBrowser` prefab WorldSpace hack; `FileBrowserVrAnchor` was later deleted (now `FileBrowserSurface`/region router). |
| `plans/2026-05-16-asset-browser-spawn-filebrowser.md` | **OBSOLETE** | Implementation of the above obsolete spec (CommandStack `[Inject]` fix is the only durable bit). |
| `plans/2026-05-16-asset-spawn-button.md` | **SUPERSEDED-BY** import-pipeline + entity-builder slices | Spawn *behavior* survives, but `SpawnAsync`/`AppEvents.cs`/`Subsystems/` paths/`AssetType.Model` are gone. |
| `reports/2026-05-16-asset-browser-spawn-sfb-vr.md` | **OBSOLETE** | Reports the obsolete SFB-VR/AssetBrowserModule work; `FileBrowserVrAnchor` since removed. |
| `specs/2026-05-31-asset-import-pipeline-design.md` | **SUPERSEDED-BY** `2026-06-01-asset-entity-builders…` | Architecture correct, but `IAssetSpawner`/`ObjectSpawner`/`Meta`/`asset-library` singular renamed/dropped downstream. |
| `plans/2026-05-31-asset-spawn-service-and-persistence.md` | **SUPERSEDED-BY** entity-builders slice1 | Builds `IAssetSpawner`/`AssetSpawnerRegistry`/`ObjectSpawner`/`RigSpawner` — all renamed to `…EntityBuilder` before final. |
| `plans/2026-05-31-asset-import-gltf-and-wizard.md` | **SUPERSEDED-BY** entity-builders slice1 | Same `IAssetSpawner`/`ReferenceQuadFactory`/`asset-library` naming; wizard+glTFast landed under new types/paths. |
| `specs/2026-06-01-asset-entity-builders-and-capability-design.md` | **DONE (minor drift)** | Shipped; deltas: `Attach`→`Apply`, no `IColliderStrategy`, recipe richer, `Meta` dropped. |
| `plans/2026-06-01-asset-entity-builders-slice1.md` | **DONE (minor drift)** | Shipped; `ColliderKind {None,Box}` and `IColliderStrategy`/`BoundsBoxColliderStrategy` evolved/dropped. |
| `specs/2026-06-01-builtin-recipe-bake-design.md` | **DONE** | Closest match in the set; only the `asset-library` singular path text is stale. |
| `plans/2026-06-01-builtin-recipe-bake.md` | **DONE** | Matches `BuiltinAssetLibraryEditor`/`BuiltinRecipeBaker`/`ReferenceImagePrefabGenerator` as shipped. |

**Stale-doc candidate count: 8** (5 OBSOLETE/SUPERSEDED needing no-action-but-archival + 3 SUPERSEDED supplanted by the 2026-06-01 cycle). The 4 docs dated 2026-06-01 are current (2 with minor drift).

---

## 6. Rudimentary / dead code

- **`AssetEntry.cs` + `IAssetRegistry`'s wider scope?** `AssetEntry` (`AssetEntry.cs`) is a `[Serializable]` class with `AssetId/Type/RelativePath/DisplayName/Icon` that **no AssetBrowser code references** (the libraries hold `*LabAsset`, not `AssetEntry`). Likely a vestige of the pre-`ILabAsset` catalog model — verify no StorageCore/scene-catalog consumer before treating as dead.
- **`ColliderKind.None`** (`ColliderKind.cs:7`) is defined but never produced by any builder/recipe and `InteractionCapability.Apply` has no `None` branch (falls through to "no collider, but still adds Selectable/Interactable if selectable"). Harmless placeholder.
- **`ImportRenderProfile` Object/Rig entries** — `TryGet` is only consulted by `ReferenceEntityFactory.BuildMaterial` (`ReferenceEntityFactory.cs:60`); the comment admits Object/Rig "can opt in later" — currently dead for non-Reference types (by design, not a bug).
- **`AssetSpawner`/`CommandStack` `.AsImplementedInterfaces()`** registration (flagged as dead in the 2026-05-16 report §debt) — `AssetSpawner` does implement `IStartable`/`IDisposable` so it is **no longer** dead for AssetSpawner; the CommandStack note is out of this domain.
- **Reference builtin double-source of recipe values** — the Reference recipe constants (`h=1, gap=0.5`, box `(1,h,0.02)`, lift) are duplicated verbatim in `ReferenceEntityBuilder.BuildAsync` (`:31-47`) and `ReferenceImagePrefabGenerator.Generate` (`:61-74`). Not dead, but a divergence risk the bake-spec explicitly tried to avoid for geometry/material yet left open for the recipe numbers.

No swallowed exceptions, no `FindObjectOfType` at runtime, no singletons found in this domain. `FileBrowserVrAnchor` (referenced by obsolete docs) is correctly **absent**.
