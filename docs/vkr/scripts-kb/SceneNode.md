> [!info] Назначение
> `SceneNode` — компонент-паспорт рантайм-узла сцены (`SceneComposition/`). Прикреплён к `GameObject` ноды; хранит `NodeId`, `AssetRef`, `DisplayName`, флаги `IsVisible`/`IsLocked`; синхронизирует отображаемое имя и активность `GameObject` с изменением данных. Это **рантайм-объект**, не путать с сериализуемой структурой `NodeData`. Листинг 3.29.

### Обзор

##### Роль и место
`SceneNode` — единственная точка, через которую `SceneGraph`, `OutlinerPanel`, `SelectionManager` и другие компоненты узнают идентичность `GameObject`. Граф хранит словарь `Dictionary<string, SceneNode>`. Проксируемые кости также получают `SceneNode` (через `BoneSceneNodeMarker`), но регистрируются в `_transientNodes`, а не в основном словаре `_nodes`.

##### Ключевые методы
- `Init` — записывает `NodeId`, `AssetRef`, `DisplayName`; вызывается однажды при добавлении в граф.
- `SetNodeId` — перезаписывает `_nodeId`; используется в `RewriteBoneNodeIds` для формирования составного id `"bone:{rigNodeId}:{boneName}"`.
- `SetDisplayName` — меняет `_displayName` **и** `gameObject.name` (синхронизация для Unity Hierarchy).
- `SetVisible` — меняет `_isVisible` **и** `gameObject.SetActive(visible)`.
- `SetLocked` — меняет только `_isLocked`; никаких побочных эффектов на `GameObject`.

### Разбор кода

##### [SerializeField] на рантайм-данных

```csharp
[SerializeField] private string   _nodeId;
[SerializeField] private AssetRef _assetRef;
[SerializeField] private string   _displayName;
[SerializeField] private bool     _isVisible = true;
[SerializeField] private bool     _isLocked;
```

> Все поля помечены `[SerializeField]`, хотя `SceneNode` — рантайм-компонент, а не данные для `JsonUtility`. Причина: `[SerializeField]` делает поля видимыми в Unity Inspector — это критично для отладки в редакторе (видно `NodeId` каждой ноды прямо в иерархии). Без `[SerializeField]` поля-`private` невидимы в Inspector.
>
> `_isVisible = true` — значение по умолчанию задано inline; при создании `GameObject` через `AddComponent<SceneNode>()` или при `Instantiate` нода считается видимой до вызова `Init`/`SetVisible`. Это правильное начальное состояние: `GameObject` после `Instantiate` активен.

##### Init — минимальный контракт

```csharp
public void Init(string nodeId, AssetRef assetRef, string displayName)
{
    _nodeId      = nodeId;
    _assetRef    = assetRef;
    _displayName = displayName;
}
```

> `Init` не вызывает `SetDisplayName` (не синхронизирует `gameObject.name`). Синхронизация имени делается в `AddNodeInternal` следующей строкой: `if (!string.IsNullOrEmpty(displayName)) go.name = displayName;`. Разделение намеренно: `Init` — чистое присваивание данных без побочных эффектов на `GameObject`; побочный эффект выполняет граф.
>
> `Init` **не** выставляет `_isVisible`/`_isLocked` — они остаются со значениями по умолчанию (`true`/`false`). При загрузке сцены видимость и блокировка восстанавливаются отдельными вызовами `SetVisible`/`SetLocked` (если логика восстановления добавлена в граф); при спавне нового объекта значения по умолчанию корректны.

##### SetNodeId — пост-init перезапись

```csharp
public void SetNodeId(string newId) => _nodeId = newId;
```

> Метод нарушает принцип «id задаётся один раз» — публичный сеттер позволяет изменить `NodeId` после `Init`. Используется исключительно в `SceneGraph.RewriteBoneNodeIds`: id кости в бейк-тайм относительный (`"pelvis"`), а в рантайме нужен составной (`"bone:abc12345:pelvis"`). Нет других вызовов этого метода в кодовой базе.

