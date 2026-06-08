---
note_type: script
subsystem: VrInteraction
listing: 3.52, Б.24
---

> [!info] Назначение
> `GizmoDriver` — MonoBehaviour, управляющий жизненным циклом гизмо-манипулятора: спавн/деспавн экземпляра из префаба, видимость по состоянию (`_panelOpen && _target != null`), подсветка хэндлов (`GizmoHighlightPainter`), делегирование перетаскивания сессии `GizmoDragSession`. Листинги 3.52 и Б.24.

### Обзор

##### Роль и место
MonoBehaviour scene-scope (регистрируется через `FindAnyObjectByType` в `LifetimeScope.Configure`). Разделение ответственности (рефакторинг A2): `GizmoDriver` — spawn/visibility/event routing; `GizmoDragSession` — логика одного перетаскивания; `IGizmoDragStrategy` — геометрия конкретного вида манипуляции. Подписывается на 4 события: `GizmoToolsPanelOpened/Closed`, `GizmoModeChanged`, `SelectionChanged`.

##### Ключевые методы
- `Construct(...)` — [Inject], подписка на события немедленно (не в `OnEnable`).
- `RefreshVisibility()` — пересчёт видимости, спавн/деспавн.
- `LateUpdate()` — синхронизация позиции/ротации экземпляра за целью (не во время drag).
- `OnHandleGrabbed/Dragged/Released/Aborted` — коллбэки от `GizmoHandle`.
- `CurrentSize()` — вычисление размера, учитывая кость.

### Разбор кода

##### Подписка в Construct, а не в OnEnable
```csharp
[Inject]
public void Construct(EventBus bus, ...)
{
    // Subscribe immediately. Doing this in OnEnable would race with LifetimeScope.Awake's
    // BuildCallback – if Activator's OnEnable ran first, _bus would be null and the bail-out
    // would silently skip all subscriptions, causing panel events to go unheard.
    _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
    _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
    _bus.Subscribe<GizmoModeChangedEvent>(OnModeChanged);
    _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
}
```

> Гонка: `OnEnable` MonoBehaviour может вызваться до того, как VContainer выполнил `[Inject] Construct`. В этом случае `_bus == null`, guard `if (_bus == null) return` молча пропускает подписку. Подписывая в `Construct`, гарантируем: к моменту первого события `_bus` уже заполнен.

##### RefreshVisibility — тройная ветка
```csharp
private void RefreshVisibility()
{
    bool shouldShow = _panelOpen && _target != null;
    if (shouldShow && _instance == null)       Spawn();
    else if (!shouldShow && _instance != null) Despawn();
    else if (shouldShow && _instance != null)  { Despawn(); Spawn(); }
}
```

> Третья ветка (`shouldShow && _instance != null`) — перерождение при смене цели. Когда выбор переходит с объекта A на объект B, нужно переместить гизмо. Вместо `_instance.transform.position = _target.position` — полный цикл Despawn/Spawn — потому что `GizmoDragSession`, `GizmoHighlightPainter` и `GizmoHierarchy` привязаны к старому экземпляру. Проще пересоздать.

##### LateUpdate — «следование за целью»
```csharp
private void LateUpdate()
{
    if (_instance == null) return;
    if (_target == null) { if (!_drag.IsActive) Despawn(); return; }
    if (_drag.IsActive) return;
    _instance.transform.position = _target.position;
    _instance.transform.rotation = _target.rotation;
}
```

> Во время drag `GizmoDragSession` мутирует `_instance` напрямую — гизмо ведёт объект, а не наоборот. `LateUpdate` пропускает этот кадр (`_drag.IsActive → return`), иначе он бы переписал позицию экземпляра из target, которую strategy как раз ещё не успела обновить. `LateUpdate` выполняется после `Update` всех компонентов — к этому моменту handle уже передал `OnHandleDragged → GizmoDragSession.Update`.

##### CurrentSize — кость получает половину
```csharp
private float CurrentSize()
{
    float size = _config != null ? _config.FixedSize : 1f;
    if (_target != null && _target.GetComponent<BoneSceneNodeMarker>() != null) size *= 0.5f;
    return size;
}
```

> `BoneSceneNodeMarker` — маркер-компонент на прокси-кости. Гизмо у кости уменьшается вдвое, чтобы не перекрывать ромбическую геометрию кости. `_config.FixedSize` — константа из ScriptableObject (`GizmoConfig`), не bounds-fit (вычисление по габаритам объекта закомментировано в `Spawn`).

##### OnPanelClosed — guard на активный drag
```csharp
private void OnPanelClosed(GizmoToolsPanelClosedEvent _)
{
    if (_drag.IsActive) return;
    _panelOpen = false;
    RefreshVisibility();
}
```

> Если пользователь закрыл панель инструментов гизмо во время перетаскивания, деспавнить гизмо немедленно нельзя — сессия ещё активна. Guard откладывает скрытие; когда `OnHandleReleased()` завершит сессию, `_drag.IsActive` станет `false`. Но `RefreshVisibility` после `End()` уже не вызывается — нужен дополнительный вызов (уточнить: возможно, это потенциальный edge-case).

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему гизмо перерождается (Despawn + Spawn) при смене цели, а не просто перемещается?
> **О:** К экземпляру привязаны `GizmoDragSession`, `GizmoHighlightPainter` (карта handle→renderer) и `GizmoHierarchy`. При смене цели нужно переинициализировать всё это для нового объекта. Despawn/Spawn чище, чем ручной ресет каждого внутреннего состояния.

> [!question]
> **В:** Зачем `LateUpdate`, а не `Update`?
> **О:** В `Update` `XRPromeonInteractable` уже перемещает объект (`ApplyMove/ApplyRotate`). В `LateUpdate` это движение уже применено — гизмо «догоняет» актуальную позицию цели за тот же кадр. Если бы синхронизация была в `Update`, порядок выполнения между разными компонентами недетерминирован.

> [!question]
> **В:** Что происходит с гизмо, если цель (выбранный объект) удалена из сцены?
> **О:** `_target` — это `Transform` удалённого GO. Unity обнуляет reference на уничтоженный объект; в следующем `LateUpdate` — `if (_target == null) { if (!_drag.IsActive) Despawn(); return; }` — гизмо деспавнится.

### Связи
[[GizmoDragSession]] · [[AxisMoveStrategy]] · [[SelectionManager]] · [[SelectionChangedEvent]] · [[GizmoDragStartedEvent]] · [[GizmoDragEndedEvent]] · [[XRPromeonInteractable]] · [[BoneEditMode]] · [[Прямой ввод вместо XRI]] · [[Внедрение зависимостей (VContainer)]]
