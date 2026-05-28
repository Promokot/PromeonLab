# PromeonLab — Target File Structure

> Целевая раскладка проекта после реорганизации. Заменяет feature-based структуру (см. `STRUCTURE.md` — current state) на layer-based внутри `_App/`. Третьесторонние ассеты, Unity package data и собственный URP-конфиг — снаружи `_App/`.

## 1. Design Rules (зафиксированные решения)

| # | Решение | Следствие |
|---|---|---|
| 1 | **3 asmdef на весь `_App/`** | `_App.Runtime` + `_App.Editor` + `_App.Tests`. Per-subsystem asmdef удалены. Границы между сабсистемами теперь только папки, не сборки. |
| 2 | **Корневая папка контента — `Content/`** | Имя `Resources/` не используем (зарезервировано Unity для `Resources.Load`). |
| 3 | **`UnityPacks/` — общая папка для всего внешнего** | Третьесторонние ассет-паки + Unity package data (XR, XRI, TMP, CompositionLayers) живут вместе. |
| 4 | **`Scripts/<Subsystem>/`** | Сабсистемы остаются как организационные папки. Имена прежние. |
| 5 | **`Scripts/Core/` для общих вещей** | `EventBus.cs`, общие интерфейсы (`ICommand`), общие enum'ы (`ContainerChange`, `KeyframeChange`). Бывший `_Shared/` распущен по сабсистемам. |
| 6 | **`Content/` — гибрид по типу→назначению** | Prefabs делятся по фиче (UI/Gizmos/...). ScriptableObjects плоско, без подпапок (их ~10). |
| 7 | **`Settings/` остаётся в корне `Assets/`** | Собственные URP RP Assets, Volume Profiles. |
| 8 | **`Assets/Resources/` распущена** | Всё содержимое переехало в `_App/Content/`. Сама пустая папка `Resources/` оставлена на будущее. |
| 9 | **Тестовые сцены остаются в `_App/Scenes/`** | `_App/Scenes/Tests/` и `_App/Scenes/_Sandbox/` — как было. |
| 10 | **`_App/Editor/` плоско** | Все editor-файлы в одной папке без подпапок. |
| 11 | **Placeholder-сабсистемы сохранены** | `InputBindings`, `ErrorHandling`, `ExportPipeline`, `AnimationPlayback` — папка + файл-маркер. |
| 12 | **Удалены три «надгробия»** | `RigBuilder/Data/{BoneRecord,IkChainRecord,RigDefinition}.cs` (пустые перенаправляющие комментарии). |
| 13 | **`AppEvents.cs` разрезан** | Каждое событие живёт у своего publisher'а. |

## 2. Top-Level Layout

```
Assets/
├── _App/                       # весь проектный код и контент PromeonLab
├── Settings/                   # собственный URP-конфиг (RP Assets, Renderers, Volume Profiles)
├── UnityPacks/                 # внешнее: ассет-паки + Unity package data (XR/XRI/TMP/Composition)
├── Resources/                  # пустая, оставлена на будущее (под Resources.Load если понадобится)
├── Samples/                    # XRI Samples — не трогаем, импортированы пакетом
├── _Recovery/                  # Unity autosave — артефакты, не код
├── Screenshots/                # отладочные скриншоты
├── TutorialInfo/               # legacy URP template — кандидат на удаление
└── InputSystem_Actions.inputactions   # глобальный input-actions asset (вариант — переехать в _App/Content/Configs/)
```

## 3. `_App/` Target Tree

