---
note_type: script
subsystem: ModeOrchestrator
source: |
  Assets/_App/Scripts/ModeOrchestrator/ModeOrchestrator.cs
  Assets/_App/Scripts/ModeOrchestrator/ModeTransitionGraph.cs
listings: [3.17]
---

> [!info] Назначение
> `ModeOrchestrator` — политика переключения режимов: проверяет допустимость перехода по графу `ModeTransitionGraph`, публикует события до и после, делегирует фактическую загрузку сцены [[SceneTransitionRunner]] через интерфейс `ISceneTransition`. Сам класс — чистая логика, без MonoBehaviour. Листинг 3.17.

### Обзор

##### Роль и место

Приложение имеет три режима: `MainMenu`, `VrEditing`, `Sandbox`. Переход между ними — не просто смена сцены Unity: нужно проверить допустимость, сохранить данные прежней сцены, загрузить новую, уведомить подписчиков. `ModeOrchestrator` разделяет эти обязанности:

- **Политика** (что разрешено) — `ModeTransitionGraph.IsAllowed`
- **Порядок событий** — `ModeExitingEvent` до загрузки, `ModeChangedEvent` после
- **Фактическая загрузка** — делегируется `ISceneTransition`

`ModeOrchestrator` регистрируется как синглтон в [[RootLifetimeScope]], принимает три зависимости через конструктор, не наследует `MonoBehaviour`.

##### Ключевые методы

| Метод | Что делает |
|---|---|
| `TransitionTo(AppMode target)` | Полный цикл перехода: проверки → `ModeExitingEvent` → делегирование загрузки → `ModeChangedEvent` в callback |
| `SceneNameFor(AppMode)` (static) | Таблица соответствия `AppMode` → имя сцены Unity (switch-expression) |
| `CurrentMode` (property) | Текущий режим; обновляется **до** публикации `ModeExitingEvent` |

### Разбор кода

##### ModeTransitionGraph — граф как ScriptableObject

```csharp
[SerializeField] private List<Transition> _allowed = new()
{
    new Transition { From = AppMode.MainMenu,  To = AppMode.VrEditing  },
    new Transition { From = AppMode.VrEditing, To = AppMode.MainMenu   },
    new Transition { From = AppMode.MainMenu,  To = AppMode.Sandbox    },
    new Transition { From = AppMode.Sandbox,   To = AppMode.MainMenu   },
};

public bool IsAllowed(AppMode from, AppMode to)
{
    foreach (var t in _allowed)
        if (t.From == from && t.To == to) return true;
    return false;
}
```

> Граф задан как `ScriptableObject`-ассет с редактируемым в инспекторе списком пар. `IsAllowed` — линейный перебор: при четырёх элементах это быстрее, чем `HashSet`. Прямой переход `VrEditing ↔ Sandbox` отсутствует намеренно: маршрут всегда через MainMenu, что гарантирует прохождение `SceneAutoSaver` через `ModeExitingEvent`. Добавить новый разрешённый переход — только в инспекторе, без правок кода.

##### TransitionTo — три барьера входа

```csharp
public void TransitionTo(AppMode target)
{
    if (_current == target) return;
    if (_transition.IsTransitioning) return;
    if (!_graph.IsAllowed(_current, target))
    {
        Debug.LogWarning($"Transition {_current} → {target} not allowed");
        return;
    }
    // ...
}
```

> Три ранних `return` — три разных защиты: (1) idempotency — переход в текущий режим игнорируется; (2) **защита от реентерабельности** — если [[SceneTransitionRunner]] уже выполняет переход (`IsTransitioning == true`), новый запрос отбрасывается тихо. Без этого второй вызов `TransitionTo` во время корутины запустил бы ещё одну загрузку сцены поверх идущей — гарантированное повреждение состояния. (3) граф — логический барьер; нарушение логируется через `Debug.LogWarning` для отладки.

##### Порядок _current = target ДО событий

```csharp
var prev = _current;
_current = target;

_bus.Publish(new ModeExitingEvent { From = prev, To = target });

_transition.Load(SceneNameFor(target), () =>
    _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target }));
```

