# Scene-Bound Objects + Selection + Outliner/Inspector

**Date:** 2026-05-17
**Scope:** Spec A of a 2-spec decomposition. Spec B (Import Wizard + universal overlay toggle + imported-asset file persistence) will follow.

---

## Цель

Привязать spawned объекты к конкретной сцене и обеспечить сохранение/восстановление между сессиями. Ввести multi-select Blender-style + Outliner и Inspector UI внутри UserPanel.

### Что будет работать на выходе

- Создать сцену, наспаунить объектов, выйти в Главное меню, вернуться — все объекты на месте с теми же позициями/именами.
- Открыть другую сцену — там СВОИ объекты, не общий пул (фиксит баг "prefab existует глобально между всеми сценами").
- Выделять объекты по одному или несколько кликами (1-й клик — выбор, 2-й по тому же — сброс).
- Клик в пустоту сбрасывает всё выделение; клик по UserPanel — не сбрасывает.
- Видеть состав сцены в иерархическом Outliner и свойства активного объекта в Inspector.
- Переименовывать объекты в Inspector через VR-клавиатуру.
- Sandbox работает так же, но без сохранения на диск (in-memory).

### Что НЕ входит в скоуп

- Import wizard и универсальный overlay-тагл — это спек B.
- Реализация `ImportedLabAsset.SpawnAsync` — также спек B (до этого imported-ноды в сцене не смогут восстановиться при загрузке, выпадет warning, restoration этой ноды пропустится).
- IMovable runtime поведение (перемещение через гизмо / grip-and-drag). Capability `Movable` зарезервирован в data, флаг можно ставить на ассеты, но никакого `MovableComponent` в этом спеке нет.
- Drag-to-reparent в Outliner; hierarchy создаётся только программно (parent при spawn — пока всегда null).
- Visibility / Lock UI в Outliner (поля в SceneNode есть, UI и сохранение — позже).
- UnsavedChangesGuard в навигации и Save кнопка.
- BoneInspectorPanel / IkSetupWizard сохраняют single-select поведение (используют back-compat `Select(id)`).

---

## Корень багов в текущем коде

1. `SceneGraph.OnSceneOpened` пустой (`{ }`) → ноды не очищаются при переключении сцен → объекты "глобальные".
2. `SceneData` хранит только `NodeIds: List<string>` без полезной нагрузки → даже если очищать, восстанавливать нечего.
3. `SaveSceneAsync` вызывается только при создании сцены с пустым SceneData → сохранение изменений сейчас отсутствует.
4. `SelectionManager` — single-select только; нет `Toggle`/`Clear`.
5. `SceneClosedEvent` объявлен, но никто его не публикует.
6. `WorldClickCatcher` / deselect-on-miss отсутствует.

---

## Архитектура

```
                              ┌───────────────────────┐
                              │ ScriptableObject SOs  │
                              │   BuiltinAssetLibrary │
                              │   (Capabilities flag) │
                              └──────────┬────────────┘
                                         │
   AssetSpawnRequestedEvent              │
   ─────────────────────┐                │
                        ▼                ▼
                  ┌─────────────────────────────┐         ┌──────────────────────┐
                  │ AssetSpawner                │ uses    │ IInteractableFactory │
                  │  (subscribes, calls Spawn)  │────────►│  reads caps, builds  │
                  └────────────┬────────────────┘         │  Collider+Selectable │
                               │                          │  +SelectionInteractor│
                               │                          └──────────────────────┘
                               ▼
                  ┌─────────────────────────────┐ pub  ┌──────────────────────┐
                  │ SceneGraph                  ├─────►│ SceneModifiedEvent   │
                  │  _spawnedRoot (Transform)   │      └──────────┬───────────┘
                  │  _nodes Dict<id,SceneNode>  │                 │
                  └───────────┬────────────┬────┘                 │
                              │            │                      │
                  SceneOpened │            │ destroys&            │
                  rebuild     │            │ rebuilds             ▼
                              │            │           ┌──────────────────────┐
                              ▼            ▼           │ SceneOutlinerView    │
                  ┌─────────────────────────────┐      │ (inside contextMenu) │
                  │ AppStorage                  │      └──────────┬───────────┘
                  │  SceneData v2 (NodeData[])  │                 │
                  │  Save on SceneClosed        │      ┌──────────▼───────────┐
                  └─────────────────────────────┘      │ SceneInspectorView   │
                                                       │ subs to SelectionEv  │
                                                       └──────────────────────┘
```

---

## Data layer

