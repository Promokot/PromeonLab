
# Архитектура ВКР — VR Animation App (PromeonLab)

> Документ описывает **фактически реализованную** архитектуру приложения. Запланированные, но
> ещё не построенные возможности отмечены явно и собраны в `docs/BACKLOG.md`.

## Целевая платформа

- **Устройство:** Meta Quest 3 (основная), Quest 2 (совместимость)
- **OS:** Android (standalone), без ПК-зависимости в рантайме
- **Движок:** Unity 6000.3.7f1, C#
- **Хранилище проектов:** `Application.persistentDataPath` (внутри приложения)

---

## Технологический стек

| Слой                     | Технология                                                      |
| ------------------------ | --------------------------------------------------------------- |
| VR-рантайм               | OpenXR (кроссплатформенный, без привязки к Meta SDK)            |
| Рендеринг                | URP 17.3                                                        |
| Dependency Injection     | VContainer (Root → Scene контейнеры)                            |
| Событийная шина          | Кастомный `EventBus` (`Publish<T>`/`Subscribe<T>`, per-scope)   |
| Асинхронность            | Unity async/await (стандартный C# async)                       |
| JSON-сериализация        | Unity `JsonUtility` (все данные версионируются `schemaVersion`) |
| Импорт 3D (рантайм)      | glTFast (`com.unity.cloud.gltfast`) для glTF/GLB + изображения (PNG/JPG). FBX в рантайме **не поддерживается** |
| Экспорт                  | Самодостаточный **ZIP-бандл** (`scene.json` + копии моделей/текстур). FBX-экспорт — **запланирован** (см. BACKLOG) |
| Версионирование          | Git / GitHub                                                    |

> **IK / Animation Rigging:** IK-цепочки сериализуются, но **решателя ещё нет** — Unity Animation
> Rigging в рантайме не подключён (см. BACKLOG).

---

## Структура Bootstrap-цепочки

Два уровня VContainer-контейнеров (отдельного Feature-scope в коде нет — не понадобился на этом масштабе).

**`RootLifetimeScope`** — синглтоны на всё время приложения, `DontDestroyOnLoad` под `PersistentRoot`:
`AppStorage`, `PathProvider`, `EventBus`, `SceneContext` (фасад над сценическими сервисами),
`ModeOrchestrator`, `ISceneTransition`/`SceneTransitionRunner`, `PanelRegionRouter`,
`AnimationClipboard`, библиотеки ассетов (`Builtin`/`Imported`/`Saved`), `AssetRegistry`,
`ImportPipeline`, `VrKeyboard`, `UserPanel`.

**Сценический scope** — собственный `LifetimeScope` загруженной mode-сцены
(`MainMenu`/`VrEditing`/`Sandbox`); грузится строго по одной за раз (`LoadSceneMode.Single`),
парентится к персистентному root'у: `SceneGraph`, `SelectionManager`, `AssetSpawner`. **Только
`VrEditing`** дополнительно
регистрирует `AnimationClock`, `AnimationAuthoring` (+ `AnimationStorage`,
`AnimationPlaybackSampler`), `BoneEditMode`, `SceneAutoSaver`, `SceneDirtyTracker`. Биндит
`SceneContext` через `SceneContextBinder` (публикует `SceneContextChangedEvent`).

Дочерние scope могут зависеть от родительских регистраций, **никогда наоборот**. Сценические
сервисы доступны всему приложению через фасад `SceneContext`, заполняемый на старте
scope'а и очищаемый при dispose. **`HasScene` (граф привязан) не гарантирует ненулевость других
сервисов** — Sandbox не регистрирует `AnimationAuthoring`/`AnimationClock`, поэтому потребитель
проверяет конкретный сервис, который использует.

---

## Режимы приложения

Переходы: `MainMenu` ↔ `VrEditing`; `MainMenu` ↔ `Sandbox`. Управляются `ModeOrchestrator` через
`ModeTransitionGraph` (SO). Оркестратор — чистая политика: валидирует переход, публикует
`ModeExitingEvent` **синхронно, пока уходящая сцена и её scope ещё живы** (хук
сохранения-при-выходе — `SceneAutoSaver`), затем делегирует загрузку `ISceneTransition`
(`SceneTransitionRunner`): fade в чёрный (`HeadFade`) → `LoadSceneMode.Single` → колбэк публикует
`ModeChangedEvent` уже **после** загрузки сцены. Сценические сервисы диспозятся во время
Single-загрузки, *до* `ModeChangedEvent`. Re-entrancy-гард отбрасывает наложенные переходы.

- **MainMenu** — управление сценами (создать / открыть / удалить).
- **VrEditing** — редактирование сцены: объекты, риги, анимация.
- **Sandbox** — отдельный песочный режим (без авто-сохранения).

---

## Подсистемы — состав и ответственности

Каждая подсистема живёт в `Assets/_App/Scripts/<Subsystem>/`. Единственный по-настоящему общий
примитив (`EventBus.cs`) лежит в `Scripts/Core/`.

### 1. StorageCore
**Ключевые типы:** `AppStorage`, `SceneSerializer`, `PathProvider`, `SceneDirtyTracker`.

Сервисный слой файлового ввода-вывода и JSON-сериализации. Все пути строятся **исключительно**
через `PathProvider` (без ручной конкатенации строк). Миграция схем — **инлайн на границе
десериализации** (`SceneSerializer.Deserialize` делает v1/v2→v3); отдельного `StorageMigrator`
нет. `SceneDirtyTracker` слушает `SceneModified` и отслеживает несохранённые изменения.

**Раскладка данных на диске:**

```
persistentDataPath/
├── asset-libraries/                  (глобально, переиспользуется между сценами)
│   ├── imported-lib.json             (записи Imported, schemaVersion 2)
│   ├── saved-lib.json                (записи Saved; flow ещё не реализован)
│   ├── sources/{assetId}.{ext}       (скопированные сырые файлы импорта)
│   └── thumbnails/{assetId}.png       (рендер-превью моделей; картинки берут свой источник)
└── scenes/{SceneId}/
    ├── scene.json            (граф сцены + позы костей, schemaVersion 3)
    ├── animation.json        (кейфреймы + интерполяция/loop + scene-wide fps, schemaVersion 2)
    └── asset-catalog.json    (реестр ассетов сцены)
```

Риги и позы костей несутся **инлайн** (риг — в рецепте ассета, позы — в `NodeData.BonePoses`),
поэтому отдельных папок `Rigs/`/`Poses/` больше нет. Экспортный ZIP пишется **вне**
`persistentDataPath` (`Documents/{productName}/{name}.zip`).

### 2. AssetBrowser
**Ключевые типы:** `AssetBrowserPanel`, `ImportPipeline`, `ImportWizardPanel`, `IAssetImporter`
(`GltfAssetImporter`/`ImageAssetImporter`), `ImportedSourceProvider`, `GltfModelImporter`,
`AssetSpawner`, `ThumbnailRenderer`, entity-builders.

VR-галерея над тремя библиотеками по ключу `AssetSource`: `Builtin` (зашиты в билд),
`Imported` (выход мастера импорта, геометрия из файла-источника), `Saved` (ручное сохранение —
flow ещё не реализован).

**Данные и спавн разделены.** Запись ассета `ILabAsset { Id, DisplayName, Type, Source, SourceRef,
Icon }` — чистые данные. Сущности строятся по принципу **build-once / restore-many**
(`IAssetEntityBuilder` + `AssetEntityBuilderRegistry`, билдеры Object/Rig/Reference); интерактивная
способность накладывается через `InteractionCapability.Apply`. Спавн идёт через `AssetSpawner`
(по `AssetSpawnRequestedEvent`) и при загрузке сцены.

**Импорт (рантайм):** выбор файла в файл-браузере → `FilePickedEvent` → `ImportPipeline`
подбирает `IAssetImporter` по расширению → открывает мастер `ImportWizardPanel` → пользователь
выбирает тип + имя → importer копирует сырой файл в `asset-libraries/sources/{assetId}.{ext}`
(через `ImportedSourceProvider`) и пишет `ImportedLabAsset` → `AssetImportedEvent` → обновление
галереи. Геометрия: glTF/GLB через glTFast (`GltfModelImporter`); изображения — текстурированный
quad. **Превью генерируются при импорте** (`ThumbnailRenderer` оффскрин-рендерит модели в
`thumbnails/{id}.png`; картинки берут свой источник). Прямого доступа к файлам нет — делегирует в
`StorageCore`/`ImportedSourceProvider`.

### 3. SceneComposition
**Ключевые типы:** `SceneGraph`, `SceneNode`, `SelectionManager`.

Иерархия нод сцены. `SelectionManager` — **одиночный выбор** (`Select(id?)` / `SelectedNodeId`).
**Undo/redo нет** — подсистема `CommandStack`/`ICommand` была удалена; мутации применяются напрямую
(возврат transform-undo — на рассмотрение, см. BACKLOG).

### 4. RigBuilder
**Ключевые типы:** `RigEntityFabricator`, `BoneFollower`, `ProxyRigRuntime`.

Рантайм-риг из proxy-костей строится на спавне (`RigEntityFabricator.BuildProxyRig` → per-bone
proxy GameObject + `BoneFollower`; координируется `ProxyRigRuntime`). Позы костей персистятся через
schema-v3 `NodeData.BonePoses` (proxy-local TRS). **IK-цепочки сериализуются, но решателя пока нет**
(Animation Rigging не подключён).

### 5. Animation
**Ключевые типы:** `AnimationAuthoring` (фасад), `AnimationClipBaker`, `AnimationPlaybackSampler`,
`AnimationStorage`, `AnimationClock`.

Покадровая авторизация на каждый `ActionContainer`. `AnimationAuthoring` — CRUD/оркестрационный
фасад, разделённый на: статический `AnimationClipBaker` (трек → `AnimationClip` + тангенсы
интерполяции), `AnimationPlaybackSampler` (`ITickable`, сэмплирование и фоновый loop-плейбек) и
`AnimationStorage` (загрузка/сохранение `animation.json`, не-деструктивная на неподдерживаемых
версиях). Интерполяция per-container **Linear/Stepped** (рантайм-тангенсы). Транспорт
(`AnimationClock`) всегда **single-shot** (scrub + play/pause + scene-wide fps). **Per-object Loop** —
фоновый плейбек: `AnimationPlaybackSampler` сэмплирует каждый зацикленный контейнер на своём
курсоре, поэтому несколько объектов крутятся одновременно независимо от выделения. Во время
**playback** сэмплирование идёт на **дробной** позиции (`AnimationClock.CurrentFrameContinuous`) —
движение плавное, не квантованное по fps. **NLA / мастер-таймлайна ещё нет** (см. BACKLOG). UI:
`AnimatorPanel` + модули `Animator*View`.

### 6. ExportPipeline
**Ключевые типы:** `SceneExporter`, `SceneBundle`.

**Рабочий ZIP-экспорт.** `SceneExporter` (app-lifetime, события request/result) снимает живое
состояние через `SceneContext` (снапшот графа + `AnimationAuthoring.CaptureForExport`), запускает
чистый `static BuildBundle` и пишет `Documents/{productName}/{name}.zip` = `scene.json` (плоская
внешняя схема `SceneBundle`, **односторонняя, не реимпортируется**) + `models/{assetId}.glb` +
`textures/{assetId}.png` (копии источников, дедуп). Builtin-ассеты не несут файла-источника →
помечаются `geometryMissing`. UI: `ExportPanel` на вкладке `exporter`. Настоящий **FBX**-экспорт —
запланирован (см. BACKLOG).

### 7. InputBindings
**Ключевые типы:** `ControlsProfile` (SO), `ControlBinding`, `SettingsPanel`.

Словарь управления для панели настроек: `ControlsProfile` + данные `ControlBinding`, рендерятся
`SettingsPanel`. Сама модель ввода взаимодействия живёт в `VrInteraction`.

### 8. ModeOrchestrator
Политика режимов: валидирует `ModeTransitionGraph`, делегирует
`ISceneTransition`/`SceneTransitionRunner` (single-scene загрузка за `HeadFade`); публикует
`ModeExitingEvent` до загрузки (scope ещё жив) и `ModeChangedEvent` после.

### 9. VrInteraction
**Ключевые типы:** `XRPromeonInteractable`, `GizmoDriver` (+ `GizmoHighlightPainter`,
`GizmoDragSession`), `InteractionMaskBinder`.

`XRPromeonInteractable` — прямой ввод на `NearFarInteractor` (tap-trigger = выбор,
hold-trigger = поворот, hold-grip = перемещение; штатный XRI select-flow отключён). `GizmoDriver` —
гизмо translate/rotate/scale (подсветка через `GizmoHighlightPainter`, drag через
`GizmoDragSession`). `InteractionMaskBinder` — контекстные маски кастеров. Аутлайн на базе
QuickOutline. **Одиночный выбор.**

### 10. SpatialUi
**Ключевые типы:** `SpatialPanel`, `PanelRegionRouter`, `NavBarConfig`, `RegionMember`,
`UserPanel`, `SettingsPanel`, `AnimatorPanel`.

VR-панели (`SpatialPanel`: `BodyLocked` / `WorldFixed` / `Free` + billboard). Модель регионов/навбара
живёт на root-lifetime (`PanelRegionRouter` + `NavBarConfig` + `RegionMember`). `UserPanel` —
grip-grab + тройной замок. Открытие панелей идёт через единый `PanelRegionRouter` (по одной открытой
поверхности на регион), не через per-panel show/hide.

### 11. ErrorHandling
**Ключевые типы:** `ErrorLevel` (enum) + `ErrorOccurredEvent`.

**`ErrorDispatcher` не реализован** — отчёты об ошибках пока идут напрямую в `Debug.Log*`
(см. BACKLOG).

---

## Межсистемная коммуникация

Все межсистемные сообщения — `struct` с суффиксом `Event`, публикуются через per-scope `EventBus`.
**Прямые вызовы методов через границы подсистем запрещены.** Ключевые события:

| Событие                | Источник                                  | Подписчики                                              |
| ---------------------- | ----------------------------------------- | ------------------------------------------------------- |
| `SceneOpened`          | StorageCore                               | SceneComposition, AssetBrowser                          |
| `SceneModified`        | SceneGraph, AnimationAuthoring            | SceneDirtyTracker                                       |
| `SelectionChanged`     | SelectionManager                          | PropertyPanel, GizmoDriver, ProxyRigRuntime, SelectionVisualSync |
| `FrameChanged`         | AnimationClock                            | AnimationPlaybackSampler (сэмпл клипа), AnimatorPanel (playhead) |
| `ModeExiting`          | ModeOrchestrator (до загрузки, scope жив) | SceneAutoSaver                                          |
| `ModeChanged`          | ModeOrchestrator (после загрузки)         | PanelRegionRouter / навбар                              |
| `SceneContextChanged`  | SceneContextBinder                        | OutlinerPanel, InspectorPanel, PropertyPanel, AnimatorPanel |
| `FilePicked`           | FileBrowserPanel                          | ImportPipeline (выбор `IAssetImporter` по расширению)   |
| `ImportRequested`      | ImportPipeline                            | ImportWizardPanel (показать мастер)                     |
| `ImportConfirmed`      | ImportWizardPanel                         | ImportPipeline (копия источника + запись в библиотеку)  |
| `AssetImported`        | ImportPipeline                            | AssetBrowser (обновление галереи)                       |
| `AssetSpawnRequested`  | AssetBrowserPanel                         | AssetSpawner                                            |

Соглашения по именованию, запретам (нет `Singleton.Instance`, нет `FindObjectOfType` в
gameplay-коде, нет мутабельных `static`-полей и т.д.) и структуре папок описаны в
`Assets/_App/Documentation/conventions.md`. Текущий обзор-источник истины — корневой `CLAUDE.md`;
код-сверенная реконсиляция подсистем — `docs/superpowers/audit-2026-06-01/`; запланированные, но
ещё не построенные фичи — `docs/BACKLOG.md`.
