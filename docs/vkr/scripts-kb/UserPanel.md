---
note_type: script
subsystem: SpatialUi
listings: [Л3.25, БЛ9]
---

> [!info] Назначение
> `UserPanel` — центральный элемент интерфейса, носитель регионов и навигационной панели. Наследует `SpatialPanel`. Реализует умное следование (`SmoothDamp`), три режима замка с пинг-понговым перебором, масштабирование (0.6–2.0), отвязку от XR-рига и пометку `DontDestroyOnLoad`. Относится к подсистеме `SpatialUi`. Листинг 3.25 (фрагмент) и Приложение Б, Листинг Б.9 (полный).

### Обзор

##### Роль и место

`UserPanel` наследует `SpatialPanel` и переопределяет `LateUpdate` с собственной логикой следования и разворота. При старте отвязывается от XR-рига (`transform.SetParent(null)`) и помечается `DontDestroyOnLoad`, чтобы пережить смену сцен. При смене режима (`ModeChangedEvent`) скрывается и сбрасывает состояние.

Панель получает `PanelGrabHandle` для перетаскивания — он вызывает `SetDragging(true/false)` и `MoveTo(worldPosition)`.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `DetachToWorld()` | `SetParent(null)` + `DontDestroyOnLoad`; вызывается в `Start` |
| `UpdateSmartFollow()` | Умное следование: угловой порог + дистанционный коридор + `SmoothDamp` |
| `FaceCameraBelow()` | Разворот к точке чуть ниже камеры |
| `CycleLockMode()` | Пинг-понг 0→1→2→1→0 через `_lockDir` |
| `AdjustSize(delta)` | Аддитивный шаг масштаба с зажимом в `[_minSizeMultiplier, _maxSizeMultiplier]` |
| `ResetPosition()` | Сброс в Follow + сброс скорости; вызывается при смене режима |
| `SetDragging / MoveTo` | API для `PanelGrabHandle` |

### Разбор кода

##### DetachToWorld — отвязка от рига

```csharp
private void DetachToWorld()
{
    transform.SetParent(null, worldPositionStays: true);
    DontDestroyOnLoad(gameObject);
}
```

> `SetParent(null, worldPositionStays: true)` — отвязывает панель от иерархии XR-рига. Без этого любое перемещение игрока (локомоция, телепорт) переносило бы панель вместе с ригом, и режим «LockPosition» не работал бы — мировые координаты объекта изменялись бы вместе с родителем. С `worldPositionStays: true` текущее мировое положение переносится в `localPosition` (относительно нового родителя — мирового корня), без скачка.
>
> Комментарий в коде уточняет: `_baseScale` захватывается **после** `DetachToWorld`, потому что `SetParent(worldPositionStays:true)` запекает прежний родительский масштаб в `localScale`. Захват `_baseScale = transform.localScale` после отвязки фиксирует реальный мировой размер как базу для мультипликатора.

##### UpdateSmartFollow — умное следование

```csharp
private void UpdateSmartFollow()
{
    if (!_initialized)
    {
        var fwd = GetCameraYawForward();
        transform.position = new Vector3(
            _cameraTransform.position.x + fwd.x * _preferredDistance,
            _cameraTransform.position.y + _yOffset,
            _cameraTransform.position.z + fwd.z * _preferredDistance);
        _initialized    = true;
        _followVelocity = Vector3.zero;
        return;
    }

    var camXZ   = new Vector3(_cameraTransform.position.x, 0f, _cameraTransform.position.z);
    var panelXZ = new Vector3(transform.position.x,        0f, transform.position.z);
    var delta   = panelXZ - camXZ;
    var xzDist  = delta.magnitude;

    if (xzDist > 0.001f)
    {
        var yaw   = GetCameraYawForward();
        var angle = Vector3.Angle(yaw, delta.normalized);

        if (angle > _recenterAngle)
        {
            var targetXZ = camXZ + yaw * _preferredDistance;
            _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
        }
        else if (xzDist < _minDistance || xzDist > _maxDistance)
        {
            var targetXZ = camXZ + delta.normalized * _preferredDistance;
            _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
        }
    }

    if (_activeTarget.HasValue)
    {
        transform.position = Vector3.SmoothDamp(
            transform.position, _activeTarget.Value,
            ref _followVelocity, _smoothTime);

        if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
        {
            transform.position = _activeTarget.Value;
            _activeTarget      = null;
            _followVelocity    = Vector3.zero;
        }
    }
}
```

> Проекции `camXZ`/`panelXZ` обнуляют `Y` — следование идёт только в горизонтальной плоскости. Высота панели (`transform.position.y`) не меняется, потому что новая цель строится как `new Vector3(targetXZ.x, transform.position.y, targetXZ.z)`.
>
> Два условия перецентровки независимы: угловое (`angle > _recenterAngle`) и дистанционное (`xzDist < _minDistance || xzDist > _maxDistance`). Угловое — основной триггер: пользователь повернул голову, панель осталась сбоку. Дистанционное — страховка от экстремальных смещений (не должна слипаться с пользователем или уходить за горизонт).
>
> `_activeTarget` — `Vector3?` (Nullable). Пока цель не выставлена, панель стоит. После достижения цели (`Distance < 0.015f`) `_activeTarget = null` — панель переходит в «ждущее» состояние. Обнуление `_followVelocity` здесь критично: `SmoothDamp` накапливает скорость в `ref`-переменной между кадрами; без сброса остаточная скорость уведёт панель мимо следующей цели при повторном запуске.
>
> `GetCameraYawForward()` — проецирует `cam.forward` на горизонталь (`f.y = 0`) и нормализует. Защита `sqrMagnitude > 0.001f` даёт `Vector3.forward` при взгляде прямо вниз (вырожденный случай).

