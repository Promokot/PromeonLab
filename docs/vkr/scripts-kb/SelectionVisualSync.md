---
note_type: script
subsystem: VrInteraction
listing: 3.49
---

> [!info] Назначение
> `SelectionVisualSync` — scene-scoped сервис, синхронизирующий визуальное состояние обводки QuickOutline для всех нод графа при смене выбора. Подписывается на `SelectionChangedEvent` и переключает `Selectable.SetVisualState` на каждой ноде. Листинг 3.49.

### Обзор

##### Роль и место
Сервис scene-scope (`IStartable`, `IDisposable`). Единственная обязанность — пробежать по `SceneGraph.Nodes` и разослать правильный визуальный статус (`Selected` / `None`). Не хранит состояния выбора — берёт из события. Разделение ответственности: `SelectionManager` владеет _логическим_ состоянием, `SelectionVisualSync` — _визуальным_.

##### Ключевые методы
- `Start()` — подписка на `SelectionChangedEvent`.
- `Dispose()` — отписка (объект уничтожается вместе со scene-scope).
- `OnSelectionChanged(SelectionChangedEvent e)` — главный обработчик.

### Разбор кода

##### OnSelectionChanged
```csharp
private void OnSelectionChanged(SelectionChangedEvent e)
{
    foreach (var pair in _graph.Nodes)
    {
        var sel = pair.Value.GetComponent<Selectable>();
        if (sel == null) continue;
        sel.SetVisualState(pair.Key == e.SelectedNodeId
            ? SelectionVisual.Selected
            : SelectionVisual.None);
    }
}
```

> Итерация по **всем** нодам графа — не только по предыдущей и новой — означает O(n) при каждом событии выбора. Для десятков объектов сцены это дешевле, чем хранить ссылку на «предыдущий» `Selectable` и рисковать dangling reference при удалении ноды. `pair.Key == e.SelectedNodeId` — строковое сравнение; `null == null` корректно снимает обводку при сбросе выбора (`Select(null)`). `GetComponent<Selectable>()` может вернуть `null` для нод, у которых нет компонента (например, нода-камера), — guard `if (sel == null) continue` безопасен.

> `_graph.Nodes` — Dictionary; порядок итерации не определён, но здесь порядок не важен: каждой ноде присваивается независимое состояние. Важно, что словарь не модифицируется во время итерации: событие `SelectionChangedEvent` не добавляет/удаляет ноды, только читает.

##### Start / Dispose
```csharp
public void Start()   => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
```

> Подписка в `Start`, а не в конструкторе — стандартный паттерн VContainer `IStartable`: все объекты scope уже построены к моменту вызова `Start`, `_graph` гарантированно заполнен. Отписка в `Dispose` обязательна: scene EventBus живёт вместе со scope, но `GC` не знает о подписке — без `Unsubscribe` утечка обработчика и, потенциально, NRE при следующем событии от уже уничтоженного сервиса.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему перебираются все ноды, а не только предыдущая и новая?
> **О:** Хранить ссылку на «прошлый Selectable» небезопасно: нода могла быть удалена между событиями. Пробег по всем — O(n) при маленьком n сцены — проще и надёжнее. Стоимость ничтожна по сравнению с кадром VR рендера.

> [!question]
> **В:** Что произойдёт, если нода без компонента `Selectable` окажется в графе?
> **О:** `GetComponent<Selectable>()` вернёт `null`, итерация продолжится через `continue`. Граф может содержать любые ноды (кости, вспомогательные маркеры); обводка нужна только объектам с `Selectable`.

> [!question]
> **В:** Почему подписка в `Start`, а не в конструкторе?
> **О:** В момент вызова конструктора VContainer другие объекты scope могут ещё не быть созданы. `IStartable.Start()` вызывается после `Awake`/`Build` всего scope — все зависимости гарантированно живы.

### Связи
[[SelectionManager]] · [[Selectable]] · [[SelectionChangedEvent]] · [[SceneGraph]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
