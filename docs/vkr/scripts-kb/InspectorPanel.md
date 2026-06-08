---
note_type: script
subsystem: SpatialUi
listing: 3.54, Б.28
---

> [!info] Назначение
> `InspectorPanel` — постоянная VR-панель (root-scope MonoBehaviour), отображающая свойства выбранной сущности: объект сцены или кость прокси-рига. Различает три состояния через префикс NodeId, поддерживает живое редактирование имени, удаление объекта, переключение режима костей. Листинги 3.54 и Б.28.

### Обзор

##### Роль и место
MonoBehaviour в root-scope (`UserPanel`). Постоянна — переживает смену сцен. Слушает `SelectionChangedEvent` и `SceneContextChangedEvent`. Читает состояние через `SceneContext` (_ctx), а не напрямую из scene-scope сервисов — поэтому безопасна при отсутствии сцены (`_ctx.HasScene`).

##### Ключевые методы
- `Refresh()` — пересчёт состояния по `SelectedNodeId`.
- `BindSingle(SceneNode)` / `BindBone(string)` — заполнение полей.
- `OnNameLiveEdit(string)` / `OnNameCommit(string)` — редактирование имени.
- `OnDeleteClicked()` — удаление ноды.
- `OnShowBonesToggleChanged(bool)` — делегирование в `BoneEditMode`.

### Разбор кода

##### Refresh — определение состояния по префиксу NodeId
```csharp
private void Refresh()
{
    if (!_ctx.HasScene) return;

    var activeId = _ctx.Selection.SelectedNodeId;
    var state    = string.IsNullOrEmpty(activeId)            ? InspectorState.Empty
                 : activeId.StartsWith("bone:")              ? InspectorState.Bone
                 :                                             InspectorState.Single;

    if (_emptyState != null) _emptyState.SetActive(state == InspectorState.Empty);
    if (_content    != null) _content   .SetActive(state == InspectorState.Single);
    if (_boneState  != null) _boneState .SetActive(state == InspectorState.Bone);
    // ...
}
```

> Формат `bone:{rigNodeId}:{boneName}` — соглашение, установленное `ProxyRigRuntime` при построении прокси-рига. `StartsWith("bone:")` — единственный способ различить кость от объекта без обращения к графу. `_ctx.HasScene` — guard на отсутствие сцены: панель постоянная, после выгрузки сцены `SceneContext` очищается (`SceneContextBinder` публикует `SceneContextChangedEvent` с `HasScene = false`).

##### BindBone — парсинг NodeId
```csharp
private void BindBone(string boneNodeId)
{
    var parts     = boneNodeId.Split(':');
    var boneName  = parts.Length >= 3 ? parts[2] : boneNodeId;
    _boneRigId    = parts.Length >= 2 ? parts[1] : "";
    // ...
    _boneTransform = _ctx.Graph.GetNode(boneNodeId)?.transform;
    // ...
}
```

> `Split(':')` разбивает `"bone:rig-abc123:Hips"` на `["bone", "rig-abc123", "Hips"]`. Fallback `boneNodeId` при `parts.Length < 3` и `""` при `< 2` — защита от некорректного формата (никогда не должно произойти в нормальной работе). `_ctx.Graph.GetNode(boneNodeId)` ищет ноду с полным bone-идентификатором — прокси-кость зарегистрирована в SceneGraph под этим же ключом.

##### OnNameLiveEdit vs OnNameCommit — два уровня персистентности
```csharp
private void OnNameLiveEdit(string newName)
{
    if (_bound == null) return;
    if (string.IsNullOrWhiteSpace(newName)) return;
    var trimmed = newName.Trim();
    _bound.SetDisplayName(trimmed);
    _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = trimmed });
}

private void OnNameCommit(string newName)
{
    // ...
    _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = finalName });
    _bus?.Publish(new SceneModifiedEvent());
}
```

> `OnNameLiveEdit` (привязан к `onValueChanged`) — немедленно меняет отображаемое имя ноды и рассылает `NodeRenamedEvent` для обозревателя. `SceneModifiedEvent` НЕ публикуется — нет смысла дебаунсить автосохранение на каждый символ. `OnNameCommit` (привязан к `onEndEdit`) — вызывается при потере фокуса или нажатии Enter; только тогда сцена помечается изменённой. `string.IsNullOrWhiteSpace` guard в `LiveEdit` — если поле пустое (пользователь стёр всё), не обновлять имя на пустоту.

