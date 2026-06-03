# Test Review — 2026-06-03

Static analysis of `Assets/_App/Tests/**/*.cs`.
No Unity Test Runner was executed — all conclusions are from code reading + cross-referencing `Assets/_App/Scripts/`.

---

## Legend

| Classification | Meaning |
|---|---|
| **LIVE** | SUT exists and is referenced in production Scripts. |
| **ORPHANED** | SUT is missing, or exists but is provably dead (zero callers in Scripts outside its own definition and tests). |
| **BRITTLE** | SUT is live but the test hard-codes values that break on routine maintenance (schema version bump, SO default change, prefab structure edit, layer name change). |

---

## Master Table

| Test File | Subject(s) Under Test | SUT exists? | SUT used in prod? | Classification | Notes |
|---|---|---|---|---|---|
| `Animation/AnimationAuthoringTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Comprehensive unit coverage of container/key/clipboard/navigation API. All helper methods (`InitForTest`, `SetKeyForFrame_Test`) are `[InternalsVisibleTo]`-gated. |
| `Animation/AnimationAuthoringCompletionTests.cs` | `AnimationAuthoring`, `AnimationClock`, `PlaybackStateChangedEvent` | Yes | Yes | **LIVE** | Verifies `OnPlaybackState` callback (end-of-animation jump to first key). |
| `Animation/AnimationAuthoringCreateContainerTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests overloaded `CreateContainer(id, frames, fps)`. |
| `Animation/AnimationAuthoringDeleteKeyTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests `TracksChanged` event on track emptying. |
| `Animation/AnimationAuthoringEnsureTrackTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests `EnsureTrack` — creates empty placeholder track. |
| `Animation/AnimationAuthoringInterpolationTests.cs` | `AnimationAuthoring`, `InterpolationMode`, `AnimationCurve` | Yes | Yes | **LIVE** | Tests `ApplyInterpolation` (static) and `SetInterpolation` + event. |
| `Animation/AnimationAuthoringLiveTrackTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests `TracksChanged` event on first/subsequent key writes. |
| `Animation/AnimationAuthoringLoopTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests loop toggle, `StartLoopPlayback`, `StopLoopPlayback`, `AdvanceLoopCursor`. |
| `Animation/AnimationAuthoringLoopRefreshTests.cs` | `AnimationAuthoring`, `AnimationClock` | Yes | Yes | **LIVE** | Tests that changing interpolation while loop is playing rebuilds running clips. Requires `Tick()`. |
| `Animation/AnimationAuthoringLoopFrameTests.cs` | `AnimationAuthoring`, `LoopFrameChangedEvent` | Yes | Yes | **LIVE** | Tests `PublishLoopFrameIfChanged` emits only on integer-frame boundary. |
| `Animation/AnimationAuthoringSceneFpsTests.cs` | `AnimationAuthoring` | Yes | Yes | **LIVE** | Tests scene-wide fps (`GetSceneFps`, `SetSceneFps`). |
| `Animation/AnimationAuthoringExportTests.cs` | `AnimationAuthoring.CaptureForExport` | Yes | Yes | **LIVE** | Tests export snapshot. |
| `Animation/AnimationClipboardTests.cs` | `AnimationClipboard`, `FrameClipboard`, `FrameClipboardEntry` | Yes | Yes | **LIVE** | Simple get/set/clear clipboard tests. |
| `Animation/AnimationClockTests.cs` | `AnimationClock`, `PlaybackStateChangedEvent` | Yes | Yes | **LIVE** | Transport state machine: play/pause/stop/seek/advance/configure. |
| `Animation/AnimationDataTests.cs` | `AnimTrackData`, `SceneAnimationData`, `ActionContainer` | Yes | Yes | **BRITTLE** | Lines 83, 148, 170 assert `schemaVersion == 2` on `SceneAnimationData`. Any v2→v3 schema bump will break these tests without touching logic. See "Brittle tests" section. |
| `Animation/ActionContainerTests.cs` | `ActionContainer` | Yes | Yes | **LIVE** | Data-layer unit tests: track creation, key presence, truncation, node-id listing. |
| `Animation/AnimatorPanelConfigTests.cs` | `AnimatorPanelConfig` | Yes | Yes | **BRITTLE** | Asserts hard-coded SO default values (`KeySize=22f`, `KeySizeSelected=26f`, `MajorTickHeight=24f`, `MinorTickHeight=16f`, `RowHeight=52f`). Any designer-driven default tweak silently breaks the test. See "Brittle tests" section. |
| `AssetBrowser/AssetEntityBuilderRegistryTests.cs` | `AssetEntityBuilderRegistry`, `ImportedLabAsset`, `BuiltinLabAsset`, `InteractionCapability` | Yes | Yes | **LIVE** | Dispatch, capability application, null-recipe guard, and Builtin guard. |
| `AssetBrowser/AssetEntityRecipeColliderTests.cs` | `ColliderKind`, `AssetEntityRecipe` | Yes | Yes | **BRITTLE** | Lines 11-13 assert specific int values for `ColliderKind` enum members (1=Box, 2=ConvexMesh, 3=BoneBoxes). These are documented as "append-only contract" — breaking if any member is re-ordered or removed. **Not a flaw in the test** — the pin is intentional, but it will fail if violated. Classify as BRITTLE to make it visible for review. |
| `AssetBrowser/AssetEntityRecipeRigTests.cs` | `AssetEntityRecipe`, `RigDefinition`, `BoneRecord` | Yes | Yes | **LIVE** | JSON round-trip for rig-embedded recipe. |
| `AssetBrowser/AssetEntityRecipeTests.cs` | `AssetEntityRecipe` | Yes | Yes | **BRITTLE** | Line 28 asserts `schemaVersion == 1` on `AssetEntityRecipe`. Schema bump breaks this test. |
| `AssetBrowser/AssetSourceStoreTests.cs` | `AssetSourceStore`, `PathProvider` | Yes | Yes | **LIVE** | Integration test: copies a file and checks relative path contains asset ID + extension. |
| `AssetBrowser/AssetTypeBinaryCompatTests.cs` | `AssetType` | Yes | Yes | **BRITTLE** | Asserts Object=0, Rig=1, Reference=2. Intentional binary-compat pin (legacy data relies on this). Fails if enum is reordered. Classify BRITTLE; the pin is correct semantically. |
| `AssetBrowser/BuiltinLabAssetRecipeTests.cs` | `BuiltinLabAsset` | Yes | Yes | **BRITTLE** | Line 18 asserts `back.Recipe.schemaVersion == 1`; also the inline JSON literal carries `schemaVersion:1`. Schema bump breaks both. |
| `AssetBrowser/ImportHandlerTests.cs` | `GltfImportHandler`, `ImageImportHandler`, `AssetSourceStore` | Yes | Yes | **LIVE** | Extension matching and import integration (copies file, checks record fields). |
| `AssetBrowser/ImportedLabAssetRecipeTests.cs` | `ImportedLabAsset`, `AssetEntityRecipe` | Yes | Yes | **LIVE** | JSON round-trip for recipe nested in `ImportedLabAsset`. |
| `AssetBrowser/LabAssetThumbnailRefTests.cs` | `ImportedLabAsset`, `SavedLabAsset` | Yes | Yes | **LIVE** | Tests `ThumbnailRef` get/set/json round-trip; also verifies `SavedLabAsset.ThumbnailRef` is null. |
| `AssetBrowser/ObjectEntityBuilderTests.cs` | `ObjectEntityBuilder`, `RigEntityBuilder` | Yes | Yes | **LIVE** | `HandledType` values and `RecipeFromInstance` for object with convex-mesh. |
| `AssetBrowser/PathProviderSourceTests.cs` | `PathProvider.SourcePath` | Yes | Yes | **LIVE** | `SourcePath` is used in production (`AssetSourceStore`). |
| `AssetBrowser/ReferenceEntityBuilderTests.cs` | `ReferenceEntityBuilder` | Yes | Yes | **LIVE** | Builds a recipe from a PNG file; checks aspect ratio and collider values. |
| `AssetBrowser/ReferenceEntityFactoryQuadTests.cs` | `ReferenceEntityFactory.BuildCenteredQuad` | Yes | Yes | **LIVE** | Mesh geometry correctness of the quad used for image references. |
| `AssetBrowser/RigEntityBuilderRecipeTests.cs` | `RigEntityBuilder.RecipeFromInstance` | Yes | Yes | **LIVE** | Validates rig recipe extraction (axis, bones, collider kind). |
| `AssetBrowser/ThumbnailRendererFrameTests.cs` | `ThumbnailRenderer.FrameDistance` | Yes | Yes | **LIVE** | Pure math: bounding-sphere framing distance at a given FOV. |
| `AssetBrowser/PathProviderThumbnailTests.cs` | `PathProvider.ThumbnailPath`, `PathProvider.ThumbnailsDir`, `PathProvider.ThumbnailRelativeRef` | Yes | Yes | **LIVE** | All three thumbnail path methods are used in `ThumbnailRenderer` + `AssetBrowserPanel`. |
| `Bootstrap/SceneContextTests.cs` | `SceneContext` | Yes | Yes | **LIVE** | Bind/clear/HasScene semantics. |
| `ExportPipeline/SceneBundleTests.cs` | `SceneBundle` | Yes | Yes | **BRITTLE** | Line 33 asserts `schemaVersion == 1` on `SceneBundle`. Schema bump will break it. |
| `ExportPipeline/SceneExporterBuildTests.cs` | `SceneExporter.BuildBundle` | Yes | Yes | **LIVE** | Covers all node-type routing (Imported/Builtin/Reference), dedup, bone-poses, animation mapping, missing-source flag. |
| `ExportPipeline/SceneExporterZipTests.cs` | `SceneExporter.WriteZipBundle` | Yes | Yes | **LIVE** | Integration: writes a real zip to temp, verifies zip contents. |
| `ModeOrchestrator/ModeOrchestratorTests.cs` | `ModeOrchestrator`, `ModeTransitionGraph`, `ISceneTransition`, `ModeExitingEvent`, `ModeChangedEvent` | Yes | Yes | **LIVE** | Tests event ordering (Exiting before Changed), same-mode no-op. |
| `RigBuilder/BoneSelectorBoxPlannerTests.cs` | `BoneSelectorBoxPlanner` | Yes | Yes | **LIVE** | Depth-limited planning and subtree encapsulation. |
| `RigBuilder/ProxyRigBonePoseTests.cs` | `ProxyRigRuntime`, `BonePose` | Yes | Yes | **LIVE** | Apply/capture pose round-trip; null/unknown bone no-ops. |
| `RigBuilder/ProxyRigRuntimeTests.cs` | `ProxyRigRuntime` | Yes | Yes | **LIVE** | `SetBonesInteractive` / `SetVisualsEnabled`. |
| `RigBuilder/RigDefinitionExtractorTests.cs` | `RigDefinitionExtractor` | Yes | Yes | **LIVE** | Null/no-bones guard and bone-name extraction from `SkinnedMeshRenderer`. |
| `RigBuilder/RigEntityFactoryBuildProxyTests.cs` | `RigEntityFactory`, `ProxyRigRuntime`, `BoneSceneNodeMarker`, `BoneFollower`, `TerminalBoneAxis` | Yes | Yes | **LIVE** | Full proxy rig build: hierarchy, markers, followers, leaf-axis orientation. |
| `SceneComposition/AssetRegistryTests.cs` | `AssetRegistry`, `BuiltinAssetLibrary` | Yes | Yes | **LIVE** | Unknown-source and missing-ID null returns. |
| `SceneComposition/CommandStackTests.cs` | `CommandStack` | Yes | Yes | **LIVE** | Undo, empty-stack guard, max-history eviction. |
| `SceneComposition/SceneGraphTests.cs` | `SceneGraph` | Yes | Yes | **BRITTLE** | Line 14 asserts `snap.SchemaVersion == 3`. Schema bump breaks this. Core logic is sound but the pin is fragile. |
| `SceneComposition/SceneNodeTests.cs` | `SceneNode` | Yes | Yes | **LIVE** | `Init` and `SetNodeId`. |
| `SceneComposition/SelectionManagerTests.cs` | `SelectionManager`, `SelectionChangedEvent` | Yes | Yes | **LIVE** | Single-select API, dedup, null clear. |
| `SpatialUi/AnimatorPanelModulePrefabTests.cs` | `TimelineRow` (prefab), `AnimatorPanelModule.prefab` | Yes | Yes | **BRITTLE** | All three tests load prefab via `AssetDatabase`. Paths (`AnimatorPanelModule.prefab`, `TimelineRow.prefab`) currently exist but any rename/restructure silently makes the test fail with a null rather than a compile error. The `LanesContent` hierarchy path is embedded as a long string — one structural prefab edit breaks it. |
| `SpatialUi/ExportModulePrefabTests.cs` | `ExportPanel`, `ExportModule.prefab`, `RegionMember` | Yes | Yes | **BRITTLE** | Same prefab-load fragility as above. Both tests assert on specific child hierarchy paths and serialized field names (`_fileNameInput`, etc.). Any prefab rename or field rename breaks the test. |
| `SpatialUi/NavBarConfigExporterRegionTests.cs` | `NavBarConfig` SO asset | Yes | Yes | **BRITTLE** | Loads the first `NavBarConfig` asset found via `FindAssets` and asserts the `"exporter"` entry is present and visible in `VrEditing`. Will fail if the SO is accidentally missing the entry or if more than one `NavBarConfig` exists and the wrong one is found first. |
| `SpatialUi/PanelGrabHandleTests.cs` | `PanelGrabHandle.CaptureOffset` / `ApplyOffset` | Yes | Yes | **LIVE** | Math round-trip: grab offset round-trips; follows moving attach point. |
| `SpatialUi/PanelRegionRouterTests.cs` | `PanelRegionRouter`, `IRegionConfig`, `IRegionSurface` | Yes | Yes | **LIVE** | Region routing, mode-change filtering, toggle, region-default fallback, `ApplyMode`. |
| `SpatialUi/UserPanelLockModeTests.cs` | `UserPanel` | Yes | Yes | **LIVE** | Lock-mode ping-pong cycle, `ResetPosition`, `MoveTo`-while-dragging guard. |
| `StorageCore/BonePosePersistenceTests.cs` | `SceneSerializer`, `SceneData`, `NodeData`, `BonePose` | Yes | Yes | **BRITTLE** | Lines 29, 41, 46 assert `SchemaVersion == 3`. A v3→v4 scene schema bump breaks these. Core coverage is valuable (v2→v3 migration). |
| `StorageCore/PathProviderTests.cs` | `PathProvider.SceneRoot`, `PathProvider.SceneJson`, `PathProvider.AssetPath`, `PathProvider.AnimationJson` | Yes (class) | Partial | **BRITTLE** | `PathProvider` is live; `SceneRoot`, `SceneJson`, `AnimationJson` are used in prod. `AssetPath(sceneId, relativePath)` is a dead method — defined in `PathProvider.cs` but called from nowhere in `Scripts/` (zero callers). The test tests a method no production code invokes. Additionally, the test asserts the exact path literal `"/data/scenes/scene-01/assets/Models/mesh.fbx"` — brittle to any layout change. |
| `StorageCore/SceneSerializerTests.cs` | `SceneSerializer` | Yes | Yes | **BRITTLE** | Lines 30 and 50 assert `SchemaVersion == 3`. A v4 schema bump breaks both assertions even if migration logic is correct. |
| `VrInteraction/AxisMoveStrategyTests.cs` | `AxisMoveStrategy` | Yes | Yes | **LIVE** | Axis projection, perpendicular-move rejection, rotated-target local-axis test. |
| `VrInteraction/AxisScaleStrategyTests.cs` | `AxisScaleStrategy` | Yes | Yes | **LIVE** | Deadzone, exp-scale doubling, shrink, multi-axis preservation. |
| `VrInteraction/BoundsFitterTests.cs` | `BoundsFitter.ComputeSize` | Yes | Yes | **LIVE** | No-renderer min, single cube, clamp to max, clamp to min. |
| `VrInteraction/GizmoActivatorStateTests.cs` | `GizmoActivator` | Yes | Yes | **LIVE** | No-throw guards for grabbing/dragging/releasing/aborting without a target. |
| `VrInteraction/GizmoDragSliderTests.cs` | `GizmoDragSlider` | Yes | Yes | **LIVE** | Deadzone, lock-then-baseline, negative displacement. |
| `VrInteraction/InteractionCapabilityConvexTests.cs` | `InteractionCapability.Apply` (ConvexMesh path), `XRPromeonInteractable` | Yes | Yes | **LIVE** | Convex mesh colliders added per renderer and registered to interactable. |
| `VrInteraction/InteractionCapabilityTests.cs` | `InteractionCapability.Apply` (Box path), `Selectable`, `XRPromeonInteractable` | Yes | Yes | **LIVE** | Box collider, idempotency guard, no-interactable path. |
| `VrInteraction/InteractionLayersTests.cs` | `InteractionLayers.UnityLayer`, `GameObjectInteractionLayerExtensions.SetInteractionLayer` | Yes | Yes | **BRITTLE** | Asserts specific Unity layer names (`GizmoHandles`, `BoneProxies`, `SceneObjects`) via `LayerMask.NameToLayer`. Will return -1 and fail if these layers are not configured in the project's TagManager. Layer names are currently correct but any layer rename breaks the test. |
| `VrInteraction/RingRotateStrategyTests.cs` | `RingRotateStrategy` | Yes | Yes | **LIVE** | Rotation direction, pull-back, deadzone. |
| `VrInteraction/UniformScaleStrategyTests.cs` | `UniformScaleStrategy` | Yes | Yes | **LIVE** | Uniform scale up/down/deadzone. |
| `VrInteraction/XRPromeonInteractableColliderMapTests.cs` | `XRPromeonInteractable.RegisterColliders`, `XRInteractionManager` | Yes | Yes | **LIVE** | Regression: late-registered colliders resolve through XRI manager's collider map. |

