---
note_type: script
subsystem: VrInteraction
listing: 3.51, Б.23
---

> [!info] Назначение
> `XRPromeonInteractable` — наследник `XRBaseInteractable` (XR Interaction Toolkit), реализующий прямой ввод поверх XRI. Штатный select-flow XRI отключён (`IsSelectableBy → false`); компонент читает входы контроллера напрямую и управляет объектом через четырёхсостоятельную машину состояний: `Idle → TriggerPressed → TriggerRotate / GripMove`. Листинги 3.51 и Б.23.

### Обзор

##### Роль и место
MonoBehaviour на каждом спавнящемся объекте сцены (и на прокси-костях). Единственный источник манипуляций объектом через контроллер. Не хранит «глобального» состояния сцены — только локальное состояние текущего взаимодействия (`_state`, `_locked`, offsets). Зависимость от `ISelectionManager` получает через `[Inject]`.

##### Ключевые методы
- `ProcessInteractable(UpdatePhase)` — главная точка входа, вызывается XRI каждый кадр.
- `IsPrimaryFor(NearFarInteractor)` — определяет, какой объект «первый» под лучом.
- `CapturePositionOffset()` / `CaptureRotationOffset()` — фиксируют смещения захвата.
- `ApplyMove()` / `ApplyRotate()` — применяют манипуляцию.
- `RefreshColliderRegistration()` — перерегистрирует коллайдеры в XRInteractionManager.

### Разбор кода

##### IsSelectableBy — отключение стандартного select-flow
```csharp
public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;
```

> XRI по умолчанию захватывает объект при нажатии grip (selectInput). Возвращая `false`, компонент полностью блокирует этот путь. Hover при этом остаётся: `IsHoverableBy` не переопределён (true по умолчанию) — именно через hover XRI передаёт объекту список `interactorsHovering`, который `ProcessInteractable` читает вручную. Это паттерн «прямого ввода» (direct input) вместо XRI.

##### Защита от «падения» заблокированного интерактора
```csharp
if (_state != State.Idle && (_locked == null || !_locked.isActiveAndEnabled))
{ EndInteraction(); return; }
```

> Если контроллер отключился (например, потерял питание или объект ушёл из зоны видимости) во время удержания, `_locked` станет `null` или неактивным. Без этой защиты машина состояний зависнет в `TriggerRotate`/`GripMove` навсегда — объект «прилипнет» к позиции захвата. `EndInteraction()` сбрасывает `_state → Idle` и `_locked → null`.

##### Разрешение «первичного» объекта под лучом
```csharp
private bool IsPrimaryFor(NearFarInteractor ni)
{
    var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
    if (ray != null)
    {
        if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
            return colliders.Contains(hit.collider);
        return false;
    }
    if (ni.interactablesHovered.Count > 0)
        return ReferenceEquals(ni.interactablesHovered[0], this);
    return false;
}
```

> Луч пронизывает все коллайдеры по пути, и XRI уведомляет **все** пересечённые `XRBaseInteractable`. Без фильтра ввод обработают несколько объектов одновременно. `TryGetCurrent3DRaycastHit` возвращает **ближайшее** попадание — тот, чей коллайдер совпадает с `hit.collider`, является «первичным». `colliders.Contains(hit.collider)` — почему список, а не одиночный коллайдер? Объект может быть составным (несколько дочерних мешей с коллайдерами), все они зарегистрированы через `RegisterColliders`.

##### TriggerPressed — различение тапа и удержания
```csharp
case State.TriggerPressed:
    if (_locked.activateInput.ReadWasCompletedThisFrame())
    {
        var node = _node;
        EndInteraction();
        if (node != null) _selectionManager.Select(node.NodeId);
        break;
    }
    if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
    {
        CaptureRotationOffset();
        _state = State.TriggerRotate;
    }
    break;
```