##### OnDeleteClicked — порядок операций
```csharp
private void OnDeleteClicked()
{
    if (_bound == null) return;
    var nodeId = _bound.NodeId;
    _bound = null;
    _ctx.Selection?.Select(null);
    _ctx.Graph.RemoveNode(nodeId); // destroys GO, publishes SceneModifiedEvent → outliner rebuilds
}
```

> Критичен порядок: сначала `_bound = null` — чтобы `SelectionChangedEvent` (который придёт от `Select(null)`) не нашёл удалённый `_bound`. Затем `Select(null)` — сброс выбора перед удалением. Наконец `RemoveNode` — уничтожение GO и публикация `SceneModifiedEvent`. Если бы `RemoveNode` шёл первым, обработчик `SelectionChangedEvent` в `Refresh` попытался бы обратиться к уничтоженному GO.

##### OnShowBonesToggleChanged — многошаговое определение рига
```csharp
private void OnShowBonesToggleChanged(bool value)
{
    string rigNodeId = null;
    if (_bound != null)
        rigNodeId = _bound.NodeId;
    else if (!string.IsNullOrEmpty(_boneRigId))
        rigNodeId = _boneRigId;
    else if (!string.IsNullOrEmpty(_boneEditMode?.ActiveRigId))
        rigNodeId = _boneEditMode.ActiveRigId;

    _boneEditMode?.SetActive(rigNodeId, value);
}
```

> Вход в режим костей сбрасывает выбор (`_bound = null`). При выходе из режима (`value = false`) нет ни `_bound`, ни `_boneRigId` (выбор сброшен). Запасной путь — `_boneEditMode.ActiveRigId`, который хранит id рига, вошедшего в bone-mode. Три-уровневый fallback гарантирует, что тумблер всегда знает, какой риг включать/выключать.

### К защите

##### Вероятные вопросы
> [!question]
> **В:** Почему инспектор определяет кость через `StartsWith("bone:")`, а не через `GetComponent<BoneSceneNodeMarker>`?
> **О:** `StartsWith` — O(1) операция на строке, не требующая обращения к SceneGraph и GO. `GetComponent` потребовал бы поиска ноды в графе, а затем GetComponent на её GO. Кроме того, `Refresh` вызывается при каждом событии выбора — строковая проверка дешевле.

> [!question]
> **В:** Почему `SceneModifiedEvent` публикуется только в `OnNameCommit`, а не в `OnNameLiveEdit`?
> **О:** `SceneModifiedEvent` запускает `SceneDirtyTracker`, который в конечном счёте ведёт к автосохранению с дебаунсом. Публиковать на каждый введённый символ — это десятки/сотни сообщений в секунду при быстром наборе. Фиксация при потере фокуса (commit) — правильный момент для персистентных изменений.

> [!question]
> **В:** Зачем обнулять `_bound` до `Select(null)` при удалении?
> **О:** `Select(null)` публикует `SelectionChangedEvent`, который вызовет `Refresh`. Внутри `Refresh` проверяется `_bound`: если он не обнулён, но уже удалён — `_ctx.Graph.GetNode(activeId)` вернёт `null`, а обращение к полям `_bound` вызовет NRE (или использование zombie-reference Unity). Обнуление до публикации устраняет race.

> [!question]
> **В:** Почему `InspectorPanel` подписывается в `OnEnable`/`OnDisable`, а не в `Construct`?
> **О:** Панель может деактивироваться в рантайме (hidden state пользовательской панели). Подписка в `OnEnable`/`OnDisable` гарантирует, что деактивированная панель не тратит ресурсы на обработку событий, и подписка корректно восстанавливается при реактивации.

### Связи
[[SelectionManager]] · [[BoneEditMode]] · [[SceneContext]] · [[ProxyRigRuntime]] · [[SelectionChangedEvent]] · [[SceneContextChangedEvent]] · [[NodeRenamedEvent]] · [[SceneModifiedEvent]] · [[Внедрение зависимостей (VContainer)]] · [[Прокси-риг]]
