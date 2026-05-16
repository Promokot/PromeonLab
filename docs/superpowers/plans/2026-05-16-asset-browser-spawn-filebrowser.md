# Asset Browser: Spawn Fix + SFB VR Panel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Починить спавн ассетов в VrEditing/Sandbox (VContainerException на CommandStack + не инициализируется `_isEditableMode`), и открывать SimpleFileBrowser как WorldSpace VR-панель поверх AssetBrowserModule вместо screen-space overlay.

**Architecture:** Три точечных code-фикса + один новый компонент + модификация SFB-префаба. Никакая логика импорта не меняется. `FileBrowserVrAnchor` — новый MonoBehaviour, добавляемый на `SimpleFileBrowserCanvas.prefab`; при старте сам находит `AssetBrowserModule` и выравнивает себя по нему в мировом пространстве.

**Tech Stack:** Unity 6000.3.7f1, VContainer, OpenXR, SimpleFileBrowser plugin, NUnit (Unity Test Runner — Edit Mode).

---

## File Map

| Файл | Действие |
|---|---|
| `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs` | Modify — `[Inject]` на default-конструктор |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs` | Modify — добавить `ModeOrchestrator`, инит `_isEditableMode` |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs` | Create — новый MonoBehaviour |
| `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab` | Modify в Unity Editor — WorldSpace Canvas, disable FileBrowserMovement, добавить FileBrowserVrAnchor |

---

## Task 1: Фикс CommandStack — VContainer выбирает неверный конструктор

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs`
- Test: `Assets/_App/Subsystems/SceneComposition/Tests/CommandStackTests.cs` (уже существует, запустить)

**Проблема:** VContainer использует конструктор с наибольшим числом параметров. `CommandStack(int maxHistory)` выигрывает у `CommandStack()`, VContainer не может разрезолвить `System.Int32` → `VContainerException` → весь scope не строится → `AssetSpawner` не создаётся.

- [ ] **Открыть** `Assets/_App/Subsystems/SceneComposition/Data/CommandStack.cs`

- [ ] **Добавить `[Inject]` на default-конструктор** — атрибут указывает VContainer использовать именно этот конструктор, игнорируя остальные:

```csharp
using System.Collections.Generic;
using VContainer;

public class CommandStack
{
    private readonly int _maxHistory;
    private readonly LinkedList<ICommand> _history = new();

    [Inject] public CommandStack() : this(30) { }
    public CommandStack(int maxHistory) => _maxHistory = maxHistory;

    public void Execute(ICommand command)
    {
        command.Execute();
        _history.AddLast(command);
        if (_history.Count > _maxHistory)
            _history.RemoveFirst();
    }

    public void Undo()
    {
        if (_history.Count == 0) return;
        var cmd = _history.Last.Value;
        _history.RemoveLast();
        cmd.Undo();
    }
}
```

- [ ] **Подождать компиляцию** (Unity Console — без ошибок)

- [ ] **Запустить существующие тесты** в Unity: `Window > General > Test Runner > Edit Mode > Run All`

  Ожидаемый результат: все 3 теста зелёные (`Undo_AfterExecute_CallsUndo`, `Undo_EmptyStack_DoesNotThrow`, `Execute_ExceedsMaxHistory_DropsOldest`). Тесты создают `CommandStack` напрямую через `new CommandStack(maxHistory: 5)` — `[Inject]` не влияет на прямое создание через конструктор.

- [ ] **Проверить в Play Mode:** войти в VrEditing или Sandbox — убедиться, что VContainerException больше не появляется в Console.

---

## Task 2: Фикс AssetBrowserModule — `_isEditableMode` не инициализируется

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs`

**Проблема:** Панель начинает с `gameObject.SetActive(false)` → Unity не вызывает `Awake()`/`Start()` до первого открытия → `_bus?.Subscribe<ModeChangedEvent>` не выполняется в момент перехода в VrEditing → `_isEditableMode` остаётся `false` → кнопка Spawn не активируется.

**Фикс:** Инжектировать `ModeOrchestrator`, в `Start()` инициализировать флаг из `CurrentMode` перед подпиской — независимо от того, когда `Start()` будет вызван.

- [ ] **Открыть** `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs`

- [ ] **Добавить поле `_orchestrator` и обновить `Construct()`:**

```csharp
private ModeOrchestrator _orchestrator;

[Inject]
public void Construct(ModeOrchestrator orchestrator, BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved, EventBus bus)
{
    _orchestrator    = orchestrator;
    _builtinLibrary  = builtin;
    _importedLibrary = imported;
    _savedLibrary    = saved;
    _bus             = bus;
}
```

- [ ] **`Awake()` не трогать** — он отвечает за позиционирование панели и CanvasGroup. Установка `_shownLocalPos` происходит там; если перенести её в `Start()`, метод получит уже смещённую позицию (`_hiddenLocalPos`) и анимация сломается.

