> [!info] Назначение
> `SceneGraph` — реестр рантайм-нод сцены (`SceneComposition/`). Реализует `ISceneGraph`, `IStartable`, `IDisposable`; управляет словарями `_nodes` и `_transientNodes`; восстанавливает граф из `scene.json` при старте; добавляет и удаляет ноды; публикует `SceneModifiedEvent`; снимает снимок `SceneData` для автосохранения. Чистый POCO-сервис (не `MonoBehaviour`), регистрируется в сценной области VContainer. Листинг 3.30 (фрагмент) + Листинг Б.12 (полный текст).

### Обзор

##### Роль и место
Единственный источник истины о содержимом открытой сцены в рантайме. Не хранит данные анимации — только геометрию и трансформы. Регистрируется в сценной области жизни (`VrEditing`/`Sandbox`); `MainMenuPanel` вызывает `SetActiveScene` до перехода, `SceneGraph.Start` читает `ActiveSceneId` и начинает восстановление.

##### Ключевые методы
- `Start` (IStartable) — создаёт `[Spawned]` root-объект, подписывается на `SceneOpenedEvent`, вызывает `OnSceneOpenedAsync` если `ActiveSceneId` уже установлен.
- `Dispose` (IDisposable) — отписывается, очищает словари, уничтожает `[Spawned]`.
- `AddNode` — публичный API: генерирует `NodeId` через `Guid`, делегирует в `AddNodeInternal`.
- `RemoveNode` — удаляет ноду из словаря, уничтожает `GameObject`, публикует `SceneModifiedEvent`.
- `GetNode` — ищет сначала в `_nodes`, затем в `_transientNodes` (кости).
- `AddTransientNode` — регистрирует прокси-кость без публикации `SceneModifiedEvent`.
- `AddNodeInternal` — приватный: парентит GO к `_spawnedRoot`, получает/добавляет `SceneNode`, инициализирует, обновляет словарь, вызывает `RewriteBoneNodeIds`.
- `RewriteBoneNodeIds` — переписывает id костей из бейк-тайм-относительных в `"bone:{rigNodeId}:{boneName}"`.
- `OnSceneOpenedAsync` — основной поток восстановления (два прохода: создание + парентинг).
- `ClearAll` — очищает словари и уничтожает дочерние GO `_spawnedRoot`.
- `CaptureSnapshot` — снимает `SceneData` из живых нод для автосохранения.

### Разбор кода

##### Start — двойной путь инициализации

```csharp
public void Start()
{
    _spawnedRoot = new GameObject("[Spawned]").transform;
    _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

    var activeId = _storage.ActiveSceneId;
    if (!string.IsNullOrEmpty(activeId))
        _ = OnSceneOpenedAsync(new SceneOpenedEvent { SceneId = activeId });
}
```

> `SceneGraph` — POCO-сервис, `Start` вызывается VContainer как `IStartable`. `SceneOpenedEvent` публикуется **до** загрузки сцены редактора (из `MainMenuPanel.OpenSceneAsync`), поэтому подписчика ещё нет в момент публикации. Решение: читать `ActiveSceneId` из `AppStorage` при старте и сразу запускать загрузку. Подписка на `SceneOpenedEvent` нужна для последующих вызовов (переоткрытие сцены без смены Unity-сцены — например, при отладке).
>
> `_ = OnSceneOpenedAsync(...)` — fire-and-forget с дискардом. `IStartable.Start` не `async`, поэтому `Task` запускается как фоновая операция. Исключения из `OnSceneOpenedAsync` не пробросятся — они перехвачены внутренним `try/catch` и уйдут в `Debug.LogError`.

##### AddNodeInternal — Re-use pre-attached SceneNode

```csharp
var node = go.GetComponent<SceneNode>();
if (node == null) node = go.AddComponent<SceneNode>();
node.Init(nodeId, assetRef, displayName);
```

