# PromeonLab — File Structure (Assets/)

> Regenerated 2026-05-29 (post-restructure + folder cleanup). Unity 6000.3.7f1.

---

## Top-level Layout

| Folder / File | Purpose |
|---|---|
| `_App/` | All project code, owned content, scenes, docs, and vendored third-party (under `ThirdParty/`) |
| `CompositionLayers/` | Meta XR Composition Layers package user-settings (auto-generated, transitive via Meta OpenXR) |
| `Samples/` | XR Interaction Toolkit 3.0.7 sample assets (Starter Assets + XR Device Simulator) — **live dependency:** base of the `User XR Origin (XR Rig)` prefab variant |
| `Settings/` | URP renderer and pipeline assets (Mobile + PC profiles, global URP settings) |
| `TextMesh Pro/` | Standard TMP package (Resources, Fonts, Shaders, Examples) |
| `XR/` | XR Plug-in Management settings (OpenXR + XR Simulation loaders) |
| `XRI/` | XR Interaction Toolkit project settings (Interaction Layers + Device Simulator) |
| `InputSystem_Actions.inputactions` | Root Input System action-map asset |

> **Removed/moved by restructure:** `_App/_Shared/`, `_App/Subsystems/`,
> `_App/DemoAssets/`, and the top-level `_App/Bootstrap/` (now `_App/Scripts/Bootstrap/`).
> `Resources/` sub-content (materials/textures/models/prefabs) moved into `_App/Content/`.
>
> **Removed by 2026-05-29 folder cleanup:** `Resources/` (was empty — `_App` does not use
> `Resources.Load`), `TutorialInfo/` (Unity URP template readme/editor scripts — not project
> code), `Screenshots/` (dev capture output — not game content), `_Recovery/` (Editor autosave
> scene), and the root `Readme.asset` (template ScriptableObject orphaned once `TutorialInfo`
> was removed).
>
> **Moved by 2026-05-29 reorg:** top-level `UnityPacks/` → `_App/ThirdParty/` (all third-party
> asset packs + C# packages now vendored under `_App`; GUIDs, `.meta`, asmdef names, and the
> QuickOutline `isReadable` patch all preserved through the `AssetDatabase` move).
>
> **Unchanged, retained on purpose** (package/engine-managed at fixed paths, or live deps):
> `XR/`, `XRI/`, `TextMesh Pro/`, `CompositionLayers/`, `Samples/`, `Settings/`.

---

## How to look up by GUID

GUIDs are **not** listed in this file to keep it lean. Every asset's GUID lives in its sidecar
`.meta` file next to the asset on disk. GUIDs survive moves — the `.meta` file travels with the
asset and its GUID never changes on rename or relocation.

```powershell
# Find asset by GUID (PowerShell — use -LiteralPath for paths containing brackets)
Select-String -Pattern "<guid>" `
    (Get-ChildItem -LiteralPath "Assets" -Recurse -Filter "*.meta" | Select-Object -ExpandProperty FullName)