```csharp
// _Shared/Data — new
[Flags]
public enum AssetCapabilities { None = 0, Selectable = 1, Movable = 2 }

public enum AssetSource { Builtin, Imported, Saved }

[Serializable]
public struct AssetRef {
    public AssetSource Source;
    public string      AssetId;
}

public enum SelectionVisual { None, InSet, Active }
```

```csharp
// StorageCore/Data — new
[Serializable]
public class NodeData {
    public string     NodeId;
    public AssetRef   AssetRef;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
    public string     DisplayName;
    public string     ParentNodeId;   // null = root
}

// StorageCore/Data — SceneData v2
[Serializable]
public class SceneData {
    public int            SchemaVersion = 2;  // bumped from 1
    public string         SceneId;
    public string         DisplayName;
    public string         CreatedAt;
    public List<NodeData> Nodes = new();      // replaces NodeIds
}
```

**Миграция** в `SceneSerializer.Deserialize`: если `SchemaVersion < 2` → бамп до 2, `Nodes = new()`, `Debug.LogWarning`. Старые `NodeIds` теряются (они и так пустые из-за бага).

**`ILabAsset`** получает `AssetCapabilities Capabilities { get; }`. Реализуется во всех трёх (`BuiltinLabAsset`, `ImportedLabAsset`, `SavedLabAsset`) через `[SerializeField] private AssetCapabilities _capabilities;`.

---

## Selection runtime

### `SelectionManager` (расширение, файл существует)

```csharp
public class SelectionManager : ISelectionManager, IStartable, IDisposable {
    private readonly EventBus     _bus;
    private readonly List<string> _selected = new();   // List, не HashSet — нужен порядок вставки
    private string _active;

    public IReadOnlyList<string> SelectedIds => _selected;
    public string ActiveId                   => _active;
    public string SelectedNodeId             => _active;  // back-compat

    public void Toggle(string nodeId) {
        var idx = _selected.IndexOf(nodeId);
        if (idx >= 0) {
            _selected.RemoveAt(idx);
            // если убрали активный — новый активный = последний оставшийся (most recently added)
            if (_active == nodeId)
                _active = _selected.Count == 0 ? null : _selected[^1];
        } else {
            _selected.Add(nodeId);
            _active = nodeId;
        }
        Publish();
    }

    public void Select(string nodeId) {   // back-compat для rig/bone-кода
        _selected.Clear();
        _selected.Add(nodeId);
        _active = nodeId;
        Publish();
    }

    public void Clear() {
        if (_selected.Count == 0) return;
        _selected.Clear();
        _active = null;
        Publish();
    }

    private void Publish() => _bus.Publish(new SelectionChangedEvent {
        SelectedNodeId  = _active,                // back-compat field
        SelectedNodeIds = _selected.ToArray(),    // new field
    });
}
```

**Почему List, а не HashSet:** при `Toggle` активного нужно вернуть активность на "последний добавленный из оставшихся" — это требует упорядоченности. HashSet порядок не гарантирует. Размер selection всегда мал (десятки максимум), `IndexOf` дешёвый.

### `SelectionChangedEvent` (расширение)

```csharp
public struct SelectionChangedEvent {
    public string   SelectedNodeId;     // existing — теперь = ActiveId
    public string[] SelectedNodeIds;    // new — полный набор
}
```

`GizmoController` и `PropertyPanel` продолжают читать `SelectedNodeId` без изменений.

### Visual feedback через QuickOutline

```csharp
public class Selectable : MonoBehaviour {
    private SceneNode _node;
    private Outline   _outline;   // lazy

    public string NodeId => _node?.NodeId;

    public void Init(SceneNode node) => _node = node;

    public void SetVisualState(SelectionVisual state) {
        EnsureOutline();
        switch (state) {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.InSet:
                _outline.enabled     = true;
                _outline.OutlineColor = new Color(1f, 0.55f, 0f);   // orange
                _outline.OutlineWidth = 4f;
                break;
            case SelectionVisual.Active:
                _outline.enabled     = true;
                _outline.OutlineColor = new Color(1f, 0.95f, 0.15f);// yellow
                _outline.OutlineWidth = 6f;
                break;
        }
    }

    private void EnsureOutline() {
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
    }
}
```

### `SelectionVisualSync`

Единственный консьюмер `SelectionChangedEvent`, отвечающий за визуалы. `Scoped`, `IStartable`. На каждый event обходит все `Selectable` в `SceneGraph.Nodes` и применяет:
- `id == ActiveId` → `Active`
- `id ∈ SelectedIds` → `InSet`
- иначе → `None`

