---
note_type: script
subsystem: VrInteraction
listing: Б.22
---

> [!info] Назначение
> `EmptySpaceClickDeselector` — MonoBehaviour, сбрасывающий выбор при нажатии триггера обеих рук в пустоту (луч не наведён ни на объект, ни на UI). Листинг Б.22.

### Обзор

##### Роль и место
Лёгкий компонент scene-scope, размещается в сцене с обеими ссылками на `NearFarInteractor` через инспектор. Опрашивает ввод в `Update` — исключение из правила «не polling, если есть событие»: XRI не предоставляет события «триггер нажат в пустоту», только «триггер нажат». Логика снятия выбора намеренно централизована здесь, а не разбросана по панелям.

##### Ключевые методы
- `Check(NearFarInteractor interactor)` — основная проверка на один контроллер.
- `IsOverUI(NearFarInteractor interactor)` — проверка через `TryGetCurrentUIRaycastResult`.

### Разбор кода

##### Check — цепочка условий сброса выбора
```csharp
private void Check(NearFarInteractor interactor)
{
    if (interactor == null || _selectionManager == null) return;
    if (!interactor.activateInput.ReadWasPerformedThisFrame()) return;
    if (IsOverUI(interactor)) return;
    if (interactor.interactablesHovered.Count > 0) return;

    _selectionManager.Select(null);
}
```

> Условия проверяются от «дешёвых» к «дорогим». `ReadWasPerformedThisFrame()` — булев флаг XRI, быстрая проверка. `IsOverUI` — вызов `TryGetCurrentUIRaycastResult`, чуть дороже. `interactablesHovered.Count > 0` — проверка списка (уже заполненного XRI). Все три guard-а должны вернуть `false`, чтобы дойти до `Select(null)`. Порядок критичен: если нажать триггер над кнопкой панели, `IsOverUI` вернёт `true` → выбор не сбросится.

##### IsOverUI — правильный способ определить UI
```csharp
private static bool IsOverUI(NearFarInteractor interactor) =>
    interactor.TryGetCurrentUIRaycastResult(out var r) && r.gameObject != null;
```

> `NearFarInteractor` реализует `IUIInteractor`, у которого есть `TryGetCurrentUIRaycastResult` — прямой способ узнать, попадает ли луч в uGUI-элемент. В коде сохранён закомментированный старый подход через `GetComponentInChildren<XRRayInteractor>` — он был неверным: `NearFarInteractor` не имеет дочернего `XRRayInteractor`. История закомментирована как документация ошибки.

##### Закомментированный Selectable-check
```csharp
// TODO: restore Selectable-in-hovered check once UI guard is confirmed stable
// foreach (var hovered in interactor.interactablesHovered)
// {
//     var go = (hovered as MonoBehaviour)?.gameObject;
//     if (go != null && go.GetComponentInParent<Selectable>() != null) return;
// }
```

> Изначально планировалось дополнительно проверять, нет ли `Selectable` среди hovering-объектов. Убрано как избыточное: `interactablesHovered.Count > 0` уже гарантирует, что луч на чём-то лежит — отдельная проверка `GetComponentInParent<Selectable>()` не добавляет информации. `TODO` — задел на возврат, если появятся объекты без `Selectable` в interactablesHovered, по которым не следует снимать выбор.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему здесь `Update`-polling, а не событийная модель?
> **О:** XRI не предоставляет события «луч попал в пустоту». Все XRI-события привязаны к hover/select конкретных interactable. Обнаружить «ничего не выбрано, но кнопка нажата» можно только опросом в Update. Исключение обосновано — альтернативы нет.

> [!question]
> **В:** Почему `IsOverUI` проверяется до `interactablesHovered.Count`?
> **О:** UI не является `XRBaseInteractable`, поэтому UI-кнопки не попадают в `interactablesHovered`. Если луч наведён только на кнопку (нет физических Interactable), `Count == 0`, но нажатие всё равно не должно сбрасывать выбор. `IsOverUI` — единственный способ поймать это.

> [!question]
> **В:** Что произойдёт, если обе руки нажмут триггер одновременно над пустотой?
> **О:** `Check` вызывается для каждого контроллера независимо. Первый вызов `Select(null)` сбросит выбор. Второй вызов тоже вызовет `Select(null)` — но `SelectionManager` имеет guard `if (_selectedNodeId == nodeId) return` (`null == null`), так что второй вызов будет no-op. Никакого двойного события.

### Связи
[[SelectionManager]] · [[SelectionChangedEvent]] · [[XRPromeonInteractable]] · [[InteractionMaskBinder]] · [[Прямой ввод вместо XRI]]