```
_App/
├── _App.Runtime.asmdef               # все runtime-скрипты
├── _App.Editor.asmdef                # editor-only
├── _App.Tests.asmdef                 # все тесты (вкл. internalsVisibleTo)
│
├── Scripts/                          # ─────────── RUNTIME CODE ───────────
│   ├── Core/                                 # общая инфраструктура (бывший _Shared, сильно уменьшенный)
│   │   ├── EventBus.cs                       # из _Shared/Events/EventBus.cs
│   │   ├── ICommand.cs                       # из _Shared/Interfaces/ICommand.cs
│   │   ├── ContainerChange.cs                # из _Shared/Events/ContainerChange.cs
│   │   └── KeyframeChange.cs                 # из _Shared/Events/KeyframeChange.cs
│   │
│   ├── Bootstrap/                            # entry point + lifetime scopes
│   │   ├── AppBootstrap.cs
│   │   ├── RootLifetimeScope.cs
│   │   ├── MainMenuSceneScope.cs
│   │   ├── VrEditingSceneScope.cs
│   │   ├── SandboxSceneScope.cs
│   │   ├── PlayerSpawnApplier.cs
│   │   ├── FallGuard.cs
│   │   ├── UndoKeyHandler.cs
│   │   └── VrInputFieldProxy.cs
│   │
│   ├── ModeOrchestrator/
│   │   ├── ModeOrchestrator.cs
│   │   ├── ModeTransitionGraph.cs            # был в Data/, поднят на уровень
│   │   ├── ModeChangedEvent.cs               # из AppEvents.cs
│   │   └── AppMode.cs                        # из _Shared/Models/AppMode.cs
│   │
│   ├── SceneComposition/
│   │   ├── SceneGraph.cs
│   │   ├── SceneNode.cs
│   │   ├── SelectionManager.cs
│   │   ├── SceneAutoSaver.cs
│   │   ├── CommandStack.cs                   # был в Data/
│   │   ├── TransformCommand.cs               # был в Data/
│   │   ├── Constraints/
│   │   │   └── ConstraintFreezePosition.cs   # placeholder
│   │   ├── ISceneGraph.cs                    # из _Shared/Interfaces/
│   │   ├── ISelectionManager.cs              # из _Shared/Interfaces/
│   │   ├── SceneOpenedEvent.cs               # из AppEvents.cs
│   │   ├── SceneModifiedEvent.cs             # из AppEvents.cs
│   │   ├── SceneClosedEvent.cs               # из AppEvents.cs
│   │   ├── SceneSelectedEvent.cs             # из AppEvents.cs
│   │   ├── SelectionChangedEvent.cs          # из AppEvents.cs
│   │   └── NodeRenamedEvent.cs               # из AppEvents.cs
│   │
│   ├── AssetBrowser/
│   │   ├── AssetImporter.cs
│   │   ├── AssetRegistry.cs
│   │   ├── AssetSpawner.cs
│   │   ├── BuiltinAssetLibrary.cs
│   │   ├── ImportedAssetLibrary.cs
│   │   ├── SavedAssetLibrary.cs
│   │   ├── BuiltinLabAsset.cs                # был в Data/
│   │   ├── ImportedLabAsset.cs               # был в Data/
│   │   ├── SavedLabAsset.cs                  # был в Data/
│   │   ├── DemoAssetCatalog.cs               # был в Data/
│   │   ├── IAssetLibrary.cs                  # из _Shared/Interfaces/
│   │   ├── IAssetRegistry.cs                 # из _Shared/Interfaces/
│   │   ├── ILabAsset.cs                      # из _Shared/Interfaces/
│   │   ├── AssetEntry.cs                     # из _Shared/Models/
│   │   ├── AssetType.cs                      # из _Shared/Models/
│   │   ├── AssetRef.cs                       # из _Shared/Data/
│   │   ├── AssetSource.cs                    # из _Shared/Data/
│   │   ├── AssetSpawnRequestedEvent.cs       # из AppEvents.cs
│   │   └── AssetImportedEvent.cs             # из AppEvents.cs
│   │
│   ├── AnimationAuthoring/
│   │   ├── AnimationAuthoring.cs
│   │   ├── AnimationClock.cs
│   │   ├── AnimationClipboard.cs
│   │   ├── ActionContainer.cs                # был в Data/
│   │   ├── AnimKeyData.cs                    # был в Data/
│   │   ├── AnimTrackData.cs                  # был в Data/
│   │   ├── FrameClipboard.cs                 # был в Data/
│   │   ├── FrameClipboardEntry.cs            # был в Data/
│   │   ├── SceneAnimationData.cs             # был в Data/
│   │   ├── InternalsVisibleTo.cs
│   │   ├── FrameChangedEvent.cs              # из AppEvents.cs
│   │   ├── PlaybackStateChangedEvent.cs      # из AppEvents.cs
│   │   ├── AnimationContainerChangedEvent.cs # был отдельным файлом, оставлен
│   │   └── AnimationKeyframeChangedEvent.cs  # из AppEvents.cs
│   │
│   ├── AnimationPlayback/                    # placeholder-сабсистема
│   │   └── AnimationPlayback.cs
│   │
│   ├── RigBuilder/
│   │   ├── PromeonProxyRigBuilder.cs
│   │   ├── RigRuntime.cs
│   │   ├── RigSerializer.cs
│   │   ├── BoneFollower.cs
│   │   ├── BoneProxy.cs
│   │   ├── IRigRuntime.cs                    # из _Shared/Interfaces/
│   │   ├── RigDefinition.cs                  # из _Shared/Models/ (НЕ из RigBuilder/Data/ — те три удалены)
│   │   ├── BoneRecord.cs                     # из _Shared/Models/
│   │   ├── IkChainRecord.cs                  # из _Shared/Models/
│   │   ├── BoneSceneNodeMarker.cs            # из _Shared/Models/
│   │   └── BonesVisibilityChangedEvent.cs    # был отдельным файлом, оставлен
│   │
│   ├── VrInteraction/
│   │   ├── XRPromeonInteractable.cs
│   │   ├── GizmoController.cs
│   │   ├── WorldClickCatcher.cs
│   │   ├── Selectable.cs
│   │   ├── SelectionVisualSync.cs
│   │   ├── IDragStrategy.cs
│   │   ├── Gizmo/
│   │   │   ├── GizmoActivator.cs
│   │   │   ├── GizmoConfig.cs
│   │   │   ├── GizmoHandle.cs
│   │   │   ├── GizmoHierarchy.cs
│   │   │   ├── BoundsFitter.cs
│   │   │   ├── AxisKind.cs
│   │   │   ├── HandleKind.cs
│   │   │   ├── GizmoToolsPanel.cs            # был в Gizmo/UI/
│   │   │   └── Strategies/
│   │   │       ├── IGizmoDragStrategy.cs
│   │   │       ├── AxisMoveStrategy.cs
│   │   │       ├── AxisScaleStrategy.cs
│   │   │       ├── RingRotateStrategy.cs
│   │   │       └── UniformScaleStrategy.cs
│   │   ├── GizmoMode.cs                      # из _Shared/Models/
│   │   ├── SelectionVisual.cs                # из _Shared/Data/
│   │   ├── GizmoToolsPanelOpenedEvent.cs     # из AppEvents.cs
│   │   ├── GizmoToolsPanelClosedEvent.cs     # из AppEvents.cs
│   │   ├── GizmoModeChangedEvent.cs          # из AppEvents.cs
│   │   ├── GizmoDragStartedEvent.cs          # из AppEvents.cs
│   │   └── GizmoDragEndedEvent.cs            # из AppEvents.cs
│   │
│   ├── SpatialUi/
│   │   ├── UiPanelManager.cs                 # был в Scripts/Panels/
│   │   ├── SpatialPanel.cs                   # был в Scripts/Panels/
│   │   ├── UserPanel.cs                      # был в Scripts/Panels/
│   │   ├── DetachablePanel.cs                # был в Scripts/Panels/
│   │   ├── MainMenuPanel.cs                  # был в Scripts/Panels/
│   │   ├── ScenePickerPanel.cs               # был в Scripts/Panels/
│   │   ├── PropertyPanel.cs                  # был в Scripts/Panels/
│   │   ├── SettingsModule.cs                 # был в Scripts/Panels/
│   │   ├── AssetBrowserModule.cs             # был в Scripts/Panels/
│   │   ├── BoneInspectorPanel.cs             # был в Scripts/Panels/
│   │   ├── IkSetupWizard.cs                  # был в Scripts/Panels/
│   │   ├── Views/                            # был Scripts/Views/
│   │   │   ├── AnimatorEmptyStateView.cs
│   │   │   ├── AnimatorPanelView.cs
│   │   │   ├── AnimatorToolbarView.cs
│   │   │   ├── AnimatorTransportView.cs
│   │   │   ├── AssetPropertiesView.cs
│   │   │   ├── SceneInspectorView.cs
│   │   │   └── SceneOutlinerView.cs
│   │   ├── Elements/                         # был Scripts/Elements/
│   │   │   ├── LabAssetCard.cs
│   │   │   ├── OutlinerItem.cs
│   │   │   ├── RigOutlinerItem.cs
│   │   │   ├── SceneItem.cs
│   │   │   ├── PanelDragHandle.cs
│   │   │   ├── DetachablePanelDragHandle.cs
│   │   │   ├── FileBrowserVrAnchor.cs
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
│   │   ├── PanelType.cs                      # был в Data/
│   │   ├── PanelRegistry.cs                  # был в Data/
│   │   ├── NavBarConfig.cs                   # был в Data/
│   │   ├── AnimatorPanelConfig.cs            # был в SpatialUi/Data/
│   │   ├── PanelId.cs                        # из _Shared/Models/
│   │   ├── KeyboardFocusEvent.cs             # из AppEvents.cs
│   │   ├── PanelDetachedEvent.cs             # из AppEvents.cs
│   │   ├── PanelLinkedEvent.cs               # из AppEvents.cs
│   │   └── PanelClosedEvent.cs               # из AppEvents.cs
│   │
│   ├── StorageCore/
│   │   ├── AppStorage.cs
│   │   ├── PathProvider.cs
│   │   ├── SceneSerializer.cs
│   │   ├── UnsavedChangesGuard.cs
│   │   ├── SceneData.cs                      # был в Data/
│   │   ├── NodeData.cs                       # был в Data/
│   │   └── AssetCatalogData.cs               # был в Data/
│   │
│   ├── InputBindings/                        # placeholder-сабсистема
│   │   └── InputBindings.cs
│   │
│   ├── ErrorHandling/                        # placeholder-сабсистема
│   │   ├── ErrorHandling.cs
│   │   ├── ErrorLevel.cs                     # из _Shared/Models/
│   │   └── ErrorOccurredEvent.cs             # из AppEvents.cs
│   │
│   └── ExportPipeline/                       # placeholder-сабсистема
│       └── ExportPipeline.cs
│
├── Editor/                           # ─────────── EDITOR CODE (плоско) ───────────
│   ├── EditorPlaceholder.cs
│   ├── PromeonProxyRigBuilderEditor.cs
│   ├── RemoveMissingScriptsTool.cs
│   └── AnimatorPanelModuleBuilder.cs        # был в Subsystems/SpatialUi/Editor/
│
├── Tests/                            # ─────────── ALL TESTS ───────────
│   ├── AnimationAuthoring/                  # из Subsystems/AnimationAuthoring/Tests/
│   │   ├── ActionContainerTests.cs
│   │   ├── AnimationAuthoringTests.cs
│   │   ├── AnimationClipboardTests.cs
│   │   ├── AnimationClockTests.cs
│   │   └── AnimationDataTests.cs
│   ├── RigBuilder/                          # из Subsystems/RigBuilder/Tests/
│   │   └── PromeonProxyRigBuilderTests.cs
│   ├── SceneComposition/                    # из Subsystems/SceneComposition/Tests/
│   │   ├── AssetRegistryTests.cs
│   │   ├── CommandStackTests.cs
│   │   ├── SceneGraphTests.cs
│   │   ├── SceneNodeTests.cs
│   │   └── SelectionManagerTests.cs
│   ├── StorageCore/                         # из Subsystems/StorageCore/Tests/
│   │   ├── PathProviderTests.cs
│   │   └── SceneSerializerTests.cs
│   └── VrInteraction/                       # из Subsystems/VrInteraction/Tests/
│       ├── AxisMoveStrategyTests.cs
│       ├── AxisScaleStrategyTests.cs
│       ├── BoundsFitterTests.cs
│       ├── GizmoActivatorStateTests.cs
│       ├── RingRotateStrategyTests.cs
│       └── UniformScaleStrategyTests.cs
│
├── Content/                          # ─────────── ASSETS WE OWN ───────────
│   ├── Prefabs/
│   │   ├── UI/
│   │   │   ├── Items/                       # из Subsystems/SpatialUi/Prefabs/Items/
│   │   │   │   ├── LabAssetCard_ItemUI.prefab
│   │   │   │   ├── OutlinerObject-Object_ItemUI.prefab
│   │   │   │   ├── OutlinerObject-Rig_ItemUI.prefab
│   │   │   │   ├── ScenePrefab_ItemUI.prefab
│   │   │   │   ├── TimelineKeyDiamond.prefab
│   │   │   │   ├── TimelineLane.prefab
│   │   │   │   ├── TimelineTick.prefab
│   │   │   │   ├── TimelineTickLabel.prefab
│   │   │   │   ├── TrackRow.prefab
│   │   │   │   └── UserPanelButton-PrefDefault.prefab
│   │   │   ├── Panels/                      # из Subsystems/SpatialUi/Prefabs/Panels/
│   │   │   │   ├── Static/
│   │   │   │   │   ├── MainMenuPanel.prefab
│   │   │   │   │   ├── MainMenu_CombinedPanel.prefab
│   │   │   │   │   └── ScenePickerPanel.prefab
│   │   │   │   └── UserPanel/
│   │   │   │       ├── UserPanel.prefab
│   │   │   │       ├── AnimatorPanelModule.prefab
│   │   │   │       ├── AssetBrowserModule.prefab
│   │   │   │       ├── ContextMenu_VrEditing.prefab
│   │   │   │       ├── GizmoToolsModule.prefab
│   │   │   │       ├── RiggingToolsModule.prefab
│   │   │   │       ├── SceneInspectorModule.prefab
│   │   │   │       ├── SceneOutlinerModule.prefab
│   │   │   │       └── SettingsModule.prefab
│   │   │   └── KeyframeMarker.prefab        # из Subsystems/AnimationAuthoring/UI/
│   │   ├── Gizmos/                          # из Assets/Resources/Prefabs/Gizmos/
│   │   │   ├── SceneOriginGizmo.prefab
│   │   │   └── Vr3D_Gizmos.prefab
│   │   ├── Assets/                          # builtin-ассеты сцены, из Assets/Resources/Prefabs/AssetLibraryPrefabs/
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
│   │   ├── Environment/                     # из Assets/Resources/Prefabs/Environment/
│   │   │   └── FloorDefault.prefab
│   │   └── XR/                              # из Assets/Resources/Prefabs/_User/
│   │       ├── User XR Origin (XR Rig).prefab
│   │       └── EventSystem.prefab
│   │
│   ├── ScriptableObjects/                   # все SO плоско, без подпапок
│   │   ├── DefaultDemoAssetCatalog.asset    # из _App/DemoAssets/
│   │   ├── DefaultBuiltinAssetLibrary.asset
│   │   ├── NoRigsBuiltinAssetLibrary.asset
│   │   ├── DefaultModeTransitionGraph.asset
│   │   ├── DefaultGizmoConfig.asset
│   │   ├── DefaultPanelRegistry.asset
│   │   ├── DefaultNavBarConfig.asset
│   │   ├── DefaultAnimatorPanelConfig.asset
│   │   └── AnimatorPanelConfig.asset
│   │
│   ├── Materials/                           # из Assets/Resources/Materials/
│   │   ├── CheckerFloor_Blue.mat
│   │   ├── CheckerFloor_Neutral.mat
│   │   ├── CheckerFloor_Neutralediting.mat
│   │   ├── CheckerFloor_Tests.mat
│   │   ├── MainMenuPanel-Bg.mat             # из Resources/Materials/Simple/
│   │   ├── WhiteUnlit_Blue.mat              # из Resources/Materials/Simple/
│   │   ├── WhiteUnlit_Green.mat
│   │   ├── WhiteUnlit_Red.mat
│   │   ├── WhiteUnlit_Yellow.mat
│   │   ├── crush_dummy_UE4.mat
│   │   ├── crush_dummy_UE4_red.mat
│   │   ├── NoSignal_Material.mat
│   │   ├── PromeonBoneRenderer_Material.mat
│   │   ├── TriplanarBase_000.mat            # из Resources/Materials/Shaders/triplanarSpecific/
│   │   └── Gizmo/                           # из Resources/Models/Gizmos/
│   │       ├── Gizmo_Default.mat
│   │       ├── Gizmo_Red.mat
│   │       ├── Gizmo_Green.mat
│   │       ├── Gizmo_Blue.mat
│   │       └── Gizmo_Move.mat
│   │
│   ├── Models/                              # из Assets/Resources/Models/
│   │   ├── Characters/
│   │   │   └── crush_dummy_UE4_skinned.fbx
│   │   └── Gizmos/
│   │       ├── Gizmo_Move.fbx
│   │       ├── Gizmo_Rotate.fbx
│   │       └── Gizmo_Scale.fbx
│   │
│   ├── Shaders/                             # из Resources/Materials/Shaders/triplanarSpecific/
│   │   ├── URP_TriplanarSimplified_Promokot.shadergraph
│   │   └── CheckerBase.png                  # текстура шейдера
│   │
│   ├── Textures/                            # из Assets/Resources/Textures/
│   │   ├── Checkers/
│   │   │   ├── !Texel Checker 4k 10.24.png
│   │   │   ├── !Texel Checker 4k 5.12.png
│   │   │   ├── DiffuseColor_Texture.png
│   │   │   ├── DiffuseColor_Texture_B.png
│   │   │   ├── DiffuseColor_Texture_G.png
│   │   │   ├── DiffuseColor_Texture_R.png
│   │   │   ├── DiffuseColor_TextureEditing.png
│   │   │   └── uv checker.jpg
│   │   ├── Pbr/                             # для crush_dummy
│   │   │   ├── crush_dummy_default_BaseColor.tga.png
│   │   │   ├── crush_dummy_default_Metallic.tga.png
│   │   │   ├── crush_dummy_default_Normal.tga.png
│   │   │   ├── crush_dummy_default_Occlusion.tga.png
│   │   │   └── crush_dummy_default_Roughness.tga.png
│   │   ├── Icons/                           # бывшие BuiltinLab_ObjectPrefabs/icons/ + Sprites/
│   │   │   ├── icnon_crashDummy.png
│   │   │   ├── icnon_crashDummy2.png
│   │   │   ├── icon_(Prb)CoffeTable.png
│   │   │   ├── icon_(Prb)Drawer1.png
│   │   │   ├── icon_(Prb)Storage2.png
│   │   │   ├── icon_(Prb)Toilet.png
│   │   │   ├── Plants1.png
│   │   │   ├── Plants2.png
│   │   │   ├── Plants3.png
│   │   │   ├── PlantsTree1.png
│   │   │   ├── PlantsTree2.png
│   │   │   ├── PlantsTree3.png
│   │   │   ├── 3d-Coordinate-Axis--Streamline-Core-Remix.png
│   │   │   ├── 3d-Module-Dimension--Streamline-Core-Remix.png
│   │   │   ├── 3d-Rotate-1--Streamline-Core-Remix.png
│   │   │   ├── black-keyboard-with-white-keys_icon-icons.com_72857.png
│   │   │   ├── exit_icon-icons.com_70975.png
│   │   │   ├── icons8-settings-240.png
│   │   │   ├── ObjectIcon_blalsalsadlasd.png
│   │   │   ├── RigIcon-Bring-To-Front.png
│   │   │   ├── secure-icon-png.png
│   │   │   └── TetoCar_AppIconPlaceHolder.png
│   │   └── Misc/
│   │       └── Тo-signal-Иackground-Сolorful.jpg
│   │
│   └── DemoAssets/                          # пред-собранные FBX-проекты, на которые ссылается DemoAssetCatalog
│       └── (источник prefab'ов из UnityPacks/HouseInteriorPack, Downtown Game Studio — каталог хранит GUID-ссылки)
│
├── Scenes/                           # ─────────── SCENES ───────────
│   ├── Bootstrap.unity
│   ├── MainMenu.unity
│   ├── VrEditing.unity
│   ├── Sandbox.unity
│   ├── Tests/                               # тестовые сцены — как сейчас
│   │   ├── Asset_Review.unity
│   │   ├── MCP_testScene.unity
│   │   └── Prototyping_UI.unity
│   └── _Sandbox/                            # dev-only sandbox-сцены
│       └── AnimatorPanelSandbox.unity
│
└── Documentation/
    ├── architecture_context.md
    ├── conventions.md
    └── coursework_context.md
```