`Selectable` сам себя не подписывает — не нужно реагировать на спавн/деспавн через события.

### Pipeline ray → toggle

```csharp
// SelectionInteractor (existing, rewrite OnSelectEntered)
protected override void OnSelectEntered(SelectEnterEventArgs args) {
    base.OnSelectEntered(args);
    var selectable = GetComponentInParent<Selectable>();
    if (selectable != null) _selectionManager.Toggle(selectable.NodeId);
}
```

### Click-in-empty-space → Clear

`WorldClickCatcher : MonoBehaviour` — один экземпляр на XR Origin (в scene prefab — VrEditing и Sandbox). В `Awake` берёт `XRRayInteractor` с обоих контроллеров (left/right) из children, подписывается на `selectAction.action.performed`:

```csharp
// при срабатывании trigger на любом контроллере
if (rayInteractor.TryGetCurrent3DRaycastHit(out var hit)) {
    if (hit.collider.GetComponentInParent<Selectable>() == null
        && hit.collider.GetComponentInParent<UnityEngine.UI.Graphic>() == null)
        _selectionManager.Clear();
} else {
    _selectionManager.Clear();   // mid-air click
}
```

Проверка `Graphic == null` гарантирует, что клик по UserPanel/AssetBrowser не сбросит выделение.

**DI-wiring `WorldClickCatcher`:** компонент висит на XR Origin в scene-prefab (VrEditing/Sandbox). В `VrEditingSceneScope.Configure` и `SandboxSceneScope.Configure` — `FindAnyObjectByType<WorldClickCatcher>(FindObjectsInactive.Include) + RegisterBuildCallback(c => c.Inject(catcher))` (тот же паттерн, что для `UserPanel` в `RootLifetimeScope`).

---

## SceneGraph: persistence + spawn/respawn

### `SceneGraph` (rewrite ~50% существующего)

```csharp
public class SceneGraph : ISceneGraph, IStartable, IDisposable {
    private readonly EventBus               _bus;
    private readonly IAssetRegistry         _registry;
    private readonly IInteractableFactory   _factory;
    private readonly AppStorage             _storage;
    private readonly Dictionary<string, SceneNode> _nodes = new();

    private Transform _spawnedRoot;

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public void Start() {
        _spawnedRoot = new GameObject("[Spawned]").transform;
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);
    }

    // — Spawn API (для AssetSpawner / future wizard) —
    public SceneNode AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null) {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        return AddNodeInternal(go, nodeId, assetRef, displayName, parentId, isLoad: false);
    }

    public void RemoveNode(string nodeId) { /* как сейчас + publish SceneModified */ }

    public SceneNode GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var n) ? n : null;

    private SceneNode AddNodeInternal(GameObject go, string nodeId, AssetRef assetRef,
                                      string displayName, string parentId, bool isLoad) {
        go.transform.SetParent(_spawnedRoot, worldPositionStays: true);
        var node = go.AddComponent<SceneNode>();
        node.Init(nodeId, assetRef, displayName);
        _nodes[nodeId] = node;
        if (!isLoad) _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    // event handler — синхронный; асинхронная работа — отдельный метод с try/catch
    private void OnSceneOpened(SceneOpenedEvent e) => _ = OnSceneOpenedAsync(e);

    private async Task OnSceneOpenedAsync(SceneOpenedEvent e) {
        try {
            ClearAll();
            var data = await _storage.LoadSceneAsync(e.SceneId, CancellationToken.None);
            if (data?.Nodes == null) return;

            // 1-й проход: спавнить все ноды (без parenting)
            foreach (var nd in data.Nodes) {
                var asset = _registry.Find(nd.AssetRef);
                if (asset == null) {
                    Debug.LogWarning($"SceneGraph: asset not found {nd.AssetRef}");
                    continue;
                }
                var go = await asset.SpawnAsync(nd.Position, nd.Rotation, CancellationToken.None);
                go.transform.localScale = nd.Scale;
                go.name = nd.DisplayName;
                _factory.MakeInteractable(go, asset.Capabilities);
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
            }
            // 2-й проход: расставить parents (только после того как все ноды существуют)
            foreach (var nd in data.Nodes) {
                if (string.IsNullOrEmpty(nd.ParentNodeId)) continue;
                if (_nodes.TryGetValue(nd.NodeId, out var child)
                    && _nodes.TryGetValue(nd.ParentNodeId, out var parent)) {
                    child.transform.SetParent(parent.transform, worldPositionStays: true);
                }
            }
            _bus.Publish(new SceneModifiedEvent());  // дать outliner перерисоваться
        }
        catch (Exception ex) {
            Debug.LogError($"SceneGraph.OnSceneOpenedAsync failed for '{e.SceneId}': {ex}");
        }
    }

    private void ClearAll() {
        _nodes.Clear();
        if (_spawnedRoot != null) {
            foreach (Transform t in _spawnedRoot)
                UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    public SceneData CaptureSnapshot(string sceneId, string displayName, string createdAt) {
        var data = new SceneData {
            SchemaVersion = 2,
            SceneId       = sceneId,
            DisplayName   = displayName,
            CreatedAt     = createdAt,
        };
        foreach (var (id, node) in _nodes) {
            data.Nodes.Add(new NodeData {
                NodeId       = id,
                AssetRef     = node.AssetRef,
                Position     = node.transform.position,
                Rotation     = node.transform.rotation,
                Scale        = node.transform.localScale,
                DisplayName  = node.DisplayName,
                ParentNodeId = node.transform.parent != null && node.transform.parent != _spawnedRoot
                    ? node.transform.parent.GetComponent<SceneNode>()?.NodeId : null
            });
        }
        return data;
    }
}
```

