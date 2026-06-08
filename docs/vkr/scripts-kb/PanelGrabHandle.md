---
note_type: script
subsystem: SpatialUi
listings: [БЛ8]
---

> [!info] Назначение
> `PanelGrabHandle` — компонент захвата и перетаскивания `UserPanel` за ручку. Наследует `XRBaseInteractable`, но отключает стандартный select-поток XRI (`IsSelectableBy => false`) и читает ввод напрямую — по той же схеме, что и `XRPromeonInteractable` для объектов сцены. Положение панели переносится без скачка через смещение в локальных координатах точки крепления интерактора. Листинг Б.8.

### Обзор

##### Роль и место

Компонент размещается на отдельном GameObject ручки (нижняя полоска `UserPanel`). Ховер — стандартный XRI (лучевой указатель), select-поток отключён. Захват обнаруживается прямым чтением `ni.selectInput.ReadWasPerformedThisFrame()`.

Два публичных статических метода `CaptureOffset`/`ApplyOffset` инкапсулируют математику смещения — отделены от логики захвата и теоретически тестируемы без Unity.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `IsSelectableBy` | Всегда `false` — XRI не берёт объект в Select |
| `ProcessInteractable` | Основной тик: читает ввод, управляет состоянием |
| `CaptureOffset / ApplyOffset` | Математика смещения в attach-local пространстве |
| `EndGrab` | Сброс состояния, уведомление панели |
| `IsPrimaryFor` | Проверка, что данный интерактор — ближайший к ручке (не перекрыт другим) |

### Разбор кода

##### Awake — перехват коллайдеров

```csharp
protected override void Awake()
{
    base.Awake();
    colliders.Clear();
    foreach (var c in GetComponents<Collider>())
        if (c != null && !colliders.Contains(c))
            colliders.Add(c);

    if (colliders.Count == 0)
        Debug.LogError($"[PanelGrabHandle] No Collider on '{name}'. ...", this);

    ApplyColor(_normalColor);
}
```

> `base.Awake()` вызывает `XRBaseInteractable.Awake`, который через `GetComponentsInChildren<Collider>(true)` собирает коллайдеры со всего поддерева GameObject — включая дочерние объекты панели. Это нежелательно: ручка должна реагировать только на свой `BoxCollider`. Поэтому сразу после `base.Awake()` список `colliders` очищается и заполняется только компонентами текущего объекта (`GetComponents`, не `GetComponentsInChildren`).

##### IsSelectableBy — отключение XRI select

```csharp
public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;
```

> `XRBaseInteractable` использует `IsSelectableBy` для проверки, можно ли интерактору захватить объект. Возврат `false` полностью отключает XRI select-flow: объект никогда не попадёт в `Selected`-состояние, `OnSelectEntered`/`OnSelectExited` не вызовутся. Это освобождает `selectInput` для прямого чтения без конфликта с XRI.

##### ProcessInteractable — машина состояний

```csharp
public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
{
    base.ProcessInteractable(phase);
    if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
    if (_panel == null) return;

    if (_state == State.Grabbing && (_locked == null || !_locked.isActiveAndEnabled))
    { EndGrab(); return; }

    switch (_state)
    {
        case State.Idle:
            UpdateLastHovering();
            var ni = CurrentHoverer();
            if (ni == null) break;
            if (!IsPrimaryFor(ni)) break;
            if (ni.selectInput.ReadWasPerformedThisFrame())
            {
                _locked     = ni;
                var attach  = _locked.GetAttachTransform(this);
                _grabOffset = CaptureOffset(attach, _panel.transform.position);
                _panel.SetDragging(true);
                _state = State.Grabbing;
                ApplyColor(_grabColor);
            }
            break;

        case State.Grabbing:
            if (_locked.selectInput.ReadWasCompletedThisFrame())
            { EndGrab(); break; }
            var a = _locked.GetAttachTransform(this);
            _panel.MoveTo(ApplyOffset(a, _grabOffset));
            break;
    }
}
```

