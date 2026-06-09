---
aliases:
  - PanelType
note_type: script
subsystem: SpatialUi
listings: [Л3.20, БЛ5]
---

> [!info] Назначение
> `SpatialPanel` — базовый класс для всех пространственных панелей приложения. Реализует три режима крепления (`PanelType`), биллборд и ленивое слежение за камерой (`_lazyFollow`). Относится к подсистеме `SpatialUi`. Листинг 3.20 (фрагмент) и Приложение Б, Листинг Б.5 (полный).

### Обзор

##### Роль и место

`SpatialPanel` — корень иерархии UI: `UserPanel` наследует его напрямую. Каждая панель-объект сцены требует `Canvas` (`[RequireComponent(typeof(Canvas))]`). Класс не регистрируется в VContainer — он `MonoBehaviour`, его потомки подхватываются инфраструктурой VContainer через `Inject`-методы.

Три режима `PanelType`:
- `BodyLocked` — панель каждый кадр притягивается перед камерой; используется в приложении фактически везде.
- `WorldFixed` — панель стоит в мировых координатах, `FollowCamera` не вызывается.
- `Free` — позиция под внешним управлением (перетаскивание).

##### Ключевые методы

| Метод | Суть |
|---|---|
| `Init(PanelId, Transform)` | Внешняя инициализация: назначает id и камеру до `Awake` |
| `FollowCamera()` | Позиционирование: жёсткое или ленивое |
| `FaceCamera()` | Биллборд: разворот лицом к камере |
| `LateUpdate()` | Точка сборки: вызывает оба метода в правильном порядке |

### Разбор кода

##### LateUpdate — точка сборки

```csharp
protected virtual void LateUpdate()
{
    if (_cameraTransform == null) return;

    if (_panelType == PanelType.BodyLocked)
        FollowCamera();

    if (_billboard)
        FaceCamera();
}
```

> `LateUpdate`, а не `Update` — гарантирует, что XR-камера уже зафиксировала свою позицию за этот кадр (XRI-rig обновляется в `Update`). Вызов `FaceCamera` **после** `FollowCamera` важен: сначала меняем позицию, потом разворачиваем по новому вектору. Если поменять порядок, биллборд будет смотреть на прошлокадровую позицию относительно новой точки.

##### FollowCamera — ленивое следование

```csharp
protected virtual void FollowCamera()
{
    var cam      = _cameraTransform;
    var idealPos = cam.position + cam.rotation * _defaultOffset;

    if (!_lazyFollow)
    {
        transform.position = idealPos;
        return;
    }

    if (!_lazyInit)
    {
        _lazyTarget        = idealPos;
        transform.position = idealPos;
        _lazyInit          = true;
        return;
    }

    var dir = transform.position - cam.position;
    if (dir.sqrMagnitude > 0.001f && Vector3.Angle(cam.forward, dir.normalized) > _lazyAngle)
        _lazyTarget = idealPos;

    transform.position = Vector3.Lerp(transform.position, _lazyTarget, Time.deltaTime * _lazySpeed);
}
```

> `cam.rotation * _defaultOffset` — вращение вектора смещения на кватернион камеры переводит смещение `(0, 0, 1.2)` из локального пространства камеры в мировое. Это чище, чем `cam.TransformPoint`, потому что смещение — направление, а не точка (нет трансляции).
>
> `_lazyInit` — «первый кадр». Без него панель прыгает из `(0,0,0)` к позиции перед пользователем. При первом вызове панель мгновенно телепортируется и выставляет `_lazyInit = true`.
>
> `dir.sqrMagnitude > 0.001f` — защита от вырожденного вектора (панель точно в позиции камеры). `Vector3.Angle` с нулевым вектором вернул бы `NaN`; `sqrMagnitude` дешевле `magnitude` (нет `sqrt`).
>
> `Vector3.Angle(cam.forward, dir.normalized) > _lazyAngle` — измеряет угол между направлением взгляда камеры и вектором «камера → панель». Если панель ушла дальше порога (`_lazyAngle = 45°`), назначается новая цель. Затем `Lerp` с коэффициентом `Time.deltaTime * _lazySpeed` — это **не** экспоненциальное сглаживание (для него нужен `1 - exp(-t*k)`), а линейный шаг; при малом `_lazySpeed` движение плавное, при большом — жёсткое.

##### FaceCamera — биллборд

```csharp
protected void FaceCamera()
{
    var dir = transform.position - _cameraTransform.position;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

> `Quaternion.LookRotation(dir)` ориентирует объект так, чтобы его ось `+Z` (вперёд) смотрела **от камеры к панели**. Результат: лицевая сторона Canvas повёрнута к наблюдателю. Без проверки `sqrMagnitude` при нулевом `dir` `LookRotation` вернул бы identity или NaN в зависимости от версии Unity.

##### Awake — резервная камера

```csharp
protected virtual void Awake()
{
    if (_cameraTransform == null)
        _cameraTransform = Camera.main?.transform;
}
```

> `Camera.main` — по тегу `MainCamera`. Панель работает без явного `Init()`-вызова, но в продакшн-сценах `Init` вызывается из `UserPanel.Start` через цепочку DI. Резервный путь — страховка для тестовых сцен.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `FollowCamera` вызывается в `LateUpdate`, а не в `Update`?
> **О:** XR-камера обновляет свою позицию в `Update` вместе с головным трекингом. `LateUpdate` выполняется после всех `Update`, поэтому панель читает уже финальную позицию камеры за кадр и не запаздывает на кадр.
>
> **В:** Что делает `cam.rotation * _defaultOffset`? Почему не `cam.TransformPoint`?
> **О:** Умножение кватерниона на вектор вращает вектор в мировое пространство без добавления трансляции. `TransformPoint` добавил бы ещё и позицию камеры дважды. Здесь смещение — это направление, поэтому нужно только вращение.
>
> **В:** Зачем нужен `_lazyInit`?
> **О:** При первом вызове панель может находиться в любой точке мира (или в `(0,0,0)`). Без `_lazyInit` `Lerp` начал бы плавно тянуть её из этой случайной позиции к целевой — пользователь увидел бы «летящую» панель. `_lazyInit` гарантирует мгновенный телепорт при первом вызове.
>
> **В:** Почему сравнение через `sqrMagnitude > 0.001f`, а не через `magnitude > 0.032f`?
> **О:** `sqrMagnitude` избегает вычисления квадратного корня (`magnitude`), что дешевле. Пороговый порядок тот же: `0.001 ≈ 0.032²`. Главная цель — защита от нулевого вектора перед `Vector3.Angle` и `LookRotation`.
>
> **В:** Что происходит при `PanelType.WorldFixed` или `Free`?
> **О:** Ветка `if (_panelType == PanelType.BodyLocked)` не выполняется, `FollowCamera` не вызывается. Биллборд при этом всё равно работает, если `_billboard = true`, — панель разворачивается к камере, но остаётся на месте.

### Связи

[[PanelType]] · [[UserPanel]] · [[PanelRegionRouter]] · [[PanelGrabHandle]] · [[Регионная модель UI]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