---

## Tests Recommended to Cut

After cross-referencing all subjects under test with the live production Scripts, **no test file targets a fully orphaned (non-existent or completely unused) type**. The closest candidate is a single test method (not a file) inside a live test file:

### PathProviderTests — `AssetPath_ReturnsExpectedPath` test method

The method `PathProvider.AssetPath(string sceneId, string relativePath)` exists in `PathProvider.cs` but has **zero callers in `Assets/_App/Scripts/`** (confirmed by grep). It is dead production code. The test covers a dead method.

**Recommendation:** Remove the `AssetPath_ReturnsExpectedPath` test method from `PathProviderTests.cs`, and consider also removing `PathProvider.AssetPath` itself since it is unreachable from production.

> Confidence: High — grep across all Scripts returned only the definition line in `PathProvider.cs`.

---

## Brittle Tests to Watch

These tests will produce **unexpected failures** not from logic regressions but from routine maintenance (schema bump, SO tweak, prefab restructure, layer name change):

| Test File | Specific Fragility |
|---|---|
| `Animation/AnimationDataTests.cs` | 3× `Assert.AreEqual(2, loaded.schemaVersion)` — breaks on `SceneAnimationData` v2→v3 bump. |
| `Animation/AnimatorPanelConfigTests.cs` | Hard-coded SO defaults (`KeySize=22f`, etc.) — breaks on any designer default change. |
| `AssetBrowser/AssetEntityRecipeColliderTests.cs` | `ColliderKind` int values pinned — intentional binary compat, but fragile to reordering. |
| `AssetBrowser/AssetEntityRecipeTests.cs` | `schemaVersion == 1` pin on `AssetEntityRecipe`. |
| `AssetBrowser/AssetTypeBinaryCompatTests.cs` | `AssetType` int values pinned — intentional, but fragile to reordering. |
| `AssetBrowser/BuiltinLabAssetRecipeTests.cs` | `schemaVersion == 1` in both inline JSON literal and assertion. |
| `ExportPipeline/SceneBundleTests.cs` | `schemaVersion == 1` on `SceneBundle`. |
| `SceneComposition/SceneGraphTests.cs` | `snap.SchemaVersion == 3` — breaks on next scene schema bump. |
| `StorageCore/BonePosePersistenceTests.cs` | Two `SchemaVersion == 3` assertions + hardcoded v2-JSON migration fixture — breaks on scene v3→v4 bump. |
| `StorageCore/PathProviderTests.cs` | `AssetPath` tests a dead method; path string literals brittle to layout changes. |
| `StorageCore/SceneSerializerTests.cs` | Two `SchemaVersion == 3` assertions — breaks on next bump. |
| `SpatialUi/AnimatorPanelModulePrefabTests.cs` | Prefab paths and deep `Find(...)` strings — any hierarchy edit silently breaks. `LanesContent` path is 7 levels deep. |
| `SpatialUi/ExportModulePrefabTests.cs` | Prefab path + 5 serialized field names via `SerializedObject` — rename of any field or prefab breaks it. |
| `SpatialUi/NavBarConfigExporterRegionTests.cs` | Depends on `t:NavBarConfig` SO having `"exporter"` entry — SO misconfiguration causes false failure. |
| `VrInteraction/InteractionLayersTests.cs` | Unity layer names (`GizmoHandles`, `BoneProxies`, `SceneObjects`) resolved at runtime via `LayerMask.NameToLayer` — layer rename causes -1 and assertion failure. |

