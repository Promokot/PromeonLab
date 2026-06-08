> [!info] Назначение
> `OutlinerPanel` — панель обозревателя иерархии сцены (`SpatialUi/Panels/`). Живёт на постоянной `UserPanel` (рут-скоп), пережи­вает смены сцен. Перестраивает список строк по графу `SceneGraph`; синхронизирует подсветку по `SelectionManager`; реагирует на точечные события (`NodeRenamedEvent`, `BonesVisibilityChangedEvent`) без полной перестройки. Листинг 3.31 (фрагмент) + Листинг Б.13 (полный текст).

### Обзор

##### Роль и место
`OutlinerPanel` получает `EventBus` и `SceneContext` через `[Inject] Construct`. `SceneContext` — фасад к `ISceneGraph` и `ISelectionManager` сценной области; панель не зависит от конкретной сцены. Живёт вне сценной области (рут-скоп), поэтому подписки управляются в `OnEnable`/`OnDisable`, а не в `Start`/`OnDestroy`.

##### Ключевые методы
- `OnEnable` / `OnDisable` — управление подписками; `OnEnable` сразу вызывает `Rebuild`.
- `Rebuild` — полная перестройка: уничтожает строки, группирует ноды по родителю, DFS-обход, создаёт строки, применяет подсветку.
- `AddRowsRecursive` — рекурсивный DFS; выбирает тип строки (объект / риг), устанавливает отступ, подключает обработчик нажатия.
- `ApplyHighlight` — итерирует строки, выставляет `SelectionVisual.Selected` на совпадающую по `NodeId`.
- `OnNodeRenamed` — точечное обновление: ищет строку по `NodeId`, вызывает `SetLabel`.
- `OnBonesVisibilityChanged` — обновляет `_bonesActiveByRig`, находит нужную риг-строку, вызывает `SetBonesMode`.
- `OnSceneContextChanged` — сбрасывает `_bonesActiveByRig`, вызывает `Rebuild` или `ClearRows`.
- `AnyBonesModeActive` — защита от выбора объекта в режиме костей.

### Разбор кода

##### OnEnable / OnDisable — управление подписками без Start

```csharp
private void OnEnable()
{
    if (_bus == null) return;
    _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
    _bus.Subscribe<SceneModifiedEvent>(OnModified);
    _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
    _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    Rebuild();
}
```

> Подписки в `OnEnable`/`OnDisable`, а не в `Start`/`OnDestroy`: панель живёт в рут-скопе и может быть скрыта/показана без уничтожения. Если подписаться в `Start`, при отключении панели события продолжали бы приходить (лишние вызовы `Rebuild`). `if (_bus == null) return` — защита от `OnEnable` до `[Inject] Construct`: Unity вызывает `OnEnable` сразу при активации объекта, а VContainer инжектирует зависимости чуть позже.

##### Rebuild — группировка через byParent

```csharp
var byParent = new Dictionary<string, List<SceneNode>>();
foreach (var pair in _ctx.Graph.Nodes)
{
    var p = GetParentId(pair.Value) ?? "";
    if (!byParent.TryGetValue(p, out var list))
        byParent[p] = list = new List<SceneNode>();
    list.Add(pair.Value);
}
foreach (var list in byParent.Values)
    list.Sort((a, b) => string.Compare(
        a.DisplayName ?? "", b.DisplayName ?? "",
        StringComparison.OrdinalIgnoreCase));
AddRowsRecursive(null, 0, byParent);
```

> `byParent` — словарь `parentId → [дочерние ноды]`. Корневые ноды (без родителя) попадают под ключ `""` (пустая строка). `GetParentId` читает родителя через `transform.parent.GetComponent<SceneNode>()` — не через сохранённые данные, а из живого графа объектов Unity.
>
> Сортировка — `OrdinalIgnoreCase`: стабильный порядок независимо от регистра, без учёта локали. `a.DisplayName ?? ""` — защита от `null` при `string.Compare`; без неё `Compare(null, ...)` бросает исключение.
>
> `AddRowsRecursive(null, 0, byParent)` — стартует с `parentId == null`; `null ?? ""` → `""` — ищет корневые ноды.

##### AddRowsRecursive — выбор типа строки и блокировка в режиме костей

```csharp
var isRig = node.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true) != null;
OutlinerNode_Item row = isRig
    ? Instantiate(_rigRowPrefab, _rowsRoot)
    : Instantiate(_objectRowPrefab, _rowsRoot);

row.Bind(node, depth * _indentPx, () =>
{
    if (AnyBonesModeActive()) return;
    _ctx.Selection?.Select(node.NodeId);
});
```

> `GetComponentInChildren<ProxyRigRuntime>(includeInactive: true)` — при скрытом риге (`SetVisible(false)`) объект деактивирован. `includeInactive: true` гарантирует поиск независимо от состояния `SetActive`. Вызов происходит при каждом `Rebuild` на каждой ноде — потенциально дорогой. Но `Rebuild` вызывается только при `SceneModifiedEvent`/`SceneContextChangedEvent`, не каждый кадр.
>
> `if (AnyBonesModeActive()) return` — блокировка в замыкании: если любой риг в режиме костей, клик по строке аутлайнера не выбирает объект. Комментарий в коде поясняет: «user can't break out of bone editing by clicking a row. Bones are picked in-scene; exit is the inspector's Show Bones toggle». `_ctx.Selection?.Select` — null-conditional: в `Sandbox` `Selection` может быть `null`.
>
> `depth * _indentPx` — отступ в пикселях (`_indentPx = 16f` по умолчанию). Корневые ноды: `depth=0` → отступ 0. Дочерние: `depth=1` → 16px и т.д.

