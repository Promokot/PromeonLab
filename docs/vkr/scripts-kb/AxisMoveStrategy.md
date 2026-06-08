---
note_type: script
subsystem: VrInteraction
listing: Б.26
---

> [!info] Назначение
> `AxisMoveStrategy` — реализация `IGizmoDragStrategy` для перемещения гизмо вдоль одной оси. Проецирует смещение руки на выбранную мировую ось через скалярное произведение и сдвигает гизмо (instance). Листинг Б.26.

### Обзор

##### Роль и место
Чистый класс без MonoBehaviour, создаётся в `GizmoDragSession.ResolveStrategy` при захвате хэндла с `HandleKind.MoveAxis`. Реализует интерфейс `IGizmoDragStrategy` с тремя методами: `BeginDrag`, `UpdateDrag`, `EndDrag`. Является эталонной реализацией — ВКР описывает именно её как «общую схему» стратегий.

##### Ключевые методы
- `BeginDrag(...)` — фиксирует ось в мировых координатах и «дистанцию захвата».
- `UpdateDrag(...)` — проецирует текущую позицию руки на ось, вычисляет дельту.
- `EndDrag()` — обнуляет `_target`.

### Разбор кода

##### BeginDrag — проекция оси и фиксация дистанции захвата
```csharp
public void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot)
{
    _target            = target;
    _axisWorld         = LocalAxis(target, axis);
    _originalTargetPos = target.position;
    _distAtGrab        = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
}
```

> `LocalAxis(target, axis)` возвращает ось в **мировом** пространстве (`target.right` / `target.up` / `target.forward`) — ось фиксируется в момент захвата и не обновляется в ходе drag. Это делает поведение предсказуемым: ось «вросла» в пространство, а не следует за вращением руки. `_distAtGrab = Vector3.Dot(handPos - _originalTargetPos, _axisWorld)` — скалярная проекция вектора «от гизмо до руки» на ось; это «исходная дистанция» в единицах оси.

##### UpdateDrag — дельта проекций
```csharp
public void UpdateDrag(Vector3 handPos, Quaternion handRot)
{
    if (_target == null) return;
    var distNow = Vector3.Dot(handPos - _originalTargetPos, _axisWorld);
    var delta   = distNow - _distAtGrab;
    _target.position = _originalTargetPos + _axisWorld * delta;
}
```

> Вычисление: `distNow` — текущая проекция руки на ось. `delta = distNow - _distAtGrab` — смещение руки вдоль оси относительно момента захвата. `_originalTargetPos + _axisWorld * delta` — новая позиция гизмо. Использование `_originalTargetPos` (снимок, а не `_target.position`) означает, что любой дрейф из других источников (например, физика) не накапливается в позиции. Движение строго ограничено осью — перпендикулярная компонента движения руки отброшена через `Dot`.

##### LocalAxis
```csharp
private static Vector3 LocalAxis(Transform target, AxisKind axis)
{
    switch (axis)
    {
        case AxisKind.X: return target.right;
        case AxisKind.Y: return target.up;
        default:         return target.forward;
    }
}
```

> `target.right` / `target.up` / `target.forward` — это мировые векторы локальных осей трансформа. Если гизмо повёрнут, оси повёрнуты вместе с ним, и стрелка оси X смотрит в верном направлении. `default: return target.forward` — безопасный fallback для `AxisKind.Z` и любых непредвиденных значений enum.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему используется `Vector3.Dot`, а не просто вычитание позиций?
> **О:** Рука движется в трёхмерном пространстве. Нас интересует только компонента смещения вдоль заданной оси — это и есть скалярное произведение (проекция). Компоненты, перпендикулярные оси, отбрасываются — иначе перемещение вдоль X «ползло» бы и по Y, и по Z.

> [!question]
> **В:** Почему ось фиксируется в `BeginDrag` и не обновляется?
> **О:** Если бы ось пересчитывалась из текущего `target.right` каждый кадр, а strategy одновременно вращает гизмо (в RingRotate режиме), ось «ускользала» бы вслед за вращением — кумулятивный дрейф. Для MoveAxis вращение гизмо не предполагается, ось стабильна.

> [!question]
> **В:** Что произойдёт, если `_axisWorld` окажется нулевым вектором?
> **О:** `Vector3.Dot` с нулевым вектором вернёт 0, `delta` всегда 0 — гизмо не двигается. Нулевой ось возникает только при нулевом `localScale` трансформа (что является вырожденным состоянием). В нормальной работе приложения это невозможно.

### Связи
[[GizmoDragSession]] · [[GizmoDriver]] · [[IGizmoDragStrategy]] · [[XRPromeonInteractable]] · [[Прямой ввод вместо XRI]]
