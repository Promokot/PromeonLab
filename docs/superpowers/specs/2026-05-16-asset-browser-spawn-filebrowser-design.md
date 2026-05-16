# Asset Browser: Spawn Fix + SFB VR Panel

**Date:** 2026-05-16  
**Scope:** Two independent fixes in the AssetBrowser/SpatialUi subsystems.

---

## Problem 1 — Spawn не работает

### Root cause chain

1. `CommandStack` имеет два конструктора: `CommandStack()` и `CommandStack(int maxHistory)`. VContainer выбирает конструктор с наибольшим числом параметров и пытается разрезолвить `System.Int32` из контейнера → `VContainerException`.
2. Исключение возникает при сборке `VrEditingSceneScope` / `SandboxSceneScope` → весь scope не создаётся → `AssetSpawner` никогда не инстанциируется и не подписывается на `AssetSpawnRequestedEvent`.
3. Параллельно: `AssetBrowserModule` начинает неактивным (hidden by default), поэтому его `Start()` не вызывается до первого открытия панели. К тому моменту `ModeChangedEvent` уже был опубликован → `_isEditableMode` остаётся `false` → кнопка Spawn не интерактивна.

### Фикс 1а — `CommandStack.cs`

Добавить `[Inject]` на безпараметрический конструктор. VContainer использует конструктор с атрибутом `[Inject]`, игнорируя остальные.

```
До:  public CommandStack() : this(30) { }
После: [Inject] public CommandStack() : this(30) { }
```

Файл: `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs`

### Фикс 1б — `AssetBrowserModule.cs`

Добавить `ModeOrchestrator` в зависимости через `Construct()`. В `Start()` инициализировать `_isEditableMode` из текущего состояния оркестратора до подписки на события.

```
Construct(): добавить параметр ModeOrchestrator orchestrator → _orchestrator = orchestrator
Start():     _isEditableMode = _orchestrator?.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
             RefreshSpawnButton();   // применить инициализированное состояние
             _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
```

`ModeOrchestrator` зарегистрирован в `RootLifetimeScope` как Singleton — доступен для инъекции без изменений в scope-ах.

---

## Problem 2 — SimpleFileBrowser открывается в screen-space, не в VR

### Цель

`FileBrowser.ShowLoadDialog()` создаёт канвас в режиме Screen Space Overlay. Нужно, чтобы он появлялся как WorldSpace суб-панель, позиционированная поверх `AssetBrowserModule` в VR-пространстве.

### Подход

Модифицировать `SimpleFileBrowserCanvas.prefab` + добавить один новый компонент `FileBrowserVrAnchor`. Логика импорта в `AssetBrowserModule.OnAddClicked()` не меняется.

### Изменения в `SimpleFileBrowserCanvas.prefab`

| Что | Изменение |
|---|---|
| `Canvas.renderMode` | `ScreenSpaceOverlay` → `WorldSpace` |
| `FileBrowserMovement` | Компонент задизейблен (`enabled = false`) — в screen-space координатах не работает в VR, даёт визуальные артефакты |
| Новый компонент | `FileBrowserVrAnchor` добавлен на root GameObject |

Путь: `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab`

### Новый класс `FileBrowserVrAnchor`

Файл: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`

**Обязанности (только `Start()`):**
1. `Object.FindAnyObjectByType<AssetBrowserModule>()` — найти цель.
2. Выровнять world position + rotation под цель.
3. Сдвинуть по локальной оси Z на `-0.02f` — чуть перед панелью, чтобы не z-fight.
4. `canvas.worldCamera = Camera.main` — нужен `GraphicRaycaster` для WorldSpace.
5. Подстроить `localScale` так, чтобы RectTransform канваса покрывал видимую область панели.

`FindAnyObjectByType` здесь допустим: вызывается однократно при инстанциировании канваса, не в Update.

**Конкретные значения scale и RectTransform размера** уточняются при тестировании — зависят от текущего scale существующих VR-панелей.

### Input / EventSystem

SFB использует Unity EventSystem через `GraphicRaycaster` (не raw `Input.GetMouseButton`). OpenXR XR UI Input Module отправляет pointer-события через тот же EventSystem в WorldSpace → совместимость без изменений в SFB.

### Изменения в `AssetBrowserModule.cs`

Метод `OnAddClicked()` не меняется. `FileBrowserVrAnchor` берёт позиционирование на себя при создании канваса.

---

## Файлы затронутые изменениями

| Файл | Тип изменения |
|---|---|
| `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs` | Edit — `[Inject]` на конструктор |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs` | Edit — добавить `ModeOrchestrator`, инициализация `_isEditableMode` |
| `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab` | Edit — Canvas WorldSpace, disable FileBrowserMovement, добавить компонент |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs` | New — позиционирование SFB canvas в VR |

---

## Что не входит в скоуп

- Кастомный VR file picker (заменяющий SFB UI) — отдельная задача при необходимости.
- Изменения в логике импорта (`HandleImportAsync`).
- Изменения в других scope-ах или subsystem-ах.
