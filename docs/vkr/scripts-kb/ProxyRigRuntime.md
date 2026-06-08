---
note_type: script
subsystem: RigBuilder
listing: "3.46, 3.47, 3.56, Б.20"
---

> [!info] Назначение
> `ProxyRigRuntime` — `MonoBehaviour`, координатор прокси-рига на скелетной модели. Хранит список прокси-GO, словарь `boneName → Transform`, селекторные коллайдеры. Управляет режимом костей (`SetBonesInteractive`), снимает/применяет позы (`CapturePoses`/`ApplyPoses`), реагирует на `SelectionChangedEvent` — подсвечивает выбранную кость. Листинги 3.46, 3.47, 3.56, Б.20.

### Обзор

##### Роль и место

Добавляется на корневой GO модели фабрикой `RigEntityFabricator.BuildProxyRig`. Внедрение зависимостей — через `[Inject] Construct` (после `AddNode` + `InjectGameObject` в `AssetSpawner`). Подписывается на `SelectionChangedEvent` в момент инжекта, а не в `Start` — компонент активен с момента создания.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Bind(...)` | принимает данные от фабрики, очищает старые коллекции, вызывает `SetBonesInteractive(false)` |
| `CapturePoses()` | `_boneProxies` → `List<BonePose>` (для сохранения в scene.json, schema v3) |
| `ApplyPoses(poses)` | `List<BonePose>` → `_boneProxies` (при загрузке сцены) |
| `SetBonesInteractive(enabled)` | вкл/выкл визуал и коллайдеры прокси; инверсия селекторных коллайдеров |
| `RegisterSelectorColliders()` | регистрирует box-коллайдеры с `XRPromeonInteractable` после `Apply` |
| `ApplyBoneSelection(selectedId)` | меняет outline-цвет и материал каждого прокси-GO |

---

### Разбор кода

##### Construct — идемпотентная переподписка

```csharp
[Inject]
public void Construct(EventBus bus, OutlineConfig outlineConfig, ProxyRigConfig proxyConfig)
{
    _outlineConfig = outlineConfig;
    _proxyConfig   = proxyConfig;
    if (_eventBus == bus) return;
    if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    _eventBus = bus;
    if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
}
```

> `if (_eventBus == bus) return` — защита от повторного вызова `InjectGameObject` (теоретически возможного при перезагрузке сцены с тем же объектом). Без гарда произошло бы двойное `Subscribe` → двойная реакция на каждое событие. Сначала отписка от старого `_eventBus`, потом подписка на новый — правильный teardown.

##### CapturePoses / ApplyPoses — источник авторитетных данных

```csharp
public List<BonePose> CapturePoses()
{
    var poses = new List<BonePose>(_boneProxies.Count);
    foreach (var kv in _boneProxies)
    {
        var t = kv.Value;
        if (t == null) continue;
        poses.Add(new BonePose
        {
            BoneName      = kv.Key,
            LocalPosition = t.localPosition,
            LocalRotation = t.localRotation,
            LocalScale    = t.localScale,
        });
    }
    return poses;
}
```

> Авторитетный источник позы — `localPosition/Rotation/Scale` прокси-`Transform`. Именно прокси двигает пользователь гизмо; `BoneFollower` копирует значения на реальную кость в `LateUpdate`. Сохранять позы реальных костей было бы ошибкой: в момент `CapturePoses` `BoneFollower.LateUpdate` мог ещё не выполниться — значения могут не совпадать с прокси.

```csharp
public void ApplyPoses(IReadOnlyList<BonePose> poses)
{
    if (poses == null) return;
    foreach (var p in poses)
    {
        if (p == null || string.IsNullOrEmpty(p.BoneName)) continue;
        if (!_boneProxies.TryGetValue(p.BoneName, out var t) || t == null) continue;
        t.localPosition = p.LocalPosition;
        t.localRotation = p.LocalRotation;
        t.localScale    = p.LocalScale;
    }
}
```

> `TryGetValue` с `continue` — неизвестные имена костей тихо пропускаются. Это позволяет загрузить сцену, если скелет модели изменился (переименовали кость): известные кости восстановятся, переименованная получит rest-позу. `BoneFollower.LateUpdate` применит позу на реальную кость в следующем кадре.

##### SetBonesInteractive — стекинг outline-материалов

```csharp
if (enabled && mr != null)
{
    var current = mr.sharedMaterials;
    var cleaned = current.Where(m => m == null ||
        (!m.name.StartsWith("OutlineMask") && !m.name.StartsWith("OutlineFill"))).ToArray();
    if (cleaned.Length != current.Length)
        mr.materials = cleaned;
}
```

> `QuickOutline.OnEnable` добавляет два материала (`OutlineMask`, `OutlineFill`) в массив `sharedMaterials` **без дедупликации**. При повторном включении (`enabled = true`) они накапливаются: второй Enable даст 4 outline-материала, третий — 6. Стенсил-записи конфликтуют → видимый баг: «outline не реагирует на клик» (исходный комментарий в коде). Очистка через LINQ фильтрует старые копии **до** `outline.enabled = true`.
>
> `mr.materials` (не `sharedMaterials`) при очистке — создаёт per-instance копию. Это намеренно: мы изменяем конкретный экземпляр рендерера, а не shared asset. В последующей строке `outline.enabled = true` снова добавит два материала к этой per-instance копии — итого ровно 3 (base + mask + fill).

##### SetBonesInteractive — инверсия коллайдеров

```csharp
foreach (var sc in _selectorColliders)
    if (sc != null) sc.enabled = !enabled;

