# Audit 2026-06-01 — Domain 03: RigBuilder (rigging, bone renderer, proxy/visual split, entity pipeline, bone-pose persistence)

Read-only reconciliation of code vs docs. Citations are `file:line`. Many generations exist (v1 BoneRenderer → v2 proxy-skeleton → v2-fixes → interaction-polish → rig-bake-prefab → entity-pipeline Slice A/B → leaf-axis → bone-pose-persistence). **Slice A/B of the entity pipeline is the generation that survives in code; everything before it is dead.**

---

## 1. Implemented reality (current rig architecture in code)

**The factory + runtime-component split won.** There is no `PromeonInteractableRigBuilder`, no `PromeonProxyRigBuilder`, no `RigRuntime`/`IRigRuntime`, no `BoneProxy`, no `MultiParentConstraint`/Animation-Rigging, no `BoneInteractableFactory`/`SelectionInteractorFactory`, no baked `ProxyRig` in prefabs, and no in-VR manual rigging panels (`BoneInspectorPanel`/`IkWizardPanel`) anywhere under `Assets/_App/Scripts`. (Grep confirmed: those names appear only in `Assets/_App/Documentation/*` and old superpowers docs.)

Current pipeline:

- **Construction core — `AssetBrowser/RigEntityFactory.cs`** (NOT in `Scripts/RigBuilder/`; it lives in AssetBrowser as a DI singleton). `BuildProxyRig(rigRoot, boneNames, terminalAxis, invertAxis, selectorDepth=3)` (`RigEntityFactory.cs:28`) builds the proxy hierarchy at **runtime on every spawn/restore** (Slice B "Approach A": proxies always built at runtime, never baked). It:
  - resolves live bones from the spawned `SkinnedMeshRenderer` by name (`ResolveTransforms` `:113`; `null` boneNames → all `smr.bones`);
  - creates a `ProxyRig` GO sibling-to-armature, per-bone `proxy_{bone.name}` GOs carrying baked diamond `Mesh` + `MeshRenderer` + `Outline` + MeshCollider/CapsuleCollider + `SceneNode` + `BoneSceneNodeMarker` + `Selectable` + `XRPromeonInteractable` (`BuildProxyNode` `:125`–`210`);
  - adds a `BoneFollower` to each real bone pointing at its proxy (`:204`–`206`);
  - builds whole-rig selector boxes via `BoneSelectorBoxPlanner.Plan` (`:75`–`111`);
  - attaches & binds one `ProxyRigRuntime` (`:65`–`66`).
