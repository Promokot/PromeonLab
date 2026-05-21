# Реализация (главы 3.1–3.3 ВКР)

Документ описывает текущую кодовую реализацию приложения PromeonLab на 2026-05-21. Указаны конкретные классы и скрипты, отвечающие за каждый функциональный блок. Где функциональность ещё не реализована (placeholder/scaffold), это отмечено явно.

---

## 3.1 Информационное обеспечение

### Общая схема хранения

Все пользовательские данные сохраняются в `Application.persistentDataPath` — внутренней папке приложения на устройстве. На ПК (Editor) это `%AppData%/.../PromeonLab/`, на Quest — внутреннее хранилище Android. Внешние редакторы или PC при работе приложения не требуются.

Единственная точка построения путей — класс `PathProvider` (`StorageCore/PathProvider.cs`). Никакая другая подсистема не конкатенирует строки путей вручную:

```csharp
public string SceneRoot(string sceneId)       => Path.Combine(_root, "scenes", sceneId);
public string SceneJson(string sceneId)       => Path.Combine(SceneRoot(sceneId), "scene.json");
public string AnimationJson(string sceneId)   => Path.Combine(SceneRoot(sceneId), "animation.json");
public string ImportedLibraryPath             => Path.Combine(_root, "asset-library", "imported.json");
```

Структура папки сцены на диске:

```
{persistentDataPath}/
├── scenes/
│   └── {SceneId}/
│       ├── scene.json          ← граф сцены + ноды
│       ├── animation.json      ← дорожки и ключевые кадры
│       └── export/             ← результаты экспорта
└── asset-library/
    ├── imported.json           ← каталог импортированных ассетов
    └── saved.json              ← каталог сохранённых поз/ригов
```

### Подсистема StorageCore

Состав: `AppStorage`, `PathProvider`, `SceneSerializer`, `UnsavedChangesGuard`.

`AppStorage` — сервисный фасад, который выполняет асинхронные операции с диском и держит in-memory кеш загруженных сцен. Зарегистрирован в `RootLifetimeScope` как Singleton.

Ключевые методы:

- `CreateSceneAsync(displayName)` — генерирует новый `SceneId` (Guid, 8 hex-символов), создаёт папку сцены, пишет начальный `scene.json`, кеширует.
- `LoadSceneAsync(sceneId)` — читает JSON, десериализует через `SceneSerializer`, кладёт в кеш.
- `SaveSceneAsync(sceneData)` — пишет обратно на диск; кеш обновляется.
- `DeleteScene(sceneId)` — удаляет всю папку сцены рекурсивно.
- `GetAllScenesAsync()` — сканирует подпапки `scenes/`, возвращает список `(SceneId, DisplayName)`.
- `BeginSandboxSession()` — создаёт временную «сцену» с зарезервированным id `__sandbox__`, не пишет на диск (см. §3.2.2).

### Структура данных сцены

`StorageCore/Data/SceneData.cs`:

```csharp
[Serializable]
public class SceneData
{
    public int            SchemaVersion = 2;
    public string         SceneId;
    public string         DisplayName;
    public string         CreatedAt;
    public List<NodeData> Nodes = new();
}
```

Каждая нода (`NodeData`) хранит позицию, поворот, масштаб, ссылку на ассет (`AssetRef`), id родителя для иерархии. Поле `SchemaVersion` позволяет переезжать между версиями формата — `SceneSerializer.Deserialize` ищет устаревший формат и поднимает структуру до актуальной.

### Сериализация

`SceneSerializer.cs` — обёртка над Unity `JsonUtility`. Сериализация бинарных ассетов (мешей, текстур) не делается — ссылка живёт через `AssetId`/`AssetRef`, а сами файлы хранит подсистема AssetBrowser в `asset-library/`.

`AnimationAuthoring.cs` сохраняет свою часть отдельным файлом `animation.json` — структура: дорожки (`Tracks`), на каждую ноду одна дорожка с упорядоченными по кадру ключами.

### Автосохранение и dirty-флаг

`SceneAutoSaver` (`SceneComposition/SceneAutoSaver.cs`) подписан на `ModeChangedEvent`. При выходе из режима `VrEditing` автоматически делает snapshot `SceneGraph`'а и сохраняет на диск:

```csharp
private void OnModeChanged(ModeChangedEvent e)
{
    if (e.PreviousMode == AppMode.VrEditing && e.CurrentMode != AppMode.VrEditing)
        _ = SaveCurrentAsync();
}
```

