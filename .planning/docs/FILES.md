# PromeonLab — File Catalog

> Generated 2026-05-25. Grouped by asset type. Descriptions cover the role and key API surface of each file in `Assets/_App/`. Headers in English, descriptions in Russian.

## Quick Navigation
- [Scripts (_App)](#scripts-_app)
  - [Bootstrap](#bootstrap)
  - [Editor](#editor)
  - [Subsystems/AnimationAuthoring](#subsystemsanimationauthoring)
  - [Subsystems/AnimationPlayback](#subsystemsanimationplayback)
  - [Subsystems/AssetBrowser](#subsystemsassetbrowser)
  - [Subsystems/ErrorHandling](#subsystemserrorhandling)
  - [Subsystems/ExportPipeline](#subsystemsexportpipeline)
  - [Subsystems/InputBindings](#subsystemsinputbindings)
  - [Subsystems/ModeOrchestrator](#subsystemsmodeorchestrator)
  - [Subsystems/RigBuilder](#subsystemsrigbuilder)
  - [Subsystems/SceneComposition](#subsystemsscenecomposition)
  - [Subsystems/SpatialUi](#subsystemsspatialui)
  - [Subsystems/StorageCore](#subsystemsstoragecore)
  - [Subsystems/VrInteraction](#subsystemsvrinteraction)
  - [_Shared](#_shared)
- [ScriptableObjects](#scriptableobjects)
- [Prefabs](#prefabs)
- [Scenes](#scenes)
- [Assembly Definitions](#assembly-definitions)
- [Input Actions](#input-actions)
- [Materials & Shaders](#materials--shaders)
- [Third-Party Plugins](#third-party-plugins)
- [Unity Infrastructure Folders](#unity-infrastructure-folders)

---

## Scripts (_App)

### Bootstrap

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Bootstrap/AppBootstrap.cs` | Точка входа приложения: грузит сцену `MainMenu` additively, скрывает курсор. | Public: `Start()`. Inject: — |
| `Bootstrap/FallGuard.cs` | Возвращает XR-rig в spawn-точку при провале ниже Y=-20. Требует `PlayerSpawnApplier`. | Public: — (`Update` polling); вызывает `_spawnApplier.Respawn()` |
| `Bootstrap/MainMenuSceneScope.cs` | VContainer-скоуп сцены MainMenu. | Регистрирует `UnsavedChangesGuard`, ищет `ScenePickerPanel` и `MainMenuPanel` |
| `Bootstrap/PlayerSpawnApplier.cs` | Телепортирует XR Rig в (0,0,0) при загрузке любой сцены через `XRBodyTransformer.QueueTransformation`. | Public: `Respawn()`. Subscribes: `SceneManager.sceneLoaded` |
| `Bootstrap/RootLifetimeScope.cs` | Корневой VContainer-скоуп: регистрирует `PathProvider`, `AppStorage`, `EventBus`, `AnimationClipboard`, библиотеки ассетов, `AssetRegistry`, `ModeOrchestrator`. | SerializeField: `_demoAssetCatalog`, `_transitionGraph`, `_builtinLibrary` |
| `Bootstrap/SandboxSceneScope.cs` | Сцен-скоуп для Sandbox: регистрирует `UiPanelManager`, `SceneGraph`, `SelectionManager`, `CommandStack`, `GizmoController`, `AssetImporter`, `AssetSpawner`, инжектит сценные MonoBehaviour. | SerializeField: `_panelRegistry`, `_gizmoConfig` |
| `Bootstrap/UndoKeyHandler.cs` | Обрабатывает Ctrl+Z в редакторе/standalone; блокируется во время gizmo-drag. | Inject: `CommandStack`, `EventBus`. Subscribes: `GizmoDragStartedEvent`, `GizmoDragEndedEvent` |
| `Bootstrap/VrEditingSceneScope.cs` | Сцен-скоуп для VrEditing: всё что в Sandbox + `UnsavedChangesGuard`, `SceneAutoSaver`, `AnimationClock`, `AnimationAuthoring` (как `IEntryPoint`). | SerializeField: `_panelRegistry`, `_gizmoConfig` |
| `Bootstrap/VrInputFieldProxy.cs` | На клик по TMP_InputField шлёт `KeyboardFocusEvent` через root-scope EventBus — переключает VR-клавиатуру на этот филд. | Implements `IPointerDownHandler`. Publishes: `KeyboardFocusEvent` |

### Editor

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Editor/EditorPlaceholder.cs` | Заглушка `VrAnimApp.Editor`. (пустой / placeholder) | — |
| `Editor/PromeonProxyRigBuilderEditor.cs` | Inspector-расширение для `PromeonProxyRigBuilder` с кнопкой Rebuild (через `EditorApplication.delayCall` чтобы не сломать редакторы). | `OnInspectorGUI()` |
| `Editor/RemoveMissingScriptsTool.cs` | Меню `Tools/PromeonLab/Remove Missing Scripts From Prefab Stage` — чистит broken-script компоненты в открытой prefab-stage. | `[MenuItem]` `RemoveMissingFromPrefabStage()` |

### Subsystems/AnimationAuthoring

| File | Role | Key API / Injects / Events |
|---|---|---|
| `AnimationAuthoring.cs` | Основной фасад анимации: контейнеры по узлам, ключи, копирование кадра, debounce-save в `animation.json`, sample → AnimationClip. | Ctor: `AnimationClock, ISceneGraph, PathProvider, AppStorage, EventBus`. Subscribes: `SceneOpenedEvent`, `FrameChangedEvent`. Publishes: `AnimationContainerChangedEvent`, `AnimationKeyframeChangedEvent`. Public: `CreateContainer/RemoveContainer/SetKey/DeleteKey/CopyFrame/PasteFrame/SetTotalFrames/SetFps` |
| `AnimationClipboard.cs` | Хранит последний `FrameClipboard` (1 слот). | Public: `Set`, `Clear`, `Current`, `IsEmpty` |
| `AnimationClock.cs` | Воспроизведение по таймеру: `Play/Pause/Stop/Seek/Configure`; тикает кадрами по `Time.deltaTime * Fps`. | Implements `ITickable`. Ctor: `EventBus`. Publishes: `FrameChangedEvent`, `PlaybackStateChangedEvent` |
| `Data/ActionContainer.cs` | Контейнер дорожек анимации для одного владельца (`OwnerNodeId`, `Fps`, `TotalFrames`, `Tracks`). | `GetOrCreateTrack`, `HasAnyKeyAtFrame`, `TruncateToTotalFrames` |
| `Data/AnimKeyData.cs` | DTO кадра: `Frame`, `Position`, `Rotation`, `Scale`. | (data only) |
| `Data/AnimTrackData.cs` | Дорожка по `NodeId` со списком ключей (`UpsertKey/RemoveKey/HasKey/TrimKeysAfter`). | (data) |
| `Data/FrameClipboard.cs` | Буфер копированного кадра (`OwnerNodeId`, `SourceFrame`, `Entries`). | `IsEmpty` |
| `Data/FrameClipboardEntry.cs` | Запись буфера для одной дорожки (TRS + `TrackNodeId`). | (data) |
| `Data/SceneAnimationData.cs` | Корневой JSON-документ анимации сцены (`schemaVersion=2`, список `ActionContainer`). | `FindByOwner/CreateContainer/RemoveContainer` |
| `InternalsVisibleTo.cs` | `[InternalsVisibleTo("Subsystems.AnimationAuthoring.Tests")]`. | — |
| `Tests/ActionContainerTests.cs` | NUnit-тесты `ActionContainer`. | — |
| `Tests/AnimationAuthoringTests.cs` | NUnit-тесты фасада `AnimationAuthoring` (используют `InitForTest`/`SetKeyForFrame_Test`). | — |
| `Tests/AnimationClipboardTests.cs` | NUnit-тесты `AnimationClipboard`. | — |
| `Tests/AnimationClockTests.cs` | NUnit-тесты `AnimationClock`. | — |
| `Tests/AnimationDataTests.cs` | NUnit-тесты `SceneAnimationData`/`AnimTrackData`. | — |

### Subsystems/AnimationPlayback

| File | Role | Key API / Injects / Events |
|---|---|---|
| `AnimationPlayback.cs` | (placeholder) Логика плейбэка перенесена в `AnimationAuthoring` + `AnimationClock` (Phase 7). | — |

### Subsystems/AssetBrowser

| File | Role | Key API / Injects / Events |
|---|---|---|
| `AssetImporter.cs` | Импорт FBX-файла: ищет имя в `DemoAssetCatalog`, инстанцирует prefab, возвращает `(Instance, AssetEntry)`. | Ctor: `DemoAssetCatalog, AppStorage`. Public: `ImportAsync(filePath, ct)` |
| `AssetRegistry.cs` | Lookup `ILabAsset` по `AssetRef` (Builtin/Imported/Saved). | Implements `IAssetRegistry`. Ctor: 3 библиотеки |
| `AssetSpawner.cs` | На `AssetSpawnRequestedEvent` спавнит ассет, добавляет в SceneGraph, прогоняет `IObjectResolver.InjectGameObject` для рантайм DI. | Implements `IStartable, IDisposable`. Ctor: `EventBus, SceneGraph, IObjectResolver`. Subscribes: `AssetSpawnRequestedEvent` |
| `BuiltinAssetLibrary.cs` | ScriptableObject-библиотека встроенных ассетов (read-only). | `[CreateAssetMenu]`. Implements `IAssetLibrary` |
| `Data/BuiltinLabAsset.cs` | Сериализуемая запись встроенного ассета: id, displayName, type, icon, prefab. | Implements `ILabAsset`. `SpawnAsync` = `Instantiate(_prefab,…)` |
| `Data/DemoAssetCatalog.cs` | ScriptableObject-каталог: имя файла → (prefab, type, icon). Используется `AssetImporter`. | `TryFind(fileName, out entry)` |
| `Data/ImportedLabAsset.cs` | Запись импортированного ассета на диске (путь к файлу). `SpawnAsync` — NotImplemented (drag-drop phase). | Implements `ILabAsset` |
| `Data/SavedLabAsset.cs` | Запись пользовательского сохранённого ассета. `SpawnAsync` — NotImplemented. | Implements `ILabAsset` |
| `ImportedAssetLibrary.cs` | Загрузка/сохранение `imported.json` через `PathProvider.ImportedLibraryPath`. | Implements `IAssetLibrary, IStartable`. Ctor: `PathProvider` |
| `SavedAssetLibrary.cs` | Загрузка/сохранение `saved.json` через `PathProvider.SavedLibraryPath`. | Implements `IAssetLibrary, IStartable`. Ctor: `PathProvider` |

### Subsystems/ErrorHandling

| File | Role | Key API / Injects / Events |
|---|---|---|
| `ErrorHandling.cs` | (placeholder) Подсистема обработки ошибок — заглушка. | — |

### Subsystems/ExportPipeline

| File | Role | Key API / Injects / Events |
|---|---|---|
| `ExportPipeline.cs` | (placeholder) FBX/JSON-экспорт — заглушка. | — |

### Subsystems/InputBindings

| File | Role | Key API / Injects / Events |
|---|---|---|
| `InputBindings.cs` | (placeholder) OpenXR-маппинг — заглушка. | — |

### Subsystems/ModeOrchestrator

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Data/ModeTransitionGraph.cs` | ScriptableObject со списком разрешённых переходов `From → To`. | `IsAllowed(from, to)` |
| `ModeOrchestrator.cs` | Управляет переходами `AppMode` — загружает/выгружает сцены additively, ставит активную. | Ctor: `EventBus, ModeTransitionGraph`. Public: `TransitionTo(target)`, `CurrentMode`. Publishes: `ModeChangedEvent` |

### Subsystems/RigBuilder

| File | Role | Key API / Injects / Events |
|---|---|---|
| `BoneFollower.cs` | `[ExecuteAlways]` — компонент на кости меша, синкает позицию/ротацию из proxy-кости каждый `LateUpdate`. | Public: `SetProxy(Transform)`, `Tick()` |
| `BoneProxy.cs` | Прокси-MonoBehaviour для кости: знает `BoneName`, исходный `Transform`, `nodeId`; на `OnSelected` зовёт `SelectionManager.Select`. | Inject: `ISelectionManager`. Public: `Init`, `OnSelected` |
| `Data/BoneRecord.cs` | (`// Moved to _Shared/Models/BoneRecord.cs`) — placeholder. | — |
| `Data/IkChainRecord.cs` | (`// Moved to _Shared/Models/IkChainRecord.cs`) — placeholder. | — |
| `Data/RigDefinition.cs` | (`// Moved to _Shared/Models/RigDefinition.cs`) — placeholder. | — |
| `PromeonProxyRigBuilder.cs` | MonoBehaviour, строит/перестраивает иерархию diamond-mesh proxy-костей со своими коллайдерами, Outline, `Selectable` и `XRPromeonInteractable`. Регенерирует мэши при загрузке prefab; разруливает duplicate outline-materials. | Inject: `EventBus`. Subscribes: `SelectionChangedEvent`. Public: `Rebuild`, `SetVisualsEnabled`, `SetBonesInteractive`, `SetTransforms`, `SetMaterial`, `ProxyGOs` |
| `RigRuntime.cs` | MonoBehaviour-реализация `IRigRuntime`: строит `RigDefinition` из `SkinnedMeshRenderer`, применяет — настраивает `PromeonProxyRigBuilder` и инжектит DI в новые proxy-GO. | Inject: `IObjectResolver`. Public: `BuildFromSkinnedMesh`, `ApplyDefinition` |
| `RigSerializer.cs` | JSON-сериализация `RigDefinition` через `JsonUtility`. | `Serialize`, `Deserialize` |
| `Tests/PromeonProxyRigBuilderTests.cs` | NUnit-тесты ProxyRigBuilder. | — |

### Subsystems/SceneComposition

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Constraints/ConstraintFreezePosition.cs` | Класс `Акуу` (пустой / placeholder, заметка к проекту). | — |
| `Data/CommandStack.cs` | Undo-стек команд `ICommand` с ёмкостью 30 (без Redo). | Public: `Execute(cmd)`, `Undo()`. Используется `GizmoController`/`UndoKeyHandler` |
| `Data/TransformCommand.cs` | `ICommand` для коммита нового TRS трансформа с возможностью отката. | `Execute`, `Undo` |
| `SceneAutoSaver.cs` | На выходе из `VrEditing` сохраняет активную сцену через `AppStorage.SaveSceneAsync` + `SceneClosedEvent`. | Implements `IStartable, IDisposable`. Ctor: `EventBus, SceneGraph, AppStorage`. Subscribes: `ModeChangedEvent`. Publishes: `SceneClosedEvent` |
| `SceneGraph.cs` | Имплементация `ISceneGraph` + `IStartable/IDisposable`: словарь рантайм-узлов + transient-bone-узлов, переписывает `NodeId` костей в `bone:{rigId}:{boneName}`, грузит сцену из JSON. | Ctor: `EventBus, IAssetRegistry, IObjectResolver, AppStorage`. Subscribes: `SceneOpenedEvent`. Publishes: `SceneModifiedEvent`. Public: `AddNode/RemoveNode/GetNode/AddTransientNode/CaptureSnapshot` |
| `SceneNode.cs` | MonoBehaviour-узел: `NodeId`, `AssetRef`, `DisplayName`, `IsVisible/Locked`. | Public: `Init`, `SetNodeId`, `SetDisplayName`, `SetVisible`, `SetLocked` |
| `SelectionManager.cs` | Хранит `SelectedNodeId` (single-select). | Implements `ISelectionManager, IStartable, IDisposable`. Ctor: `EventBus`. Publishes: `SelectionChangedEvent` |
| `Tests/AssetRegistryTests.cs` | NUnit-тесты `AssetRegistry`. | — |
| `Tests/CommandStackTests.cs` | NUnit-тесты `CommandStack`. | — |
| `Tests/SceneGraphTests.cs` | NUnit-тесты `SceneGraph`. | — |
| `Tests/SceneNodeTests.cs` | NUnit-тесты `SceneNode`. | — |
| `Tests/SelectionManagerTests.cs` | NUnit-тесты `SelectionManager`. | — |

### Subsystems/SpatialUi

#### Data

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Data/AnimatorPanelConfig.cs` | ScriptableObject-конфиг таймлайна аниматора: `FramePx`, цвета ключей/строк, defaults. | `[CreateAssetMenu]` |
| `Data/NavBarConfig.cs` | ScriptableObject со списком NavBar-entry (id, visibleModes, exclusiveGroup). | `TryGetEntry`, `IsVisibleInMode` |
| `Data/PanelRegistry.cs` | ScriptableObject-список спаунимых `SpatialPanel`-prefab'ов с фильтром по `AppMode`. | `IsVisibleIn(id, mode)` |
| `Data/PanelType.cs` | `enum PanelType { BodyLocked, WorldFixed, Free }`. | — |

#### Editor

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Editor/AnimatorPanelModuleBuilder.cs` | Editor-меню `PromeonLab/Build/*`: процедурно собирает prefab `AnimatorPanelModule.prefab`, вшивает его в `UserPanel.prefab` и регистрирует entry "animator" в `NavBarConfig`. | `[MenuItem]` `BuildAndSave`, `VerifyWiring`, `WireUserPanel`, `RegisterAnimatorNavBarEntry` |

#### Scripts/Elements

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Scripts/Elements/DetachablePanelDragHandle.cs` | Drag-handle для `DetachablePanel`: проектирует screen-delta в world и зовёт `MoveDelta`. | Implements `IPointer*Handler, IDragHandler` |
| `Scripts/Elements/FileBrowserVrAnchor.cs` | Привязывает SimpleFileBrowser-canvas к позиции `AssetBrowserModule` в VR, прячет диалог при свернутом панеле. | (no DI) |
| `Scripts/Elements/LabAssetCard.cs` | UI-карточка ассета в галерее, событие `Selected` при клике. | Public: `Bind(ILabAsset)`, `event Selected` |
| `Scripts/Elements/OutlinerItem.cs` | Базовая строка outliner'а: лейбл, indent, highlight, click. | Public: `Bind(node, indent, onClick)`, `SetVisualState`, `SetLabel` |
| `Scripts/Elements/PanelDragHandle.cs` | Drag-handle для `UserPanel` (аналог Detachable, для прикреплённой панели). | Implements `IPointer*Handler, IDragHandler` |
| `Scripts/Elements/RigOutlinerItem.cs` | Подтип `OutlinerItem` для рига — подсветка фона/иконки when bones-mode active. | Public: `SetBonesMode(bool)` |
| `Scripts/Elements/SceneItem.cs` | Кнопка-строка списка сцен в `ScenePickerPanel`. | Public: `Init`, `SetSelected`, `event Clicked` |
| `Scripts/Elements/SceneOutlinerRow.cs` | (`// Renamed to OutlinerItem.cs`) — placeholder. | — |
| `Scripts/Elements/TimelineInputHandler.cs` | Pointer-handler на полотне таймлайна: преобразует клик/драг в номер кадра, шлёт `OnFrameRequested`. | Implements `IPointerDownHandler, IDragHandler` |
| `Scripts/Elements/TimelineLaneView.cs` | Одна горизонтальная дорожка с pool-ом ключей-ромбиков; раскрашивает текущий ключ. | Public: `Bind`, `SetActive`, `SetKeys(frames, currentFrame)` |
| `Scripts/Elements/TimelineLanesView.cs` | Контейнер дорожек, pool-управление `TimelineLaneView`. | Public: `Rebuild`, `FindLane(nodeId)` |
| `Scripts/Elements/TimelinePlayheadView.cs` | Вертикальная полоса-playhead с числом кадра. | Public: `SetFrame(int)`, `SetHeight(float)` |
| `Scripts/Elements/TimelineRulerView.cs` | Линейка кадров: pool тиков + label'ов через `_majorTickInterval`. | Public: `Rebuild(totalFrames)` |
| `Scripts/Elements/TimelineScrollSync.cs` | Синкает вертикальный скролл `TracksColumn` с timeline-скроллом. | (`OnEnable/OnDisable` listeners) |
| `Scripts/Elements/TrackRowView.cs` | Строка слева от таймлайна (Object/Rig/Bone): лейбл, иконка, hasKey-dot, indent, фон. | Public: `Bind(nodeId, name, kind, hasKeys, indent, onClick)`, `SetActive(bool)` |
| `Scripts/Elements/UserPanelKeyboardToggle.cs` | Кнопка переключения дефолтного контента UserPanel ↔ VR-клавиатуры. | (no DI) |
| `Scripts/Elements/UserPanelOpener.cs` | XR primaryButton (X/A) — toggle видимости `UserPanel` через `InputAction`. | Subscribes: `<XRController>{*Hand}/primaryButton` |
| `Scripts/Elements/VrKeyboard.cs` | VR-клавиатура: на `KeyboardFocusEvent` цепляется к TMP_InputField, кнопки добавляют/стирают символы. | Inject: `EventBus`. Subscribes: `KeyboardFocusEvent`. Public: `AddLetter`, `DeleteLetter`, `SubmitWord` |

#### Scripts/Panels

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Scripts/Panels/AssetBrowserModule.cs` | UserPanel-модуль галереи ассетов: табы (builtin/imported/saved), grid, импорт через SimpleFileBrowser, кнопка Spawn. | Inject: `ModeOrchestrator, BuiltinAssetLibrary, ImportedAssetLibrary, SavedAssetLibrary, EventBus`. Subscribes: `ModeChangedEvent`. Publishes: `AssetSpawnRequestedEvent` |
| `Scripts/Panels/BoneInspectorPanel.cs` | Панель свойств выбранного рига: кнопки Build Rig / Open IK Wizard, счётчик костей. | Inject: `IRigRuntime, ISelectionManager, ISceneGraph, IkSetupWizard` |
| `Scripts/Panels/DetachablePanel.cs` | Базовый компонент детачимой панели: Show/Hide/Unlink/LinkBack, поддержка lock и close. | Inject: `EventBus`. Publishes: `PanelDetachedEvent`, `PanelLinkedEvent`, `PanelClosedEvent`. Public: `Show/Hide/Unlink/LinkBack/MoveDelta/ToggleLinked` |
| `Scripts/Panels/IkSetupWizard.cs` | Мини-визард IK: dropdown'ы Root/End-кости из текущего рига, добавляет `IkChainRecord` и переапплаит. | Inject: `IRigRuntime, ISelectionManager, ISceneGraph`. Public: `OpenForSelection()` |
| `Scripts/Panels/MainMenuPanel.cs` | Главное меню: Open Sandbox / Open Scene. | Inject: `AppStorage, EventBus, ModeOrchestrator`. Subscribes: `SceneSelectedEvent`. Publishes: `SceneOpenedEvent` |
| `Scripts/Panels/PropertyPanel.cs` | TMP-вывод TRS выбранного узла. | Inject: `EventBus, ISceneGraph`. Implements `IStartable, IDisposable`. Subscribes: `SelectionChangedEvent` |
| `Scripts/Panels/ScenePickerPanel.cs` | Список сохранённых сцен + create/delete. | Inject: `AppStorage, EventBus`. Publishes: `SceneSelectedEvent` |
| `Scripts/Panels/SettingsModule.cs` | (placeholder) пустой настроечный модуль UserPanel. | — |
| `Scripts/Panels/SpatialPanel.cs` | Базовый класс VR-панели: `BodyLocked`/`WorldFixed`/`Free`, lazy-follow, billboard. | Public: `Init(id, cameraTransform)`, `SetVisible(bool)`, `PanelId` |
| `Scripts/Panels/UiPanelManager.cs` | Спавнит `PanelRegistry.Panels` через `IObjectResolver.Instantiate`, переключает видимость по `AppMode`. | Implements `IStartable, IDisposable`. Ctor: `EventBus, PanelRegistry, Camera, IObjectResolver`. Subscribes: `ModeChangedEvent` |
| `Scripts/Panels/UserPanel.cs` | Главный hand-attached панель: NavBar-bindings, lock, smart-follow, MainMenu/Exit, регистрирует `EntryId` каждого `DetachablePanel`. | Inject: `ModeOrchestrator, EventBus`. Public: `ResetPosition`, `SetDragging`, `MoveDelta` |

#### Scripts/Views

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Scripts/Views/AnimatorEmptyStateView.cs` | Пустое состояние Animator-панели: NoSelection / NoContainer + кнопка Add Animation. | Public: `Show(State)`, `HideAll()`, `event OnAddAnimationClicked` |
| `Scripts/Views/AnimatorPanelView.cs` | View-композитор Animator-модуля: ruler/lanes/playhead/tracks, обработка кнопок toolbar/transport, sync с `AnimationAuthoring` и `AnimationClock`. | Inject: `EventBus, AnimationAuthoring, AnimationClock, ISelectionManager, AnimationClipboard, SceneGraph`. Subscribes: `SelectionChangedEvent`, `FrameChangedEvent`, `PlaybackStateChangedEvent`, `AnimationContainerChangedEvent`, `AnimationKeyframeChangedEvent` |
| `Scripts/Views/AnimatorToolbarView.cs` | Верхняя панель Animator'а: frame/total/fps inputs + key/copy/paste/remove кнопки; Action-callbacks наружу. | Public: `Set*Interactable`, `SetCurrentFrame/SetTotalFrames/SetFps`, `On*` events |
| `Scripts/Views/AnimatorTransportView.cs` | Нижняя transport-панель: prev-key/frame/start/play/end/next-frame/next-key + смена sprite-иконки play/pause. | Public: `SetPlaying(bool)`, `On*` events |
| `Scripts/Views/AssetPropertiesView.cs` | Простой view свойств `ILabAsset` (name/type/icon). | Public: `Bind(ILabAsset)` |
| `Scripts/Views/SceneInspectorView.cs` | Инспектор: имя, TRS, type/icon, sub-state для bone (rig/bone name); toggle "Show Bones", delete. | Inject: `EventBus, SceneGraph, ISelectionManager, IAssetRegistry`. Subscribes: `SelectionChangedEvent` |
| `Scripts/Views/SceneOutlinerView.cs` | Outliner-список объектов сцены + опционально расширяющиеся кости. | Inject: `EventBus, SceneGraph, ISelectionManager`. Subscribes: `SceneModifiedEvent`, `SelectionChangedEvent`, `NodeRenamedEvent`, `BonesVisibilityChangedEvent` |

### Subsystems/StorageCore

| File | Role | Key API / Injects / Events |
|---|---|---|
| `AppStorage.cs` | CRUD над сценами на диске + in-memory кэш; sandbox-session с фиктивным id `__sandbox__`. | Ctor: `PathProvider`. Public: `CreateSceneAsync/LoadSceneAsync/SaveSceneAsync/DeleteScene/GetAllScenesAsync/SetActiveScene/BeginSandboxSession/ActiveSceneId` |
| `Data/AssetCatalogData.cs` | DTO каталога ассетов сцены (`SchemaVersion=1`, `SceneId`, `Entries`). | (data) |
| `Data/NodeData.cs` | DTO ноды: id, assetRef, TRS, displayName, parentNodeId. | (data) |
| `Data/SceneData.cs` | DTO сцены (`SchemaVersion=2`, sceneId, displayName, createdAt, nodes). | (data) |
| `PathProvider.cs` | Все пути в `persistentDataPath` — `SceneRoot/SceneJson/AssetCatalogJson/AnimationJson/AssetPath/ExportDir/ScenesRoot/ImportedLibraryPath/SavedLibraryPath`. | `[VContainer.Inject]` ctor; параметризованный ctor для тестов |
| `SceneSerializer.cs` | JSON-сериализация `SceneData` + миграция v1→v2 (присвоение пустого `Nodes`). | `Serialize`, `Deserialize` |
| `Tests/PathProviderTests.cs` | NUnit-тесты `PathProvider`. | — |
| `Tests/SceneSerializerTests.cs` | NUnit-тесты round-trip и v1→v2 миграции. | — |
| `UnsavedChangesGuard.cs` | Флаг `IsDirty` от `SceneModifiedEvent`, сбрасывается `SceneOpenedEvent`. | Implements `IStartable, IDisposable`. Ctor: `EventBus`. Public: `CanNavigate`, `ClearDirty` |

### Subsystems/VrInteraction

| File | Role | Key API / Injects / Events |
|---|---|---|
| `Gizmo/AxisKind.cs` | `enum AxisKind { X, Y, Z, Uniform }`. | — |
| `Gizmo/BoundsFitter.cs` | Подбирает размер гизмо по bounding box ренгдеров (`coefficient` × max extent, clamped). | Static `ComputeSize` |
| `Gizmo/GizmoActivator.cs` | Корень управления гизмо: спавнит prefab из `GizmoConfig`, подписывается на режимы/выбор, обрабатывает grab/drag/release ручек, стратегии Move/Rotate/Scale, инвертирует scale через mirror-фактор. | Inject: `EventBus, SceneGraph, ISelectionManager, GizmoController`. Subscribes: `GizmoToolsPanelOpened/Closed/ModeChanged/SelectionChanged`. Publishes: `GizmoDragStarted/EndedEvent`. Public: `OnHandleGrabbed/Dragged/Released/Aborted` |
| `Gizmo/GizmoConfig.cs` | ScriptableObject: prefab гизмо + `boundsCoefficient/min/max size`. | `[CreateAssetMenu]` |
| `Gizmo/GizmoHandle.cs` | XRBaseInteractable одной ручки (axis+kind): сам читает grip-инпут `NearFarInteractor`, считает виртуальную руку как точку перед контроллером. | Public: `Bind(GizmoActivator)`, `Kind`, `Axis` |
| `Gizmo/GizmoHierarchy.cs` | MonoBehaviour, держит ссылки на корни Move/Rotate/Scale; тонирует активную ручку через material-instance подмены. | Public: `ShowMode(GizmoMode)`, `OnHandleGrabbed/Released(handle, mode)`, `ResetHierarchy` |
| `Gizmo/HandleKind.cs` | `enum HandleKind { Center, Axis, Ring, Box }` (плагинная классификация). | — |
| `Gizmo/Strategies/AxisMoveStrategy.cs` | Drag-стратегия трансляции вдоль локальной оси; project'ит руку на ось. | Implements `IGizmoDragStrategy`. `BeginDrag/UpdateDrag/EndDrag` |
| `Gizmo/Strategies/AxisScaleStrategy.cs` | Drag-стратегия per-axis scale на основании дистанции рука↔pivot. | Implements `IGizmoDragStrategy` |
| `Gizmo/Strategies/IGizmoDragStrategy.cs` | Контракт стратегии: `BeginDrag/UpdateDrag/EndDrag` с `Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot`. | — |
| `Gizmo/Strategies/RingRotateStrategy.cs` | Drag-стратегия вращения вокруг оси по углу руки относительно pivot. | Implements `IGizmoDragStrategy` |
| `Gizmo/Strategies/UniformScaleStrategy.cs` | Uniform scale: рост коэффициента по дистанции от центра. | Implements `IGizmoDragStrategy` |
| `Gizmo/UI/GizmoToolsPanel.cs` | UI-панель Move/Rotate/Scale: переключает `GizmoMode`, во время drag блокирует кнопки. | Inject: `EventBus`. Subscribes: `GizmoDragStarted/EndedEvent`. Publishes: `GizmoToolsPanelOpened/ClosedEvent`, `GizmoModeChangedEvent` |
| `GizmoController.cs` | Тонкая обёртка над `CommandStack`: получает SelectionChanged → знает target, `CommitTransform`/`CommitMove` пушит `TransformCommand`. | Implements `IStartable, IDisposable`. Ctor: `EventBus, CommandStack, SceneGraph`. Subscribes: `SelectionChangedEvent` |
| `IDragStrategy.cs` | `IDragStrategy` + `SingleDragStrategy` для XRPromeonInteractable (move-only / rotate-only по `DragMode`). | — |
| `Selectable.cs` | Компонент-маркер: добавляет/тоглит QuickOutline по `SelectionVisual`. | Public: `SetVisualState`, `NodeId`, `Node` |
| `SelectionVisualSync.cs` | На `SelectionChangedEvent` обходит `SceneGraph.Nodes` и зовёт `Selectable.SetVisualState`. | Implements `IStartable, IDisposable`. Ctor: `EventBus, SceneGraph`. Subscribes: `SelectionChangedEvent` |
| `Tests/AxisMoveStrategyTests.cs` | NUnit-тесты `AxisMoveStrategy`. | — |
| `Tests/AxisScaleStrategyTests.cs` | NUnit-тесты `AxisScaleStrategy`. | — |
| `Tests/BoundsFitterTests.cs` | NUnit-тесты `BoundsFitter`. | — |
| `Tests/GizmoActivatorStateTests.cs` | NUnit-тесты defensive-логики state-machine `GizmoActivator`. | — |
| `Tests/RingRotateStrategyTests.cs` | NUnit-тесты `RingRotateStrategy`. | — |
| `Tests/UniformScaleStrategyTests.cs` | NUnit-тесты `UniformScaleStrategy`. | — |
| `WorldClickCatcher.cs` | На `activateInput` (trigger) если interactor ни на UI ни на интерактабле — снимает выделение. | Inject: `ISelectionManager`. Public: — |
| `XRPromeonInteractable.cs` | Кастомный `XRBaseInteractable`: tap-trigger = select, hold-trigger = rotate, hold-grip = move. XRI select-flow выключен (`IsSelectableBy=false`), коллайдеры контролируются явно. | Inject: `ISelectionManager, GizmoController`. Public: `RegisterColliders(IEnumerable<Collider>)` |

### _Shared

#### _Shared/Data

| File | Role | Key API |
|---|---|---|
| `Data/AssetRef.cs` | `struct AssetRef { AssetSource Source; string AssetId }`. | (data) |
| `Data/AssetSource.cs` | `enum AssetSource { Builtin, Imported, Saved }`. | — |
| `Data/SelectionVisual.cs` | `enum SelectionVisual { None, Selected }`. | — |

#### _Shared/Events

| File | Role | Key API |
|---|---|---|
| `Events/AnimationContainerChangedEvent.cs` | `struct` (OwnerNodeId + `ContainerChange`). | — |
| `Events/AppEvents.cs` | Свалка struct-событий: `SceneOpened/Modified/Closed/Selected`, `SelectionChanged`, `NodeRenamed`, `ModeChanged`, `FrameChanged`, `PlaybackStateChanged`, `ErrorOccurred`, `AssetSpawnRequested`, `KeyboardFocus`, `PanelDetached/Linked/Closed`, `AnimationKeyframeChanged`, `GizmoToolsPanelOpened/Closed`, `GizmoModeChanged`, `GizmoDragStarted/Ended`. | (events) |
| `Events/BonesVisibilityChangedEvent.cs` | `struct` (RigNodeId + Visible). | — |
| `Events/ContainerChange.cs` | `enum { Added, Removed, LengthChanged, FpsChanged }`. | — |
| `Events/EventBus.cs` | Простой in-memory pub/sub по `Type → List<Action<T>>`. (Не MessagePipe, своя реализация.) | Public: `Subscribe<T>`, `Unsubscribe<T>`, `Publish<T>` (T : struct) |
| `Events/KeyframeChange.cs` | `enum { Added, Removed, Overwritten }`. | — |

#### _Shared/Interfaces

| File | Role | Key API |
|---|---|---|
| `Interfaces/IAssetLibrary.cs` | Библиотека ассетов: `Assets`, `LoadAsync/SaveAsync/Add/Remove`. | — |
| `Interfaces/IAssetRegistry.cs` | `ILabAsset Find(AssetRef)`. | — |
| `Interfaces/ICommand.cs` | `Execute/Undo` — контракт для `CommandStack`. | — |
| `Interfaces/ILabAsset.cs` | `Id/DisplayName/Type/Icon/SpawnAsync`. | — |
| `Interfaces/IRigRuntime.cs` | `BuildFromSkinnedMesh`, `ApplyDefinition`. | — |
| `Interfaces/ISceneGraph.cs` | `GetNode/AddNode/RemoveNode`. | — |
| `Interfaces/ISelectionManager.cs` | `SelectedNodeId`, `Select(nodeId)`. | — |

#### _Shared/Models

| File | Role | Key API |
|---|---|---|
| `Models/AppMode.cs` | `enum AppMode { MainMenu, VrEditing, Sandbox, Debug }`. | — |
| `Models/AssetEntry.cs` | Сериализуемая запись ассета в каталоге сцены. | (data) |
| `Models/AssetType.cs` | `enum { Model, Rig, Texture, Material, Video, Audio, Pose }`. | — |
| `Models/BoneRecord.cs` | Сериализуемая запись кости (`BoneName`, `TranslationLocked`). | (data) |
| `Models/BoneSceneNodeMarker.cs` | Marker-компонент для proxy-кости — SceneGraph по нему перетягивает NodeId в `bone:{rigId}:{name}`. | — |
| `Models/ErrorLevel.cs` | `enum { Warning, Error, Critical }`. | — |
| `Models/GizmoMode.cs` | `enum { Move, Rotate, Scale }`. | — |
| `Models/IkChainRecord.cs` | Сериализуемая запись IK-цепочки (Root/End/Pole/Weight). | (data) |
| `Models/PanelId.cs` | `enum PanelId { Toolbar, Properties, RigBuilder, KeyframeEditor, SceneOutliner, ComingSoon, UserPanel }`. | — |
| `Models/RigDefinition.cs` | Сериализуемый риг: `SchemaVersion=1`, `AssetId`, `List<BoneRecord>`, `List<IkChainRecord>`. | (data) |

---

## ScriptableObjects

| File | Type | Role | Consumed by |
|---|---|---|---|
| `_App/DemoAssets/DefaultDemoAssetCatalog.asset` | `DemoAssetCatalog` | Каталог `fileName→prefab` для импортируемых FBX. | `AssetImporter`, `RootLifetimeScope` |
| `_App/Subsystems/AnimationAuthoring/Data/DefaultAnimatorPanelConfig.asset` | `AnimatorPanelConfig` | Конфиг таймлайна (FramePx, цвета). | `AnimatorPanelView`/`*View` (через `_config`) |
| `_App/Subsystems/AssetBrowser/Data/DefaultBuiltinAssetLibrary.asset` | `BuiltinAssetLibrary` | Список встроенных ассетов (по умолчанию). | `RootLifetimeScope._builtinLibrary` |
| `_App/Subsystems/AssetBrowser/Data/NoRigsBuiltinAssetLibrary.asset` | `BuiltinAssetLibrary` | Альтернативный набор (без ригов) — для отладки/Sandbox. | — (свопится в инспекторе) |
| `_App/Subsystems/ModeOrchestrator/Data/DefaultModeTransitionGraph.asset` | `ModeTransitionGraph` | Граф разрешённых переходов между режимами. | `ModeOrchestrator` (через `RootLifetimeScope`) |
| `_App/Subsystems/SceneComposition/Data/DefaultGizmoConfig.asset` | `GizmoConfig` | Prefab + размеры гизмо. | `GizmoActivator` (через VrEditing/Sandbox scope) |
| `_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.asset` | `AnimatorPanelConfig` | Рабочий конфиг Animator-панели, используется в prefab'е. | `AnimatorPanelView` и его sub-views |
| `_App/Subsystems/SpatialUi/Data/DefaultNavBarConfig.asset` | `NavBarConfig` | Карта NavBar-кнопок UserPanel: entry id → visibleModes/exclusiveGroup. | `UserPanel` |
| `_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset` | `PanelRegistry` | Список `SpatialPanel`-prefab'ов с фильтром по `AppMode`. | `UiPanelManager` (через `VrEditingSceneScope`/`SandboxSceneScope`) |
| `Settings/Mobile_RPAsset.asset` | URP RP Asset | Mobile-render-pipeline (Quest target). | URP Quality settings |
| `Settings/Mobile_Renderer.asset` | URP Renderer | Renderer для mobile-RP. | `Mobile_RPAsset` |
| `Settings/PC_RPAsset.asset` | URP RP Asset | PC fallback / editor preview. | URP Quality |
| `Settings/PC_Renderer.asset` | URP Renderer | Renderer для PC-RP. | `PC_RPAsset` |
| `Settings/DefaultVolumeProfile.asset` | Volume Profile | Дефолтные пост-эффекты. | URP global |
| `Settings/SampleSceneProfile.asset` | Volume Profile | Пресет для sample-сцен. | Volume в сценах |
| `Settings/UniversalRenderPipelineGlobalSettings.asset` | URP Global Settings | Глобальный URP-конфиг. | URP runtime |
| `_App/Readme.asset` | Readme (Unity sample) | Стартовое README в Project view. | — |

---

## Prefabs

| File | Role | Key components |
|---|---|---|
| `_App/Subsystems/AnimationAuthoring/UI/KeyframeMarker.prefab` | Маркер ключа на старой таймлайн-полосе (до AnimatorPanel v4). | UI Image / RectTransform |
| `_App/Subsystems/SpatialUi/Prefabs/Items/LabAssetCard_ItemUI.prefab` | UI-карточка ассета в галерее. | `LabAssetCard`, Image, Button |
| `_App/Subsystems/SpatialUi/Prefabs/Items/OutlinerObject-Object_ItemUI.prefab` | Строка outliner'а для обычного объекта. | `OutlinerItem` |
| `_App/Subsystems/SpatialUi/Prefabs/Items/OutlinerObject-Rig_ItemUI.prefab` | Строка outliner'а для рига (раскрывается в кости). | `RigOutlinerItem` |
| `_App/Subsystems/SpatialUi/Prefabs/Items/ScenePrefab_ItemUI.prefab` | Строка списка сцен в `ScenePickerPanel`. | `SceneItem`, Button |
| `_App/Subsystems/SpatialUi/Prefabs/Items/TimelineKeyDiamond.prefab` | Ромбик-ключ в `TimelineLaneView`. | Image |
| `_App/Subsystems/SpatialUi/Prefabs/Items/TimelineLane.prefab` | Шаблон дорожки таймлайна. | `TimelineLaneView` |
| `_App/Subsystems/SpatialUi/Prefabs/Items/TimelineTick.prefab` | Тик линейки таймлайна. | RectTransform + Image |
| `_App/Subsystems/SpatialUi/Prefabs/Items/TimelineTickLabel.prefab` | Текст-метка тика. | TextMeshProUGUI |
| `_App/Subsystems/SpatialUi/Prefabs/Items/TrackRow.prefab` | Шаблон строки в TracksColumn. | `TrackRowView` |
| `_App/Subsystems/SpatialUi/Prefabs/Items/UserPanelButton-PrefDefault.prefab` | Шаблон NavBar-кнопки UserPanel. | Button, Image |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/Static/MainMenuPanel.prefab` | Главное меню (Open Sandbox / Open Scene). | `MainMenuPanel` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/Static/MainMenu_CombinedPanel.prefab` | Композитная панель MainMenu + ScenePicker для bootstrap сцены. | MainMenuPanel + ScenePickerPanel (вложен) |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/Static/ScenePickerPanel.prefab` | Список сцен, create/delete. | `ScenePickerPanel`, ScrollRect |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimatorPanelModule.prefab` | Процедурно собранный AnimatorPanel: ruler/lanes/playhead/tracks + toolbar + transport. | `DetachablePanel`, `AnimatorPanelView`, `AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorEmptyStateView`, `TimelineRulerView/LanesView/PlayheadView/InputHandler/ScrollSync` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AssetBrowserModule.prefab` | Галерея ассетов (Builtin/Imported/Saved + Add/Spawn). | `DetachablePanel`, `AssetBrowserModule` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/ContextMenu_VrEditing.prefab` | Контекстное меню в режиме VrEditing. | UI menu |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/GizmoToolsModule.prefab` | Move/Rotate/Scale toggle-кнопки. | `DetachablePanel`, `GizmoToolsPanel` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/RiggingToolsModule.prefab` | Build Rig + IK Wizard. | `DetachablePanel`, `BoneInspectorPanel`, `IkSetupWizard` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab` | Инспектор узла (TRS + bone-state + show bones). | `DetachablePanel`, `SceneInspectorView` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneOutlinerModule.prefab` | Дерево объектов сцены. | `DetachablePanel`, `SceneOutlinerView` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SettingsModule.prefab` | (placeholder) пустая Settings-панель. | `DetachablePanel`, `SettingsModule` |
| `_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab` | Хост-панель (NavBar + slots): держит все модули, lock/main-menu/exit. | `UserPanel`, `PanelDragHandle`, `UserPanelKeyboardToggle`, `VrKeyboard` |

---

## Scenes

| File | Role |
|---|---|
| `_App/Scenes/Bootstrap.unity` | Bootstrap: `RootLifetimeScope` + `AppBootstrap`. Грузит MainMenu additively. |
| `_App/Scenes/MainMenu.unity` | Main menu: `MainMenuSceneScope`, MainMenuPanel и ScenePickerPanel. |
| `_App/Scenes/VrEditing.unity` | Основная сцена редактирования: `VrEditingSceneScope`, XR Rig вариант, UserPanel, gizmo, outliner, inspector. |
| `_App/Scenes/Sandbox.unity` | Sandbox-режим (тот же UI-контекст что и VrEditing, но без save-loop): `SandboxSceneScope`. |
| `_App/Scenes/Tests/Asset_Review.unity` | Тест-сцена ревью ассетов. |
| `_App/Scenes/Tests/MCP_testScene.unity` | Тест-сцена для Unity-MCP экспериментов. |
| `_App/Scenes/Tests/Prototyping_UI.unity` | Прототипирование UI-кусков. |
| `_App/Scenes/_Sandbox/AnimatorPanelSandbox.unity` | Изолированная сцена для отладки `AnimatorPanelModule` (Canvas + панель в screen-space). |

---

## Assembly Definitions

| File | Defines | References |
|---|---|---|
| `_App/_App.asmdef` | `_App` — composition root (содержит Bootstrap-скоупы, AppBootstrap). `autoReferenced: false`. | `_Shared`, `Unity.TextMeshPro`, `VContainer`, все `Subsystems.*`, `Unity.XR.Interaction.Toolkit` |
| `_App/_Shared/_Shared.asmdef` | `_Shared` — кросс-подсистемные контракты (Events/Interfaces/Models/Data). `autoReferenced: true`. | `VContainer`, `Unity.TextMeshPro` |
| `_App/Editor/PromeonLab.Editor.asmdef` | `PromeonLab.Editor` — project-wide editor код (RemoveMissingScripts, ProxyRigBuilder inspector). Editor-only. | `_Shared`, `Subsystems.SpatialUi`, `Subsystems.AnimationAuthoring`, `Subsystems.RigBuilder`, `Unity.TextMeshPro`, `Unity.Animation.Rigging` |
| `_App/Subsystems/AnimationAuthoring/Subsystems.AnimationAuthoring.asmdef` | `Subsystems.AnimationAuthoring`. | `_Shared`, `VContainer`, `Subsystems.StorageCore`, `Subsystems.SceneComposition` |
| `_App/Subsystems/AnimationAuthoring/Tests/Subsystems.AnimationAuthoring.Tests.asmdef` | `Subsystems.AnimationAuthoring.Tests`. | NUnit + AnimationAuthoring (internals-visible) |
| `_App/Subsystems/AnimationPlayback/Subsystems.AnimationPlayback.asmdef` | `Subsystems.AnimationPlayback` (placeholder). | — |
| `_App/Subsystems/AssetBrowser/Subsystems.AssetBrowser.asmdef` | `Subsystems.AssetBrowser`. | `_Shared`, `VContainer`, `Subsystems.StorageCore`, `Subsystems.SceneComposition` |
| `_App/Subsystems/ErrorHandling/Subsystems.ErrorHandling.asmdef` | `Subsystems.ErrorHandling` (placeholder). | — |
| `_App/Subsystems/ExportPipeline/Subsystems.ExportPipeline.asmdef` | `Subsystems.ExportPipeline` (placeholder). | — |
| `_App/Subsystems/InputBindings/Subsystems.InputBindings.asmdef` | `Subsystems.InputBindings` (placeholder). | — |
| `_App/Subsystems/ModeOrchestrator/Subsystems.ModeOrchestrator.asmdef` | `Subsystems.ModeOrchestrator`. | `_Shared`, `VContainer` |
| `_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef` | `Subsystems.RigBuilder`. | `_Shared`, `VContainer`, `Subsystems.VrInteraction`, `Subsystems.SceneComposition`, QuickOutline |
| `_App/Subsystems/RigBuilder/Tests/Subsystems.RigBuilder.Tests.asmdef` | `Subsystems.RigBuilder.Tests`. | NUnit + RigBuilder |
| `_App/Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef` | `Subsystems.SceneComposition`. | `_Shared`, `VContainer`, `Subsystems.StorageCore`, `Subsystems.AssetBrowser` |
| `_App/Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef` | `Subsystems.SceneComposition.Tests`. | NUnit + SceneComposition |
| `_App/Subsystems/SpatialUi/Subsystems.SpatialUi.asmdef` | `Subsystems.SpatialUi`. | `_Shared`, `VContainer`, `Unity.XR.Interaction.Toolkit`, `Subsystems.ModeOrchestrator`, `Subsystems.StorageCore`, `Subsystems.SceneComposition`, `Subsystems.AssetBrowser`, `Subsystems.RigBuilder`, `Subsystems.AnimationAuthoring`, `SimpleFileBrowser.Runtime`, `Unity.TextMeshPro`, `Unity.InputSystem` |
| `_App/Subsystems/SpatialUi/Editor/Subsystems.SpatialUi.Editor.asmdef` | `Subsystems.SpatialUi.Editor` (Editor-only builder/verifier). | `Subsystems.SpatialUi`, `Unity.TextMeshPro` |
| `_App/Subsystems/StorageCore/Subsystems.StorageCore.asmdef` | `Subsystems.StorageCore`. | `_Shared`, `VContainer` |
| `_App/Subsystems/StorageCore/Tests/Subsystems.StorageCore.Tests.asmdef` | `Subsystems.StorageCore.Tests`. | NUnit + StorageCore |
| `_App/Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef` | `Subsystems.VrInteraction`. | `_Shared`, `VContainer`, `Unity.XR.Interaction.Toolkit`, `Subsystems.SceneComposition`, QuickOutline |
| `_App/Subsystems/VrInteraction/Tests/Subsystems.VrInteraction.Tests.asmdef` | `Subsystems.VrInteraction.Tests`. | NUnit + VrInteraction |

---

## Input Actions

| File | Action maps / contexts |
|---|---|
| `Assets/InputSystem_Actions.inputactions` | Стандартный Unity InputSystem-набор: maps **Player** (Move, Look, Attack, Interact, Crouch, Jump, …) и **UI** (стандартные UI-actions). Project-wide дефолт. Кастомные VR-биндинги для UserPanel toggle создаются программно в `UserPanelOpener.cs` (`<XRController>{*Hand}/primaryButton`). |

---

## Materials & Shaders

В `_App/` нет собственных `.mat` / `.shader` файлов — все материалы рига и UI берутся из `Plugins/QuickOutline` (shader + материалы), `Assets/UnityPacks/*` (демо-ассеты) и URP-defaults в `Assets/Settings/`.

---

## Third-Party Plugins

В дереве проекта нет папки `Assets/Plugins/`. Третья сторона распределена по `Assets/UnityPacks/` (model-паки) и `Assets/Samples/` (XRI samples).

| Folder | What it is | How we use it |
|---|---|---|
| `Assets/UnityPacks/SimpleFileBrowser` | Runtime/Editor файловый диалог. | `AssetBrowserModule` использует для импорта FBX; `FileBrowserVrAnchor` позиционирует его canvas в VR |
| `Assets/UnityPacks/QuickOutline` | Outline shader/компонент. | `Selectable.SetVisualState` / `PromeonProxyRigBuilder` для подсветки selection. Пакет пропатчен (guard `isReadable` в `LoadSmoothNormals`) — re-import затрёт правку |
| `Assets/UnityPacks/ColorSkies` | Skybox/материалы. | Скайбоксы в demo/test сценах |
| `Assets/UnityPacks/Downtown Game Studio` | Демо-модели (городские ассеты). | Источник prefab'ов в `DemoAssetCatalog` |
| `Assets/UnityPacks/HouseInteriorPack` | Демо-модели интерьера. | Источник prefab'ов в `DemoAssetCatalog` |
| `Assets/UnityPacks/Keyboard Package` | UI-клавиатура (assets). | Visual base for `VrKeyboard` (кнопки/раскладка) |
| `Assets/Samples/XR Interaction Toolkit` | Samples из XRI package. | XR Rig prefab, demo input-actions, reference; не модифицируем |

---

## Unity Infrastructure Folders

| Folder | Contents | Notes |
|---|---|---|
| `Assets/XR/` | OpenXR + Meta loaders + XR Generals Settings. | Привязан к `ProjectSettings/XR`; включает Meta Quest Touch Pro/Plus profiles |
| `Assets/XRI/` | Дефолтные input-actions/presets XR Interaction Toolkit. | Reference; используем production-копию из `_App` и Samples |
| `Assets/Settings/` | URP RP Assets (Mobile/PC), Renderers, Volume Profiles, URP Global Settings. | Активный pipeline — Mobile_RPAsset для Quest |
| `Assets/Samples/` | Imported XR Interaction Toolkit samples (XR Rig, demo). | Источник базового XR Rig prefab'а, на котором собран кастомный `User XR Origin (XR Rig)` вариант |
| `Assets/TextMesh Pro/` | TMP runtime fonts/sprites/shaders. | Авто-импорт |
| `Assets/UnityPacks/` | Все third-party content-паки (см. выше). | Заменяет роль `Plugins/` для контент-ассетов |
| `Assets/Screenshots/` | Скриншоты Editor/runtime. | Артефакты — не код |
| `Assets/TutorialInfo/` | Unity template tutorial (Readme + sprites). | Можно удалить, не используется |
| `Assets/_Recovery/` | Auto-recovery snapshots Editor'а. | Артефакты — игнор |
| `Assets/CompositionLayers/` | Asset-папка для XR Composition Layers. | OpenXR composition-layers reference |
| `Assets/Resources/` | `Materials/`, `Models/`, `Prefabs/`, `Textures/` — корневые resources. | Сам `_App` `Resources.Load` не использует (запрещено CLAUDE.md); содержимое — для third-party и тестов |
| `Assets/_App/DemoAssets/` | Pre-bundled FBX prefabs + `DefaultDemoAssetCatalog.asset`. | Источник встроенных rig-ассетов в `BuiltinAssetLibrary` |
| `Assets/_App/Documentation/` | Внутренние markdown/изображения по подсистемам. | Reference, не код |