> Две ветки взаимоисключающи в пределах одного кадра: `ReadWasCompletedThisFrame()` — кнопка **отпущена** (тап), переходим в Select. Если кнопка ещё нажата, проверяем таймаут `_tapWindow` (0.5 с). Важно: `var node = _node; EndInteraction()` — сначала запоминаем `_node` в локальную переменную, потом вызываем `EndInteraction()` (который мог бы обнулить поля). `IsObjectSelected()` в ветке перехода в `TriggerRotate` — ротация доступна **только** для уже выбранного объекта: нельзя вращать то, что ещё не выбрано.

##### CapturePositionOffset / ApplyMove
```csharp
private void CapturePositionOffset()
{
    var attach = _locked.GetAttachTransform(this);
    _grabPosOffset = attach.InverseTransformPoint(transform.position);
}

private void ApplyMove()
{
    var attach    = _locked.GetAttachTransform(this);
    var targetPos = attach.TransformPoint(_grabPosOffset);
    _dragStrategy.Apply(transform, targetPos, transform.rotation, DragMode.PositionOnly);
}
```

> `GetAttachTransform` возвращает «точку крепления» руки (attach point NearFarInteractor). `InverseTransformPoint` переводит мировую позицию объекта в **локальное** пространство attach — это смещение фиксируется. `TransformPoint` в `ApplyMove` возвращает смещение обратно в мировое при текущем положении attach. Результат: объект «прилипает» к руке с сохранением взаимного расположения, даже если рука вращается.

##### RefreshColliderRegistration
```csharp
private void RefreshColliderRegistration()
{
    if (interactionManager == null || !interactionManager.IsRegistered((IXRInteractable)this)) return;
    interactionManager.UnregisterInteractable((IXRInteractable)this);
    interactionManager.RegisterInteractable((IXRInteractable)this);
}
```

> XRInteractionManager строит свою карту `collider → interactable` один раз при регистрации. Коллайдеры, добавленные позже (дочерние меши объекта, selector-боксы рига), невидимы для луча. Цикл Un/Register пересобирает карту по полному актуальному списку `colliders`. Guard `IsRegistered` предотвращает вызов на неактивном объекте — `OnEnable` сам заполнит карту при активации.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему XRI select-flow отключён через `IsSelectableBy → false`, а не через настройку интерактора?
> **О:** XRI `NearFarInteractor.selectInput` — это gripButton. Если оставить стандартный select-flow, grip одновременно захватит объект (XRI) и начнёт наш `GripMove` — двойной ответ на одну кнопку. Возврат `false` из `IsSelectableBy` блокирует путь XRI полностью; hover (и соответственно `interactorsHovering`) при этом остаётся.

> [!question]
> **В:** Зачем хранить `_grabPosOffset` в локальном пространстве attach, а не в мировом?
> **О:** Attach-трансформ вращается вместе с рукой. Если хранить смещение в мировых координатах, при вращении руки объект «соскользнёт» относительно неё. Локальное смещение сохраняет взаимное расположение объекта и руки при любом повороте.

> [!question]
> **В:** Что произойдёт, если `_selectionManager == null` в момент вызова Select?
> **О:** Guard `if (_selectionManager == null) return` в начале `ProcessInteractable` предотвращает любой ввод. `_selectionManager` может быть `null` только до DI injection (первый кадр). После `[Inject] Construct(...)` он всегда заполнен.

> [!question]
> **В:** Почему `var node = _node` перед `EndInteraction()` в ветке тапа?
> **О:** `EndInteraction()` обнуляет `_locked` и сбрасывает `_state`, но `_node` не трогает. Однако с точки зрения корректности — если `EndInteraction` в будущем расширится и обнулит `_node`, порядок сломается. Паттерн «capture → EndInteraction → use» — защитная конвенция.

### Связи
[[SelectionManager]] · [[GizmoDriver]] · [[InteractionMaskBinder]] · [[XRPromeonInteractable]] · [[Прямой ввод вместо XRI]] · [[Внедрение зависимостей (VContainer)]] · [[SelectionChangedEvent]]
