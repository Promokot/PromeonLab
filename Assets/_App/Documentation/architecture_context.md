
# Архитектура ВКР — VR Animation App

## Целевая платформа

- **Устройство:** Meta Quest 3 (основная), Quest 2 (совместимость)
- **OS:** Android (standalone), без ПК-зависимости в рантайме
- **Движок:** Unity + C#
- **Хранилище проектов:** `Application.persistentDataPath` (внутри приложения)

---

## Технологический стек

| Слой                     | Технология                                                      |
| ------------------------ | --------------------------------------------------------------- |
| VR-рантайм               | OpenXR (кроссплатформенный, без привязки к Meta SDK)            | 
| Маркерный трекинг        | Quest Image Tracking (Aupuma) поверх OpenCV для Unity           |
| Dependency Injection     | VContainer (Root → Scene → Feature контейнеры)                  |
| Событийные шины          | VContainer-native (MessagePipe или аналог, per-scope)           |
| Асинхронность            | Unity async/await (стандартный C# async, как у загрузки сцен)   |
| JSON-сериализация        | Unity JsonUtility                                               |
| Экспорт 3D               | Unity FBX Exporter SDK + кастомный JSON-формат (fallback)       |
| Анимационные констрейнты | Unity Animation Rigging (процедурные IK/FK поверх Animator)     |
| Версионирование          | Git / GitHub                                                    |
| Постобработка            | Blender 3D + Python API (внешний редактор, не часть приложения) |

---

## Структура Bootstrap-цепочки

Три уровня VContainer-контейнеров:


RootLifetimeScope — синглтоны на всё время приложения ├── AppStorage ├── AssetImporter ├── PathProvider └── AnimationClock

SceneLifetimeScope — живёт пока загружена Unity-сцена ├── ModeOrchestrator ├── SceneGraph ├── SelectionManager ├── UiPanelManager └── CommandStack (per-context)

FeatureLifetimeScope — живёт пока активен конкретный режим (Feature) ├── MappingSession (ArMapping) ├── PlaybackController (VrEditing) ├── RigRuntime (VrEditing) └── TrackRecorder (VrEditing)

---

## Режимы приложения (AppMode)

Управляются `ModeOrchestrator` через `AppStateMachine` + `ModeTransitionGraph` (SO).

MainMenu — управление сценами (создать / открыть / удалить) VrEditing — редактирование сцены: объекты, риги, анимация ArMapping — захват пространства: маркеры, якорные точки, меш ArPreview — просмотр сцены в реальном помещении через passthrough Debug — оверлей поверх любого режима (XR Simulator в билде)

---

## 13 подсистем — состав и ответственности

### 1. StorageCore
**Файлы:** `AppStorage`, `SceneSerializer`, `AssetImporter`, `AssetFileManager`, `PathProvider`, `UnsavedChangesGuard`, `StorageMigrator`

Сервисный слой «под капотом» — пользователь не взаимодействует с ним напрямую. Отвечает за импорт внешних файлов, сериализацию/десериализацию внутренних структур, управление путями и миграцию схем. Как база для работы с файловой системой пак "Runtime File Browser" https://assetstore.unity.com/packages/tools/gui/runtime-file-browser-113006.

**Зоны ответственности:**

- **Импорт** — `AssetImporter` принимает файл через системный пикер, определяет тип, копирует в `persistentDataPath`, распаковывает embedded материалы/текстуры как отдельные ассеты, обновляет реестр.
- **Сериализация** — сохранение и загрузка всех внутренних сущностей. Импортированные ассеты (меши, текстуры, медиа) хранятся как есть и перезаписываются только при переименовании или изменении настроек (например, слотов материала). Производные данные — риги, позы, анимационные экшены, граф сцены — сериализуются независимо друг от друга. Настройки приложения и история изменений хранятся отдельно от данных сцены.
- **Пути** — `PathProvider` — единая точка для построения путей по `SceneId` / `AssetId`. Другие системы не собирают пути вручную.
- **Миграция** — `StorageMigrator` сравнивает `schemaVersion` при загрузке и применяет миграции.
- **Dirty-флаг** — `UnsavedChangesGuard` слушает `SceneModified` и блокирует смену сцены / выход без подтверждения.
- **Кеш** — `AppStorage` хранит in-memory кеш; повторный запрос по `AssetId` не читает диск.

Основные категории хранимых данных:

| Категория               | Примеры                                | Изменяемость                                |
| ----------------------- | -------------------------------------- | ------------------------------------------- |
| Импортированные ассеты  | меши, текстуры, материалы, медиа       | редко (переименование, настройки материала) |
| Производные ассеты      | риги, позы                             | активно в процессе работы                   |
| Данные сцены            | граф объектов, трансформы, иерархия    | активно в процессе работы                   |
| Анимационные данные     | экшены, NLA-композиция, ключевые кадры | активно в процессе работы                   |
| Пространственные данные | маппинги, маркеры, якорные точки       | при AR-сессиях                              |
| Настройки приложения    | биндинги, UI-предпочтения, дефолты     | по необходимости                            |
| История изменений       | CommandStack per-контекст              | локально, не экспортируется                 |

**Структура папки сцены на диске** (`PathProvider` строит все пути относительно неё):

/persistentDataPath/scenes/{SceneId}/ scene.json ← граф сцены + анимационные данные asset-catalog.json ← реестр всех ассетов сцены assets/ Models/ Textures/ Materials/ Media/ ← видео + аудио Rigs/ ← rig-{assetId}.json Poses/ ← pose-{assetId}.json .thumbnails/ ← кеш превью (не индексируется) mappings/ mapping.json ← маркеры + якорные точки + пространственный меш export/ *.fbx / *.json

**Ключевые сущности:** `AssetId`, `SceneId`, `AssetType: Model | Material | Texture | Video | Audio | Rig | Pose`, `AssetEntry { AssetId, AssetType, relativePath, thumbnailPath, metadata }`, `SaveResult`, `LoadedAsset`

---

### 2. AssetBrowser
**Файлы:** `AssetBrowserPanel`, `AssetGridView`, `AssetContextMenu`, `ScenePickerView`, `MappingPickerView`, `ThumbnailService`, `AssetBrowserController`

Визуальный фронтенд над `StorageCore` — то, что видит пользователь в VR. Сам не читает и не пишет файлы, делегирует операции в `AppStorage` и `AssetImporter`.

**Зоны ответственности:**

- **Галерея** — `AssetGridView` отображает плиточный список ассетов сцены с превью и фильтрацией по типу (`Model | Texture | Material | Rig | Pose | Audio | Video`). Превью генерируется `ThumbnailService` при импорте; для ригов и поз — скриншот из runtime-рендера.
- **Управление** — `AssetContextMenu` (долгое нажатие на плитке): переименование, удаление с подтверждением, просмотр метаданных. Для поз дополнительно доступно применение к совместимому ригу в сцене или сохранение как ключевых кадров в `ActionData`.
- **Drag-and-drop** — перетаскивание ассета из браузера в 3D-пространство инстанцирует объект в `SceneGraph` через `RayInteractor`.
- **Выбор сцены** — `ScenePickerView`: список всех сцен с превью, действия — открыть / создать / переименовать / удалить. Основной экран `MainMenu`.
- **Выбор маппинга** — `MappingPickerView`: список маппингов с датой захвата и root-маркером; привязка / смена / отвязка от сцены.
- **Импорт** — кнопка вызывает системный пикер, делегирует `AssetImporter`, обновляет галерею по завершении.

**Ключевые сущности:** `AssetBrowserState { activeFilter, selectedAssetId? }`, `ThumbnailHandle`, `DropPayload { assetId, worldPosition }`

---

### 3. SceneComposition
**Файлы:** `SceneGraph`, `SceneNode`, `SceneSerializer`, `SelectionManager`, `PropertyPanel`, `CommandStack`

- Иерархия нод + folder-группы (как Unity Hierarchy)
- Типы нод: `MeshObject | Light | Camera | GroupNode`
- `MeshObject` имеет `MaterialSlot[]` — переназначаемые слоты материалов
- Аутлайнер: видимость + lock; трансформы — в отдельной Property Panel
- `CommandStack` на контекст (SceneContext), длина истории = SO-конфиг (дефолт 30)
- Три независимых стека: `MainMenu`, `VrEditing`, `AssetBrowser`
- Редактирование через VR UI аутлайнер и UI свойств выделенных объектов + взаимодействие непосредственно с объектами

**Ключевые сущности:** `SceneNodeId`, `SceneNode { isVisible, isLocked }`, `MaterialSlot { slotIndex, AssetId? }`, `Selection`

---

### 4. EnvironmentMapping
**Файлы:** `MappingSession`, `IMarkerTracker` / `QuestMarkerTracker`, `AnchorPointRecorder`, `MeshBuilder`, `MappingRepository`, `SceneAnchor`

- Один маппинг на сцену (сцена привязана к конкретной локации)
- `IMarkerTracker` — абстракция над Quest Image Tracking + OpenCV (единственный платформозависимый слой)
- Один маркер помечается как root вручную → `SceneAnchor` = начало координат
- Якорные точки расставляются кнопкой контроллера (`AnchorPlacementAction`)
- `MeshBuilder` строит нецельный `SpatialMesh` по выбранным точкам
- Восстановление координат при каждом входе в AR-режим через переобнаружение root-маркера

**Ключевые сущности:** `MarkerRecord { MarkerId, isRoot, poseRelativeToRoot }`, `AnchorPoint { positionRelativeToRoot }`, `SpatialMesh { polygons: Polygon[] }`, `MappingData`

---

### 5. RigBuilder
Как база для работы с риггингом пак юнити "Animation Rigging".

**Файлы:** `RigDefinition`, `BoneInspector`, `ConstraintConfigurator`, `IkSetupWizard`, `ControlShapeAssigner`, `RigSerializer`, `RigRuntime`

- Скелет приходит из импортированного файла (skinned mesh); риг — отдельный ассет
- Дефолт для каждой кости: `TranslationLockConstraint` (отключаемый)
- FK — базовый принцип; IK через мини-операцию: выбрать root → end → pole target
- Контрол-шейп = кастомный меш из каталога ассетов поверх кости
- `RigRuntime` применяет `RigDefinition` через Unity Animation Rigging
- Настройка весовых групп — только во внешнем редакторе до импорта

**Ключевые сущности:** `RigAssetId`, `BoneRecord { boneName, controlShapeAssetId? }`, `TranslationLockConstraint`, `IkChainConstraint { rootBone, endBone, poleBone?, weight }`, `RigDefinition`

---

### 6. AnimationAuthoring
**Файлы:** `ActionData`, `TrackRecorder`, `KeyframeEditor`, `NlaComposer`, `AnimationClock`, `AnimationSerializer`

- Один `ActionData` на объект; общая NLA-композиция на уровне сцены
- Два способа записи: ручная фиксация кейфреймов + режим записи (каждый кадр = ключ)
- Анимируются: transform костей + transform объектов сцены + кастомные свойства (Light.intensity, Camera.fov)
- `ChannelPath` — строковый адрес канала: `"bone:Hips/rotation.x"` / `"node:Light01/intensity"`
- Интерполяция: `Linear | Stepped` (per-keyframe)
- NLA: только глобальные сдвиги стрипов, без редактирования кривых в VR
- `AnimationClock` — единый источник истины для текущего кадра, FPS, диапазона

**Ключевые сущности:** `ActionId`, `AnimTrack { ChannelPath, Keyframe[] }`, `Keyframe { frame, value, interpolation }`, `NlaStrip { ActionId, startFrame, isEnabled }`, `NlaComposition`

---

### 7. AnimationPlayback
**Файлы:** `PlaybackController`, `PlaybackMode`, `AnimationEvaluator`, `PropertyApplicator`, `PlaybackSpeedController`

- Два режима: `ScenePreview` (вся NLA) / `ActionIsolated` (один экшен, остальные заморожены)
- Transport: play / pause / stop / loop / scrub / скорость (глобально, 0.25x–2x)
- `AnimationEvaluator` → `PropertyApplicator` → Unity objects (Transform, Light, Camera, SkinnedMesh bones)
- Stop → сброс на `frameStart`

**Ключевые сущности:** `PlaybackState`, `PlaybackContext { mode, isolatedActionId?, isLooping, speedMultiplier }`, `EvaluatedFrame { frame, values: Dict<ChannelPath, float> }`

---

### 8. ExportPipeline
**Файлы:** `ExportOrchestrator`, `ExportConfig`, `IAnimationExporter`, `FbxExporter`, `CustomFormatExporter`

Формирует итоговое представление сцены для внешних редакторов. Обратный импорт не предусмотрен.

**Зоны ответственности:**

- **Сбор данных** — `ExportOrchestrator` собирает из `SceneGraph`, `AnimationAuthoring` и `EnvironmentMapping` единую экспортную модель: структуру сцены, трансформы, ключевые кадры, пространственные привязки.
- **FBX-экспорт** — `FbxExporter` через Unity FBX Exporter SDK; анимационные кривые запекаются по кадрам.
- **Custom JSON-экспорт** — `CustomFormatExporter` как fallback, когда FBX не сохраняет полноту данных (высокая плотность ключей, нестандартная иерархия ригов, пространственная привязка). Допускается создание плагина-импортёра для Blender.
- **Область экспорта** — отдельный объект, отдельный экшен или вся сцена.
- **Origin** — `Zero` (мировой ноль) или `SceneRoot` (относительно root-маркера).

**Ключевые сущности:** `ExportConfig { scope, format, origin, outputName }`

---

### 9. InputBindings
**Файлы:** `OpenXrControllerSource`, `KeyboardSource` (debug), `ActionRouter`, `BindingProfile` (SO/JSON), `InputContext`

- Контексты: `Navigation | Ui | ObjectSelection | GizmoManipulation | AnchorPlacement | Debug`
- Группы действий: `Ui* | Selection* | Gizmo* | Navigation* | Debug*`
- Cheatsheet-панель с биндингами доступна в VR
- `KeyboardSource` — только для XR Simulator / debug-билдов

---

### 10. ModeOrchestrator
**Файлы:** `AppStateMachine`, `ModeTransitionGraph` (SO), `ModeActivator`, `DebugOverlay`, `SceneEnvironmentLinker`

- `ModeTransitionGraph` — граф допустимых переходов между режимами (ScriptableObject)
- `SceneEnvironmentLinker` хранит `EnvironmentLink { SceneId, MappingId, RootMarkerId }`
- `DebugOverlay` — оверлей поверх любого режима, включается отдельно

---

### 11. VrInteraction
Как база для работы VR юнити-паки "OpenXR Plugin" и "Unity OpenXR Meta"

**Файлы:** `InteractionController` (×2, симметрично L/R), `RayInteractor`, `NearInteractor`, `GizmoController`, `DirectManipulator`, `SelectionInteractor`

- Обе руки симметричны; основной инструментарий через UI
- `RayInteractor` — raycast по слоям: `SceneObjects | UiPanels | GizmoHandles`
- `NearInteractor` — сфера ~15 см, перехватывает приоритет у луча при близком касании
- `GizmoController` — translate/rotate/scale, активируется через ToolbarPanel (как T-panel в Blender)
- `DirectManipulator` — перемещение/поворот при захвате (луч + near)
- Мультиселект — modifier-кнопка на контроллере

**Ключевые сущности:** `InteractionEvent { source: Ray|Near, controllerId, targetObject, eventType }`, `GizmoMode: Translate|Rotate|Scale`, `GizmoSpace: Local|World`

---

### 12. SpatialUi
**Файлы:** `UiPanelManager`, `SpatialPanel`, `PanelHandle`, `ToolbarPanel`, `UiInputRouter`, `PanelRegistry` (SO)

- Панели: `BodyLocked` (следуют за пользователем) / `WorldFixed` (зафиксированы в сцене) / `Free` (плавающие)
- Billboard-режим: панель всегда смотрит к камере (отключаемо)
- `PanelHandle` — виджет для перетаскивания/поворота панели (виден только если не billboard)
- `ToolbarPanel` — body-locked, всегда доступна; содержит активный инструмент и transport
- `PanelRegistry SO` — дефолтные позиции и настройки всех панелей
- `UiPanelManager` слушает `ModeOrchestrator.ModeChanged` → показывает/скрывает панели
- `UiInputRouter` → UGUI через XR UI Input Module

---

### 13. ErrorHandling
**Файлы:** `ErrorDispatcher`, `ErrorContext`, `ErrorLogger`, `ErrorRecoveryHandler`, `ErrorReporter`

- Единая точка для обработки и логирования ошибок из всех подсистем
- Три уровня: `Warning | Error | Critical`
- `ErrorDispatcher` — маршрутизирует ошибки в UI (уведомление) / лог / crash-reporter
- `ErrorContext` сохраняет стек вызовов, временные метки, состояние сцены для отладки
- `ErrorRecoveryHandler` — автоматические попытки восстановления (повторная загрузка, откат до последней сохранённой версии)
- `ErrorReporter` — анонимная отправка критических ошибок на сервер (опционально)
- Все `async` операции оборачиваются в try-catch с делегированием в `ErrorDispatcher`

**Ключевые сущности:** `ErrorLevel`, `ErrorRecord { timestamp, level, message, stackTrace, context }`, `RecoveryStrategy`

___

## Ключевые межсистемные связи

AppStorage ──открывает──────────► SceneGraph, AssetBrowser, MappingRepository AssetImporter ───при импорте────► BoneInspector → RigDefinition (авто) SceneGraph ──────────────────────► AppStorage (загрузить меш/материал) SelectionManager ────────────────► PropertyPanel, GizmoController AnimationClock ──FrameChanged───► AnimationEvaluator → PropertyApplicator NlaComposer ─────────────────────► AnimationEvaluator (стрипы + offsets) ModeOrchestrator ────────────────► UiPanelManager, FeatureLifetimeScope SceneAnchor ─────AnchorUpdated──► SceneGraph (пересчёт позиций) UnsavedChangesGuard ◄────────────── SceneModified (от SceneGraph, AnimationAuthoring) CommandStack ◄───────────────────── InputBindings (Undo/Redo actions) AssetBrowser ────────────────────► AppStorage (сохранить позу как ассет) AssetBrowser ────PoseApply──────► RigRuntime + KeyframeEditor (опц.) ExportOrchestrator ──────────────► SceneGraph, AnimationAuthoring, EnvironmentMapping
ErrorDispatcher ◄────────────────────── все подсистемы (ErrorOccurred) UnsavedChangesGuard ───при критической ошибке──► ErrorRecoveryHandler

---

## Событийные шины (per-scope)

| Событие                | Источник                       | Подписчики                                         |                                      |
| ---------------------- | ------------------------------ | -------------------------------------------------- | ------------------------------------ |
| `SceneOpened`          | AppStorage                     | SceneComposition, AssetBrowser, EnvironmentMapping |                                      |
| `SceneSaved`           | AppStorage                     | UnsavedChangesGuard                                |                                      |
| `SceneModified`        | SceneGraph, AnimationAuthoring | UnsavedChangesGuard                                |                                      |
| `AssetImported`        | AssetImporter                  | AppStorage, SpatialUi                              |                                      |
| `SelectionChanged`     | SelectionManager               | PropertyPanel, GizmoController                     |                                      |
| `FrameChanged`         | AnimationClock                 | AnimationEvaluator, TrackRecorder                  |                                      |
| `ModeChanged`          | ModeOrchestrator               | UiPanelManager, FeatureLifetimeScope               |                                      |
| `AnchorUpdated`        | SceneAnchor                    | SceneGraph                                         |                                      |
| `MarkerDetected`       | IMarkerTracker                 | MappingSession                                     |                                      |
| `PlaybackStateChanged` | PlaybackController             | SpatialUi (transport UI)                           |                                      |
| `NlaChanged`           | NlaComposer                    | AnimationPlayback                                  |                                      |
| `ExportCompleted`      | ExportOrchestrator             | SpatialUi                                          |                                      |
|                        | `ErrorOccurred`                | ErrorDispatcher                                    | SpatialUi (уведомление), ErrorLogger |