## 4. Migration Map (откуда → куда)

Сводная таблица перемещений. Группы укрупнены, конкретные файлы видны в дереве выше.

### 4.1 Per-subsystem asmdef → удалены

| Удаляется | Заменяется на |
|---|---|
| `_App/_App.asmdef` | `_App/_App.Runtime.asmdef` (расширенные references) |
| `_App/_Shared/_Shared.asmdef` | — (код распущен) |
| `_App/Editor/PromeonLab.Editor.asmdef` | `_App/_App.Editor.asmdef` |
| `_App/Subsystems/*/Subsystems.*.asmdef` (×11) | — (всё в `_App.Runtime`) |
| `_App/Subsystems/*/Tests/Subsystems.*.Tests.asmdef` (×5) | `_App/_App.Tests.asmdef` (один на всё) |
| `_App/Subsystems/SpatialUi/Editor/Subsystems.SpatialUi.Editor.asmdef` | `_App/_App.Editor.asmdef` |

`_App.Runtime.asmdef` references (минимум): `Unity.TextMeshPro`, `VContainer`, `Unity.XR.Interaction.Toolkit`, `Unity.InputSystem`, `SimpleFileBrowser.Runtime`, `QuickOutline`, `Unity.Animation.Rigging`.

`_App.Editor.asmdef` references: `_App.Runtime`, `Unity.TextMeshPro`, `Unity.Animation.Rigging`. Editor-only флаг.