- [ ] **Обновить `Start()` — добавить инициализацию `_isEditableMode` перед подпиской:**

```csharp
private void Start()
{
    _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
    _isEditableMode = _orchestrator?.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
    RefreshSpawnButton();
    if (_builtinLibrary != null)
        SwitchLibrary(_builtinLibrary);
}
```

> `_orchestrator?.CurrentMode` возвращает актуальный режим в момент вызова `Start()`, независимо от того, когда именно панель была активирована и когда был опубликован `ModeChangedEvent`.

- [ ] **Подождать компиляцию** — Console без ошибок.

- [ ] **Проверить в Play Mode:**
  1. Войти в VrEditing или Sandbox
  2. Открыть панель Assets
  3. Выбрать любой ассет из сетки
  4. Убедиться, что кнопка **Spawn** стала интерактивной
  5. Нажать Spawn → в сцене должен появиться объект

---

## Task 3: Создать `FileBrowserVrAnchor.cs`

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`

Компонент добавляется на `SimpleFileBrowserCanvas.prefab`. При старте находит `AssetBrowserModule`, выравнивает себя по нему в мировом пространстве и настраивает Canvas.

- [ ] **Создать файл** `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`:

```csharp
using UnityEngine;

public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private void Start()
    {
        var target = Object.FindAnyObjectByType<AssetBrowserModule>();
        if (target == null)
        {
            Debug.LogWarning("FileBrowserVrAnchor: AssetBrowserModule not found — file browser will appear at world origin.");
            return;
        }

        var t = target.transform;
        transform.position = t.position - t.forward * _forwardOffset;
        transform.rotation = t.rotation;
        transform.localScale = Vector3.one * _scale;

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.worldCamera = Camera.main;
    }
}
```

**Значения по умолчанию:**
- `_forwardOffset = 0.02f` — 2 см перед панелью чтобы не z-fight
- `_scale = 0.001f` — при стандартном SFB canvas 700×500 пикселей даёт ~70×50 см в мировом пространстве (может потребовать подгонки при тестировании через Inspector)

- [ ] **Подождать компиляцию** — Console без ошибок. Тип `FileBrowserVrAnchor` должен появиться в Add Component.

---

## Task 4: Модификация `SimpleFileBrowserCanvas.prefab` в Unity Editor

**Files:**
- Modify: `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab`

После того как Task 3 скомпилировался, открыть префаб в Unity Editor и внести изменения.

- [ ] **Открыть префаб:** двойной клик на `Assets/Plugins/SimpleFileBrowser/Resources/SimpleFileBrowserCanvas.prefab` в Project window → Prefab Mode.

- [ ] **Выбрать root-объект** (тот же, что несёт компонент `Canvas`).

- [ ] **Изменить Canvas.renderMode:** в Inspector → компонент `Canvas` → поле `Render Mode` → выбрать `World Space`.

- [ ] **Убедиться, что `GraphicRaycaster` присутствует** на root-объекте (он уже должен быть там по умолчанию в SFB — если нет, добавить через Add Component).

- [ ] **Найти компонент `FileBrowserMovement`** на root-объекте (или дочернем — проверить иерархию). Снять чекбокс `enabled` (задизейблить компонент). Этот компонент перемещает окно в screen-space координатах — в WorldSpace Canvas даёт некорректное поведение.

- [ ] **Добавить компонент `FileBrowserVrAnchor`** на root-объект: Add Component → `FileBrowserVrAnchor`. Оставить `_forwardOffset = 0.02` и `_scale = 0.001` (значения по умолчанию).

- [ ] **Сохранить префаб:** кнопка `Save` в верхней части Prefab Mode (или Ctrl+S).

---

## Task 5: Интеграционная проверка SFB в VR

Нет автоматических тестов для позиционирования в мировом пространстве — проверяем вручную в Play Mode.

- [ ] **Войти в Play Mode** (убедиться, что в VrEditing или Sandbox)

- [ ] **Открыть AssetBrowser** (кнопка Assets в UserPanel)

- [ ] **Нажать кнопку Add** → файловый браузер должен появиться как WorldSpace объект поверх панели, а не как экранный overlay.

- [ ] **Если браузер слишком большой/маленький:** выйти из Play Mode → открыть `SimpleFileBrowserCanvas.prefab` → выбрать `FileBrowserVrAnchor` → подкрутить `_scale` (уменьшить если велик, увеличить если мал). Типичный рабочий диапазон: `0.0005` — `0.002`.

- [ ] **Если браузер смещён относительно панели:** подкрутить `_forwardOffset` в `FileBrowserVrAnchor` (или через Inspector в Play Mode для live preview, затем применить значение в префабе).

- [ ] **Выбрать файл в браузере** → убедиться, что импорт работает (файл появляется в Imported tab после закрытия браузера).

- [ ] **Повторно нажать Spawn** для импортированного ассета → объект появляется в сцене.