Sandbox (id `__sandbox__`) явно исключён из автосохранения — см. §3.2.2.

`UnsavedChangesGuard` следит за `SceneModifiedEvent`, поднимает dirty-флаг для UI-блокировки выхода без подтверждения. Зарегистрирован в `MainMenuSceneScope`.

---

## 3.2 Программное обеспечение

### 3.2.1 Главное меню

#### Запуск приложения

Сцена `Bootstrap.unity` — самая первая сцена, выставленная в `EditorBuildSettings`. На ней живёт компонент `AppBootstrap.cs`:

```csharp
public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.sceneLoaded += OnMainMenuLoaded;
        SceneManager.LoadScene(MAIN_MENU_SCENE, LoadSceneMode.Additive);
    }
}
```

Параллельно `RootLifetimeScope` (VContainer) собирает корневой DI-контейнер, в который попадают синглтоны времени жизни всего приложения: `PathProvider`, `AppStorage`, `EventBus`, `ModeOrchestrator`, ассет-библиотеки (`BuiltinAssetLibrary`, `ImportedAssetLibrary`, `SavedAssetLibrary`), `AssetRegistry`. Сценные сервисы (`SceneGraph`, `SelectionManager`, `CommandStack` и т.д.) регистрируются в дочерних scope'ах — `MainMenuSceneScope`, `SandboxSceneScope`, `VrEditingSceneScope`.

После загрузки `MainMenu.unity` управление передаётся UI-панелям.

#### UI главного меню

Главное меню состоит из двух взаимосвязанных панелей:

**`ScenePickerPanel`** (`SpatialUi/Scripts/Panels/ScenePickerPanel.cs`) — список существующих сцен с возможностью создать, выделить, удалить:

- `Start()` загружает все сцены через `AppStorage.GetAllScenesAsync()` и порождает по `SceneItem` на каждую.
- Кнопка «Create» зачитывает имя из `TMP_InputField`, вызывает `AppStorage.CreateSceneAsync(name)`, перерисовывает список.
- Клик по элементу публикует `SceneSelectedEvent` — событие подхватывает `MainMenuPanel`.

**`MainMenuPanel`** (`SpatialUi/Scripts/Panels/MainMenuPanel.cs`) — две кнопки: «Open Sandbox» и «Open Scene». Подписан на `SceneSelectedEvent`, активирует «Open Scene» только когда какая-то сцена выбрана:

```csharp
private void OnSceneSelected(SceneSelectedEvent e)
{
    _selectedSceneId = e.SceneId;
    var hasScene = !string.IsNullOrEmpty(e.SceneId);
    _openSceneButton.interactable = hasScene;
    _openSceneLabel.text = hasScene ? $"Open  {e.DisplayName}" : "Open Scene";
}

private async Task OpenSceneAsync()
{
    if (string.IsNullOrEmpty(_selectedSceneId)) return;
    var data = await _storage.LoadSceneAsync(_selectedSceneId, CancellationToken.None);
    _storage.SetActiveScene(data);
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.VrEditing);
}
```

Переход в режим выполняет `ModeOrchestrator.TransitionTo(AppMode)`, который через `ModeTransitionGraph` (SO) проверяет допустимость перехода, выгружает текущую additive-сцену и грузит целевую.

#### VR-клавиатура

Ввод текста на Quest невозможен через физическую клавиатуру — поэтому в VR используется собственная экранная клавиатура `VrKeyboard` (`SpatialUi/Scripts/Elements/VrKeyboard.cs`):

```csharp
public class VrKeyboard : MonoBehaviour
{
    private TMP_InputField _target;
    private EventBus       _bus;

    private void OnFocus(KeyboardFocusEvent e) => _target = e.Target;
    public void AddLetter(string letter)       { if (_target != null) _target.text += letter; }
    public void DeleteLetter()                 { /* trim last char */ }
}
```

Подсистема ввода TextMeshPro `TMP_InputField` была переопределена через `VrInputFieldProxy.cs` — обычный фокус на текстовое поле в VR-руках публикует `KeyboardFocusEvent`, который привязывает поле к клавиатуре. После этого нажатие на клавиши клавиатуры (виртуальные кнопки в сцене) добавляет/удаляет символы.

#### Анимированное меню кнопок (NavBar)

`UserPanel.cs` содержит «плавающую» панель навигации — это центральная VR-панель, которая болтается у руки пользователя и переключает модули (Settings, Assets, Outliner, Gizmo Tools, RigBuilder). Конфигурируется через `NavBarConfig` (SO) + массив `NavBarBinding[]`, где каждой кнопке навбара сопоставлена дочерняя панель.