`_App.Tests.asmdef` references: `_App.Runtime` + NUnit. Editor-only. Требует `[InternalsVisibleTo("_App.Tests")]` в `_App.Runtime` (заменяет существующий `InternalsVisibleTo.cs` в AnimationAuthoring).

### 4.2 Распускание `_Shared/`

| Из `_App/_Shared/...` | В `_App/Scripts/...` |
|---|---|
| `Events/EventBus.cs` | `Core/EventBus.cs` |
| `Events/ContainerChange.cs` | `Core/ContainerChange.cs` |
| `Events/KeyframeChange.cs` | `Core/KeyframeChange.cs` |
| `Events/AppEvents.cs` | **разрезан** — см. таблицу 4.3 |
| `Events/AnimationContainerChangedEvent.cs` | `AnimationAuthoring/AnimationContainerChangedEvent.cs` |
| `Events/BonesVisibilityChangedEvent.cs` | `RigBuilder/BonesVisibilityChangedEvent.cs` |
| `Interfaces/ICommand.cs` | `Core/ICommand.cs` |
| `Interfaces/IAssetLibrary.cs` | `AssetBrowser/IAssetLibrary.cs` |
| `Interfaces/IAssetRegistry.cs` | `AssetBrowser/IAssetRegistry.cs` |
| `Interfaces/ILabAsset.cs` | `AssetBrowser/ILabAsset.cs` |
| `Interfaces/IRigRuntime.cs` | `RigBuilder/IRigRuntime.cs` |
| `Interfaces/ISceneGraph.cs` | `SceneComposition/ISceneGraph.cs` |
| `Interfaces/ISelectionManager.cs` | `SceneComposition/ISelectionManager.cs` |
| `Models/AppMode.cs` | `ModeOrchestrator/AppMode.cs` |
| `Models/AssetEntry.cs` | `AssetBrowser/AssetEntry.cs` |
| `Models/AssetType.cs` | `AssetBrowser/AssetType.cs` |
| `Models/BoneRecord.cs` | `RigBuilder/BoneRecord.cs` |
| `Models/BoneSceneNodeMarker.cs` | `RigBuilder/BoneSceneNodeMarker.cs` |
| `Models/ErrorLevel.cs` | `ErrorHandling/ErrorLevel.cs` |
| `Models/GizmoMode.cs` | `VrInteraction/GizmoMode.cs` |
| `Models/IkChainRecord.cs` | `RigBuilder/IkChainRecord.cs` |
| `Models/PanelId.cs` | `SpatialUi/PanelId.cs` |
| `Models/RigDefinition.cs` | `RigBuilder/RigDefinition.cs` |
| `Data/AssetRef.cs` | `AssetBrowser/AssetRef.cs` |
| `Data/AssetSource.cs` | `AssetBrowser/AssetSource.cs` |
| `Data/SelectionVisual.cs` | `VrInteraction/SelectionVisual.cs` |