> Комментарий в коде: «Re-use a pre-attached SceneNode (baked into the prefab per the bake-time refactor) so that all Awake-time references... point to the SAME SceneNode instance whose NodeId we now stamp with the runtime GUID». `SceneNode` может быть уже на префабе; `AddComponent` — fallback. Если добавить новый `SceneNode`, а не использовать существующий, то компоненты, захватившие ссылку в `Awake` (`XRPromeonInteractable._node`), будут указывать на старый экземпляр без `NodeId`.

##### AddNodeInternal — публикация только при не-загрузке

```csharp
if (!isLoad) _bus.Publish(new SceneModifiedEvent());
```

> При загрузке сцены (`isLoad: true`) `SceneModifiedEvent` не публикуется на каждую ноду — иначе `OutlinerPanel` перестраивался бы N раз. Одна публикация в конце `OnSceneOpenedAsync` после второго прохода (парентинг). При добавлении ноды пользователем (`isLoad: false`) — немедленная публикация. Флаг `isLoad` — единственный различитель двух путей.

##### RewriteBoneNodeIds — составной id кости

```csharp
private void RewriteBoneNodeIds(GameObject root, string rigNodeId)
{
    var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true);
    foreach (var marker in markers)
    {
        var sn = marker.GetComponent<SceneNode>();
        if (sn == null) continue;
        var boneName = sn.NodeId;
        sn.SetNodeId($"bone:{rigNodeId}:{boneName}");
        AddTransientNode(sn);
    }
}
```

> `BoneSceneNodeMarker` — маркер-компонент на прокси-костях, выставленный в бейк-тайм. `GetComponentsInChildren(includeInactive: true)` — кости могут быть деактивированы (режим скелета выключен). `boneName = sn.NodeId` читает id **до** перезаписи: в момент вызова `sn.NodeId` ещё содержит бейк-тайм имя (`"pelvis"`). Итоговый id: `"bone:abc12345:pelvis"` — уникален в масштабе приложения.
>
> `AddTransientNode` намеренно не публикует `SceneModifiedEvent`: аутлайнер не отображает кости как строки (только риг-строка с кнопкой).

##### OnSceneOpenedAsync — два прохода

```csharp
foreach (var nd in data.Nodes)  // Первый проход: создание нод
{
    // ... RestoreAsync, AddNodeInternal, InjectGameObject, ApplyPoses
}

foreach (var nd in data.Nodes)  // Второй проход: парентинг
{
    if (string.IsNullOrEmpty(nd.ParentNodeId)) continue;
    if (_nodes.TryGetValue(nd.NodeId, out var child)
        && _nodes.TryGetValue(nd.ParentNodeId, out var parent))
    {
        child.transform.SetParent(parent.transform, worldPositionStays: true);
    }
}
```

> Два прохода необходимы: порядок `NodeData` в JSON не гарантирует, что родитель создаётся раньше ребёнка. Первый проход создаёт все ноды, второй — устанавливает иерархию. `worldPositionStays: true` — ребёнок не сдвигается при смене родителя (мировые координаты сохраняются, локальные пересчитываются).

##### OnSceneOpenedAsync — устойчивость к ошибкам

```csharp
go = await _spawners.RestoreAsync(asset, nd.Position, nd.Rotation, CancellationToken.None);
```

> Вызов обёрнут в отдельный `try/catch` (не общий): `RestoreAsync` может провалиться для одной ноды (например, файл GLB повреждён), но загрузка не прерывается — нода пропускается с `Debug.LogWarning`. Аналогично — `asset == null` (ассет удалён из библиотеки). Внешний `try/catch` ловит непредвиденные исключения и логирует через `Debug.LogError`.

##### _resolver.InjectGameObject

```csharp
_resolver.InjectGameObject(go);
```

> Все компоненты нового `GameObject` получают DI-инжекцию из сценной области VContainer. Это единственное место, где `[Inject]`-методы на `MonoBehaviour`-компонентах вызываются для рантайм-порождённых объектов. Без этого вызова инжектированные зависимости (`EventBus`, `SelectionManager` и т.д.) на компонентах ноды остались бы `null`.

