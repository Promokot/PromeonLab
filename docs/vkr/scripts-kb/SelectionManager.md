---
note_type: script
subsystem: SceneComposition
listing: 3.48
---

> [!info] Назначение
> `SelectionManager` — единственный владелец состояния выбора в рамках сцены. Хранит `string? _selectedNodeId` (единственная выбранная нода или `null`), при смене публикует `SelectionChangedEvent`. Листинг 3.48. Регистрируется в scene-scope VContainer как `ISelectionManager`.

### Обзор

##### Роль и место
Сервис scene-scope (`IStartable`, `IDisposable`, зарегистрирован в `VrEditingLifetimeScope`). Выбор всегда единичный; одновременный выбор нескольких сущностей в приложении не предусмотрен. Два источника выбора: `XRPromeonInteractable` (луч контроллера) и `OutlinerPanel` (нажатие строки обозревателя) — оба вызывают один и тот же `Select(id)`.

##### Ключевые методы
- `Select(string nodeId)` — единственная точка записи состояния выбора.
- `SelectedNodeId` — read-only геттер для опроса текущего выбора.
- `Start()` / `Dispose()` — пустые; реализованы ради `IStartable`/`IDisposable` (VContainer требует интерфейс, логики нет).

### Разбор кода

##### Select(string nodeId)
```csharp
public void Select(string nodeId)
{
    if (_selectedNodeId == nodeId) return;
    _selectedNodeId = nodeId;
    _bus.Publish(new SelectionChangedEvent { SelectedNodeId = _selectedNodeId });
}
```

> Идемпотентный guard `if (_selectedNodeId == nodeId) return` критичен: без него повторный вызов `Select(id)` с тем же значением разбудил бы всех подписчиков (`SelectionVisualSync`, `GizmoDriver`, `InspectorPanel`, `InteractionMaskBinder`) без реальной смены состояния. Сравнение строк по значению (`==` для `string` в C#) корректно, `null == null` тоже `true` — сброс выбора, когда он уже сброшен, тоже глушится. `_selectedNodeId` обновляется **до** публикации: подписчики, читающие `SelectedNodeId` внутри обработчика, получат уже новое значение.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему выбор единственный, а не множественный?
> **О:** Приложение не предусматривает групповые операции. Одновременный выбор нескольких объектов потребовал бы агрегирования трансформаций в инспекторе и определения «сводного» гизмо — вся эта логика отсутствует. `string _selectedNodeId` — намеренное ограничение, а не недоработка.

> [!question]
> **В:** Что произойдёт, если `Select` вызовут из двух потоков одновременно?
> **О:** `SelectionManager` проектировался для вызова только из Unity main thread (все `XRPromeonInteractable.ProcessInteractable`, `OutlinerPanel.OnClick` — Update-цикл). Никакой thread-safety нет; вызов из фонового потока — UB.

> [!question]
> **В:** Зачем `IStartable` и `IDisposable`, если `Start()`/`Dispose()` пусты?
> **О:** VContainer регистрирует `IStartable` для автоматического вызова `Start()` на старте scope и `IDisposable` для `Dispose()` при его уничтожении. Реализация пуста сейчас, но интерфейс зарезервирован: в будущем `Dispose` может снять подписки или логировать утечки.

> [!question]
> **В:** Почему `_bus.Publish` вызывается после записи в `_selectedNodeId`, а не до?
> **О:** Подписчики вправе читать `SelectedNodeId` прямо в обработчике. Если бы публикация шла до обновления, `SelectionVisualSync` или `GizmoDriver` прочитали бы старое значение — рассинхрон. Порядок «сначала запись, потом публикация» — инвариант.

### Связи
`ISelectionManager` · [[SelectionChangedEvent]] · [[SelectionVisualSync]] · [[GizmoDriver]] · [[XRPromeonInteractable]] · [[InspectorPanel]] · [[InteractionMaskBinder]] · [[EmptySpaceClickDeselector]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