### 4.3 Разрезка `AppEvents.cs`

| Event struct | Целевой файл |
|---|---|
| `SceneOpenedEvent` | `Scripts/SceneComposition/SceneOpenedEvent.cs` |
| `SceneModifiedEvent` | `Scripts/SceneComposition/SceneModifiedEvent.cs` |
| `SceneClosedEvent` | `Scripts/SceneComposition/SceneClosedEvent.cs` |
| `SceneSelectedEvent` | `Scripts/SceneComposition/SceneSelectedEvent.cs` |
| `SelectionChangedEvent` | `Scripts/SceneComposition/SelectionChangedEvent.cs` |
| `NodeRenamedEvent` | `Scripts/SceneComposition/NodeRenamedEvent.cs` |
| `ModeChangedEvent` | `Scripts/ModeOrchestrator/ModeChangedEvent.cs` |
| `FrameChangedEvent` | `Scripts/AnimationAuthoring/FrameChangedEvent.cs` |
| `PlaybackStateChangedEvent` | `Scripts/AnimationAuthoring/PlaybackStateChangedEvent.cs` |
| `AnimationKeyframeChangedEvent` | `Scripts/AnimationAuthoring/AnimationKeyframeChangedEvent.cs` |
| `ErrorOccurredEvent` | `Scripts/ErrorHandling/ErrorOccurredEvent.cs` |
| `AssetSpawnRequestedEvent` | `Scripts/AssetBrowser/AssetSpawnRequestedEvent.cs` |
| `AssetImportedEvent` | `Scripts/AssetBrowser/AssetImportedEvent.cs` |
| `KeyboardFocusEvent` | `Scripts/SpatialUi/KeyboardFocusEvent.cs` |
| `PanelDetachedEvent` | `Scripts/SpatialUi/PanelDetachedEvent.cs` |
| `PanelLinkedEvent` | `Scripts/SpatialUi/PanelLinkedEvent.cs` |
| `PanelClosedEvent` | `Scripts/SpatialUi/PanelClosedEvent.cs` |
| `GizmoToolsPanelOpenedEvent` | `Scripts/VrInteraction/GizmoToolsPanelOpenedEvent.cs` |
| `GizmoToolsPanelClosedEvent` | `Scripts/VrInteraction/GizmoToolsPanelClosedEvent.cs` |
| `GizmoModeChangedEvent` | `Scripts/VrInteraction/GizmoModeChangedEvent.cs` |
| `GizmoDragStartedEvent` | `Scripts/VrInteraction/GizmoDragStartedEvent.cs` |
| `GizmoDragEndedEvent` | `Scripts/VrInteraction/GizmoDragEndedEvent.cs` |