### `SceneNode` (расширение)

```csharp
public class SceneNode : MonoBehaviour {
    public string   NodeId      { get; private set; }
    public AssetRef AssetRef    { get; private set; }
    public string   DisplayName { get; private set; }
    public bool     IsVisible   { get; private set; } = true;
    public bool     IsLocked    { get; private set; }

    public void Init(string id, AssetRef assetRef, string displayName) {
        NodeId = id; AssetRef = assetRef; DisplayName = displayName;
    }

    public void SetDisplayName(string name) { DisplayName = name; gameObject.name = name; }
    public void SetVisible(bool v) { IsVisible = v; gameObject.SetActive(v); }
    public void SetLocked(bool l)  { IsLocked = l; }
}
```

### `AssetSpawner` (правка ~10 строк)

```csharp
private async Task SpawnCoreAsync(AssetSpawnRequestedEvent e) {
    var go = await e.Asset.SpawnAsync(e.Position, e.Rotation, CancellationToken.None);
    _factory.MakeInteractable(go, e.Asset.Capabilities);   // new
    var assetRef = new AssetRef {
        Source = e.Asset is BuiltinLabAsset  ? AssetSource.Builtin
               : e.Asset is ImportedLabAsset ? AssetSource.Imported : AssetSource.Saved,
        AssetId = e.Asset.Id
    };
    _graph.AddNode(go, assetRef, e.Asset.DisplayName);
}
```

`AssetImporter` (legacy, не вызывается из flow в `AssetBrowserModule`) — убираются его `_interactableFactory.MakeInteractable(instance)` и `_sceneGraph.AddNode(instance)`, потому что (а) сигнатура `MakeInteractable` изменилась, (б) `AddNode` теперь требует `AssetRef`, (в) после Spec B весь imported-spawn пойдёт через `AssetSpawnRequestedEvent` шину. Тело метода может остаться (создание `AssetEntry`), но связанные с graph/factory строки удаляются.

### `ISceneGraph` interface (правка)

Старая сигнатура `void ISceneGraph.AddNode(GameObject go)` удаляется. Новая: `void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null)`. Все callers обязаны передавать `AssetRef` — это идентичность ноды для restore. Старые места: `AssetSpawner` (обновлён выше), `AssetImporter` (legacy, удалён).

### `IAssetRegistry` / `AssetRegistry` (new)

```csharp
public interface IAssetRegistry {
    ILabAsset Find(AssetRef reference);   // null если не найден
}

public class AssetRegistry : IAssetRegistry {
    private readonly BuiltinAssetLibrary  _builtin;
    private readonly ImportedAssetLibrary _imported;
    private readonly SavedAssetLibrary    _saved;

    public AssetRegistry(BuiltinAssetLibrary b, ImportedAssetLibrary i, SavedAssetLibrary s) {
        _builtin = b; _imported = i; _saved = s;
    }

    public ILabAsset Find(AssetRef r) {
        IAssetLibrary lib = r.Source switch {
            AssetSource.Builtin  => _builtin,
            AssetSource.Imported => _imported,
            AssetSource.Saved    => _saved,
            _ => null
        };
        if (lib == null) return null;
        foreach (var a in lib.Assets)
            if (a.Id == r.AssetId) return a;
        return null;
    }
}
```