- **Runtime coordinator — `RigBuilder/ProxyRigRuntime.cs`** (MonoBehaviour on the rig root). DI `Construct(EventBus, OutlineConfig, ProxyRigConfig)` (`:20`). Owns proxy list, selector colliders, and a `boneName→proxyTransform` map. Subscribes `SelectionChangedEvent` → per-bone outline color + selected-material swap (`:160`–`199`). `SetBonesInteractive(bool)` toggles whole-rig-select ↔ per-bone mode (strips stacked QuickOutline mask/fill mats, re-asserts `OutlineMode`/`RenderPriority=1`, re-tags `BoneProxies` layer, inverts root selector colliders) (`:109`–`158`). `RegisterSelectorColliders()` (`:89`) wires the whole-rig boxes onto the root interactable.
- **Inversion of control:** gizmo moves the proxy GO → `BoneFollower.Tick` (LateUpdate, `[ExecuteAlways]`) copies `proxy.local`→`bone.local`, scale as a multiplier on captured rest scale (`BoneFollower.cs:26`–`35`). Survives domain reload via serialized `_proxy` (`:6`).
- **Entity-pipeline seams (`AssetBrowser/`):** `RigEntityBuilder` (`HandledType => Rig`, `:21`) does `BuildAsync` (import → `RecipeFromInstance` → `RigDefinitionExtractor.FromSkinnedMesh`, `:35`) and `RestoreAsync` (builtin `Instantiate` / imported `factory.CreateAsync` glTF reload, then `factory.BuildProxyRig`, `:89`). `AssetEntityBuilderRegistry.RestoreAsync` is the single door: dispatch by type → `InteractionCapability.Apply` (common tail) → `ProxyRigRuntime.RegisterSelectorColliders()` when `colliderKind == BoneBoxes` (`AssetEntityBuilderRegistry.cs:31`–`44`).
- **Data contract:** rig data lives **inside the recipe** (`AssetEntityRecipe.rig : RigDefinition`), not in a separate `rig-{assetId}.json`. `RigDefinition` = `SchemaVersion`, `AssetId`, `TerminalBonesAxis`, `InvertTerminalBonesAxis`, `Bones` (`BoneRecord{BoneName, TranslationLocked}`), `IkChains` (`RigDefinition.cs:4`–`13`). `RigSerializer` is a trivial JsonUtility wrapper (`RigSerializer.cs`), retained but with no live caller for rig persistence (recipe carries everything).
- **Bone-pose persistence (schema v3) — fully implemented & matches its spec.** `BonePose{BoneName, LocalPosition, LocalRotation, LocalScale}` (`StorageCore/BonePose.cs`); `NodeData.BonePoses` (`NodeData.cs:15`); capture/restore are proxy-LOCAL TRS owned by `ProxyRigRuntime.CapturePoses/ApplyPoses` (`ProxyRigRuntime.cs:55`–`84`). `SceneGraph.CaptureSnapshot` writes `BonePoses` per rig node (`SceneGraph.cs:209`,`:219`); `OnSceneOpenedAsync` applies after spawn+inject (`:158`–`159`). Migration `<3` branch in `SceneSerializer.Deserialize` (`SceneSerializer.cs:20`–`25`); `SceneData.SchemaVersion = 3`.
- **Leaf-bone axis (v2: default Y, invert flag):** enum `TerminalBoneAxis{Auto,X,Y,Z}` (`TerminalBoneAxis.cs`); per-rig axis+invert threaded through recipe → `BuildProxyNode` leaf branch (`RigEntityFactory.cs:146`–`162`). Field renamed `TerminalAxis`→`TerminalBonesAxis` per the v2 refinement.
- **Bone-mode UI:** `InspectorPanel` detects "is-rig" via `GetComponentInChildren<ProxyRigRuntime>` and drives `SetBonesInteractive` + publishes `BonesVisibilityChangedEvent` (`InspectorPanel.cs:132`,`:291`–`292`); `OutlinerPanel` uses the same detect (`OutlinerPanel.cs:106`). `InteractionMaskBinder` consumes `BonesVisibilityChangedEvent` (`InteractionMaskBinder.cs:38`,`:75`).
- **Tests present (match current arch):** `RigEntityFactoryBuildProxyTests`, `ProxyRigRuntimeTests`, `ProxyRigBonePoseTests`, `BoneSelectorBoxPlannerTests`, `RigDefinitionExtractorTests`. **No `PromeonProxyRigBuilderTests`** (Slice B3 deletion done).

---

## 2. Doc↔code matches

- **`2026-06-01-bone-pose-persistence-design.md` / plan** — exact match (BonePose shape, v2→v3 migration, proxy-local capture, `Bind` carries the bone-name map, ShowBones-on-exit reset). Spec self-marked "✅ Implemented & verified". Confirmed in `ProxyRigRuntime.cs`, `SceneGraph.cs`, `SceneSerializer.cs`, `InspectorPanel.cs`.
- **`2026-06-01-rig-in-entity-pipeline-design.md` (Slice A)** — recipe gains `RigDefinition rig`; `InteractionCapability.Apply` hoisted to `Registry.RestoreAsync`; `RigEntityBuilder.BuildAsync` writes `recipe.rig` or null; no-skeleton → `ConvexMesh` static fallback (`RigEntityBuilder.cs:40`–`46`). All present.
- **`2026-06-01-rig-slice-b-runtime-proxy-design.md` (Slice B)** — `BuildProxyRig` filled, `ProxyRigRuntime` created, `PromeonProxyRigBuilder`/`RigBakeTool` dissolved, `BoneProxy` gone, builtin proxies built at runtime (no baked `ProxyRig`). All present. (One signature drift — see §3.)
- **`2026-06-01-rig-leaf-bone-axis-design.md`** — enum, recipe field, invert flag, default-Y wizard, leaf-branch switch. Match.
- **`BoneSceneNodeMarker.cs`** doc-comment still accurately describes the transient-node rewrite (`bone:{rigNodeId}:{boneName}`).

---

## 3. Drift / mismatches