`AppEvents.cs` после миграции удаляется.

### 4.4 Распускание `Subsystems/<Name>/Data/` (плоско в сабсистему)

Все DTO / SO-классы из подпапок `Data/` поднимаются на уровень сабсистемы. Примеры:

- `Subsystems/AnimationAuthoring/Data/ActionContainer.cs` → `Scripts/AnimationAuthoring/ActionContainer.cs`
- `Subsystems/AssetBrowser/Data/BuiltinLabAsset.cs` → `Scripts/AssetBrowser/BuiltinLabAsset.cs`
- `Subsystems/SceneComposition/Data/CommandStack.cs` → `Scripts/SceneComposition/CommandStack.cs`
- `Subsystems/StorageCore/Data/SceneData.cs` → `Scripts/StorageCore/SceneData.cs`
- `Subsystems/SpatialUi/Data/PanelType.cs` → `Scripts/SpatialUi/PanelType.cs`
- (и так далее для всех `Data/`)

Подпапки `Data/` ликвидируются. Исключение — `VrInteraction/Gizmo/Strategies/` сохраняется (5 файлов с одной семантической ролью, логично держать вместе).

### 4.5 Распускание `Assets/Resources/`

| Из `Assets/Resources/...` | В `Assets/_App/Content/...` |
|---|---|
| `Materials/*.mat` | `Materials/` |
| `Materials/Simple/*.mat` | `Materials/` |
| `Materials/Shaders/triplanarSpecific/*.mat` | `Materials/` |
| `Materials/Shaders/triplanarSpecific/*.shadergraph` | `Shaders/` |
| `Materials/Shaders/triplanarSpecific/CheckerBase.png` | `Shaders/` |
| `Models/Characters/*.fbx` | `Models/Characters/` |
| `Models/Gizmos/*.fbx` | `Models/Gizmos/` |
| `Models/Gizmos/*.mat` | `Materials/Gizmo/` |
| `Prefabs/_User/*.prefab` | `Prefabs/XR/` |
| `Prefabs/AssetLibraryPrefabs/BuiltinLab_ObjectPrefabs/*.prefab` | `Prefabs/Assets/` |
| `Prefabs/AssetLibraryPrefabs/BuiltinLab_ObjectPrefabs/icons/*.png` | `Textures/Icons/` |
| `Prefabs/Environment/*.prefab` | `Prefabs/Environment/` |
| `Prefabs/Gizmos/*.prefab` | `Prefabs/Gizmos/` |
| `Textures/Checkers/*` | `Textures/Checkers/` |
| `Textures/Pbr/*` | `Textures/Pbr/` |
| `Textures/Sprites/*` | `Textures/Icons/` |