Smart-Follow поведение (поле `_recenterAngle`, `_smoothTime`, `_minDistance`, `_preferredDistance`, `_maxDistance`, `_yOffset`) — панель плавно догоняет взгляд пользователя, не дёргаясь при мелких движениях головы. Smooth interpolation через `Vector3.SmoothDamp` с переменной скоростью.

Активная кнопка визуально отличается тремя ColorBlock'ами (`_inactiveHoverBrightness`, `_activeBrightness`, `_activeHoverBrightness`) — это даёт ощущение «анимированной» подсветки. `DetachablePanel.cs` позволяет «отлепить» отдельный модуль панели в мировое пространство (например, оставить инструмент рядом с объектом).

Хоткей toggle'а панели — primaryButton (X на левом контроллере / A на правом), см. `UserPanelOpener.cs`. Подробнее — `memory/project_hotkeys.md`.

---

### 3.2.2 Режим песочницы

Sandbox — отдельный режим (`AppMode.Sandbox`), который намеренно не привязан к диску. Используется для свободных экспериментов без риска повредить пользовательский проект.

**Запуск** (из `MainMenuPanel.cs`):

```csharp
private void OnOpenSandbox()
{
    var data = _storage.BeginSandboxSession();
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.Sandbox);
}
```

**Реализация «не сохраняется»** в `AppStorage.cs`:

```csharp
public SceneData BeginSandboxSession()
{
    var data = new SceneData
    {
        SceneId     = "__sandbox__",
        DisplayName = "Sandbox",
        CreatedAt   = DateTime.UtcNow.ToString("yyyy-MM-dd")
    };
    _activeSceneId = data.SceneId;
    return data;
}
```

Зарезервированный id `__sandbox__` — это маркер. Папка для него не создаётся (`Directory.CreateDirectory` не вызывается). `SceneAutoSaver` имеет явный guard:

```csharp
if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;
```

Поэтому при переходе из Sandbox в MainMenu auto-save срабатывает, но видит зарезервированный id и просто выходит. Никаких файлов не создаётся.

Графически Sandbox использует ту же `VrEditing.unity` инфраструктуру через `SandboxSceneScope`, который регистрирует те же сценные сервисы: `SceneGraph`, `SelectionManager`, `CommandStack`, `GizmoController`, `AssetSpawner`. То есть весь рабочий инструментарий доступен — только сохранения нет.

---

### 3.2.3 Управление сценами

#### Создание

Алгоритм создания, инициированный из `ScenePickerPanel`:

```csharp
private async Task OnCreateClickedAsync()
{
    var name = _nameInput.text;
    if (string.IsNullOrWhiteSpace(name)) name = "New Scene";
    _nameInput.text = string.Empty;
    await _storage.CreateSceneAsync(name, CancellationToken.None);
    await RefreshAsync();
}
```

Внутри `AppStorage.CreateSceneAsync`:

```csharp
public async Task<SceneData> CreateSceneAsync(string displayName, CancellationToken ct = default)
{
    var sceneId = Guid.NewGuid().ToString("N")[..8];
    var data = new SceneData
    {
        SceneId     = sceneId,
        DisplayName = displayName,
        CreatedAt   = DateTime.UtcNow.ToString("o")
    };
    Directory.CreateDirectory(_paths.SceneRoot(sceneId));
    await SaveSceneAsync(data, ct);
    _cache[sceneId] = data;
    return data;
}
```

Сцена создаётся как пустая (`Nodes` пустой список), пишется на диск, кешируется. Имя берётся из `TMP_InputField`, в который пользователь вводит через VR-клавиатуру.

#### Редактирование

«Редактирование» = вход в `VrEditing.unity` для конкретной сцены и работа со всеми инструментами (см. §3.2.4–3.2.7). Точка входа — `MainMenuPanel.OpenSceneAsync()` (приведена выше).

После входа сцена живёт в памяти как `SceneGraph` (`SceneComposition/SceneGraph.cs`) — дерево узлов с операциями `AddNode`, `RemoveNode`, ребэйз, рейзинг событий `SceneModified`. Граф собирается из загруженных `NodeData` (стартует в `IStartable.Start`):