### Priority Ranking (highest to lowest break risk)

1. **Schema version pins** — All `Assert.AreEqual(N, ...schemaVersion)` tests across `AnimationDataTests`, `SceneGraphTests`, `BonePosePersistenceTests`, `SceneSerializerTests`, `SceneBundleTests`, `AssetEntityRecipeTests`, `BuiltinLabAssetRecipeTests`. CLAUDE.md explicitly warns: *"bumping schema breaks these."* Fix: use `Assert.GreaterOrEqual(N, ...)` or assert on behavior not version number.

2. **Prefab structural tests** — `AnimatorPanelModulePrefabTests`, `ExportModulePrefabTests`. Hard-coded `Find(...)` paths several nodes deep — any prefab edit breaks silently.

3. **NavBarConfig SO content test** — `NavBarConfigExporterRegionTests`. Depends on SO asset state, not code logic.

4. **SO default values** — `AnimatorPanelConfigTests` — minor, but designer-facing values that can be freely changed.

5. **Dead method test** — `PathProviderTests.AssetPath_ReturnsExpectedPath` — not a break risk, but accumulates confusion.

---

## Summary Counts

| Category | Count |
|---|---|
| Total test files | 68 |
| LIVE | 43 |
| BRITTLE/SUSPECT | 15 |
| ORPHANED (entire file) | 0 |
| Contains dead-method test (within live file) | 1 (`PathProviderTests`) |