> `phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic` — XRI вызывает `ProcessInteractable` несколько раз за кадр с разными фазами (Dynamic, Fixed, Late). Ввод читается только в `Dynamic` (соответствует `Update`), чтобы избежать двойного чтения.
>
> Защита `_locked == null || !_locked.isActiveAndEnabled` — страховка от «зависшего» захвата: если контроллер был отключён или уничтожен в середине захвата, `_locked` становится недоступным, но `_state` остался бы `Grabbing` навсегда. Проверка обнаруживает это и вызывает `EndGrab`.
>
> `IsPrimaryFor(ni)` — ограничивает ввод только интерактором, чей луч реально попал в коллайдер ручки. Без этого любой интерактор, наводящий луч на любую часть панели (мимо ручки), мог бы инициировать захват.

##### CaptureOffset / ApplyOffset — математика без скачка

```csharp
public static Vector3 CaptureOffset(Transform attach, Vector3 worldPos)
    => attach.InverseTransformPoint(worldPos);

public static Vector3 ApplyOffset(Transform attach, Vector3 localOffset)
    => attach.TransformPoint(localOffset);
```

> `InverseTransformPoint(worldPos)` переводит мировую позицию панели в локальное пространство `attach`-трансформа интерактора. При захвате запоминается смещение от точки крепления до центра панели в этой системе координат. При каждом кадре захвата `TransformPoint(localOffset)` переводит смещение обратно в мировое пространство уже относительно новой позиции `attach`. Результат: центр панели всегда движется так, чтобы разница между ним и `attach` оставалась той же, что в момент захвата — панель «держится» за точку хвата без скачка.
>
> Комментарий в коде оговаривает предположение об `attach`: `unit scale` (XRI attach-точки не масштабированы), что делает `InverseTransformPoint` математически точным. Если attach имел бы произвольный масштаб, цикл `InverseTransform→Transform` не был бы точным обратным.

##### IsPrimaryFor — ближайший луч

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

> Два пути: лучевой (ray interactor найден) и ближний (физическое взаимодействие). В лучевом пути `TryGetCurrent3DRaycastHit` возвращает `RaycastHit` последнего хита луча. Если луч бьёт в другой объект (не ручку), `colliders.Contains(hit.collider)` вернёт `false` — ввод игнорируется. Это предотвращает захват ручки, когда луч направлен через ручку на панель за ней.
>
> `includeInactive: true` в `GetComponentInChildren` — луч-интерактор может быть временно выключен при переключении режима интерактора; нужно найти его независимо от активности.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `IsSelectableBy` возвращает `false`? Как тогда работает захват?
> **О:** Стандартный select-поток XRI нельзя использовать одновременно для двух целей — он уже задействован для захвата объектов сцены (гизмо, модели). `false` полностью отключает XRI-select на ручке. Захват реализован вручную: `ProcessInteractable` читает `ni.selectInput.ReadWasPerformedThisFrame()` напрямую, без участия XRI.
>
> **В:** Зачем смещение хранится в локальных координатах attach, а не в мировых?
> **О:** Контроллер движется в реальном времени. Мировое смещение от фиксированной мировой точки захвата к панели изменялось бы каждый кадр — панель бы «прилипла» к мировой точке, а не к руке. Локальное смещение в attach-пространстве остаётся неизменным, а `TransformPoint` переводит его обратно в мир относительно новой позиции руки каждый кадр — панель движется вместе с рукой.
>
> **В:** Зачем в `Awake` очищается и перезаполняется `colliders`?
> **О:** `XRBaseInteractable.Awake` собирает коллайдеры через `GetComponentsInChildren` — со всего поддерева. Для ручки это неверно: у панели есть свои коллайдеры (кнопки, элементы UI), которые не должны активировать захват ручки. После `base.Awake()` список очищается и заполняется только коллайдерами текущего GameObject.
>
> **В:** Что такое `IsPrimaryFor` и почему он нужен?
> **О:** В VR одновременно может быть два интерактора (левый и правый контроллер). Если оба наводят луч на область ручки, каждый получит ховер и будет читать ввод. `IsPrimaryFor` разрешает ввод только тому интерактору, чей луч реально бьёт в коллайдер ручки — ближайшее попадание. Второй луч, попавший в другую часть панели, игнорируется.

### Связи

[[UserPanel]] · [[SpatialPanel]] · [[XRPromeonInteractable]] · [[Прямой ввод вместо XRI]] · [[Внедрение зависимостей (VContainer)]] · [[Регионная модель UI]]