Регистрируется в `RootLifetimeScope` как Singleton.

### `SceneAutoSaver` (new, Scoped, IStartable/IDisposable)

```csharp
public class SceneAutoSaver : IStartable, IDisposable {
    private readonly EventBus         _bus;
    private readonly SceneGraph       _graph;
    private readonly AppStorage       _storage;

    public void Start()   => _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    public void Dispose() => _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    private void OnModeChanged(ModeChangedEvent e) {
        if (e.PreviousMode == AppMode.VrEditing && e.CurrentMode != AppMode.VrEditing)
            _ = SaveCurrentAsync();
    }

    private async Task SaveCurrentAsync() {
        try {
            var activeId = _storage.ActiveSceneId;
            if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;
            var cached = await _storage.LoadSceneAsync(activeId);
            if (cached == null) return;
            var snap = _graph.CaptureSnapshot(activeId, cached.DisplayName, cached.CreatedAt);
            await _storage.SaveSceneAsync(snap, CancellationToken.None);
            _bus.Publish(new SceneClosedEvent());
        }
        catch (Exception ex) {
            Debug.LogError($"SceneAutoSaver failed: {ex}");
        }
    }
}
```

Регистрируется только в `VrEditingSceneScope`. Sandbox не сохраняется.

### `SceneSerializer` миграция

```csharp
public static SceneData Deserialize(string json) {
    if (string.IsNullOrEmpty(json)) return null;
    var data = JsonUtility.FromJson<SceneData>(json);
    if (data.SchemaVersion < 2) {
        Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v2");
        data.SchemaVersion = 2;
        data.Nodes ??= new List<NodeData>();
    }
    return data;
}
```

---

## Outliner + Inspector UI

**Где живут:** оба внутри `UserPanel_ContextMenu_VrEditing.prefab` и `UserPanel_ContextMenu_Sandbox.prefab`. Структура одинаковая.

```
ContextMenu_VrEditing (Panel)
├── Outliner            [SceneOutlinerView + ScrollRect + Vertical Layout Group]
│    ├── Header (TMP "Outliner")
│    └── Content (parent для SceneOutlinerRow instances)
└── Inspector           [SceneInspectorView]
     ├── Header (TMP "Inspector")
     ├── NameField (TMP_InputField + VrInputFieldProxy)
     ├── TypeLabel (TMP_Text)
     ├── PositionLabel (TMP_Text)   // read-only
     ├── RotationLabel (TMP_Text)
     └── ScaleLabel (TMP_Text)
```

### `SceneOutlinerView` (new)

```csharp
public class SceneOutlinerView : MonoBehaviour {
    [SerializeField] private Transform        _rowsRoot;
    [SerializeField] private SceneOutlinerRow _rowPrefab;
    [SerializeField] private float            _indentPx = 16f;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection) {
        _bus = bus; _graph = graph; _selection = selection;
    }

    private void OnEnable() {
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        Rebuild();
    }
    private void OnDisable() {
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnModified(SceneModifiedEvent _)        => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _) => ApplyHighlight();

    private void Rebuild() {
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);
        var byParent = _graph.Nodes.Values
            .GroupBy(n => GetParentId(n))
            .ToDictionary(g => g.Key ?? "", g => g.ToList());
        AddRowsRecursive(null, 0, byParent);
        ApplyHighlight();
    }

    private string GetParentId(SceneNode n) {
        var p = n.transform.parent;
        if (p == null) return null;
        var pn = p.GetComponent<SceneNode>();
        return pn != null ? pn.NodeId : null;
    }

    private void AddRowsRecursive(string parentId, int depth,
                                   Dictionary<string, List<SceneNode>> byParent) {
        if (!byParent.TryGetValue(parentId ?? "", out var children)) return;
        foreach (var node in children) {
            var row = Instantiate(_rowPrefab, _rowsRoot);
            row.Bind(node, depth * _indentPx, () => _selection.Toggle(node.NodeId));
            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

    private void ApplyHighlight() {
        var active = _selection.ActiveId;
        var set    = new HashSet<string>(_selection.SelectedIds);
        foreach (var row in _rowsRoot.GetComponentsInChildren<SceneOutlinerRow>()) {
            var state = row.NodeId == active ? SelectionVisual.Active
                      : set.Contains(row.NodeId) ? SelectionVisual.InSet
                      : SelectionVisual.None;
            row.SetVisualState(state);
        }
    }
}
```

### `SceneOutlinerRow` (new)