```csharp
public void Start()
{
    _spawnedRoot = new GameObject("[Spawned]").transform;
    _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

    var activeId = _storage.ActiveSceneId;
    if (!string.IsNullOrEmpty(activeId))
        _ = OnSceneOpenedAsync(new SceneOpenedEvent { SceneId = activeId });
}
```

Все операции пользователя над сценой проходят через `CommandStack` (`SceneComposition/Data/CommandStack.cs`) — линкед-лист команд с дефолтной глубиной 30 (тюнится через DI). Реализация Undo выглядит так:

```csharp
public void Undo()
{
    if (_history.Count == 0) return;
    var cmd = _history.Last.Value;
    _history.RemoveLast();
    cmd.Undo();
}
```

Конкретная команда трансформа (`TransformCommand.cs`) хранит «было» и «стало» снимки и поддерживает обратное применение:

```csharp
public class TransformCommand : ICommand
{
    public void Execute() { _target.position = _newPosition; _target.rotation = _newRotation; ... }
    public void Undo()    { _target.position = _oldPosition; _target.rotation = _oldRotation; ... }
}
```

При выходе из режима `VrEditing` — авто-сохранение через `SceneAutoSaver`.

---

### 3.2.4 Добавление ассетов

Подсистема `AssetBrowser` поддерживает три источника ассетов, каждый реализует интерфейс `ILabAsset`:

| Класс | Источник | Хранение |
|---|---|---|
| `BuiltinLabAsset` | Встроенные в билд prefab'ы | Поле `_prefab` в SO `BuiltinAssetLibrary` |
| `ImportedLabAsset` | Пользовательский файл (FBX/OBJ) | Скопированный файл в `persistentDataPath` |
| `SavedLabAsset` | Сохранённая поза/риг | `assetId` ссылка в каталоге `saved.json` |

Контракт `ILabAsset.SpawnAsync(position, rotation, ct)` отличается по реализации:

**BuiltinLabAsset** — мгновенный `Instantiate` префаба:
```csharp
public Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct)
{
    var instance = UnityEngine.Object.Instantiate(_prefab, position, rotation);
    return Task.FromResult(instance);
}
```

**ImportedLabAsset** — сейчас выбрасывает `NotImplementedException("ImportedLabAsset.SpawnAsync — drag-drop phase")`. Загрузка внешних FBX/OBJ запланирована, но реализация ещё не завершена (TODO).

**SavedLabAsset** — аналогично имеет stub.

Координация спавна выполняется через `AssetSpawner` (`AssetBrowser/AssetSpawner.cs`) — `IStartable`-сервис, который слушает `AssetSpawnRequestedEvent`:

```csharp
private async Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
{
    var go = await e.Asset.SpawnAsync(e.Position, e.Rotation, CancellationToken.None);
    var assetRef = new AssetRef
    {
        Source  = e.Asset is BuiltinLabAsset  ? AssetSource.Builtin
                : e.Asset is ImportedLabAsset ? AssetSource.Imported
                                              : AssetSource.Saved,
        AssetId = e.Asset.Id,
    };
    _graph.AddNode(go, assetRef, e.Asset.DisplayName);
    _resolver.InjectGameObject(go);
}
```

После создания GameObject:
1. Добавляется в `SceneGraph` через `AddNode` — это автоматически генерирует `SceneNodeId`, регистрирует ноду в графе, публикует `SceneModifiedEvent`.
2. Через `IObjectResolver.InjectGameObject` Unity-DI прогоняет цепочку `Construct`'ов на всех `MonoBehaviour` нового объекта — `XRPromeonInteractable`, `PromeonProxyRigBuilder`, `Selectable` получают свои зависимости.