##### CaptureSnapshot — определение родителя

```csharp
if (node.transform.parent != null && node.transform.parent != _spawnedRoot)
{
    var pn = node.transform.parent.GetComponent<SceneNode>();
    if (pn != null) parentId = pn.NodeId;
}
```

> Родителем в сериализованном виде считается только другая **нода** (содержит `SceneNode`). `_spawnedRoot` — технический контейнер, не нода; если нода парентована к нему — `parentId = null`. `GetComponent<SceneNode>()` на родительском трансформе: если к родителю прикреплён `SceneNode`, он и есть родительская нода. Если нет — `parentId = null` (нода корневая).

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `SceneGraph` — POCO-сервис, а не `MonoBehaviour`?
> **О:** `SceneGraph` — чистый сервис без жизненного цикла Unity (`Update`, `OnDestroy` не нужны). VContainer управляет его жизнью через `IStartable`/`IDisposable`. Если бы он был `MonoBehaviour`, требовался бы отдельный `GameObject` в сцене, а зависимости пришлось бы передавать через Inspector или `Find`. POCO + VContainer — каноничный вариант для сервисов без Unity-специфики.

> [!question]
> **В:** Зачем два словаря — `_nodes` и `_transientNodes`?
> **О:** `_nodes` — постоянные ноды сцены (объекты, сохраняемые в `scene.json`). `_transientNodes` — прокси-кости рига: они существуют только в рантайме, не сохраняются отдельно (позы записываются в `NodeData.BonePoses` родительской ноды). `GetNode` ищет в обоих словарях — для `SelectionManager` кость не отличается от обычной ноды по API.

> [!question]
> **В:** Почему при загрузке сцены `SceneModifiedEvent` публикуется только один раз в конце, а не на каждую ноду?
> **О:** Производительность: `OutlinerPanel.Rebuild` перестраивает весь список — это операция O(N). N вызовов на N нод дали бы O(N²). Флаг `isLoad: true` подавляет промежуточные публикации; одна публикация после второго прохода обновляет аутлайнер один раз с полным графом.

> [!question]
> **В:** Зачем `_resolver.InjectGameObject(go)` после создания ноды?
> **О:** `SceneGraph` — POCO-сервис, создаёт `GameObject` через `RestoreAsync` (не через VContainer). Движок Unity не знает о DI — компоненты на новом объекте не получат зависимости автоматически. `IObjectResolver.InjectGameObject` — VContainer-метод, который проходит по компонентам и вызывает `[Inject]`-методы, передавая зависимости из сценной области.

> [!question]
> **В:** Как работает `CaptureSnapshot` — это синхронная операция?
> **О:** Да, полностью синхронная: обход `_nodes`, чтение трансформов и поз костей из живых объектов, сборка `SceneData`. Вызывается `SceneAutoSaver` по `ModeExitingEvent` пока сцена ещё живёт. После снимка запись на диск — уже IO, которое `AppStorage` выполняет асинхронно.

> [!question]
> **В:** Зачем `worldPositionStays: true` при установке парентинга в втором проходе?
> **О:** Позиция, ротация и масштаб нод уже применены в первом проходе (`go.transform.localScale = nd.Scale`, `RestoreAsync` применяет `Position`/`Rotation`). При `SetParent(worldPositionStays: true)` Unity сохраняет мировые координаты, пересчитывая локальные. Если бы использовался `worldPositionStays: false`, мировые координаты дочернего объекта сместились бы на позицию родителя — иерархия отобразилась бы неверно.

### Связи

[[SceneNode]] · [[AppStorage]] · [[SceneContext]] · [[ModeOrchestrator]] · [[OutlinerPanel]] · [[SceneModifiedEvent]] · [[SceneOpenedEvent]] · [[ModeExitingEvent]] · [[Структуры данных сцены]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
