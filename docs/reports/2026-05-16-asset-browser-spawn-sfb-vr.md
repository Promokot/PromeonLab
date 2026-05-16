# Asset Browser — Spawn Fix + SFB VR Panel: Отчёт

**Дата:** 2026-05-16  
**Статус:** Реализовано, требует ручного тестирования в Play Mode

---

## Что было сломано и почему

### Баг 1: Spawn не работает (VContainerException)

`CommandStack` имеет два конструктора: `CommandStack()` и `CommandStack(int maxHistory)`.  
VContainer по умолчанию выбирает конструктор с наибольшим числом параметров → пытается разрезолвить `System.Int32` из контейнера → кидает `VContainerException` при сборке `VrEditingSceneScope` и `SandboxSceneScope` → весь scope не собирается → `AssetSpawner` не создаётся → событие `AssetSpawnRequestedEvent` некому обрабатывать → спавн молча не работает.

### Баг 2: Кнопка Spawn не активируется

`AssetBrowserModule` живёт в иерархии Bootstrap-сцены и начинает неактивным (`gameObject.SetActive(false)`).  
Unity не вызывает `Awake()`/`Start()` на неактивных объектах → когда пользователь переходит в VrEditing, `ModeChangedEvent` публикуется, но `AssetBrowserModule` ещё не подписался → `_isEditableMode` остаётся `false` → `RefreshSpawnButton()` делает кнопку `interactable = false` → кнопка никогда не становится кликабельной.

---

## Что было сделано

### Fix 1 — `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs`

Добавлен атрибут `[Inject]` на безпараметрический конструктор. VContainer теперь использует его вместо `CommandStack(int)`.

```csharp
[Inject] public CommandStack() : this(30) { }
```

Существующие тесты (создают `CommandStack` через `new` напрямую) не затронуты.

### Fix 2 — `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs`

Добавлена инъекция `ModeOrchestrator`. В `Start()` `_isEditableMode` инициализируется из `CurrentMode` в момент первого открытия панели — независимо от того, когда именно был опубликован `ModeChangedEvent`.

```csharp
// В Construct():
_orchestrator = orchestrator;

// В Start():
_bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
_isEditableMode = _orchestrator?.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
RefreshSpawnButton();
```

`Awake()` не тронут.

### Feature — SFB как WorldSpace VR-панель

**Новый файл:** `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`

MonoBehaviour, добавляемый на `SimpleFileBrowserCanvas.prefab`. При `Start()` находит `AssetBrowserModule` в сцене (включая inactive), выравнивает canvas поверх него в мировом пространстве, выставляет `worldCamera = Camera.main` для VR raycasting.

**Изменён:** `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab`
- `Canvas.renderMode` → WorldSpace
- `FileBrowserMovement` — компонент отсутствовал (не активен)
- Добавлен компонент `FileBrowserVrAnchor` с дефолтными значениями (`_forwardOffset = 0.02`, `_scale = 0.001`)

---

## Ручное тестирование (необходимо)

| Шаг | Ожидаемый результат |
|---|---|
| Войти в VrEditing или Sandbox | В Console — нет `VContainerException` |
| Открыть панель Assets, выбрать ассет | Кнопка **Spawn** стала интерактивной |
| Нажать Spawn | Объект появляется в сцене перед камерой |
| Нажать **Add** | Файловый браузер появляется в VR поверх панели (не экранный overlay) |
| Выбрать файл в браузере | Файл появляется во вкладке Imported |
| Нажать Spawn для импортированного ассета | Объект появляется в сцене |

**Если SFB canvas слишком большой/маленький:** подстроить `_scale` в компоненте `FileBrowserVrAnchor` на префабе `SimpleFileBrowserCanvas`. Рабочий диапазон: `0.0005–0.002`.

---

## Технический долг (не блокирует, зафиксировать для follow-up)

- `AssetSpawner` и `CommandStack` регистрируются с `.AsImplementedInterfaces().AsSelf()`, но у `CommandStack` нет интерфейса — `.AsImplementedInterfaces()` — мёртвый код.
- `OnAddClicked` использует `_ = HandleImportAsync(...)` (fire-and-forget) — исключения внутри `HandleImportAsync` могут быть проглочены молча; стоит добавить try/catch с `ErrorDispatcher`.
- `Camera.main` используется напрямую в нескольких местах — в VR-контексте безопасно, но можно заменить на DI (`Camera` регистрируется в scope-ах).