```

```bash
# git grep
git grep "<guid>" -- "*.meta"
```

---

## Tree

```
Assets/
├── InputSystem_Actions.inputactions
│
├── CompositionLayers/
│   └── UserSettings/
│       ├── CompositionLayersPreferences.asset
│       └── Resources/
│           └── CompositionLayersRuntimeSettings.asset
│
├── Samples/
│   └── XR Interaction Toolkit/
│       └── 3.0.7/
│           ├── Starter Assets/             (~17 .cs, ~50 .prefab, StarterAssets.asmdef,
│           │                                Editor/StarterAssets.Editor.asmdef)
│           └── XR Device Simulator/        (~3 .cs, 2 .prefab,
│                                            DeviceSimulator.asmdef)
│
├── Settings/
│   ├── DefaultVolumeProfile.asset
│   ├── Mobile_Renderer.asset
│   ├── Mobile_RPAsset.asset
│   ├── PC_Renderer.asset
│   ├── PC_RPAsset.asset
│   ├── SampleSceneProfile.asset
│   └── UniversalRenderPipelineGlobalSettings.asset
│
└── _App/
    │
    ├── Content/                            ← owned assets (no .cs here)
    │   ├── Materials/
    │   │   ├── CheckerFloor_Blue.mat
    │   │   ├── CheckerFloor_Neutral.mat
    │   │   ├── CheckerFloor_Neutralediting.mat
    │   │   ├── CheckerFloor_Tests.mat
    │   │   ├── MainMenuPanel-Bg.mat
    │   │   ├── NoSignal_Material.mat
    │   │   ├── PromeonBoneRenderer_Material.mat
    │   │   ├── TriplanarBase_000.mat
    │   │   ├── WhiteUnlit_Blue.mat
    │   │   ├── WhiteUnlit_Green.mat
    │   │   ├── WhiteUnlit_Red.mat
    │   │   ├── WhiteUnlit_Yellow.mat
    │   │   ├── crush_dummy_UE4.mat
    │   │   ├── crush_dummy_UE4_red.mat
    │   │   └── Gizmo/
    │   │       ├── Gizmo_Blue.mat
    │   │       ├── Gizmo_Default.mat
    │   │       ├── Gizmo_Green.mat
    │   │       └── Gizmo_Red.mat
    │   │
    │   ├── Models/
    │   │   ├── Characters/
    │   │   │   └── crush_dummy_UE4_skinned.fbx
    │   │   └── Gizmos/
    │   │       ├── Gizmo_Move.fbx
    │   │       ├── Gizmo_Rotate.fbx
    │   │       └── Gizmo_Scale.fbx
    │   │
    │   ├── Prefabs/
    │   │   ├── Assets/                     (spawnable scene-object prefabs)
    │   │   │   ├── (Prb)CoffeTable.prefab
    │   │   │   ├── (Prb)Drawer1.prefab
    │   │   │   ├── (Prb)Storage2.prefab
    │   │   │   ├── (Prb)Toilet.prefab
    │   │   │   ├── Crush Dummy.prefab
    │   │   │   ├── Potted Plant 1.prefab
    │   │   │   ├── Potted Plant 2.prefab
    │   │   │   ├── Potted Plant 3.prefab
    │   │   │   ├── Street Tree 1.prefab
    │   │   │   ├── Street Tree 2.prefab
    │   │   │   └── Street Tree 3.prefab
    │   │   ├── Environment/
    │   │   │   └── FloorDefault.prefab
    │   │   ├── Gizmos/
    │   │   │   ├── SceneOriginGizmo.prefab
    │   │   │   └── Vr3D_Gizmos.prefab
    │   │   ├── XR/
    │   │   │   ├── EventSystem.prefab
    │   │   │   └── User XR Origin (XR Rig).prefab   ← custom XR hooks live here
    │   │   └── UI/
    │   │       ├── KeyframeMarker.prefab
    │   │       ├── Items/                  (list-item templates)
    │   │       │   ├── LabAssetCard_ItemUI.prefab
    │   │       │   ├── OutlinerObject-Object_ItemUI.prefab
    │   │       │   ├── OutlinerObject-Rig_ItemUI.prefab
    │   │       │   ├── ScenePrefab_ItemUI.prefab
    │   │       │   ├── TimelineKeyDiamond.prefab
    │   │       │   ├── TimelineLane.prefab
    │   │       │   ├── TimelineTick.prefab
    │   │       │   ├── TimelineTickLabel.prefab
    │   │       │   ├── TrackRow.prefab
    │   │       │   └── UserPanelButton-PrefDefault.prefab
    │   │       └── Panels/
    │   │           ├── Static/             (world-fixed or scene-entry panels)
    │   │           │   ├── MainMenuPanel.prefab
    │   │           │   ├── MainMenu_CombinedPanel.prefab
    │   │           │   └── ScenePickerPanel.prefab
    │   │           └── UserPanel/          (body-locked wrist panel + module slots)
    │   │               ├── UserPanel.prefab
    │   │               ├── AnimatorPanelModule.prefab
    │   │               ├── AssetBrowserModule.prefab
    │   │               ├── ContextMenu_VrEditing.prefab
    │   │               ├── GizmoToolsModule.prefab
    │   │               ├── RiggingToolsModule.prefab
    │   │               ├── SceneInspectorModule.prefab
    │   │               ├── SceneOutlinerModule.prefab
    │   │               └── SettingsModule.prefab
    │   │
    │   ├── ScriptableObjects/              (all DefaultXxx named; flat folder)
    │   │   ├── DefaultAnimatorPanelConfig.asset
    │   │   ├── DefaultBuiltinAssetLibrary.asset
    │   │   ├── DefaultDemoAssetCatalog.asset
    │   │   ├── DefaultGizmoConfig.asset
    │   │   ├── DefaultModeTransitionGraph.asset
    │   │   ├── DefaultNavBarConfig.asset
    │   │   ├── DefaultPanelRegistry.asset
    │   │   └── NoRigsBuiltinAssetLibrary.asset
    │   │
    │   ├── Shaders/
    │   │   ├── CheckerBase.png
    │   │   └── URP_TriplanarSimplified_Promokot.shadergraph
    │   │
    │   └── Textures/
    │       ├── AssetIcons/                 (12 × .png — catalog entry thumbnails)
    │       │   ├── icnon_crashDummy.png
    │       │   ├── icnon_crashDummy2.png
    │       │   ├── icon_(Prb)CoffeTable.png
    │       │   ├── icon_(Prb)Drawer1.png
    │       │   ├── icon_(Prb)Storage2.png
    │       │   ├── icon_(Prb)Toilet.png
    │       │   ├── Plants1.png
    │       │   ├── Plants2.png
    │       │   ├── Plants3.png
    │       │   ├── PlantsTree1.png
    │       │   ├── PlantsTree2.png
    │       │   └── PlantsTree3.png
    │       ├── Checkers/                   (8 × .png/.jpg — UV-debug / checker tiles)
    │       │   ├── !Texel Checker 4k 10.24.png
    │       │   ├── !Texel Checker 4k 5.12.png
    │       │   ├── DiffuseColor_Texture.png
    │       │   ├── DiffuseColor_TextureEditing.png
    │       │   ├── DiffuseColor_Texture_B.png
    │       │   ├── DiffuseColor_Texture_G.png
    │       │   ├── DiffuseColor_Texture_R.png
    │       │   └── uv checker.jpg
    │       ├── Icons/                      (10 × .png — app and UI icons)
    │       │   ├── 3d-Coordinate-Axis--Streamline-Core-Remix.png
    │       │   ├── 3d-Module-Dimension--Streamline-Core-Remix.png
    │       │   ├── 3d-Rotate-1--Streamline-Core-Remix.png
    │       │   ├── ObjectIcon_blalsalsadlasd.png
    │       │   ├── RigIcon-Bring-To-Front.png
    │       │   ├── TetoCar_AppIconPlaceHolder.png
    │       │   ├── black-keyboard-with-white-keys_icon-icons.com_72857.png
    │       │   ├── exit_icon-icons.com_70975.png
    │       │   ├── icons8-settings-240.png
    │       │   └── secure-icon-png.png
    │       ├── Misc/                       (1 × .jpg — no-signal background)
    │       │   └── Тo-signal-Иackground-Сolorful.jpg
    │       └── Pbr/                        (5 × .png — crush_dummy PBR maps)
    │           ├── crush_dummy_default_BaseColor.tga.png
    │           ├── crush_dummy_default_Metallic.tga.png
    │           ├── crush_dummy_default_Normal.tga.png
    │           ├── crush_dummy_default_Occlusion.tga.png
    │           └── crush_dummy_default_Roughness.tga.png
    │
    ├── Documentation/
    │   ├── architecture_context.md
    │   ├── conventions.md
    │   ├── coursework_context.md
    │   ├── STRUCTURE.md                    (this file)
    │   └── STRUCTURE_TARGET.md
    │
    ├── Editor/                             ← editor-only code (_App.Editor.asmdef)
    │   ├── _App.Editor.asmdef
    │   ├── AnimatorPanelModuleBuilder.cs
    │   ├── EditorPlaceholder.cs
    │   ├── PromeonProxyRigBuilderEditor.cs
    │   └── RemoveMissingScriptsTool.cs
    │
    ├── Scenes/
    │   ├── Bootstrap.unity
    │   ├── MainMenu.unity
    │   ├── Sandbox.unity
    │   ├── VrEditing.unity
    │   ├── Tests/
    │   │   ├── Asset_Review.unity
    │   │   ├── MCP_testScene.unity
    │   │   └── Prototyping_UI.unity
    │   └── _Sandbox/
    │       └── AnimatorPanelSandbox.unity
    │
    ├── Scripts/                            ← ALL runtime C# (_App.Runtime.asmdef)
    │   ├── _App.Runtime.asmdef
    │   │
    │   ├── Core/                           shared primitives used across all subsystems
    │   │   ├── EventBus.cs
    │   │   └── ICommand.cs
    │   │
    │   ├── AnimationAuthoring/
    │   │   ├── ActionContainer.cs
    │   │   ├── AnimKeyData.cs
    │   │   ├── AnimTrackData.cs
    │   │   ├── AnimationAuthoring.cs
    │   │   ├── AnimationClipboard.cs
    │   │   ├── AnimationClock.cs
    │   │   ├── ContainerChange.cs
    │   │   ├── FrameClipboard.cs
    │   │   ├── FrameClipboardEntry.cs
    │   │   ├── InternalsVisibleTo.cs
    │   │   ├── KeyframeChange.cs
    │   │   ├── SceneAnimationData.cs
    │   │   └── Events/
    │   │       ├── AnimationContainerChangedEvent.cs
    │   │       ├── AnimationKeyframeChangedEvent.cs
    │   │       ├── FrameChangedEvent.cs
    │   │       └── PlaybackStateChangedEvent.cs
    │   │
    │   ├── AnimationPlayback/
    │   │   └── AnimationPlayback.cs
    │   │
    │   ├── AssetBrowser/
    │   │   ├── AssetEntry.cs
    │   │   ├── AssetImporter.cs
    │   │   ├── AssetRef.cs
    │   │   ├── AssetRegistry.cs
    │   │   ├── AssetSource.cs
    │   │   ├── AssetSpawner.cs
    │   │   ├── AssetType.cs
    │   │   ├── BuiltinAssetLibrary.cs
    │   │   ├── BuiltinLabAsset.cs
    │   │   ├── DemoAssetCatalog.cs
    │   │   ├── IAssetLibrary.cs
    │   │   ├── IAssetRegistry.cs
    │   │   ├── ILabAsset.cs
    │   │   ├── ImportedAssetLibrary.cs
    │   │   ├── ImportedLabAsset.cs
    │   │   ├── SavedAssetLibrary.cs
    │   │   ├── SavedLabAsset.cs
    │   │   └── Events/
    │   │       ├── AssetImportedEvent.cs
    │   │       └── AssetSpawnRequestedEvent.cs
    │   │
    │   ├── Bootstrap/
    │   │   ├── AppBootstrap.cs
    │   │   ├── FallGuard.cs
    │   │   ├── MainMenuSceneScope.cs
    │   │   ├── PlayerSpawnApplier.cs
    │   │   ├── RootLifetimeScope.cs
    │   │   ├── SandboxSceneScope.cs
    │   │   ├── UndoKeyHandler.cs
    │   │   ├── VrEditingSceneScope.cs
    │   │   └── VrInputFieldProxy.cs
    │   │
    │   ├── ErrorHandling/
    │   │   ├── ErrorHandling.cs
    │   │   ├── ErrorLevel.cs
    │   │   └── Events/
    │   │       └── ErrorOccurredEvent.cs
    │   │
    │   ├── ExportPipeline/
    │   │   └── ExportPipeline.cs
    │   │
    │   ├── InputBindings/
    │   │   └── InputBindings.cs
    │   │
    │   ├── ModeOrchestrator/
    │   │   ├── AppMode.cs
    │   │   ├── ModeOrchestrator.cs
    │   │   ├── ModeTransitionGraph.cs
    │   │   └── Events/
    │   │       └── ModeChangedEvent.cs
    │   │
    │   ├── RigBuilder/
    │   │   ├── BoneFollower.cs
    │   │   ├── BoneProxy.cs
    │   │   ├── BoneRecord.cs
    │   │   ├── BoneSceneNodeMarker.cs
    │   │   ├── IkChainRecord.cs
    │   │   ├── IRigRuntime.cs
    │   │   ├── PromeonProxyRigBuilder.cs
    │   │   ├── RigDefinition.cs
    │   │   ├── RigRuntime.cs
    │   │   ├── RigSerializer.cs
    │   │   └── Events/
    │   │       └── BonesVisibilityChangedEvent.cs
    │   │
    │   ├── SceneComposition/
    │   │   ├── CommandStack.cs
    │   │   ├── ISceneGraph.cs
    │   │   ├── ISelectionManager.cs
    │   │   ├── SceneAutoSaver.cs
    │   │   ├── SceneGraph.cs
    │   │   ├── SceneNode.cs
    │   │   ├── SelectionManager.cs
    │   │   ├── TransformCommand.cs
    │   │   ├── Constraints/
    │   │   │   └── ConstraintFreezePosition.cs
    │   │   └── Events/
    │   │       ├── NodeRenamedEvent.cs
    │   │       ├── SceneClosedEvent.cs
    │   │       ├── SceneModifiedEvent.cs
    │   │       ├── SceneOpenedEvent.cs
    │   │       ├── SceneSelectedEvent.cs
    │   │       └── SelectionChangedEvent.cs
    │   │
    │   ├── SpatialUi/
    │   │   ├── AnimatorPanelConfig.cs
    │   │   ├── AssetBrowserModule.cs
    │   │   ├── BoneInspectorPanel.cs
    │   │   ├── DetachablePanel.cs
    │   │   ├── IkSetupWizard.cs
    │   │   ├── MainMenuPanel.cs
    │   │   ├── NavBarConfig.cs
    │   │   ├── PanelId.cs
    │   │   ├── PanelRegistry.cs
    │   │   ├── PanelType.cs
    │   │   ├── PropertyPanel.cs
    │   │   ├── ScenePickerPanel.cs
    │   │   ├── SettingsModule.cs
    │   │   ├── SpatialPanel.cs
    │   │   ├── UiPanelManager.cs
    │   │   ├── UserPanel.cs
    │   │   ├── Elements/               (self-contained UI MonoBehaviours and widgets)
    │   │   │   ├── DetachablePanelDragHandle.cs
    │   │   │   ├── FileBrowserVrAnchor.cs
    │   │   │   ├── LabAssetCard.cs
    │   │   │   ├── OutlinerItem.cs
    │   │   │   ├── PanelDragHandle.cs
    │   │   │   ├── RigOutlinerItem.cs
    │   │   │   ├── SceneItem.cs
    │   │   │   ├── TimelineInputHandler.cs
    │   │   │   ├── TimelineLaneView.cs
    │   │   │   ├── TimelineLanesView.cs
    │   │   │   ├── TimelinePlayheadView.cs
    │   │   │   ├── TimelineRulerView.cs
    │   │   │   ├── TimelineScrollSync.cs
    │   │   │   ├── TrackRowView.cs
    │   │   │   ├── UserPanelKeyboardToggle.cs
    │   │   │   ├── UserPanelOpener.cs
    │   │   │   └── VrKeyboard.cs
    │   │   ├── Events/
    │   │   │   ├── KeyboardFocusEvent.cs
    │   │   │   ├── PanelClosedEvent.cs
    │   │   │   ├── PanelDetachedEvent.cs
    │   │   │   └── PanelLinkedEvent.cs
    │   │   └── Views/                  (data-bound read-only display components)
    │   │       ├── AnimatorEmptyStateView.cs
    │   │       ├── AnimatorPanelView.cs
    │   │       ├── AnimatorToolbarView.cs
    │   │       ├── AnimatorTransportView.cs
    │   │       ├── AssetPropertiesView.cs
    │   │       ├── SceneInspectorView.cs
    │   │       └── SceneOutlinerView.cs
    │   │
    │   ├── StorageCore/
    │   │   ├── AppStorage.cs
    │   │   ├── AssetCatalogData.cs
    │   │   ├── NodeData.cs
    │   │   ├── PathProvider.cs
    │   │   ├── SceneData.cs
    │   │   ├── SceneSerializer.cs
    │   │   └── UnsavedChangesGuard.cs
    │   │
    │   └── VrInteraction/
    │       ├── GizmoController.cs
    │       ├── GizmoMode.cs
    │       ├── IDragStrategy.cs
    │       ├── Selectable.cs
    │       ├── SelectionVisual.cs
    │       ├── SelectionVisualSync.cs
    │       ├── WorldClickCatcher.cs
    │       ├── XRPromeonInteractable.cs
    │       ├── Events/
    │       │   ├── GizmoDragEndedEvent.cs
    │       │   ├── GizmoDragStartedEvent.cs
    │       │   ├── GizmoModeChangedEvent.cs
    │       │   ├── GizmoToolsPanelClosedEvent.cs
    │       │   └── GizmoToolsPanelOpenedEvent.cs
    │       └── Gizmo/
    │           ├── AxisKind.cs
    │           ├── BoundsFitter.cs
    │           ├── GizmoActivator.cs
    │           ├── GizmoConfig.cs
    │           ├── GizmoHandle.cs
    │           ├── GizmoHierarchy.cs
    │           ├── GizmoToolsPanel.cs
    │           ├── HandleKind.cs
    │           └── Strategies/
    │               ├── AxisMoveStrategy.cs
    │               ├── AxisScaleStrategy.cs
    │               ├── IGizmoDragStrategy.cs
    │               ├── RingRotateStrategy.cs
    │               └── UniformScaleStrategy.cs
    │
    ├── ThirdParty/                         ← vendored third-party (moved 2026-05-29 from top-level UnityPacks/)
    │   ├── ColorSkies/                     skybox cubemaps + 2 demo .cs (Assembly-CSharp, no asmdef)
    │   ├── Downtown Game Studio/           nature/city demo models
    │   ├── HouseInteriorPack/              interior demo models
    │   ├── Keyboard Package/               VR keyboard — KeyboardPackage.asmdef → _App.Runtime
    │   ├── QuickOutline/                   outline FX — QuickOutline.asmdef; PATCHED isReadable guard (reimport overwrites)
    │   └── SimpleFileBrowser/              Android file dialog — SimpleFileBrowser.Runtime.asmdef
    │
    └── Tests/                              ← NUnit tests (_App.Tests.asmdef)
        ├── _App.Tests.asmdef
        ├── AnimationAuthoring/             (5 × .cs)
        │   ├── ActionContainerTests.cs
        │   ├── AnimationAuthoringTests.cs
        │   ├── AnimationClipboardTests.cs
        │   ├── AnimationClockTests.cs
        │   └── AnimationDataTests.cs
        ├── RigBuilder/                     (1 × .cs)
        │   └── PromeonProxyRigBuilderTests.cs
        ├── SceneComposition/               (5 × .cs)
        │   ├── AssetRegistryTests.cs
        │   ├── CommandStackTests.cs
        │   ├── SceneGraphTests.cs
        │   ├── SceneNodeTests.cs
        │   └── SelectionManagerTests.cs
        ├── StorageCore/                    (2 × .cs)
        │   ├── PathProviderTests.cs
        │   └── SceneSerializerTests.cs
        └── VrInteraction/                  (6 × .cs)
            ├── AxisMoveStrategyTests.cs
            ├── AxisScaleStrategyTests.cs
            ├── BoundsFitterTests.cs
            ├── GizmoActivatorStateTests.cs
            ├── RingRotateStrategyTests.cs
            └── UniformScaleStrategyTests.cs
