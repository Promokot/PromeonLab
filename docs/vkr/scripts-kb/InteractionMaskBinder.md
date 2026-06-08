---
note_type: script
subsystem: VrInteraction
listing: 3.53, Б.27
---

> [!info] Назначение
> `InteractionMaskBinder` — MonoBehaviour на корне XR-рига, переключающий физические маски кастеров `NearFarInteractor` по активному контексту взаимодействия: SceneObjects / BoneProxies / GizmoHandles. Слой UI присутствует в маске всегда. Листинги 3.53 и Б.27.

### Обзор

##### Роль и место
Постоянный компонент (пережишает смену сцен, живёт на XR-риге, который является `DontDestroyOnLoad`). Слушает **root** EventBus (не сценовый) — поэтому получает `ModeChangedEvent` и может сбрасывать контекст между сценами. Три флага (`_bonesMode`, `_panelOpen`, `_hasSelection`) → один контекст → одна маска.

##### Ключевые методы
- `Construct(EventBus)` — [Inject], подписка на 5 событий.
- `Awake()` — autodiscovery кастеров из иерархии рига.
- `Apply()` — пересчёт и установка маски.
- `OnModeChanged(...)` — сброс всех флагов при смене режима.

### Разбор кода

##### Awake — autodiscovery кастеров
```csharp
private void Awake()
{
    foreach (var ix in GetComponentsInChildren<NearFarInteractor>(includeInactive: true))
    {
        var near = ix.GetComponent<SphereInteractionCaster>();
        var far  = ix.GetComponent<CurveInteractionCaster>();
        if (near != null) _nearCasters.Add(near);
        if (far  != null) _farCasters.Add(far);
    }
    if (_nearCasters.Count == 0 && _farCasters.Count == 0)
        Debug.LogError("InteractionMaskBinder: found no NearFarInteractor casters in children – " +
                       "place this component on the XR rig root.");
    _uiMask = LayerMask.GetMask("UI");
}
```

> `GetComponentsInChildren` с `includeInactive: true` — обе руки могут быть деактивированы при инициализации рига; без флага половина кастеров не обнаружится. Компонент размещён на корне рига намеренно — «инспекторные ссылки» заменены автообнаружением, устойчивым к переименованию дочерних объектов. `_uiMask = LayerMask.GetMask("UI")` кешируется здесь: `LayerMask.GetMask` делает строковый поиск — лучше один раз при Awake.

##### Apply — приоритет контекстов и всегда-UI
```csharp
private void Apply()
{
    InteractionLayer context =
        (_panelOpen && _hasSelection) ? InteractionLayer.GizmoHandles
        : _bonesMode                  ? InteractionLayer.BoneProxies
        :                               InteractionLayer.SceneObjects;

    int unity = InteractionLayers.UnityLayer(context);
    if (unity < 0) return;
    int mask = (1 << unity) | _uiMask; // context layer + always-on UI

    foreach (var c in _nearCasters) if (c != null) c.physicsLayerMask = mask;
    foreach (var c in _farCasters)  if (c != null) c.raycastMask      = mask;
}
```

> Приоритеты сверху вниз: GizmoHandles > BoneProxies > SceneObjects. Гизмо-режим модальный — когда открыта панель и есть выбор, луч видит только хэндлы (цель позади не перехватит клик). `(1 << unity) | _uiMask` — UI-слой присутствует всегда, иначе кнопки панелей «умрут»: `CurveInteractionCaster` использует `raycastMask` и для 3D физики, и для `TrackedDeviceGraphicRaycaster` (uGUI). `if (unity < 0) return` — защита от некорректного enum-значения, для которого `InteractionLayers.UnityLayer` не нашёл слой.

##### OnModeChanged — сброс при смене сцены
```csharp
private void OnModeChanged(ModeChangedEvent _)
{
    _bonesMode    = false;
    _panelOpen    = false;
    _hasSelection = false;
    Apply();
}
```

> Ключевой инсайт из комментария кода: scene-scoped издатели (`SelectionManager`, `GizmoToolsPanel`, `BoneEditMode`) не повторяют своё «выключенное» состояние для новой сцены. Без этого сброса маска оставалась бы на `GizmoHandles` из предыдущей сессии — луч не видел бы `SceneObjects`, и ничего нельзя было бы выбрать. `ModeChangedEvent` приходит после загрузки новой сцены — к этому моменту новый scope уже жив.

##### _uiMask — почему не константа
> `LayerMask.GetMask("UI")` читает конфигурацию слоёв проекта по имени. Если проект переназначит слой «UI» на другой номер, захардкоженная константа `1 << 5` сломалась бы тихо. Кеш в `_uiMask` даёт правильный номер слоя при любой конфигурации.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему UI-слой всегда присутствует в маске, а не добавляется отдельным кастером?
> **О:** `CurveInteractionCaster` (дальний луч) использует `raycastMask` и для физических хитов, и для uGUI. `TrackedDeviceGraphicRaycaster` читает ту же маску. Если убрать UI из маски — кнопки не отреагируют на луч вне зависимости от контекста.

> [!question]
> **В:** Почему контекст GizmoHandles требует и `_panelOpen`, и `_hasSelection`?
> **О:** Панель инструментов гизмо может быть открыта без выбранного объекта (пользователь открыл панель, потом сбросил выбор). В этом случае гизмо не отображается — переключение маски на GizmoHandles бессмысленно и заблокировало бы выбор объектов.

> [!question]
> **В:** Зачем `foreach ... if (c != null)` при итерации по спискам кастеров?
> **О:** Компоненты в списках добавляются через `GetComponentsInChildren`; теоретически один из них может быть уничтожен позже (хотя XR-риг не пересоздаётся). Guard `if (c != null)` — стандартная защитная мера от NRE при мутации иерархии.

> [!question]
> **В:** Что произойдёт, если `InteractionLayers.UnityLayer(context)` вернёт -1?
> **О:** Ранний `return` — маска не меняется. Это означает, что слой, соответствующий контексту, не зарегистрирован в настройках проекта. Поведение деградирует (маска не обновилась), но NRE/исключения не будет.

### Связи
[[SelectionManager]] · [[GizmoDriver]] · [[BoneEditMode]] · [[XRPromeonInteractable]] · [[SelectionChangedEvent]] · [[BonesVisibilityChangedEvent]] · [[ModeChangedEvent]] · [[Прямой ввод вместо XRI]] · [[Внедрение зависимостей (VContainer)]]