1. **`ProxyRigRuntime.Construct` signature drift vs Slice B spec.** Spec says `Construct(EventBus, OutlineConfig)` (`rig-slice-b-...-design.md:17,21`). Code is `Construct(EventBus, OutlineConfig, ProxyRigConfig)` (`ProxyRigRuntime.cs:20`) — a third DI dependency (`ProxyRigConfig`) was added for the selected-bone material swap (`BoneSelectedMaterial`). The spec lists `ProxyRigConfig` as `{BoneMaterial, BoneWidth, UseConvexCollider}` only — code adds `BoneSelectedMaterial` (`ProxyRigConfig.cs:11`,`:16`). Minor, undocumented enhancement.
2. **Slice B spec "ПЕРЕПОДКЛЮЧАЕМ (сохраняем): `RigRuntime.ApplyDefinition`, `SceneContext.Rig`, `IRigRuntime`, `BoneInspectorPanel`/`IkWizardPanel`" is FALSE in final code.** The "Поправка к прежней спеке" (`...slice-b...design.md:9`,`:20`–`28`) explicitly says `RigRuntime` is NOT retired and manual in-scene rigging is kept. **In reality all of these were deleted** (grep: zero references in `Scripts/`). This is the spec drifting from what shipped — the manual-rig path was removed (consistent with memory `project_interaction_context_reset`: "ручной риг/IK wizard удалён"). The Slice B *design* doc is therefore partly stale on this point even though the slice "shipped".
3. **`RigEntityFactory` location vs folder convention.** CLAUDE.md/structure imply rig code under `Scripts/RigBuilder/`, but the construction core `RigEntityFactory.cs` and the seam `RigEntityBuilder.cs` live under `Scripts/AssetBrowser/`. Intentional (factory triad lives with the entity pipeline) but worth noting for anyone searching `RigBuilder/`.
4. **`BuildProxyRig` parameter count vs leaf-axis spec.** Leaf-axis spec describes a 3-arg `BuildProxyRig(rigRoot, boneNames, terminalAxis)` (`rig-leaf-bone-axis-design.md:71`). Code is **5-arg** `(rigRoot, boneNames, terminalAxis, invertAxis, selectorDepth=3)` (`RigEntityFactory.cs:28`) — the v2 invert refinement + selector depth were folded in afterward. The spec's own "v2 changes" header notes the invert addition, but the signature in the body is stale.

---

## 4. Planned-but-not-implemented