```

---

## Asset Counts

| Folder | `.cs` | `.prefab` | `.unity` | `.asset` |
|---|---|---|---|---|
| `_App/Scripts/` total | **156** | — | — | — |
| — `Core/` | 2 | — | — | — |
| — `AnimationAuthoring/` | 16 | — | — | — |
| — `AnimationPlayback/` | 1 | — | — | — |
| — `AssetBrowser/` | 17 | — | — | — |
| — `Bootstrap/` | 9 | — | — | — |
| — `ErrorHandling/` | 3 | — | — | — |
| — `ExportPipeline/` | 1 | — | — | — |
| — `InputBindings/` | 1 | — | — | — |
| — `ModeOrchestrator/` | 4 | — | — | — |
| — `RigBuilder/` | 11 | — | — | — |
| — `SceneComposition/` | 14 | — | — | — |
| — `SpatialUi/` | 44 | — | — | — |
| — `StorageCore/` | 7 | — | — | — |
| — `VrInteraction/` | 26 | — | — | — |
| `_App/Editor/` | 4 | — | — | — |
| `_App/Tests/` | **19** | — | — | — |
| `_App/Content/Prefabs/` | — | **39** | — | — |
| `_App/Content/ScriptableObjects/` | — | — | — | **8** |
| `_App/Scenes/` | — | — | **8** | — |
| `_App/ThirdParty/` (vendored) | ~30 | ~23 | 3 | ~2 |
| `Samples/XRI 3.0.7/` | 17 | ~52 | — | — |

> Notes:
> - `.cs` counts include all `Events/`, `Views/`, `Elements/`, `Constraints/`, and
>   `Gizmo/Strategies/` subfiles; `.asmdef` files are excluded.
> - `_App/Content/Prefabs/` count of 39 is exact and includes all UI items, panels, gizmos,
>   environment, XR-rig, and spawnable asset prefabs.
> - `_App/Scenes/` 8 scenes: Bootstrap, MainMenu, Sandbox, VrEditing, Tests/Asset_Review,
>   Tests/MCP_testScene, Tests/Prototyping_UI, _Sandbox/AnimatorPanelSandbox.
> - Old structure dissolved by the restructure: `_App/_Shared/`, `_App/Subsystems/`, and
>   `_App/DemoAssets/` no longer exist on disk. All contracts, data types, and events now live
>   inside their subsystem folder under `_App/Scripts/<Subsystem>/Events/` or directly in the
>   subsystem root.