После миграции `Assets/Resources/` оставлена пустой — на случай если в будущем понадобится `Resources.Load`.

### 4.6 Распускание `Subsystems/SpatialUi/Prefabs/`

Все prefab'ы из `Subsystems/SpatialUi/Prefabs/Items/` и `Subsystems/SpatialUi/Prefabs/Panels/` переезжают в `_App/Content/Prefabs/UI/`. `Subsystems/AnimationAuthoring/UI/KeyframeMarker.prefab` тоже едет в `_App/Content/Prefabs/UI/`.

### 4.7 Распускание SO-инстансов

Все `.asset` (`ScriptableObject`-инстансы) из `Subsystems/<*>/Data/` и `_App/DemoAssets/` едут плоско в `_App/Content/ScriptableObjects/`.

| Откуда | Файл |
|---|---|
| `_App/DemoAssets/` | `DefaultDemoAssetCatalog.asset` |
| `Subsystems/AnimationAuthoring/Data/` | `DefaultAnimatorPanelConfig.asset` |
| `Subsystems/AssetBrowser/Data/` | `DefaultBuiltinAssetLibrary.asset`, `NoRigsBuiltinAssetLibrary.asset` |
| `Subsystems/ModeOrchestrator/Data/` | `DefaultModeTransitionGraph.asset` |
| `Subsystems/SceneComposition/Data/` | `DefaultGizmoConfig.asset` |
| `Subsystems/SpatialUi/Data/` | `AnimatorPanelConfig.asset`, `DefaultNavBarConfig.asset`, `DefaultPanelRegistry.asset` |

### 4.8 Тесты

Все Tests-папки сабсистем переезжают в `_App/Tests/<Subsystem>/`:

| Из | В |
|---|---|
| `Subsystems/AnimationAuthoring/Tests/*` (без asmdef) | `Tests/AnimationAuthoring/` |
| `Subsystems/RigBuilder/Tests/*` (без asmdef) | `Tests/RigBuilder/` |
| `Subsystems/SceneComposition/Tests/*` (без asmdef) | `Tests/SceneComposition/` |
| `Subsystems/StorageCore/Tests/*` (без asmdef) | `Tests/StorageCore/` |
| `Subsystems/VrInteraction/Tests/*` (без asmdef) | `Tests/VrInteraction/` |

### 4.9 Editor

| Из | В |
|---|---|
| `_App/Editor/*.cs` | `_App/Editor/*.cs` (без изменения) |
| `Subsystems/SpatialUi/Editor/AnimatorPanelModuleBuilder.cs` | `_App/Editor/AnimatorPanelModuleBuilder.cs` |

### 4.10 Внешние папки → `UnityPacks/`

| Из | В |
|---|---|
| `Assets/XR/` | `UnityPacks/XR/` |
| `Assets/XRI/` | `UnityPacks/XRI/` |
| `Assets/TextMesh Pro/` | `UnityPacks/TextMesh Pro/` |
| `Assets/CompositionLayers/` | `UnityPacks/CompositionLayers/` |