```csharp
public class SceneOutlinerRow : MonoBehaviour {
    [SerializeField] private TMP_Text      _label;
    [SerializeField] private Image         _highlight;
    [SerializeField] private LayoutElement _indentSpacer;
    [SerializeField] private Button        _button;

    public string NodeId { get; private set; }

    public void Bind(SceneNode node, float indentPx, System.Action onClick) {
        NodeId = node.NodeId;
        _label.text = node.DisplayName;
        _indentSpacer.preferredWidth = indentPx;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetVisualState(SelectionVisual state) {
        _highlight.enabled = state != SelectionVisual.None;
        _highlight.color = state switch {
            SelectionVisual.Active => new Color(1f, 0.95f, 0.15f, 0.35f),
            SelectionVisual.InSet  => new Color(1f, 0.55f, 0f, 0.25f),
            _ => Color.clear
        };
    }
}
```

### `SceneInspectorView` (new)

```csharp
public class SceneInspectorView : MonoBehaviour {
    [SerializeField] private GameObject     _emptyState;     // "Nothing selected"
    [SerializeField] private GameObject     _content;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private TMP_Text       _typeLabel;
    [SerializeField] private TMP_Text       _positionLabel;
    [SerializeField] private TMP_Text       _rotationLabel;
    [SerializeField] private TMP_Text       _scaleLabel;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private SceneNode         _bound;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection) {
        _bus = bus; _graph = graph; _selection = selection;
    }

    private void OnEnable() {
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _nameField.onEndEdit.AddListener(OnNameChanged);
        Refresh();
    }
    private void OnDisable() {
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _nameField.onEndEdit.RemoveListener(OnNameChanged);
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void Refresh() {
        var activeId = _selection.ActiveId;
        _bound = string.IsNullOrEmpty(activeId) ? null : _graph.GetNode(activeId);
        var has = _bound != null;
        _emptyState.SetActive(!has);
        _content.SetActive(has);
        if (!has) return;
        _nameField.SetTextWithoutNotify(_bound.DisplayName);
        _typeLabel.text     = $"Type: {_bound.AssetRef.Source}/{_bound.AssetRef.AssetId}";
        _positionLabel.text = $"Pos: {_bound.transform.position:F2}";
        _rotationLabel.text = $"Rot: {_bound.transform.rotation.eulerAngles:F1}";
        _scaleLabel.text    = $"Scale: {_bound.transform.localScale:F2}";
    }

    private void OnNameChanged(string newName) {
        if (_bound == null || string.IsNullOrWhiteSpace(newName)) return;
        _bound.SetDisplayName(newName.Trim());
        _bus.Publish(new SceneModifiedEvent());
    }
}
```

**Live transform updates:** view не подписан на каждый кадр. Refresh идёт только по событиям. Когда введём IMovable, добавится `NodeTransformedEvent` → подписка. Сейчас Pos/Rot/Scale показываются "as of last selection event" — для read-only это нормально.

### Keyboard для переименования

`_nameField` (TMP_InputField) получает `VrInputFieldProxy` компонент (из существующего vr-keyboard flow). Клик публикует `KeyboardFocusEvent`, VrKeyboard принимает фокус, печать обновляет text, `onEndEdit` → `OnNameChanged`.

### VContainer wiring для context menu

Context-menu prefab инстанциируется в runtime `UserPanel.SwapContext()`. Нужен scope-aware inject:

```csharp
private void SwapContext(AppMode mode) {
    if (_currentContext != null) { Destroy(_currentContext); _currentContext = null; }
    if (_contextSlot == null) return;

    foreach (var entry in _contextMenus) {
        if (entry.Mode == mode && entry.Prefab != null) {
            _currentContext = Instantiate(entry.Prefab, _contextSlot);

            LifetimeScope scope = mode switch {
                AppMode.VrEditing => LifetimeScope.Find<VrEditingSceneScope>(),
                AppMode.Sandbox   => LifetimeScope.Find<SandboxSceneScope>(),
                _                 => LifetimeScope.Find<RootLifetimeScope>()
            };
            scope?.Container.InjectGameObject(_currentContext);

            _currentContext.transform.localPosition = Vector3.zero;
            _currentContext.transform.localRotation = Quaternion.identity;
            break;
        }
    }
}
```

`InjectGameObject` — стандартный VContainer API, инжектит во все `[Inject]`-методы во всех компонентах иерархии.

---

## Тестирование

### Unit-тесты (Subsystems.SceneComposition.Tests, без Unity runtime)