##### SetDisplayName — двойной эффект

```csharp
public void SetDisplayName(string name)
{
    _displayName = name;
    gameObject.name = name;
}
```

> `gameObject.name = name` синхронизирует имя объекта в Unity Hierarchy — полезно для отладки в редакторе и корректного отображения в `OutlinerPanel` (который читает `node.DisplayName`, а не `gameObject.name`). Двойная запись означает, что `DisplayName` и `gameObject.name` всегда совпадают, если имя меняется через `SetDisplayName`. Прямая запись в `gameObject.name` в обход `SetDisplayName` создаст расхождение.

##### SetVisible — gameObject.SetActive

```csharp
public void SetVisible(bool visible)
{
    _isVisible = visible;
    gameObject.SetActive(visible);
}
```

> `gameObject.SetActive(false)` деактивирует весь объект: скрывает меш, останавливает анимацию, отключает компоненты (`Update` не вызывается). Это более сильное действие, чем скрытие рендерера. Важное следствие: деактивированный объект не участвует в `GetComponentInChildren` с параметром `includeInactive: false`. Код `OutlinerPanel` и `SceneGraph` использует `includeInactive: true` для `ProxyRigRuntime`, поэтому скрытые риги корректно обрабатываются.

##### SetLocked — флаг без побочных эффектов

```csharp
public void SetLocked(bool locked) => _isLocked = locked;
```

> `SetLocked` только записывает флаг — никакого физического эффекта на `GameObject` нет. Блокировка — семантический флаг, интерпретируемый потребителями (`VrInteraction`, `SelectionManager`): заблокированный объект игнорируется при интерактивности. Аналогия с Unity Editor: объект в Hierarchy можно «заблокировать» — он остаётся видимым, но не выбирается.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Чем `SceneNode` отличается от `NodeData`?
> **О:** `NodeData` — сериализуемая структура (`[Serializable]`), данные для `JsonUtility`; существует только в памяти при чтении/записи `scene.json`. `SceneNode` — `MonoBehaviour`, прикреплён к `GameObject` во время работы приложения. `SceneGraph` читает `NodeData` из файла, создаёт `GameObject`, вызывает `SceneNode.Init` с данными из `NodeData`, и после этого `NodeData` не нужна — живёт только `SceneNode`.

> [!question]
> **В:** Почему `NodeId` — `string`, а не `int` или `Guid`?
> **О:** `Guid.NewGuid().ToString("N")[..8]` — восьмисимвольный hex-срез GUID. Строка удобна для `Dictionary<string, SceneNode>`, читаема в Inspector и JSON, не требует конвертации. Коллизия восьмисимвольного hex статистически ничтожна в масштабе одной сцены (десятки объектов).

> [!question]
> **В:** `Init` не синхронизирует `gameObject.name` — почему это не проблема?
> **О:** Синхронизация сделана в `AddNodeInternal` (`SceneGraph`): `if (!string.IsNullOrEmpty(displayName)) go.name = displayName`. `Init` — чистое присваивание полей без побочных эффектов; граф управляет побочными эффектами на `GameObject`. Это разделение ответственности: `SceneNode` = паспорт, `SceneGraph` = регистратор.

> [!question]
> **В:** Зачем `[SerializeField]` на рантайм-компоненте, который не читается `JsonUtility`?
> **О:** Для видимости в Unity Inspector при отладке. Без `[SerializeField]` приватные поля невидимы — нельзя проверить `NodeId` прямо в иерархии. `JsonUtility` работает с `NodeData` (другая структура), `SceneNode` в JSON не сериализуется.

### Связи

[[SceneGraph]] · [[OutlinerPanel]] · [[SceneContext]] · [[AppStorage]] · [[SceneNode]] · [[Структуры данных сцены]] · [[Внедрение зависимостей (VContainer)]]