if (enabled) ApplyBoneSelection(null);
```

> Два режима взаимоисключающие: при `enabled=true` (режим костей) селекторные коллайдеры рига выключены — луч попадёт в прокси-кость. При `enabled=false` (режим модели) — прокси-коллайдеры выключены, работают только box-селекторы рига, выбирающие модель целиком. `ApplyBoneSelection(null)` при входе в режим костей сбрасывает выделение (ни одна кость не выбрана).

##### ApplyBoneMaterial — submesh 0, sharedMaterials

```csharp
private void ApplyBoneMaterial(GameObject go, bool isSelected)
{
    if (_proxyConfig == null || _proxyConfig.BoneMaterial == null) return;
    var mr = go.GetComponent<MeshRenderer>();
    if (mr == null) return;

    var target = isSelected && _proxyConfig.BoneSelectedMaterial != null
        ? _proxyConfig.BoneSelectedMaterial
        : _proxyConfig.BoneMaterial;

    var mats = mr.sharedMaterials;
    if (mats.Length == 0 || mats[0] == target) return;
    mats[0] = target;
    mr.sharedMaterials = mats;
}
```

> `sharedMaterials` возвращает **копию** массива. Изменение `mats[0]` не трогает оригинал — нужно присвоить обратно `mr.sharedMaterials = mats`. `if (mats[0] == target) return` — дешёвая проверка перед присваиванием: сравниваются ссылки на shared материалы, не глубокое равенство. Это предотвращает лишний dirty-вызов Unity на каждое `SelectionChanged` когда материал уже правильный.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `CapturePoses` читает трансформации прокси, а не реальных костей?
> **О:** Прокси — авторитетный источник. Пользователь двигает прокси; `BoneFollower` копирует результат в `LateUpdate`. В момент `CapturePoses` (например, при сохранении сцены) `BoneFollower` мог ещё не запуститься в текущем кадре — реальная кость может отставать на кадр. Сохранение значений прокси устраняет эту гонку.

> [!question]
> **В:** Что такое «schema v3» в контексте `BonePoses`?
> **О:** `scene.json` версионируется полем `schemaVersion`. Schema v3 добавила `NodeData.BonePoses` — список `BonePose` в записи каждой скелетной ноды. `SceneSerializer.Deserialize` содержит inline-миграцию: при чтении v1/v2 `BonePoses` будет `null`, что `ApplyPoses` обработает тихо (все кости в rest-позе).

> [!question]
> **В:** Зачем `RegisterSelectorColliders` отдельный метод, а не часть `Bind`?
> **О:** В момент `Bind` (вызов из фабрики) `XRPromeonInteractable` ещё не существует — он создаётся в `InteractionCapability.Apply`, которое вызывается позже в `AssetEntityBuilderRegistry.RestoreAsync`. `RegisterSelectorColliders` вызывается уже после `Apply` — последовательность: `RestoreAsync` → `Apply` → `RegisterSelectorColliders`.

> [!question]
> **В:** Почему накопление outline-материалов приводит именно к багу «outline не реагирует на клик»?
> **О:** `QuickOutline` использует stencil-буфер: `OutlineMask` пишет маску, `OutlineFill` рисует по маске. При двойном стеке два прохода `OutlineMask` записывают разные stencil-значения; результирующий тест в `OutlineFill` ожидает конкретное значение и «не видит» объект. Визуально outline пропадает или становится артефактом.

> [!question]
> **В:** Как `SetBonesInteractive` реализует переключение между режимом модели и режимом костей?
> **О:** В режиме модели: прокси-GO неактивны, селекторные box-коллайдеры активны → луч попадает в selector-box → выбирается корневой GO модели. В режиме костей: прокси-GO активны с коллайдерами, selector-boxes деактивированы → луч попадает в прокси-кость → выбирается `SceneNode` кости.

---

### Связи

[[BoneFollower]] · [[RigEntityFabricator]] · [[AssetSpawner]] · [[AssetEntityBuilderRegistry]] · [[Структуры скелета]] · [[Прокси-риг]] · [[Паттерн Publish-Subscribe]] · [[SelectionChangedEvent]] · [[Внедрение зависимостей (VContainer)]]