| Тест | Класс | Что проверяет |
|---|---|---|
| `SelectionManagerToggleAddsToSelection` | SelectionManager | первый Toggle добавляет, `_active = id` |
| `SelectionManagerToggleRemovesFromSelection` | SelectionManager | повторный Toggle убирает, `_active` переключается |
| `SelectionManagerClearEmptiesAll` | SelectionManager | Clear обнуляет и публикует event с null |
| `SelectionManagerSelectReplacesSet` | SelectionManager | back-compat: Select(id) очищает + добавляет один |
| `SceneGraphAddNodeAssignsId` | SceneGraph | AddNode возвращает узел с непустым NodeId, появляется в `_nodes` |
| `SceneGraphCaptureSnapshotMatchesNodes` | SceneGraph | CaptureSnapshot сериализует все ноды + parent relationships |
| `SceneSerializerMigratesV1ToV2` | SceneSerializer | старая SceneData с `NodeIds` грузится без exception, получает `Nodes = []` |
| `AssetRegistryFindsByRefAcrossLibraries` | AssetRegistry | для каждого `AssetSource` находит ассет по Id, возвращает null если нет |

### Не покрываем unit-тестами (требуют Unity/VR)

- `SceneGraph.OnSceneOpened` rebuild (нужны Instantiate + async).
- `WorldClickCatcher` (требует XR-симуляции).
- `SelectionVisualSync` (требует QuickOutline компонент).
- Outliner/Inspector views (UI).

### Manual playmode чек-лист

| Шаг | Ожидаемо |
|---|---|
| VrEditing, спавн 3 ассетов | Все 3 видны, под `[Spawned]` в иерархии, Outliner показывает 3 строки |
| Click на один | Выделен, в Inspector имя/тип/transform, в Outliner подсветка yellow |
| Click на второй | Оба выделены: первый orange, второй yellow |
| Click на первый ещё раз | Только второй остался (yellow) |
| Click в пустоту | Selection clear, Inspector показывает "Nothing selected" |
| Click на UserPanel-кнопку | Selection не сбрасывается |
| Переименовать в Inspector | Имя меняется в Outliner и `gameObject.name` |
| Exit в MainMenu, потом обратно открыть ту же сцену | Все 3 ассета снова в сцене с теми же positions и именами |
| Sandbox: спавн 2 ассетов, exit, обратно в Sandbox | Сцена пустая (in-memory) |
| Создать новую сцену → спавн → exit → открыть СТАРУЮ сцену | Старая показывает свои ноды, не новой |

---

## Файлы