> `_current` обновляется до публикации `ModeExitingEvent`. Это значит: если обработчик `ModeExitingEvent` вызовет `orchestrator.CurrentMode` — получит уже `target`, а не `prev`. Логически это корректно: переход начался, режим изменился. Обработчик может опросить `CurrentMode`, чтобы понять, куда идём.
>
> `ModeExitingEvent` публикуется **синхронно** — прямо в `TransitionTo`, пока старая сцена и её область ещё живы. `SceneAutoSaver` подписан на это событие и сохраняет `animation.json` / `scene.json` здесь. После публикации `_transition.Load` начинает асинхронную загрузку — `LoadSceneMode.Single` выгрузит старую сцену, уничтожит её область и вызовет `Dispose` на `SceneAutoSaver`. К этому моменту сохранение уже выполнено.
>
> `ModeChangedEvent` публикуется внутри callback, который `SceneTransitionRunner` вызывает **после** того, как новая сцена загружена и её `LifetimeScope` построен (см. исходник `RunRoutine`). Подписчики `ModeChangedEvent` могут немедленно обращаться к объектам новой сцены.

##### SceneNameFor — switch-expression без default

```csharp
private static string SceneNameFor(AppMode mode) => mode switch
{
    AppMode.MainMenu  => "MainMenu",
    AppMode.VrEditing => "VrEditing",
    AppMode.Sandbox   => "Sandbox",
    _                 => null,
};
```

> Метод `static` — не зависит от состояния экземпляра, чистая функция. `_ => null` — возврат `null` для неизвестного `AppMode`. В `SceneTransitionRunner.Load` первой строкой стоит `if (... string.IsNullOrEmpty(sceneName)) return` — нулевое имя сцены тихо блокирует загрузку. Это fail-safe: новый `AppMode`, забытый в этом switch, не вызовет crash, а только тихий no-op.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Почему `ModeExitingEvent` публикуется до загрузки, а `ModeChangedEvent` — после?
> **О:** Потому что у каждого события разные потребители и разные требования к контексту. `ModeExitingEvent` нужен `SceneAutoSaver` — он должен сохранить данные, пока старая сцена ещё жива. После `LoadSceneMode.Single` объекты старой сцены уничтожены — сохранять уже нечем. `ModeChangedEvent` нужен UI для перестройки навигации и панелей новой сцены — они доступны только после того, как сцена загружена и область построена.
>
> **В:** Что защищает от двойного вызова `TransitionTo` во время загрузки?
> **О:** `_transition.IsTransitioning` — флаг `bool` в [[SceneTransitionRunner]], который устанавливается в `true` в начале `Load` и возвращается в `false` в конце корутины. Второй `TransitionTo` проверяет этот флаг и делает ранний `return`. Без этой защиты вторая загрузка `LoadSceneMode.Single` выгрузила бы уже начавшую строиться новую сцену.
>
> **В:** Почему `ModeOrchestrator` не сам загружает сцену через `SceneManager`?
> **О:** Принцип разделения ответственности: `ModeOrchestrator` — политика (что разрешено, в каком порядке события). `SceneTransitionRunner` — механизм (анимация затемнения, асинхронная загрузка). Оркестратор зависит от интерфейса `ISceneTransition`, а не от конкретного класса — при необходимости `SceneTransitionRunner` можно заменить без правки оркестратора (например, заглушкой для тестов).
>
> **В:** Что означает граф переходов и почему нет прямого перехода VrEditing ↔ Sandbox?
> **О:** `ModeTransitionGraph` определяет, какие переходы допустимы. Прямой переход VrEditing → Sandbox пропустил бы MainMenu — а значит, пропустил бы `SceneAutoSaver.OnModeExiting`, который сохраняет анимацию и граф сцены. Маршрут через MainMenu гарантирует, что данные всегда сохраняются при выходе из режима редактирования.
>
> **В:** `_current = target` — до или после `ModeExitingEvent`?
> **О:** До. Строка `_current = target` выполняется перед `_bus.Publish(new ModeExitingEvent {...})`. Поэтому если обработчик `ModeExitingEvent` спросит `orchestrator.CurrentMode` — получит уже новый режим. Это намеренно: переход уже начался.

### Связи

[[SceneTransitionRunner]] · [[RootLifetimeScope]] · [[EventBus]] · [[ModeExitingEvent]] · [[ModeChangedEvent]] · [[SceneAutoSaver]] · [[Области жизни сцен]] · [[SceneContext]] · [[Внедрение зависимостей (VContainer)]]
