---
note_type: script
subsystem: AssetBrowser
listing: "3.38"
---

> [!info] Назначение
> `AssetSpawner` — сценный сервис (внутрисценная область жизни), восстанавливающий `GameObject` из записи ассета по событию `AssetSpawnRequestedEvent`. Регистрирует объект в `SceneGraph`, инжектирует зависимости в его иерархию. Листинг 3.38.

### Обзор

##### Роль и место

Реализует `IStartable, IDisposable`. Регистрируется в сценной области жизни (`VrEditing` / `Sandbox`). Зависит от сценного `SceneGraph` и `IObjectResolver` — поэтому не может быть в корневой области.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Start` / `Dispose` | подписка / отписка от `AssetSpawnRequestedEvent` |
| `OnSpawnRequested` | fire-and-forget вызов `SpawnCoreAsync` |
| `SpawnCoreAsync` | применяет `spawnOffset`, вызывает `RestoreAsync`, регистрирует ноду, инжектирует DI |

---

### Разбор кода

##### OnSpawnRequested — fire-and-forget

```csharp
private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
    _ = SpawnCoreAsync(e);
```

> `EventBus` вызывает обработчик синхронно. `async` в обработчике запрещён (`async void` под запретом). Паттерн `_ = Task` — огненно-забыть с логированием ошибки внутри `SpawnCoreAsync`. Пока `SpawnCoreAsync` выполняется, новые события `AssetSpawnRequestedEvent` могут прийти и запустить параллельные `SpawnCoreAsync` — это допустимо, так как каждый спавн независим.

##### SpawnCoreAsync — spawnOffset и порядок операций

```csharp
var recipe = e.Asset.Recipe;
var pos    = recipe != null ? e.Position + recipe.spawnOffset : e.Position;
var go = await _builders.RestoreAsync(e.Asset, pos, e.Rotation, CancellationToken.None);
var assetRef = new AssetRef { Source = e.Asset.Source, AssetId = e.Asset.Id };
_graph.AddNode(go, assetRef, e.Asset.DisplayName);
_resolver.InjectGameObject(go);
```

> `recipe.spawnOffset` применяется **только при свежем спавне**, не при загрузке сцены. `SceneGraph.OnSceneOpenedAsync` восстанавливает объекты по сохранённой позиции (которая уже включает смещение) — поэтому двойного подъёма не происходит. Если `recipe == null` (незапечённый Builtin) — `RestoreAsync` бросит `NotSupportedException`, которое поймает `catch`.
>
> `_graph.AddNode` вызывается **после** `RestoreAsync`, когда `GameObject` уже существует. Граф внутри регистрирует `BoneSceneNodeMarker`-компоненты как транзитные ноды и переписывает их `NodeId` в форму `bone:{nodeId}:{boneName}`.
>
> `_resolver.InjectGameObject(go)` — VContainer обходит **всю иерархию** `go` и вызывает `[Inject]` на каждом `MonoBehaviour`. Порядок: сначала `AddNode` (чтобы `SceneNode.NodeId` был уже установлен), затем `Inject` (чтобы `XRPromeonInteractable.Construct`, `ProxyRigRuntime.Construct` и другие получили правильный контекст).

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `AssetSpawner` в сценной области, а не в корневой?
> **О:** Он зависит от `SceneGraph` — сценного сервиса. `IObjectResolver` тоже привязан к области жизни: он должен знать контекст текущей сцены для корректного `InjectGameObject`. Корневая область не имеет доступа к сценным регистрациям.

> [!question]
> **В:** Зачем `_resolver.InjectGameObject(go)` после `_graph.AddNode`, а не до?
> **О:** `InjectGameObject` запустит `Construct` на `ProxyRigRuntime`, который подписывается на `SelectionChangedEvent` — событие, которое зависит от `NodeId` кости. Если бы `AddNode` (и перезапись `NodeId`) произошли после, первые события пришли бы с неправильным идентификатором.

> [!question]
> **В:** Могут ли несколько `SpawnCoreAsync` выполняться параллельно?
> **О:** Да — каждый вызов `OnSpawnRequested` запускает независимый `Task`. Они не конкурируют за общее состояние: `_graph.AddNode` добавляет разные узлы, `_resolver.InjectGameObject` работает с разными объектами. `SceneGraph` внутри должен быть thread-safe относительно параллельных добавлений — это гарантируется тем, что Unity API вызывается на главном потоке (await возвращает на него).

---

### Связи

[[AssetEntityBuilderRegistry]] · [[AssetRegistry]] · [[AssetBrowserPanel]] · [[ProxyRigRuntime]] · [[RigEntityFabricator]] · [[Внедрение зависимостей (VContainer)]] · [[Паттерн Publish-Subscribe]] · [[AssetSpawnRequestedEvent]]