- **IK chains.** `RigDefinition.IkChains` / `IkChainRecord{RootBone,EndBone,PoleBone,Weight}` are serialized and round-trip in the recipe, but **nothing consumes them** — no IK solver, no constraint build, no extractor population (grep: `IkChain` only in the data class, `RigDefinition`, and an `AssetEntityRecipe` comment). IK is a data placeholder only. (v1 bone-renderer spec promised "IK/FK via Animation Rigging"; not in code.)
- **Slice C — `RigBakeTool` / "Bake to Built-in Library" / `*BakeTool` triad** (`rig-in-entity-pipeline-design.md:113`, `rig-slice-b...design.md:73`). Not implemented; builtin rigs ship as bare skinned-mesh prefabs and are proxied at runtime. (NB: a separate `2026-06-01-builtin-recipe-bake` plan exists for baking *recipes* onto builtin records — adjacent, AssetBrowser-owned, out of this domain's scope.)
- **`BoneRecord.TranslationLocked`** (`BoneRecord.cs:7`) is serialized/defaults `true` but never read by build or interaction code — planned per-bone lock, unimplemented.
- **Stable rig `AssetId`** (Slice A/B debt at `rig-slice-b...design.md:96`): `RigDefinitionExtractor` still stamps `AssetId = smr.gameObject.name` (`RigDefinitionExtractor.cs:12`), the temp-GO name, not `record.Id`. Harmless (mapping is by bone name) but the documented cleanup wasn't applied.

---

## 5. Stale-doc candidates (DO NOT delete — flag only)

All v1/v2/intermediate rig docs are superseded by the final entity-pipeline generation. Listed oldest→newest:

| Doc (spec & plan) | Status | Reason (one line) |
|---|---|---|
| `specs/2026-05-20-promeon-bone-renderer-design.md` + `plans/2026-05-20-promeon-bone-renderer.md` | **SUPERSEDED-BY** `2026-06-01-rig-in-entity-pipeline` + `-slice-b` | Describes `PromeonInteractableRigBuilder : BoneRenderer` + `MultiParentConstraint`/Animation-Rigging at `Subsystems/RigBuilder/` — class, package dependency, and path all gone. |
| `plans/2026-05-20-rig-builder-proxy-visual-split.md` | **SUPERSEDED-BY** `rig-builder-v2-proxy-skeleton` | Constraint-mode proxy/visual two-GO split inside `PromeonInteractableRigBuilder`; constraint mode was abandoned for the proxy-skeleton. |
| `specs/2026-05-20-rig-builder-v2-proxy-skeleton.md` + plan | **SUPERSEDED-BY** `rig-builder-v2-fixes`, then entity-pipeline | First proxy-skeleton/`BoneFollower` design on `PromeonInteractableRigBuilder.Rebuild`; builder class gone, but the `BoneFollower`/proxy *idea* survived. |
| `specs/2026-05-20-rig-builder-v2-fixes.md` + plan | **SUPERSEDED-BY** entity-pipeline Slice B | Per-bone baked diamond meshes + sibling `ProxyRig` + `[ExecuteAlways]` BoneFollower — the surviving *mesh/follower technique*, but its host `PromeonInteractableRigBuilder` and `RigRuntime` wiring are gone. |
| `specs/2026-05-20-rig-interaction-polish.md` + plan | **SUPERSEDED-BY** `2026-05-21-rig-bake-prefab` then entity-pipeline | Introduced `BoneInteractableFactory`, `SceneInspectorView` Bone state, Show-Bones; factory deleted, inspector replaced by `InspectorPanel`/`ProxyRigRuntime`. |
| `specs/2026-05-21-rig-bake-prefab-design.md` + plan | **SUPERSEDED-BY** `2026-06-01-rig-slice-b-runtime-proxy` (Approach A) | Whole premise = bake proxies into prefabs at edit time; Slice B chose "always build at runtime," deleting baked `ProxyRig`, `RegenerateMissingProxyMeshes`, `OnEnable` repopulation. Self-declares it supersedes `rig-interaction-polish`. |
| `docs/session-reports/2026-05-20-rig-builder-refactor.md` | **OBSOLETE** (historical) | Reports `EffectiveWidth`/constraint-fallback work on `PromeonInteractableRigBuilder` — class no longer exists. Keep as history. |
| `specs/2026-06-01-rig-in-entity-pipeline-design.md` + `plans/...slice-a.md` | **DONE** (Slice A shipped) | Recipe `rig` field + hoisted `Apply` + null-skeleton fallback all in code. Body still references soon-to-be-deleted `PromeonProxyRigBuilder`/`RigRuntime` as "не трогаем" — accurate for Slice A's moment, stale now. |
| `specs/2026-06-01-rig-slice-b-runtime-proxy-design.md` + plan | **DONE-WITH-DRIFT** | Shipped, but its "сохраняем `RigRuntime`/`IRigRuntime`/manual-rig panels" amendment is false in final code (see §3.2); `Construct` signature stale (§3.1). |
| `specs/2026-06-01-rig-leaf-bone-axis-design.md` + plan | **DONE** | Implemented incl. v2 default-Y + invert; body's 3-arg `BuildProxyRig` signature is stale vs the shipped 5-arg (§3.4). |
| `specs/2026-06-01-bone-pose-persistence-design.md` + plan | **DONE** | Fully matches code; no drift. |

---

## 6. Rudimentary / dead code (old rig scripts no longer wired)

Within the surviving codebase the rig folder is lean — the big dead classes were physically deleted, not left behind. Remaining low-value items:

- **`RigSerializer.cs`** — no live caller for rig persistence (recipe carries rig data inline; `Rigs/rig-{assetId}.json` flow retired per entity-pipeline spec `:94`). Kept "for editor authoring/bake if needed," but Slice C (the would-be user) doesn't exist. Effectively dormant.
- **`BoneRecord.TranslationLocked`** — serialized, never read (see §4).
- **`IkChainRecord` + `RigDefinition.IkChains`** — serialized, never consumed (see §4). Whole IK surface is rudimentary.
- **`RigDefinitionExtractor.AssetId` assignment** — sets a throwaway temp-GO name; documented-but-unapplied cleanup (§4).
- No orphaned `PromeonProxyRigBuilder`/`BoneProxy`/`RigRuntime`/factory files remain under `Scripts/` (verified by grep) — only stale *doc/Documentation* references persist.

**Note (out of domain, flag for other auditors):** `Assets/_App/Documentation/STRUCTURE.md`, `architecture_context.md`, `conventions.md` still name `RigRuntime`/`PromeonProxyRigBuilder`-era types (grep hits) and were explicitly slated for update in the entity-pipeline spec's cleanup list (`rig-in-entity-pipeline-design.md:138`) — not done.
