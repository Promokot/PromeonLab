---
note_type: script
subsystem: VrInteraction
listing: Б.25
---

> [!info] Назначение
> `GizmoDragSession` — чистый (не MonoBehaviour) класс, инкапсулирующий одну сессию перетаскивания гизмо. Снимает снимок целевой позы при старте, делегирует геометрию конкретной `IGizmoDragStrategy`, синхронизирует цель за гизмо, поддерживает отмену сессии с восстановлением исходной позы. Листинг Б.25.

### Обзор

##### Роль и место
Вспомогательный класс (A2 рефакторинг), выделен из `GizmoDriver`. Не имеет зависимости на Unity lifecycle — `pure helper`. Создаётся в `GizmoDriver.Construct`, живёт всё время жизни `GizmoDriver`. В каждый момент может быть максимум одна активная сессия (`_dragActive` — guard). Стратегия (`IGizmoDragStrategy`) выбирается при `Begin` по типу хэндла и заморожена до конца сессии.

##### Ключевые методы
- `Begin(...)` — старт сессии: снимок позы, выбор стратегии, публикация `GizmoDragStartedEvent`.
- `Update(...)` — кадровое обновление: стратегия мутирует гизмо, цель подтягивается.
- `End()` — нормальное завершение: сброс размера гизмо, публикация `GizmoDragEndedEvent`.
- `Abort()` — отмена: восстановление снимка позы цели.
- `ResolveStrategy(GizmoHandle)` — выбор стратегии по `HandleKind`.

### Разбор кода

##### Begin — снимок позы и заморозка режима
```csharp
public void Begin(Transform instance, Transform target, string targetNodeId, GizmoHierarchy hierarchy,
                  GizmoMode mode, float resetSize, GizmoHandle handle, Vector3 handPos, Quaternion handRot)
{
    if (_dragActive || target == null || handle == null || instance == null) return;
    // ...
    _originalPos         = target.position;
    _originalRot         = target.rotation;
    _originalScale       = target.localScale;
    _instanceScaleAtGrab = instance.localScale;
    _targetScaleAtGrab   = target.localScale;
    _modeAtGrab          = mode;
    // ...
    _activeStrategy.BeginDrag(instance, handle.Axis, handPos, handRot);
    _bus?.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
}
```

> `_modeAtGrab` замораживает режим: `GizmoDriver.OnModeChanged` бейлится при `_drag.IsActive`, поэтому смена режима во время перетаскивания невозможна — стратегия, выбранная при `Begin`, используется до конца. Оба scale-базиса (`_instanceScaleAtGrab`, `_targetScaleAtGrab`) снимаются здесь: гизмо и цель могут иметь разный масштаб (гизмо нормирован под `FixedSize`). Guard `if (_dragActive ... ) return` предотвращает реентерабельность.

##### Update — тип синка зависит от стратегии
```csharp
switch (_activeStrategy)
{
    case AxisMoveStrategy:
        _target.position = _instance.position;
        break;
    case RingRotateStrategy:
        _target.rotation = _instance.rotation;
        break;
    case AxisScaleStrategy:
    case UniformScaleStrategy:
        var inst = _instance.localScale;
        var fX = SafeRatio(inst.x, _instanceScaleAtGrab.x);
        var fY = SafeRatio(inst.y, _instanceScaleAtGrab.y);
        var fZ = SafeRatio(inst.z, _instanceScaleAtGrab.z);
        _target.localScale = new Vector3(
            _targetScaleAtGrab.x * fX,
            _targetScaleAtGrab.y * fY,
            _targetScaleAtGrab.z * fZ);
        break;
}
```

> Pattern matching `case AxisMoveStrategy:` (C# 7+ type pattern) — стратегии являются типами, а не enum. Для scale: гизмо визуально масштабируется (стратегия мутирует `instance.localScale`), но его базовый масштаб = `FixedSize`, а у цели — произвольный. Нельзя просто копировать `localScale` гизмо в цель. `SafeRatio` вычисляет коэффициент изменения (`inst / instanceAtGrab`) и применяет к `targetScaleAtGrab`. `SafeRatio` защищает от деления на 0 (`Mathf.Abs(den) < 1e-6f → 1f`).

##### Abort — восстановление снимка
```csharp
public void Abort()
{
    if (!_dragActive) return;
    if (_target != null)
    {
        _target.position   = _originalPos;
        _target.rotation   = _originalRot;
        _target.localScale = _originalScale;
    }
    _activeStrategy?.EndDrag();
    if (_hierarchy != null) _hierarchy.OnHandleReleased(_modeAtGrab);
    EndDragInternal();
}
```

> `Abort` вызывается из `GizmoDriver.Despawn` (когда гизмо уничтожается, пока активна сессия — например, при сбросе выбора во время перетаскивания). Восстанавливаются все три компоненты трансформа: position, rotation, localScale. `EndDrag()` у стратегии обнуляет её внутренний `_target` reference.

##### EndDragInternal — порядок сброса и публикации
```csharp
private void EndDragInternal()
{
    if (_grabbedHandle != null)
    {
        _painter.Restore(_grabbedHandle);
        _grabbedHandle = null;
    }
    var id = _targetNodeId;
    _activeStrategy = null;
    _dragActive     = false;
    _bus?.Publish(new GizmoDragEndedEvent { TargetNodeId = id });
}
```

> `var id = _targetNodeId` перед обнулением — `GizmoDragEndedEvent` должен нести корректный `TargetNodeId`. `_dragActive = false` устанавливается до `Publish`: обработчики `GizmoDragEndedEvent` (например, `GizmoDriver.OnSelectionChanged`) проверяют `_drag.IsActive` — к моменту обработки события флаг уже `false`, поэтому они могут корректно реагировать.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему стратегия мутирует гизмо (instance), а не напрямую цель?
> **О:** Гизмо — «первичный источник истины» во время drag. Цель подтягивается за гизмо в `Update`. Это позволяет корректно синхронизировать scale-дельту (гизмо и цель имеют разные базовые масштабы), а также изолировать геометрию перетаскивания в стратегии.

> [!question]
> **В:** Зачем `SafeRatio` и что такое `1e-6f` как порог?
> **О:** При масштабировании делим текущий масштаб гизмо на его масштаб в момент захвата. Если он был нулевым (объект был «схлопнут»), деление на 0 дало бы `Infinity`. `1e-6f` — машинная эпсилон-граница для `float`; значения меньше её практически равны 0.

> [!question]
> **В:** Что происходит с состоянием при `Abort`, если `_target` уже уничтожен?
> **О:** Guard `if (_target != null)` перед восстановлением позы — если GO удалён, Unity обнулит reference. Сессия всё равно корректно завершится: `_dragActive = false`, `GizmoDragEndedEvent` публикуется с сохранённым `id`.

### Связи
[[GizmoDriver]] · [[AxisMoveStrategy]] · [[IGizmoDragStrategy]] · [[GizmoDragStartedEvent]] · [[GizmoDragEndedEvent]] · [[SelectionManager]] · [[Паттерн Publish-Subscribe]]
