---
note_type: script
subsystem: VrInteraction
listing: 3.50
---

> [!info] Назначение
> `Selectable` — MonoBehaviour-компонент на спавнящихся объектах сцены, маркирующий их как выбираемые. Управляет обводкой QuickOutline: лениво добавляет `Outline`-компонент при первом выборе и переключает его цвет/ширину. Листинг 3.50.

### Обзор

##### Роль и место
MonoBehaviour, живёт на корневом GO каждой спавнящейся ноды. Не несёт логики выбора — она в `SelectionManager`. `Selectable` — исключительно визуальный контракт: получить команду `SetVisualState(...)` и показать/скрыть обводку. `NodeId` делегирует к `SceneNode`, который лежит на том же GO.

##### Ключевые методы
- `Awake()` — ищет `SceneNode` на том же объекте.
- `Construct(OutlineConfig)` — [Inject], получает конфигурацию обводки из DI.
- `SetVisualState(SelectionVisual state)` — переключает визуальное состояние.
- `EnsureOutline()` — ленивая инициализация `Outline`-компонента.

### Разбор кода

##### EnsureOutline — ленивое добавление компонента
```csharp
private void EnsureOutline()
{
    if (_outline == null)
    {
        // A bone proxy may already carry an Outline (Outline is [DisallowMultipleComponent]),
        // so reuse an existing one before adding.
        _outline = GetComponent<Outline>();
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
    }
    if (_outlineConfig != null)
        _outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
}
```

> `Outline` помечен `[DisallowMultipleComponent]`: добавление второго вызовет ошибку Unity. Прокси-кость может уже нести `Outline` от `ProxyRigRuntime`. Поэтому сначала `GetComponent<Outline>()`, и только если `null` — `AddComponent`. Материалы переустанавливаются при каждом вызове `EnsureOutline`, потому что `OutlineConfig` мог прийти позже (DI injection порядок не всегда детерминирован).

##### SetVisualState — приоритеты отрисовки
```csharp
case SelectionVisual.Selected:
    _outline.enabled        = true;
    _outline.OutlineColor   = _outlineConfig != null ? _outlineConfig.SelectColor : new Color(1f, 0.95f, 0.15f);
    _outline.OutlineWidth   = 6f;
    _outline.RenderPriority = 0; // base layer; bones (1) and gizmo (2) draw on top
    break;
```

> `RenderPriority = 0` — объект рисуется первым; кости (`priority = 1`) и гизмо (`priority = 2`) перекрывают его. Магическая константа `6f` — ширина обводки в пикселях (QuickOutline). Hardcoded fallback-цвет `new Color(1f, 0.95f, 0.15f)` (жёлтый) защищает от случая, когда `OutlineConfig` не заинжектировался (например, в тесте без DI).

##### Закомментированный Start — история бага
```csharp
// TODO(bug2): Reverted – this targeted the wrong root cause. The "stale blue rig on re-entry" was
// OutlinerPanel's _bonesActiveByRig surviving the scene swap (fixed there), not a pre-existing mesh
// Outline. Kept commented in case a residual cosmetic 3D outline shows up and needs a real fix.
// private void Start() { var existing = GetComponent<Outline>(); if (existing != null) existing.enabled = false; }
```

> Попытка фикса «синего контура рига при повторном входе в сцену» была откатана: корень оказался в `OutlinerPanel._bonesActiveByRig`, переживавшем смену сцены. Закомментированный код оставлен как документация истории.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему `Outline` добавляется лениво, а не в `Awake`?
> **О:** Большинство объектов никогда не выбираются за сессию. Добавление `Outline` при спавне на каждом объекте тратило бы память и увеличивало число `MeshRenderer`-ов (QuickOutline добавляет рендерер). Ленивый подход — добавлять только при первом выборе.

> [!question]
> **В:** Что такое `RenderPriority` у QuickOutline и зачем объект рисуется с приоритетом 0?
> **О:** QuickOutline использует `RenderPriority` для сортировки pass-ов обводки. Объект (0) рисуется первым, кости (1) и гизмо (2) поверх. Это нужно, чтобы контур кости или гизмо не перекрывался контуром объекта — визуально «вышестоящие» элементы всегда читаются.

> [!question]
> **В:** Зачем `[Inject]` на `Construct`, а не конструктор — ведь это MonoBehaviour?
> **О:** MonoBehaviour нельзя создать через `new` — Unity сама управляет их жизненным циклом. VContainer инжектирует зависимости в MonoBehaviour через метод, помеченный `[Inject]`, после спавна объекта.

### Связи
[[SelectionVisualSync]] · [[SelectionManager]] · [[SelectionChangedEvent]] · [[SceneNode]] · [[ProxyRigRuntime]] · [[Внедрение зависимостей (VContainer)]]