**Риск:** TMP внутренне грузит шрифты через `Resources.Load("Fonts & Materials/...")` — после перемещения этот путь сменится с `Assets/TextMesh Pro/Resources/...` на `Assets/UnityPacks/TextMesh Pro/Resources/...`. Unity должна найти всё через GUID, но если TMP завязан на текстовом пути — придётся откатить эту папку обратно. Аналогично XR и XRI: `XRPackageSettings` ссылается на свои `.asset` по GUID, должно пережить move; XRI грузит `InteractionLayerSettings.asset` через `Resources.Load` — путь `Resources/InteractionLayerSettings.asset` остаётся валидным (Unity рекурсивно сканирует все `Resources/` папки).

### 4.11 Удаления

- `RigBuilder/Data/BoneRecord.cs` (пустой с комментарием moved)
- `RigBuilder/Data/IkChainRecord.cs` (пустой с комментарием moved)
- `RigBuilder/Data/RigDefinition.cs` (пустой с комментарием moved)
- `Subsystems/SpatialUi/Scripts/Elements/SceneOutlinerRow.cs` (пустой с комментарием renamed)
- `_Shared/Events/AppEvents.cs` (после разрезки)
- `_App/_Shared/` (вся папка после миграции)
- `_App/DemoAssets/` (после переноса SO в Content/)
- `_App/Subsystems/` (вся иерархия после миграции — папки и `.meta` тоже удаляются вручную)

## 5. Asset Counts (After Migration)

| Folder | Approx files | Notes |
|---|---:|---|
| `_App/Scripts/` | ~165 .cs | Те же, что и раньше, переразложены |
| `_App/Editor/` | 4 .cs | плоско |
| `_App/Tests/` | ~24 .cs | по сабсистемам, один asmdef |
| `_App/Content/Prefabs/` | ~36 .prefab | UI + Gizmos + Assets + Environment + XR |
| `_App/Content/ScriptableObjects/` | ~9 .asset | плоско |
| `_App/Content/Materials/` | ~20 .mat | |
| `_App/Content/Models/` | ~4 .fbx | |
| `_App/Content/Shaders/` | 1 .shadergraph + 1 .png | |
| `_App/Content/Textures/` | ~40 .png/.jpg | Checkers, Pbr, Icons, Misc |
| `_App/Content/DemoAssets/` | 0 | SO с GUID-ссылками — данные в UnityPacks/ |
| `_App/Scenes/` | 8 .unity | Bootstrap, MainMenu, VrEditing, Sandbox + Tests/_Sandbox |
| `_App/Documentation/` | 3 .md | |
| `UnityPacks/` | без изменений + XR/XRI/TMP/Composition | |
| `Settings/` | 7 .asset | URP RP Assets, Renderers, Volume Profiles |

## 6. Migration Risks & Order

Рекомендуемый порядок перемещений (от наименее рискованного к наиболее):

1. **Документация** (`_App/Documentation/`) — не используется в рантайме, ничего не сломает.
2. **Тесты** — переехать `Tests/` папки + создать `_App.Tests.asmdef`. Удалить per-subsystem test asmdef. Прогнать тесты — должны пройти.
3. **Editor** — переместить `AnimatorPanelModuleBuilder.cs`, удалить `Subsystems.SpatialUi.Editor.asmdef`, объединить под `_App.Editor.asmdef`.
4. **`_Shared` → распустить** — переместить файлы по сабсистемам, разрезать `AppEvents.cs`. Удалить `_Shared.asmdef`. Скомпилировать — пути в исходниках не меняются (`using` тот же — namespace'ы не привязаны к папкам), но если по факту namespace'ы привязаны к именам папок (`_Shared.Events`, `_Shared.Models`) — потребуется массовая правка `using`'ов.
5. **Per-subsystem asmdef → удалить** — собрать всё в `_App.Runtime.asmdef`. Это самый рискованный шаг: если в runtime есть скрытые циклические зависимости между сабсистемами, они раньше были запрещены asmdef'ами и при их удалении могут проявиться как cycles внутри одной сборки (это уже ok — компилятор не запрещает, но логически плохо).
6. **`Subsystems/<Name>/Data/` → плоско в сабсистему** — простой move без правок кода.
7. **Prefab'ы и SO** → в `Content/`. GUID не меняется, ссылки переживают move.
8. **`Assets/Resources/` → `_App/Content/`** — то же, через GUID.
9. **XR/XRI/TMP/CompositionLayers → `UnityPacks/`** — самый рискованный, в конце. Сразу после перемещения — открыть Project Settings, проверить XR Plug-in Management; запустить любую сцену с TMP-текстом, убедиться что шрифт грузится.

После каждого шага: `Reimport All` (или `Library/` snapshot), запуск Bootstrap-сцены, прогон тестов.

## 7. Namespace Policy (рекомендация)

При нынешней структуре кода namespace'ы привязаны к asmdef'ам (`Subsystems.AnimationAuthoring`, `_Shared.Models`, etc). После слияния в один asmdef рекомендуется:

- **Корневой namespace:** `PromeonLab` (или сохранить текущий, если он есть).
- **Папки сабсистем → namespace:** `PromeonLab.AnimationAuthoring`, `PromeonLab.SceneComposition`, и т.д.
- **`Core/` → `PromeonLab.Core`** (содержит `EventBus`, `ICommand`).
- **`Editor/` → `PromeonLab.Editor`**.
- **`Tests/` → `PromeonLab.Tests.<Subsystem>`**.

Эта правка — отдельный шаг, не часть file-structure миграции. Можно отложить.
