---
note_type: script
subsystem: RigBuilder
listing: 3.55
---

> [!info] Назначение
> `BoneEditMode` — scene-scoped чистый класс, централизующий переход в режим редактирования костей и выход из него: включает интерактивность прокси-костей, передаёт/возвращает выбор, публикует `BonesVisibilityChangedEvent`. Листинг 3.55.

### Обзор

##### Роль и место
Чистый класс (не MonoBehaviour), scene-scope, получает зависимости через `[VContainer.Inject]` конструктор. Единственный владелец флага `ActiveRigId` — какой риг сейчас в режиме костей. `InspectorPanel` вызывает `SetActive`; `AnimatorPanel` и `InteractionMaskBinder` реагируют на `BonesVisibilityChangedEvent`. Рефакторинг: до выделения этого класса логика была дублирована в нескольких панелях.

##### Ключевые методы
- `SetActive(string rigNodeId, bool on)` — входит/выходит из режима для данного рига.
- `ClearActive()` — забывает активный риг без воздействия на геометрию (для смены сцен).
- `IsActive` — геттер, проверяет `!string.IsNullOrEmpty(ActiveRigId)`.

### Разбор кода

##### SetActive — порядок операций при входе и выходе
```csharp
public void SetActive(string rigNodeId, bool on)
{
    var rigNode = string.IsNullOrEmpty(rigNodeId) ? null : _graph?.GetNode(rigNodeId);
    var rig     = rigNode != null ? rigNode.GetComponentInChildren<ProxyRigRuntime>(true) : null;
    if (rig == null) return;

    rig.SetBonesInteractive(on);
    _bus?.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = on });

    if (on)
    {
        ActiveRigId = rigNodeId;
        _selection?.Select(null);
    }
    else
    {
        ActiveRigId = null;
        _selection?.Select(rigNodeId);
    }
}
```

> Guard `if (rig == null) return` — no-op при передаче id не-рига. Это позволяет `InspectorPanel` вызывать `SetActive` с `rigNodeId` без предварительной проверки типа ноды. Порядок при входе (`on = true`): сначала `SetBonesInteractive(true)` — физический слой костей становится интерактивным; затем `Publish(BonesVisibilityChangedEvent)` — маска переключается на `BoneProxies`; затем `Select(null)` — выбор рига снимается, чтобы начать «чисто» внутри рига. `Select(null)` идёт **после** публикации события: подписчики маски уже переключились, когда придёт `SelectionChangedEvent` от сброса выбора — нет момента, когда маска на `SceneObjects`, но выбор ещё есть.

> При выходе (`on = false`): `SetBonesInteractive(false)` → `Publish` → `Select(rigNodeId)`. Маска возвращается к `SceneObjects`; затем rig-нода выбирается — `SelectionChangedEvent` с id рига приходит при уже корректной маске.

> `_bus?.Publish(...)` — null-conditional, хотя `_bus` обязателен через конструктор. Защитная мера на случай теста без DI или патологического состояния scope.

##### ClearActive — «тихий сброс»
```csharp
public void ClearActive() => ActiveRigId = null;
```

> В отличие от `SetActive(id, false)`, `ClearActive` не вызывает `SetBonesInteractive`, не публикует событие и не трогает выбор. Используется, когда риг уже исчез (смена сцены): вызывать `SetBonesInteractive` на уничтоженном GO бессмысленно и опасно. `InspectorPanel.OnSceneContextChanged` вызывает `ClearActive`, чтобы тумблер показа костей не застрял в состоянии ON при следующей сцене.

##### IsActive — строковая проверка вместо bool-флага
```csharp
public bool IsActive => !string.IsNullOrEmpty(ActiveRigId);
```

> Хранить отдельный `bool _isActive` значит синхронизировать два поля. Здесь `ActiveRigId == null` — это и есть «не активен». `IsNullOrEmpty` перекрывает оба случая: `null` и `""` (пустая строка возможна при некорректном вызове). Одно поле — один источник истины.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему режим костей обслуживает ровно один риг одновременно?
> **О:** Одновременный bone-edit нескольких ригов потребовал бы отдельного состояния `ActiveRigId` на риг, агрегации масок (какой слой активен?) и сложной логики выбора. Для задач приложения (анимировать один скелет в VR) это избыточно. Единственный активный риг — намеренное упрощение.

> [!question]
> **В:** Зачем `Select(null)` при входе в режим костей? Разве нельзя оставить риг выбранным?
> **О:** Маска переключается на `BoneProxies` — `SceneObjects` становятся невидимы для луча. Если оставить rig-ноду выбранной, гизмо попытается следовать за рига-объектом, а инспектор покажет объект. Вход в режим костей — «сброс» до чистого состояния: пользователь начинает выбирать конкретные кости.

> [!question]
> **В:** Что произойдёт, если `SetActive` вызван с `rigNodeId = null`?
> **О:** `string.IsNullOrEmpty(rigNodeId)` → `rigNode = null` → `rig = null` → ранний `return`. Метод является no-op. Это безопасно: `InspectorPanel.OnShowBonesToggleChanged` устанавливает `rigNodeId` из нескольких источников, один из которых может оказаться null.

> [!question]
> **В:** Почему `BoneEditMode` — чистый класс, а не MonoBehaviour?
> **О:** Ему не нужен Update-цикл, он не отображает ничего в 3D, не нуждается в `SerializeField`. Чистый класс легче тестировать, его нельзя случайно добавить в сцену «лишним» компонентом. VContainer инжектирует зависимости через конструктор — не требуется `[Inject]` на MonoBehaviour с его порядком Awake.

### Связи
[[ProxyRigRuntime]] · [[SelectionManager]] · [[InspectorPanel]] · [[InteractionMaskBinder]] · [[BonesVisibilityChangedEvent]] · [[SelectionChangedEvent]] · [[Прокси-риг]] · [[Внедрение зависимостей (VContainer)]] · [[Паттерн Publish-Subscribe]]