| Путь | Изменение |
|---|---|
| `Assets/_App/_Shared/Data/AssetCapabilities.cs` | **new** — `[Flags]` enum |
| `Assets/_App/_Shared/Data/AssetRef.cs` | **new** — struct |
| `Assets/_App/_Shared/Data/AssetSource.cs` | **new** — enum |
| `Assets/_App/_Shared/Data/SelectionVisual.cs` | **new** — enum |
| `Assets/_App/_Shared/Events/AppEvents.cs` | **edit** — расширить `SelectionChangedEvent` (добавить `SelectedNodeIds[]`) |
| `Assets/_App/_Shared/Interfaces/ILabAsset.cs` | **edit** — добавить `AssetCapabilities Capabilities` |
| `Assets/_App/_Shared/Interfaces/ISelectionManager.cs` | **edit** — добавить `Toggle`, `Clear`, `SelectedIds`, `ActiveId` |
| `Assets/_App/_Shared/Interfaces/IInteractableFactory.cs` | **edit** — `MakeInteractable(GameObject, AssetCapabilities)` |
| `Assets/_App/_Shared/Interfaces/IAssetRegistry.cs` | **new** |
| `Assets/_App/Subsystems/AssetBrowser/AssetRegistry.cs` | **new** |
| `Assets/_App/Subsystems/AssetBrowser/Data/BuiltinLabAsset.cs` | **edit** — `_capabilities` field + getter |
| `Assets/_App/Subsystems/AssetBrowser/Data/ImportedLabAsset.cs` | **edit** — то же |
| `Assets/_App/Subsystems/AssetBrowser/Data/SavedLabAsset.cs` | **edit** — то же |
| `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs` | **edit** — передать AssetRef + DisplayName + caps в фабрику |
| `Assets/_App/Subsystems/AssetBrowser/AssetImporter.cs` | **edit** — убрать `_graph.AddNode(go)` (legacy, не вызывается из flow; чтобы не путать после wizard) |
| `Assets/_App/Subsystems/SceneComposition/SceneNode.cs` | **edit** — поля AssetRef + DisplayName + методы |
| `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs` | **edit** — rewrite OnSceneOpened, `_spawnedRoot`, CaptureSnapshot, перегрузка AddNode |
| `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs` | **edit** — multi-select API |
| `Assets/_App/Subsystems/SceneComposition/SceneAutoSaver.cs` | **new** |
| `Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs` | **new** |
| `Assets/_App/Subsystems/SceneComposition/Tests/SceneGraphTests.cs` | **new** |
| `Assets/_App/Subsystems/SceneComposition/Tests/AssetRegistryTests.cs` | **new** |
| `Assets/_App/Subsystems/StorageCore/Data/SceneData.cs` | **edit** — `SchemaVersion=2`, `List<NodeData>` |
| `Assets/_App/Subsystems/StorageCore/Data/NodeData.cs` | **new** |
| `Assets/_App/Subsystems/StorageCore/SceneSerializer.cs` | **edit** — миграция v1→v2 |
| `Assets/_App/Subsystems/StorageCore/Tests/SceneSerializerTests.cs` | **edit** — добавить v1→v2 |
| `Assets/_App/Subsystems/VrInteraction/Selectable.cs` | **new** |
| `Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs` | **new** |
| `Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs` | **edit** — Toggle вместо Select |
| `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs` | **edit** — принимает AssetCapabilities, ветвится |
| `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` | **new** |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneOutlinerView.cs` | **new** |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneOutlinerRow.cs` | **new** |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneInspectorView.cs` | **new** |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | **edit** — scope-aware `InjectGameObject` в SwapContext |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_VrEditing.prefab` | **edit** — добавить Outliner + Inspector |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_Sandbox.prefab` | **edit** — то же |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Rows/SceneOutlinerRow.prefab` | **new** — строка outliner |
| `Assets/_App/Bootstrap/RootLifetimeScope.cs` | **edit** — register `AssetRegistry` |
| `Assets/_App/Bootstrap/VrEditingSceneScope.cs` | **edit** — register `SceneAutoSaver`, `SelectionVisualSync` |
| `Assets/_App/Bootstrap/SandboxSceneScope.cs` | **edit** — register `SelectionVisualSync` (без AutoSaver) |
| `Assets/_App/Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef` | **edit** — добавить ref `QuickOutline` |
| `Assets/UnityPacks/QuickOutline/Scripts/QuickOutline.asmdef` | **new** — `{ "name":"QuickOutline", "autoReferenced":false }` |

### Ручная работа после имплементации

- После того как `BuiltinLabAsset._capabilities` появится, у всех entries в `BuiltinAssetLibrary.asset` поле будет дефолтным `None`. Вручную выставить `Selectable | Movable` для всех существующих демо-ассетов в Inspector (5-10 минут).
- Собрать prefab `SceneOutlinerRow` (Button + Image + TMP_Text + LayoutElement) и положить в `Prefabs/Rows/`.
- Добавить Outliner + Inspector в `UserPanel_ContextMenu_VrEditing.prefab` и `_Sandbox.prefab` с разметкой согласно дизайну.
- На XR Origin в scene prefab положить `WorldClickCatcher` MonoBehaviour.
- На `NameField` в Inspector сцены добавить `VrInputFieldProxy` (как описано в `docs/developer-notes/vr-keyboard.md`).

---

## Соглашения проекта (соответствие)

- Все cross-subsystem обмены — через events (`SceneOpenedEvent`, `SceneModifiedEvent`, `SelectionChangedEvent`, `ModeChangedEvent`, `SceneClosedEvent`).
- Нет `FindObjectOfType` в runtime (только в `LifetimeScope.Find<T>` для VContainer, что является штатным API DI).
- Нет singletons; всё через VContainer scopes.
- `[SerializeField] private` для всех инспектор-полей.
- Schema-versioned data с миграцией в `SceneSerializer`.
- Тесты живут в `Subsystems/{Name}/Tests/`.
- Subsystem-specific UI в `Subsystems/{Name}/UI` (Outliner/Inspector в `SpatialUi/UI_Scripts/`).
- Никаких generic суффиксов (`Manager`, `Helper`, `Controller`) на новых типах. `SelectionManager`, `SceneAutoSaver` — существующие исключения сохранены; новые имена: `Selectable`, `SelectionVisualSync`, `SceneOutlinerView`, `SceneInspectorView`, `WorldClickCatcher`, `AssetRegistry`.