##### OnSceneContextChanged — сброс bonesActiveByRig

```csharp
private void OnSceneContextChanged(SceneContextChangedEvent e)
{
    _bonesActiveByRig.Clear();
    if (e.HasScene) Rebuild();
    else            ClearRows();
}
```

> `_bonesActiveByRig.Clear()` — критично при смене сцены. Панель живёт в рут-скопе и переживает все смены сцен. Без сброса риг, оставленный в режиме костей, блокировал бы `AnyBonesModeActive()` в следующей сцене — `_ctx.Selection?.Select` никогда не вызывался бы из аутлайнера. Комментарий в коде явно описывает этот сценарий.
>
> `e.HasScene` — флаг фасада `SceneContext`: `true` если граф сцены привязан (`SceneContextBinder` установил ссылки). `false` при переходе в главное меню (граф уничтожен). При `false` — `ClearRows()` (пустой список), при `true` — `Rebuild()`.

##### ApplyHighlight — итерация по живым строкам

```csharp
private void ApplyHighlight()
{
    if (_rowsRoot == null || _ctx.Selection == null) return;
    var selectedId = _ctx.Selection.SelectedNodeId;
    foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerNode_Item>())
    {
        row.SetVisualState(row.NodeId == selectedId
            ? SelectionVisual.Selected
            : SelectionVisual.None);
    }
}
```

> `GetComponentsInChildren<OutlinerNode_Item>()` — Unity-поиск по всем строкам в `_rowsRoot`. `OutlinerNode_Rig_Item` наследует `OutlinerNode_Item`, поэтому попадает в результат. Нет кэшированного списка строк — при каждом `ApplyHighlight` Unity перебирает дерево. Вызывается при `SelectionChangedEvent` (не `Rebuild`), то есть не пересоздаёт строки — только меняет их визуальное состояние.
>
> `_ctx.Selection == null` — защита: в `Sandbox` `ISelectionManager` не зарегистрирован, `SceneContext.Selection` вернёт `null`.

##### OnNodeRenamed — точечное обновление без Rebuild

```csharp
private void OnNodeRenamed(NodeRenamedEvent e)
{
    if (_rowsRoot == null) return;
    foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerNode_Item>())
        if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
}
```

> Ранний `return` после первого совпадения: `NodeId` уникальны — повторного совпадения быть не может. Без `return` цикл прошёл бы все строки даже после нахождения нужной. Точечное обновление вместо `Rebuild` экономит перестройку всего DOM-списка при переименовании.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `OutlinerPanel` живёт в рут-скопе, а не в сценной области?
> **О:** Панель находится на `UserPanel` — физической панели в VR, прикреплённой к рукоятке контроллера. `UserPanel` — рут-скоп, `DontDestroyOnLoad`. Если бы `OutlinerPanel` жила в сценной области, при каждой смене сцены панель уничтожалась бы и пересоздавалась — пользователь видел бы мигание. Рут-скоп + `SceneContext`-фасад = постоянная панель с актуальными данными текущей сцены.

> [!question]
> **В:** Зачем отдельный `_bonesActiveByRig`, а не просто читать `BoneEditMode` напрямую?
> **О:** `BoneEditMode` живёт в сценной области `VrEditing`. `OutlinerPanel` в рут-скопе не может зависеть от сценной области (нарушение иерархии VContainer: дочерняя область не доступна из родительской). Локальный словарь `_bonesActiveByRig` обновляется по `BonesVisibilityChangedEvent` через `EventBus` — единственный безопасный канал из сценной области в рут-скоп.

> [!question]
> **В:** Почему граф строится через словарь `byParent`, а не рекурсивным обходом `transform` иерархии?
> **О:** `_ctx.Graph.Nodes` — плоский `Dictionary<string, SceneNode>`; нет гарантии порядка итерации. Прямой обход `transform` иерархии выдал бы ноды в порядке Unity, а не в алфавитном. `byParent` позволяет сначала сгруппировать, затем отсортировать каждую группу — алфавитный порядок на каждом уровне иерархии.

> [!question]
> **В:** Что происходит при `SelectionChangedEvent` — список пересоздаётся?
> **О:** Нет. `OnSelectionChanged` вызывает только `ApplyHighlight()` — перебор живых строк с обновлением `SelectionVisual`. Строки не пересоздаются. `Rebuild()` (полное пересоздание) вызывается только при `SceneModifiedEvent` и `SceneContextChangedEvent`.

> [!question]
> **В:** Как `OutlinerPanel` узнаёт о смене сцены — ведь она не в сценном скопе?
> **О:** Через `SceneContextChangedEvent`, публикуемый `SceneContextBinder` при привязке/отвязке сценного `SceneContext`. Событие идёт через рут-скоп `EventBus`, к которому `OutlinerPanel` подписана. `e.HasScene` различает «сцена загружена» и «сцена выгружена».

> [!question]
> **В:** Почему `AnyBonesModeActive` не просто `bool`, а итерация по словарю?
> **О:** В сцене может быть несколько ригов. Если у одного рига включён режим костей, а другой — нет, выбор объекта в аутлайнере должен быть заблокирован. `_bonesActiveByRig` — словарь `rigNodeId → bool`; `AnyBonesModeActive` — логическое ИЛИ по значениям. Один `bool`-флаг не справился бы с несколькими ригами.

### Связи

[[SceneGraph]] · [[SceneNode]] · [[SceneContext]] · [[AppStorage]] · [[ModeOrchestrator]] · [[SceneModifiedEvent]] · [[SceneContextChangedEvent]] · [[SelectionChangedEvent]] · [[NodeRenamedEvent]] · [[BonesVisibilityChangedEvent]] · [[EventBus]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