UI добавления реализован через `AssetBrowserModule.cs` (вкладка в `UserPanel`'е) и `LabAssetCard.cs` (отдельный «карточка ассета» с превью). Тап по карточке публикует `AssetSpawnRequestedEvent` с координатами перед камерой.

#### Типы ассетов

`AssetType` enum (`_Shared/Models/AssetType.cs`):

```csharp
public enum AssetType { Model, Rig, Texture, Material, Video, Audio, Pose }
```

- **Model** — статический меш или скиннутая модель. Спавнится напрямую.
- **Rig** — отдельный ассет рига (BoneHierarchy + ControlShapes). Применяется к Model через `RigRuntime.ApplyDefinition` (см. §3.2.6).
- **Pose** — снимок состояния костей рига. Применяется к совместимому ригу. **Сохранение позы как ассета и применение из библиотеки сейчас являются TODO** — `AssetType.Pose` декларирован, но pipeline'а сохранения/применения нет в коде.
- **Texture / Material / Video / Audio** — задекларированы, не используются.

---

### 3.2.5 Взаимодействие с объектами на сцене

#### Базовая модель ввода

3D-объекты на сцене обрабатываются скриптом `XRPromeonInteractable.cs` (`VrInteraction/`). Это кастомный наследник `XRBaseInteractable` (из Unity XR Interaction Toolkit), который **отключает стандартный XRI select-flow** через `IsSelectableBy => false` и сам читает кнопки контроллера каждый кадр в `ProcessInteractable(Dynamic)`.

Маппинг кнопок:

| Кнопка | Tap | Hold |
|---|---|---|
| Trigger | `SelectionManager.Select(nodeId)` | Rotation drag (вращение объекта вокруг своей оси) |
| Grip | — игнор | Position drag (свободное перемещение объекта) |

`_tapWindow` (SerializeField, дефолт 0.5s) определяет порог. Подробнее — `memory/interaction-input-model.md`.

#### Включение гизмоса

Гизмо — 3D-манипулятор для точной осевой трансформации (Move/Rotate/Scale вдоль осей). Активируется через UI: пользователь нажимает на навбар-кнопку «Gizmo Tools» в `UserPanel`, открывается `GizmoToolsPanel`.

Поток событий:

1. `GizmoToolsPanel` в `OnEnable` публикует `GizmoToolsPanelOpenedEvent`.
2. `GizmoActivator` (`VrInteraction/Gizmo/GizmoActivator.cs`) слушает событие. Если есть `SelectedNodeId`, спавнит prefab `Vr3D_Gizmos`.
3. Spawn:
   ```csharp
   _instance = Instantiate(_config.GizmoPrefab);
   _instance.transform.position = _target.position;
   _instance.transform.rotation = _target.rotation;
   var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
   _instance.transform.localScale = Vector3.one * size;
   ```
4. Размер гизмо подгоняется под bounding box объекта через `BoundsFitter.ComputeSize`.
5. Кнопки `Move | Rotate | Scale` переключают активный sub-набор ручек через `GizmoHierarchy.ShowMode`.

#### Воздействие на объект (Inversion model)

Гизмо устроена по принципу: **гизмо — primary source-of-truth, target подтягивается за ней.** Стратегия мутирует `_instance.transform`, а Activator каждый кадр копирует изменения в target.

Архитектура — pattern Strategy:

| Стратегия | Что считает |
|---|---|
| `AxisMoveStrategy` | Projection руки на бесконечную линию вдоль оси |
| `AxisScaleStrategy` | Signed distance вдоль оси → factor для одной оси |
| `UniformScaleStrategy` | `magnitude(hand→pivot) / distAtGrab` → uniform factor |
| `RingRotateStrategy` | Projection руки на плоскость кольца → SignedAngle |

Все четыре реализуют `IGizmoDragStrategy.BeginDrag/UpdateDrag/EndDrag`.

Захват ручки в `GizmoHandle.cs`:

```csharp
if (gripDownNow && !_gripWasDownLastFrame)
{
    _locked = ni;
    _state  = HandleState.Dragging;
    var ctrl = ni.transform;
    _grabRayDistance = Vector3.Distance(ctrl.position, transform.position);
    var virtualPos = ctrl.position + ctrl.forward * _grabRayDistance;
    _activator?.OnHandleGrabbed(this, virtualPos, ctrl.rotation);
}
```

**Виртуальная hand-точка** = `controller.position + controller.forward * grabDistance`. Это даёт поведение, как у regular grab — поворот контроллера тоже двигает гизмо (точка качается по сфере вокруг контроллера). Не требуется буквально переносить руку по комнате для каждого движения.

Каждый кадр в `OnHandleDragged` Activator вызывает strategy, потом синхронизирует target:

```csharp
public void OnHandleDragged(Vector3 handPos, Quaternion handRot)
{
    _activeStrategy?.UpdateDrag(handPos, handRot);
    switch (_activeStrategy)
    {
        case AxisMoveStrategy:
            _target.position = _instance.transform.position;
            break;
        case RingRotateStrategy:
            _target.rotation = _instance.transform.rotation;
            break;
        case AxisScaleStrategy:
        case UniformScaleStrategy:
            var inst = _instance.transform.localScale;
            var factor = new Vector3(inst.x / _instanceScaleAtGrab.x, ...);
            _target.localScale = new Vector3(_targetScaleAtGrab.x * factor.x, ...);
            break;
    }
}
```

На release — pattern «snapshot/restore»:
1. `OnHandleReleased` запоминает финальную позицию/поворот/масштаб.
2. Восстанавливает target к состоянию на момент grab.
3. Вызывает `GizmoController.CommitTransform(target, final...)` → `CommandStack.Execute(new TransformCommand(...))` → накатывает final.

Это даёт один атомарный коммит для Undo — то есть отпустил ручку, нажал Ctrl-Z, объект вернулся в состояние до начала драга, без промежуточных шагов.

#### Иерархия объектов в сцене

`SceneGraph` хранит словарь `Dictionary<string, SceneNode>` и `_transientNodes` (для прокси-костей ригов, которые в граф добавляются но в JSON не пишутся). Каждая нода связана с `GameObject` в Unity-сцене; имена в иерархии формируются из `DisplayName` ассета.

UI-представление иерархии — `SceneOutlinerView` (`SpatialUi/Scripts/Views/SceneOutlinerView.cs`) — это VR-аутлайнер: вертикальный список с отступами по иерархии, видимостью, lock'ом. Подписан на `SceneModifiedEvent` и `SelectionChangedEvent`, перерисовывается по событию.

Скрин иерархии (для ВКР) делается вручную из Editor'а или из VR — outlinerView показывает то же, что Unity Hierarchy в редакторе, но в VR-пространстве.

---

### 3.2.6 Работа с ригом

Подсистема `RigBuilder` отвечает за визуализацию и взаимодействие со скелетом 3D-модели.

#### Подтягивание рига

`RigRuntime` (`RigBuilder/RigRuntime.cs`) — точка входа. Принимает `SkinnedMeshRenderer` импортированной модели и `RigDefinition` (отдельный ассет с описанием рига):

```csharp
public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
{
    var boneRenderer = smr.GetComponentInParent<PromeonProxyRigBuilder>();
    if (boneRenderer == null)
        boneRenderer = smr.gameObject.AddComponent<PromeonProxyRigBuilder>();

    var bones = new List<Transform>();
    foreach (var bone in definition.Bones)
    {
        var t = FindBone(smr, bone.BoneName);
        if (t != null) bones.Add(t);
    }
    boneRenderer.SetTransforms(bones.ToArray());
    boneRenderer.Rebuild();

    if (_resolver != null) _resolver.InjectGameObject(boneRenderer.gameObject);
}
```

`RigDefinition` сериализуется как JSON-ассет с массивом `BoneRecord { BoneName, ControlShapeAssetId? }`. По именам костей строится `Transform[]`, на основе которого `PromeonProxyRigBuilder` строит прокси-визуализацию.

#### Связь прокси-костей с настоящими

`PromeonProxyRigBuilder` строит для каждой кости персональный «пирамидальный» меш, который ведёт от позиции родителя к позиции дочерней кости. Эти прокси-меши — это то, что пользователь видит и трогает в VR (вместо традиционных пустых Transform'ов как у Unity Editor).

Синхронизация прокси с настоящей костью выполняется компонентом `BoneFollower.cs`. Он добавляется на каждый прокси-GameObject и в `LateUpdate` копирует мировой transform целевой кости:

```csharp
void LateUpdate()
{
    transform.position = _bone.position;
    transform.rotation = _bone.rotation;
}
```

Каждый прокси-GO также получает компонент `BoneSceneNodeMarker.cs` — маркер, который содержит `SceneNodeId` соответствующей ноды в `SceneGraph`. Это позволяет аутлайнеру отображать кости как обычные ноды иерархии — выбираемые, переименуемые. `SceneGraph.AddTransientNode` добавляет прокси-кости как `_transientNodes` — они существуют в графе для UI, но не сохраняются в `scene.json` (физически они порождены ригом).

Когда пользователь хватает прокси-кость через `XRPromeonInteractable`, выделение через `SelectionManager.Select(nodeId)` подсвечивает её через outline (см. memory `quickoutline-patched`).

#### IK-сетап

`IkSetupWizard` (`SpatialUi/Scripts/Panels/IkSetupWizard.cs`) — UI-визард для построения IK-цепочки. Пользователь выбирает root-кость и end-кость через dropdown'ы, нажимает Confirm — wizard вызывает `RigRuntime.AddIkChain(rootBone, endBone)`. Внутри строится `IkChainConstraint` через Unity Animation Rigging.

`BoneInspectorPanel` отображает свойства выделенной кости (имя, ограничители трансформа, наличие IK).

#### Сохранение позы

**TODO в текущей кодовой базе.** `AssetType.Pose` декларирован, но pipeline сохранения позы в JSON ассет и применения из библиотеки **не реализован**:

- Метод `SaveAsPose(rigNodeId)` отсутствует в `RigRuntime`.
- Каталог `saved.json` существует (через `SavedAssetLibrary`), но запись поз туда не написана.
- `SavedLabAsset.SpawnAsync` имеет stub.

Это запланированная функциональность, не доведённая до кодовой реализации на 2026-05-21.

---

### 3.2.7 Создание анимаций

Подсистема `AnimationAuthoring` (`Subsystems/AnimationAuthoring/AnimationAuthoring.cs`) реализует ручной keyframing — пользователь устанавливает текущий кадр через `AnimationClock`, расставляет объект в нужное положение и фиксирует ключ.

#### Ключевые кадры

Структура данных (`Data/SceneAnimationData.cs` и связанные):
- `SceneAnimationData` — корневой объект, хранит `List<TrackData>`.
- `TrackData { NodeId, List<KeyframeData> Keys }` — одна дорожка на ноду.
- `KeyframeData { Frame, LocalPosition, LocalRotation, LocalScale, Interpolation }`.

Класс `AnimationClock` — единый источник истины для текущего кадра / FPS / диапазона. Публикует `FrameChangedEvent` при изменении.

#### Запись ключа

`AnimationAuthoring.SetKey(nodeId, frame)`:

```csharp
public void SetKey(string nodeId, int frame)
{
    var go = _sceneGraph.GetNode(nodeId);
    if (go == null) return;

    EnsureData();
    var track = _data.GetOrCreateTrack(nodeId);
    track.UpsertKey(frame, go.transform.localPosition,
                            go.transform.localRotation,
                            go.transform.localScale);
    RebuildClip(track);
    _ = SaveAsync(CancellationToken.None);
    _bus.Publish(new AnimationKeyframeChangedEvent { NodeId = nodeId });
}
```

`UpsertKey` либо обновляет существующий ключ на этом кадре, либо вставляет новый в отсортированный список (по `Frame`). `RebuildClip` пересобирает `UnityEngine.AnimationClip` из дорожки — этот клип потом используется системой воспроизведения (через `Animator` или `Playables`).

#### Удаление ключа

```csharp
public void DeleteKey(string nodeId, int frame)
{
    var track = _data?.FindTrack(nodeId);
    if (track == null) return;
    track.RemoveKey(frame);
    if (track.Keys.Count == 0)
    {
        _data.Tracks.Remove(track);
        _clips.Remove(nodeId);
    }
    else RebuildClip(track);
    _ = SaveAsync(CancellationToken.None);
}
```

Пустая дорожка удаляется целиком — нет смысла хранить track без ключей.

#### Воспроизведение

Подсистема `AnimationPlayback` (`PlaybackController`, `AnimationEvaluator`) **на 2026-05-21 — placeholder**. Файл `Subsystems/AnimationPlayback/AnimationPlayback.cs` содержит лишь:

```csharp
public static class AnimationPlaybackPlaceholder { }
```

С комментарием в шапке: `// Playback logic merged into AnimationAuthoring + AnimationClock`. То есть проигрывание идёт частично через `AnimationClock.FrameChanged` + интерпретация клипов на лету — но полноценного PlaybackController с play/pause/loop/speed UI ещё нет в коде.

#### Сохранение

`AnimationAuthoring.SaveAsync(ct)` пишет `animation.json` в папку сцены через `PathProvider.AnimationJson(sceneId)`. Сериализация через `JsonUtility`. Загрузка происходит автоматически при `SceneOpenedEvent` (если файл существует).

---

### 3.2.8 Экспорт анимаций

**Статус на 2026-05-21: scaffold, не реализовано.**

Подсистема `ExportPipeline` (`Subsystems/ExportPipeline/`) имеет только assembly definition и placeholder-класс:

```csharp
// Placeholder for ExportPipeline subsystem
public static class ExportPipelinePlaceholder { }
```

Запланированная архитектура (по spec'у):
- `ExportOrchestrator` — собирает данные из `SceneGraph` и `AnimationAuthoring` в единую экспортную модель.
- `FbxExporter` — Unity FBX Exporter SDK; запекание анимационных кривых по кадрам.
- `CustomFormatExporter` — fallback в собственный JSON для случаев, когда FBX не сохраняет полноту (высокая плотность ключей, нестандартная иерархия ригов). Допускается создание плагина-импортёра для Blender.
- `ExportConfig { Scope, Format, Origin, OutputName }` — настройки экспорта.
- Область экспорта: отдельный объект, отдельный экшен или вся сцена.

Реализация откладывается на следующий phase. Целевая папка вывода — `{persistentDataPath}/scenes/{SceneId}/export/` — уже определена в `PathProvider.ExportDir(sceneId)`.

---

## 3.3 Технологическое обеспечение

### Платформа и устройства

- **Целевое устройство:** Meta Quest 3 (основная).
- **Совместимость:** Meta Quest 2 (требует тестирования по производительности; рендеринг адаптируется через URP Quality Settings).
- **ОС устройства:** Android 10+ (Quest стандартно работает на Android-based fork).
- **Распространение:** Standalone Android APK, без зависимости от PC во время работы (нет Air Link / Quest Link).

### Минимальные требования для разработки

- **Операционная система:** Windows 10/11 (x64). Сборка Android требует Unity Editor; macOS теоретически работает, но не тестируется.
- **Unity Editor:** **6000.3.7f1** (LTS-кандидат семейства Unity 6). Точная версия — `6000.3.7f1` — зафиксирована в `ProjectSettings/ProjectVersion.txt`.
- **Build target:** Android (ARM64 mandatory для Quest), API level ≥ 29.
- **Android SDK / NDK / OpenJDK:** идут с Unity 6 (`Unity Hub` → `Add Modules` → Android Build Support).
- **IDE для C#:** JetBrains Rider или Visual Studio 2022 с workload «Game development with Unity».
- **Git:** для контроля версий (репозиторий — GitHub).
- **VR-симуляция:** XR Device Simulator (входит в `com.unity.xr.interaction.toolkit`) — позволяет тестировать на PC без надевания шлема.

### Минимальные требования для пользователя

- **Устройство:** Meta Quest 2 или Quest 3 с прошивкой v62 или новее.
- **Свободное место:** не менее 500 MB на устройстве (внутреннее хранилище приложения растёт от пользовательских проектов).
- **Внешняя периферия:** не требуется — приложение полностью автономно. Импорт внешних 3D-файлов выполняется через системный пикер Android (планируется через Unity Runtime File Browser package).

### Технологический стек

| Слой | Технология | Версия |
|---|---|---|
| Движок | Unity | 6000.3.7f1 |
| Графический pipeline | URP (Universal Render Pipeline) | 17.3.0 |
| VR-рантайм | OpenXR + Meta OpenXR | `com.unity.xr.openxr` 1.16.1, `com.unity.xr.meta-openxr` 2.5.0 |
| XR-взаимодействие | Unity XR Interaction Toolkit | 3.0.7 |
| Dependency Injection | VContainer | jp.hadashikick.vcontainer |
| Event-шина | MessagePipe-style EventBus (per-scope) | Собственная обёртка |
| Анимационные констрейнты | Unity Animation Rigging | (включён в URP) |
| Сериализация | Unity JsonUtility | стандарт |
| Экспорт 3D | Unity FBX Exporter SDK | планируется в Phase ExportPipeline |
| Язык | C# | .NET Standard 2.1 |
| Контроль версий | Git + GitHub | — |
| Постобработка анимаций | Blender 3D + Python API | внешний редактор, не часть приложения |

### Структура сборки

Сцены, входящие в билд (порядок задаёт `EditorBuildSettings.asset`):

1. `Bootstrap.unity` — точка входа, `AppBootstrap` грузит MainMenu.
2. `MainMenu.unity` — выбор / создание сцен.
3. `VrEditing.unity` — редактирование пользовательской сцены.
4. `Sandbox.unity` — режим экспериментов без сохранения.

Дополнительно: `Tests/Asset_Review.unity` — внутренняя сцена для review импортированных асет-паков (не входит в production-билд).

### Производительность

- **Целевой FPS:** 72 FPS (Quest 3 standard) / 90 FPS (Quest 3 boost mode).
- **Render scale:** 1.0 (фиксированный); URP с Multi-Pass отключён для уменьшения draw calls.
- **Polygon budget per scene:** ~500K verts суммарно (рекомендация по best practice для Quest).
- **Texture streaming:** включён по дефолту в URP, mipmap'ы автоматические.

Профайлинг проводится через Unity Profiler с подключением к устройству по USB (ADB) или Wi-Fi (Quest Developer Hub).