##### CycleLockMode — пинг-понг замка

```csharp
public void CycleLockMode()
{
    int next = (int)_lockMode + _lockDir;
    if (next >= 2)      { next = 2; _lockDir = -1; }
    else if (next <= 0) { next = 0; _lockDir =  1; }
    _lockMode = (LockMode)next;

    _activeTarget   = null;
    _followVelocity = Vector3.zero;
    ApplyLockVisual();
}
```

> `_lockDir` — текущее направление перебора: `+1` (вперёд) или `-1` (назад). При достижении крайних значений (0 или 2) направление разворачивается, само крайнее значение устанавливается. Последовательность: Follow(0) → LockPos(1) → LockPosRot(2) → LockPos(1) → Follow(0) → ... Без разворота последовательность замкнулась бы в цикл 0→1→2→0 и пользователь не мог бы «отмотать» назад одной кнопкой.
>
> Сброс `_activeTarget = null` и `_followVelocity = Vector3.zero` при смене режима необходим: если переключение произошло в момент движения к цели, остаточная скорость и цель принадлежали Follow-режиму и в LockPosition неприменимы.

##### FaceCameraBelow — доворот ниже камеры

```csharp
private void FaceCameraBelow()
{
    var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
    var dir    = transform.position - target;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

> Вместо `_cameraTransform.position` вектор строится на `_faceBelowOffset` ниже камеры. Результат: панель слегка наклоняется «к пользователю» снизу, что при близком расстоянии делает текст читаемее. Базовый `SpatialPanel.FaceCamera` разворачивается к точной позиции камеры — угол резче. `UserPanel` переопределяет это поведение.

##### AdjustSize — аддитивное масштабирование

```csharp
private void AdjustSize(float delta)
{
    _sizeMultiplier = Mathf.Clamp(_sizeMultiplier + delta, _minSizeMultiplier, _maxSizeMultiplier);
    ApplyScale();
}

private void ApplyScale() => transform.localScale = _baseScale * _sizeMultiplier;
```

> `_baseScale * _sizeMultiplier` — умножение каждой компоненты вектора на скаляр. `_baseScale` снят после `DetachToWorld`, поэтому содержит реальный мировой масштаб панели при мультипликаторе 1.0. Диапазон `[0.6, 2.0]` — жёсткий `Mathf.Clamp`, предотвращающий накопление ошибок при многократных нажатиях.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Зачем `UserPanel` отвязывается от XR-рига через `SetParent(null)`?
> **О:** XR-rig — объект, который перемещается при локомоции и телепортации. Если панель остаётся в его иерархии, режим `LockPosition` не работает: мировые координаты объекта-потомка изменяются вместе с родителем, даже если `localPosition` заморожена. После отвязки панель находится в корне мировой иерархии и её мировые координаты контролируются исключительно скриптом.
>
> **В:** Почему `_baseScale` захватывается после `DetachToWorld`, а не до?
> **О:** `SetParent(worldPositionStays: true)` запекает прежний родительский масштаб в `localScale` объекта. До отвязки `localScale` может отражать масштаб в системе координат рига. После отвязки `localScale` — это и есть реальный мировой масштаб. Захват после гарантирует, что мультипликатор 1.0 соответствует авторскому размеру панели в мире.
>
> **В:** Почему при смене режима (`ModeChangedEvent`) панель скрывается, а не репозиционируется?
> **О:** Смена сцены инициирует рецентровку XR-рига. Если бы панель только двигалась к новой позиции прямо сейчас, она перемещалась бы на один кадр раньше, чем камера займёт новое место — результат непредсказуем. Скрытие + сброс состояния, а затем повторное открытие пользователем (или автоматически маршрутизатором) позволяет позиционироваться относительно уже установленной камеры.
>
> **В:** Как работает пинг-понг замка? Почему не обычный цикл 0→1→2→0?
> **О:** Цикличный перебор вернул бы пользователя из «полного замка» сразу в «следование», перепрыгивая промежуточное состояние «замок позиции». Пинг-понг 0→1→2→1→0 позволяет отмотать назад одной кнопкой без прыжка. `_lockDir` — направление перебора (+1 или -1), разворачивается при достижении крайних значений.
>
> **В:** Почему `_followVelocity` сбрасывается при достижении цели и при смене режима?
> **О:** `Vector3.SmoothDamp` накапливает скорость в `ref`-аргументе между кадрами. Если цель изменилась (новая перецентровка) или режим сменился, остаточная скорость из предыдущего движения уведёт объект мимо новой цели — он «перелетит» её. Сброс в `Vector3.zero` начинает движение заново без инерции.
>
> **В:** Чем `FaceCameraBelow` отличается от базового `FaceCamera` в `SpatialPanel`?
> **О:** `SpatialPanel.FaceCamera` строит вектор от позиции камеры. `UserPanel.FaceCameraBelow` строит вектор от точки `_faceBelowOffset` ниже камеры. Результат: панель слегка наклоняется «на пользователя» снизу. При коротком расстоянии это снижает угол наклона текста и делает его удобнее для чтения.

### Связи

[[SpatialPanel]] · [[PanelGrabHandle]] · [[PanelRegionRouter]] · [[RegionNavButton]] · [[ModeChangedEvent]] · [[ModeOrchestrator]] · [[Регионная модель UI]] · [[Прямой ввод вместо XRI]] · [[Внедрение зависимостей (VContainer)]] · [[Паттерн Publish-Subscribe]]
