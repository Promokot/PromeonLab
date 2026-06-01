# Scene-Bound Objects + Selection + Outliner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Привязать spawned объекты к конкретной сцене, обеспечить сохранение/восстановление между сессиями, ввести multi-select Blender-style + Outliner и Inspector UI внутри UserPanel.

**Architecture:** Расширяем `SceneData` до v2 (NodeData[] с AssetRef и transform). `SceneGraph` получает `[Spawned]` root, очищает и перестраивает ноды на `SceneOpenedEvent`. `SelectionManager` поддерживает multi-select через `Toggle/Clear/ActiveId`. Outliner+Inspector живут в `UserPanel_ContextMenu_*` prefab. QuickOutline вешается ленив через `Selectable` component. Capability флаги в `ILabAsset` управляют тем, что фабрика навешивает при спавне.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces для runtime), VContainer DI, MessagePipe-style EventBus, NUnit для unit-тестов, QuickOutline (Asset Store pack в `Assets/UnityPacks/`).

**Project conventions:** см. `CLAUDE.md`. Главное: `_camelCase` private fields, `[SerializeField] private` для inspector-полей, no `FindObjectOfType` runtime, no singletons, all data versioned (`schemaVersion`).

**Memory note:** Пользователь коммитит вручную. Шаги "Commit" в плане — это **suggested commit messages**, не auto-execute. Агент должен останавливаться и ждать ручного коммита пользователя.

---

## Pre-flight: проверка окружения

- [ ] **Step 0.1:** Убедиться, что Unity Editor открыт и проект скомпилирован без ошибок (Console > Clear, проверить 0 errors).
- [ ] **Step 0.2:** Проверить, что текущий git branch чист (`git status` — нет uncommitted кроме `Assets/_Recovery/`).
- [ ] **Step 0.3:** Запустить существующие тесты (`Window > General > Test Runner > EditMode > Run All`) — все должны быть зелёными. Если красные — починить до начала.

---

## Phase 1: Data layer — enums, struct, interface extensions

### Task 1: Создать AssetCapabilities, AssetSource, AssetRef, SelectionVisual

**Files:**
- Create: `Assets/_App/_Shared/Data/AssetCapabilities.cs`
- Create: `Assets/_App/_Shared/Data/AssetSource.cs`
- Create: `Assets/_App/_Shared/Data/AssetRef.cs`
- Create: `Assets/_App/_Shared/Data/SelectionVisual.cs`

- [ ] **Step 1.1: Создать AssetCapabilities.cs**

```csharp
using System;

[Flags]
public enum AssetCapabilities
{
    None       = 0,
    Selectable = 1,
    Movable    = 2,
}
```

- [ ] **Step 1.2: Создать AssetSource.cs**

```csharp
public enum AssetSource
{
    Builtin,
    Imported,
    Saved,
}
```

- [ ] **Step 1.3: Создать AssetRef.cs**

```csharp
using System;

[Serializable]
public struct AssetRef
{
    public AssetSource Source;
    public string      AssetId;

    public override string ToString() => $"{Source}/{AssetId}";
}
```

- [ ] **Step 1.4: Создать SelectionVisual.cs**

```csharp
public enum SelectionVisual
{
    None,
    InSet,
    Active,
}
```

- [ ] **Step 1.5: Проверить компиляцию**

В Unity Editor: дождаться domain reload, открыть Console — 0 errors.

- [ ] **Step 1.6: Suggested commit** (user runs)

```
feat(shared): add AssetCapabilities/AssetSource/AssetRef/SelectionVisual core types
```

---

### Task 2: Расширить ILabAsset и добавить Capabilities в библиотечные типы

**Files:**
- Modify: `Assets/_App/_Shared/Interfaces/ILabAsset.cs`
- Modify: `Assets/_App/Subsystems/AssetBrowser/Data/BuiltinLabAsset.cs`
- Modify: `Assets/_App/Subsystems/AssetBrowser/Data/ImportedLabAsset.cs`
- Modify: `Assets/_App/Subsystems/AssetBrowser/Data/SavedLabAsset.cs`

- [ ] **Step 2.1: Добавить Capabilities в ILabAsset**

Перепиши файл целиком:

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface ILabAsset
{
    string             Id           { get; }
    string             DisplayName  { get; }
    AssetType          Type         { get; }
    Sprite             Icon         { get; }
    AssetCapabilities  Capabilities { get; }

    Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct);
}
```

- [ ] **Step 2.2: Добавить _capabilities в BuiltinLabAsset**

В файле `BuiltinLabAsset.cs` добавить поле после `_prefab`:

```csharp
[SerializeField] private AssetCapabilities _capabilities;
```

И добавить getter:

```csharp
public AssetCapabilities Capabilities => _capabilities;
```

- [ ] **Step 2.3: Добавить _capabilities в ImportedLabAsset**

Аналогично — после `_filePath` добавить:

```csharp
[SerializeField] private AssetCapabilities _capabilities = AssetCapabilities.Selectable;
```

И getter:

```csharp
public AssetCapabilities Capabilities => _capabilities;
```

Также добавить в конструктор `ImportedLabAsset(...)` параметр `AssetCapabilities capabilities = AssetCapabilities.Selectable | AssetCapabilities.Movable` и присваивание `_capabilities = capabilities;`.

- [ ] **Step 2.4: Добавить _capabilities в SavedLabAsset**

Так же, как в Imported: новый field `_capabilities`, getter, параметр в конструктор.

- [ ] **Step 2.5: Проверить компиляцию**

Console — 0 errors. Существующие entries в `BuiltinAssetLibrary.asset` получат дефолтные `None` capabilities — это норма, исправим в финальной ручной задаче.

- [ ] **Step 2.6: Suggested commit**

```
feat(asset-browser): add Capabilities to ILabAsset and three library entries
```

---

### Task 3: Расширить SelectionChangedEvent и ISelectionManager

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs:5`
- Modify: `Assets/_App/_Shared/Interfaces/ISelectionManager.cs`

- [ ] **Step 3.1: Расширить SelectionChangedEvent**

В `AppEvents.cs` заменить строку 5:

```csharp
public struct SelectionChangedEvent  { public string SelectedNodeId; public string[] SelectedNodeIds; }
```

- [ ] **Step 3.2: Расширить ISelectionManager**

Заменить файл целиком:

```csharp
using System.Collections.Generic;

public interface ISelectionManager
{
    string               SelectedNodeId { get; }      // back-compat = ActiveId
    string               ActiveId       { get; }
    IReadOnlyList<string> SelectedIds   { get; }

    void Select(string nodeId);     // single-select; clears + adds (back-compat)
    void Toggle(string nodeId);     // multi-select add/remove
    void Clear();
}
```

- [ ] **Step 3.3: Проверить компиляцию**

В Console будут ошибки — `SelectionManager` ещё не реализует `Toggle/Clear/ActiveId/SelectedIds`. Это ОК — починим в Task 5. Console-ошибки на этом этапе ожидаемые: "SelectionManager does not implement interface member ISelectionManager.Toggle/Clear/ActiveId/SelectedIds".

- [ ] **Step 3.4: Suggested commit**

```
refactor(shared): extend SelectionChangedEvent and ISelectionManager for multi-select
```

---

## Phase 2: Core services

### Task 4: AssetRegistry + IAssetRegistry + DI

**Files:**
- Create: `Assets/_App/_Shared/Interfaces/IAssetRegistry.cs`
- Create: `Assets/_App/Subsystems/AssetBrowser/AssetRegistry.cs`
- Create: `Assets/_App/Subsystems/SceneComposition/Tests/AssetRegistryTests.cs`
- Modify: `Assets/_App/Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef`
- Modify: `Assets/_App/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 4.1: Создать IAssetRegistry**

```csharp
public interface IAssetRegistry
{
    ILabAsset Find(AssetRef reference);
}
```

- [ ] **Step 4.2: Создать AssetRegistry**

```csharp
public class AssetRegistry : IAssetRegistry
{
    private readonly BuiltinAssetLibrary  _builtin;
    private readonly ImportedAssetLibrary _imported;
    private readonly SavedAssetLibrary    _saved;

    public AssetRegistry(BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved)
    {
        _builtin  = builtin;
        _imported = imported;
        _saved    = saved;
    }

    public ILabAsset Find(AssetRef r)
    {
        IAssetLibrary lib = r.Source switch
        {
            AssetSource.Builtin  => _builtin,
            AssetSource.Imported => _imported,
            AssetSource.Saved    => _saved,
            _                    => null,
        };
        if (lib == null) return null;
        foreach (var a in lib.Assets)
            if (a.Id == r.AssetId) return a;
        return null;
    }
}
```

- [ ] **Step 4.2.5: Обновить Subsystems.SceneComposition.Tests.asmdef**

Прочитать существующий и добавить `Subsystems.AssetBrowser` в массив `references`. Результат:

```json
{
  "name": "Subsystems.SceneComposition.Tests",
  "references": ["_Shared", "Subsystems.SceneComposition", "Subsystems.AssetBrowser"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 4.3: Написать failing test**

`Subsystems/SceneComposition/Tests/AssetRegistryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AssetRegistryTests
{
    [Test]
    public void Find_UnknownSource_ReturnsNull()
    {
        var builtin  = ScriptableObject.CreateInstance<BuiltinAssetLibrary>();
        var imported = new ImportedAssetLibraryStub();
        var saved    = new SavedAssetLibraryStub();
        var sut = new AssetRegistry(builtin, imported, saved);

        var result = sut.Find(new AssetRef { Source = (AssetSource)999, AssetId = "x" });

        Assert.IsNull(result);
    }

    [Test]
    public void Find_BuiltinByExistingId_ReturnsAsset()
    {
        // BuiltinAssetLibrary читает приватный _entries; для теста используем рефлексию
        // через JsonUtility-патч или просто проверяем что null если пусто
        var builtin  = ScriptableObject.CreateInstance<BuiltinAssetLibrary>();
        var imported = new ImportedAssetLibraryStub();
        var saved    = new SavedAssetLibraryStub();
        var sut = new AssetRegistry(builtin, imported, saved);

        var result = sut.Find(new AssetRef { Source = AssetSource.Builtin, AssetId = "no-such" });

        Assert.IsNull(result);  // пустая библиотека
    }

    // ImportedAssetLibrary/SavedAssetLibrary требуют PathProvider — используем in-memory stubs
    private class ImportedAssetLibraryStub : ImportedAssetLibrary
    {
        public ImportedAssetLibraryStub() : base(null) { }
    }
    private class SavedAssetLibraryStub : SavedAssetLibrary
    {
        public SavedAssetLibraryStub() : base(null) { }
    }
}
```

**Примечание:** если конструктор `ImportedAssetLibrary(PathProvider)` упадёт с `null` → перепишите стабы как новые классы `: IAssetLibrary` с пустыми методами. Главное — что `Find` корректно возвращает `null` для пустых библиотек и неизвестного source.

- [ ] **Step 4.4: Run test, verify FAIL**

В Test Runner: `AssetRegistryTests` — FAIL (компиляция или null reference, потому что `AssetRegistry` ещё не зарегистрирован). Если тест PASS — нормально, переходим дальше; если FAIL по компиляции — починить стабы.

- [ ] **Step 4.5: Зарегистрировать AssetRegistry в RootLifetimeScope**

В `RootLifetimeScope.cs` после строки `builder.Register<SavedAssetLibrary>(Lifetime.Singleton);` добавить:

```csharp
builder.Register<AssetRegistry>(Lifetime.Singleton).As<IAssetRegistry>();
```

- [ ] **Step 4.6: Re-run AssetRegistryTests — should PASS**

В Test Runner — оба теста зелёные.

- [ ] **Step 4.7: Suggested commit**

```
feat(asset-browser): add AssetRegistry to resolve AssetRef → ILabAsset across libraries
```

---

### Task 5: SelectionManager multi-select rewrite + тесты

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/SelectionManager.cs`
- Create: `Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs`

- [ ] **Step 5.1: Написать failing tests**

`Subsystems/SceneComposition/Tests/SelectionManagerTests.cs`:

```csharp
using NUnit.Framework;

public class SelectionManagerTests
{
    private EventBus _bus;
    private SelectionManager _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _sut = new SelectionManager(_bus);
    }

    [Test]
    public void Toggle_FirstCall_AddsAndSetsActive()
    {
        _sut.Toggle("a");
        Assert.AreEqual("a", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_SecondDifferent_AddsAndSetsActiveToSecond()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        Assert.AreEqual("b", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a", "b" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_ExistingActive_RemovesAndActivatesLastRemaining()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Toggle("b");                // удалили активный
        Assert.AreEqual("a", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_ExistingNonActive_RemovesKeepsActive()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Toggle("a");                // удалили не-активный
        Assert.AreEqual("b", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "b" }, _sut.SelectedIds);
    }

    [Test]
    public void Clear_EmptiesSelectionAndActive()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Clear();
        Assert.IsNull(_sut.ActiveId);
        Assert.AreEqual(0, _sut.SelectedIds.Count);
    }

    [Test]
    public void Select_ReplacesWholeSelectionWithSingle()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Select("c");
        Assert.AreEqual("c", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "c" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_PublishesSelectionChangedEvent()
    {
        SelectionChangedEvent received = default;
        bool fired = false;
        _bus.Subscribe<SelectionChangedEvent>(e => { received = e; fired = true; });
        _sut.Toggle("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("a", received.SelectedNodeId);
        CollectionAssert.AreEqual(new[] { "a" }, received.SelectedNodeIds);
    }
}
```

- [ ] **Step 5.2: Run tests, verify FAIL**

Test Runner: `SelectionManagerTests` — все FAIL (методы отсутствуют, либо новый интерфейс не реализован).

- [ ] **Step 5.3: Переписать SelectionManager**

Файл целиком:

```csharp
using System;
using System.Collections.Generic;
using VContainer.Unity;

public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus     _bus;
    private readonly List<string> _selected = new();   // List сохраняет порядок вставки
    private string _active;

    public IReadOnlyList<string> SelectedIds   => _selected;
    public string                ActiveId      => _active;
    public string                SelectedNodeId => _active;     // back-compat

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()   { }
    public void Dispose() { }

    public void Toggle(string nodeId)
    {
        var idx = _selected.IndexOf(nodeId);
        if (idx >= 0)
        {
            _selected.RemoveAt(idx);
            if (_active == nodeId)
                _active = _selected.Count == 0 ? null : _selected[^1];
        }
        else
        {
            _selected.Add(nodeId);
            _active = nodeId;
        }
        Publish();
    }

    public void Select(string nodeId)
    {
        _selected.Clear();
        _selected.Add(nodeId);
        _active = nodeId;
        Publish();
    }

    public void Clear()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        _active = null;
        Publish();
    }

    private void Publish() =>
        _bus.Publish(new SelectionChangedEvent
        {
            SelectedNodeId  = _active,
            SelectedNodeIds = _selected.ToArray(),
        });
}
```

- [ ] **Step 5.4: Run tests, verify PASS**

Test Runner: все `SelectionManagerTests` зелёные.

- [ ] **Step 5.5: Suggested commit**

```
feat(scene-composition): SelectionManager supports multi-select via Toggle/Clear/ActiveId
```

---

### Task 6: NodeData + SceneData v2 + SceneSerializer миграция + тесты

**Files:**
- Create: `Assets/_App/Subsystems/StorageCore/Data/NodeData.cs`
- Modify: `Assets/_App/Subsystems/StorageCore/Data/SceneData.cs`
- Modify: `Assets/_App/Subsystems/StorageCore/SceneSerializer.cs`
- Modify: `Assets/_App/Subsystems/StorageCore/Tests/SceneSerializerTests.cs`

- [ ] **Step 6.1: Создать NodeData**

```csharp
using System;
using UnityEngine;

[Serializable]
public class NodeData
{
    public string     NodeId;
    public AssetRef   AssetRef;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
    public string     DisplayName;
    public string     ParentNodeId;   // null = root
}
```

- [ ] **Step 6.2: Обновить SceneData до v2**

Заменить файл целиком:

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class SceneData
{
    public int            SchemaVersion = 2;
    public string         SceneId;
    public string         DisplayName;
    public string         CreatedAt;
    public List<NodeData> Nodes = new();
}
```

- [ ] **Step 6.3: Обновить SceneSerializer с миграцией**

```csharp
using System.Collections.Generic;
using UnityEngine;

public static class SceneSerializer
{
    public static string Serialize(SceneData data) =>
        JsonUtility.ToJson(data, prettyPrint: true);

    public static SceneData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var data = JsonUtility.FromJson<SceneData>(json);
        if (data == null) return null;
        if (data.SchemaVersion < 2)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v2");
            data.SchemaVersion = 2;
            data.Nodes ??= new List<NodeData>();
        }
        return data;
    }
}
```

- [ ] **Step 6.4: Обновить SceneSerializerTests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class SceneSerializerTests
{
    [Test]
    public void Serialize_ThenDeserialize_RoundTripsV2Fields()
    {
        var original = new SceneData
        {
            SceneId     = "scene-42",
            DisplayName = "My Scene",
            CreatedAt   = "2026-05-14",
        };
        original.Nodes.Add(new NodeData
        {
            NodeId      = "n1",
            AssetRef    = new AssetRef { Source = AssetSource.Builtin, AssetId = "chair" },
            Position    = new Vector3(1, 2, 3),
            Rotation    = Quaternion.Euler(0, 90, 0),
            Scale       = Vector3.one,
            DisplayName = "Chair 1",
        });

        var json   = SceneSerializer.Serialize(original);
        var result = SceneSerializer.Deserialize(json);

        Assert.AreEqual("scene-42", result.SceneId);
        Assert.AreEqual("My Scene", result.DisplayName);
        Assert.AreEqual(2,          result.SchemaVersion);
        Assert.AreEqual(1,          result.Nodes.Count);
        Assert.AreEqual("n1",       result.Nodes[0].NodeId);
        Assert.AreEqual(AssetSource.Builtin, result.Nodes[0].AssetRef.Source);
        Assert.AreEqual("chair",    result.Nodes[0].AssetRef.AssetId);
        Assert.AreEqual("Chair 1",  result.Nodes[0].DisplayName);
    }

    [Test]
    public void Deserialize_NullJson_ReturnsNull()
    {
        Assert.IsNull(SceneSerializer.Deserialize(null));
    }

    [Test]
    public void Deserialize_V1Json_MigratesToV2WithEmptyNodes()
    {
        // Synthesize v1 JSON (без поля Nodes, со старым NodeIds — JsonUtility просто проигнорит)
        var v1Json = "{ \"SchemaVersion\": 1, \"SceneId\": \"old\", \"DisplayName\": \"Old\", \"CreatedAt\": \"2024-01-01\" }";
        var result = SceneSerializer.Deserialize(v1Json);

        Assert.AreEqual(2, result.SchemaVersion);
        Assert.IsNotNull(result.Nodes);
        Assert.AreEqual(0, result.Nodes.Count);
    }
}
```

- [ ] **Step 6.5: Run tests, verify PASS**

Test Runner: `SceneSerializerTests` — все три зелёные.

- [ ] **Step 6.6: Suggested commit**

```
feat(storage-core): SceneData v2 with NodeData[], SceneSerializer migrates v1→v2
```

---

### Task 7: ISceneGraph + SceneNode extension

**Files:**
- Modify: `Assets/_App/_Shared/Interfaces/ISceneGraph.cs`
- Modify: `Assets/_App/Subsystems/SceneComposition/SceneNode.cs`

- [ ] **Step 7.1: Обновить ISceneGraph**

Заменить файл целиком (старая `AddNode(GameObject)` УДАЛЕНА — все вызывающие должны передавать `AssetRef`):

```csharp
using UnityEngine;

public interface ISceneGraph
{
    GameObject GetNode(string nodeId);
    void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null);
    void RemoveNode(string nodeId);
}
```

- [ ] **Step 7.2: Расширить SceneNode**

Заменить файл целиком:

```csharp
using UnityEngine;

public class SceneNode : MonoBehaviour
{
    public string   NodeId      { get; private set; }
    public AssetRef AssetRef    { get; private set; }
    public string   DisplayName { get; private set; }
    public bool     IsVisible   { get; private set; } = true;
    public bool     IsLocked    { get; private set; }

    public void Init(string nodeId, AssetRef assetRef, string displayName)
    {
        NodeId      = nodeId;
        AssetRef    = assetRef;
        DisplayName = displayName;
    }

    public void SetDisplayName(string name)
    {
        DisplayName = name;
        gameObject.name = name;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => IsLocked = locked;
}
```

- [ ] **Step 7.3: Проверить компиляцию**

На этом шаге Console покажет МНОГО ошибок:
- `SceneGraph` — старый код `_node.Init(nodeId)` (1 параметр) — будет исправлен в Task 8.
- `AssetSpawner` — старый `_graph.AddNode(go)` — Task 9.
- `AssetImporter` — старый `_sceneGraph.AddNode(instance)` — Task 10.

**Это ожидаемые компиляционные ошибки.** Не коммитим этот шаг отдельно — продолжаем дальше.

---

### Task 8: SceneGraph rewrite (spawnedRoot, rebuild, snapshot)

**Files:**
- Modify: `Assets/_App/Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef` (add StorageCore ref)
- Modify: `Assets/_App/Subsystems/SceneComposition/SceneGraph.cs`
- Create: `Assets/_App/Subsystems/SceneComposition/Tests/SceneGraphTests.cs`

- [ ] **Step 8.0: Обновить Subsystems.SceneComposition.asmdef**

`SceneGraph` теперь принимает `AppStorage` (тип из `Subsystems.StorageCore`), `SceneAutoSaver` тоже. Без этой ссылки компиляция упадёт. Заменить файл:

```json
{ "name": "Subsystems.SceneComposition", "references": ["_Shared", "VContainer", "Unity.TextMeshPro", "Subsystems.StorageCore"], "autoReferenced": false }
```


- [ ] **Step 8.1: Переписать SceneGraph полностью**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class SceneGraph : ISceneGraph, IStartable, IDisposable
{
    private readonly EventBus             _bus;
    private readonly IAssetRegistry       _registry;
    private readonly IInteractableFactory _factory;
    private readonly AppStorage           _storage;
    private readonly Dictionary<string, SceneNode> _nodes = new();

    private Transform _spawnedRoot;

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public SceneGraph(EventBus bus, IAssetRegistry registry, IInteractableFactory factory, AppStorage storage)
    {
        _bus      = bus;
        _registry = registry;
        _factory  = factory;
        _storage  = storage;
    }

    public void Start()
    {
        _spawnedRoot = new GameObject("[Spawned]").transform;
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);
    }

    public void Dispose() =>
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);

    public SceneNode AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        return AddNodeInternal(go, nodeId, assetRef, displayName, parentId, isLoad: false);
    }

    // Explicit interface implementation — same signature
    void ISceneGraph.AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId) =>
        AddNode(go, assetRef, displayName, parentId);

    public void RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        _nodes.Remove(nodeId);
        UnityEngine.Object.Destroy(node.gameObject);
        _bus.Publish(new SceneModifiedEvent());
    }

    public SceneNode GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var n) ? n : null;

    // Explicit ISceneGraph.GetNode returns GameObject
    GameObject ISceneGraph.GetNode(string nodeId) => GetNode(nodeId)?.gameObject;

    private SceneNode AddNodeInternal(GameObject go, string nodeId, AssetRef assetRef,
                                       string displayName, string parentId, bool isLoad)
    {
        go.transform.SetParent(_spawnedRoot, worldPositionStays: true);
        var node = go.AddComponent<SceneNode>();
        node.Init(nodeId, assetRef, displayName);
        if (!string.IsNullOrEmpty(displayName)) go.name = displayName;
        _nodes[nodeId] = node;
        if (!isLoad) _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    private void OnSceneOpened(SceneOpenedEvent e) => _ = OnSceneOpenedAsync(e);

    private async Task OnSceneOpenedAsync(SceneOpenedEvent e)
    {
        try
        {
            ClearAll();
            var data = await _storage.LoadSceneAsync(e.SceneId, CancellationToken.None);
            if (data?.Nodes == null) return;

            // 1-й проход: спавнить все ноды (без parenting)
            foreach (var nd in data.Nodes)
            {
                var asset = _registry.Find(nd.AssetRef);
                if (asset == null)
                {
                    Debug.LogWarning($"SceneGraph: asset not found {nd.AssetRef}");
                    continue;
                }
                GameObject go;
                try
                {
                    go = await asset.SpawnAsync(nd.Position, nd.Rotation, CancellationToken.None);
                }
                catch (NotImplementedException)
                {
                    Debug.LogWarning($"SceneGraph: SpawnAsync not implemented for {nd.AssetRef} (likely Imported/Saved before Spec B)");
                    continue;
                }
                go.transform.localScale = nd.Scale;
                _factory.MakeInteractable(go, asset.Capabilities);
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
            }

            // 2-й проход: расставить parents
            foreach (var nd in data.Nodes)
            {
                if (string.IsNullOrEmpty(nd.ParentNodeId)) continue;
                if (_nodes.TryGetValue(nd.NodeId, out var child)
                    && _nodes.TryGetValue(nd.ParentNodeId, out var parent))
                {
                    child.transform.SetParent(parent.transform, worldPositionStays: true);
                }
            }
            _bus.Publish(new SceneModifiedEvent());
        }
        catch (Exception ex)
        {
            Debug.LogError($"SceneGraph.OnSceneOpenedAsync failed for '{e.SceneId}': {ex}");
        }
    }

    private void ClearAll()
    {
        _nodes.Clear();
        if (_spawnedRoot != null)
        {
            foreach (Transform t in _spawnedRoot)
                UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    public SceneData CaptureSnapshot(string sceneId, string displayName, string createdAt)
    {
        var data = new SceneData
        {
            SchemaVersion = 2,
            SceneId       = sceneId,
            DisplayName   = displayName,
            CreatedAt     = createdAt,
        };
        foreach (var pair in _nodes)
        {
            var id   = pair.Key;
            var node = pair.Value;
            string parentId = null;
            if (node.transform.parent != null && node.transform.parent != _spawnedRoot)
            {
                var pn = node.transform.parent.GetComponent<SceneNode>();
                if (pn != null) parentId = pn.NodeId;
            }
            data.Nodes.Add(new NodeData
            {
                NodeId       = id,
                AssetRef     = node.AssetRef,
                Position     = node.transform.position,
                Rotation     = node.transform.rotation,
                Scale        = node.transform.localScale,
                DisplayName  = node.DisplayName,
                ParentNodeId = parentId,
            });
        }
        return data;
    }
}
```

- [ ] **Step 8.2: Написать SceneGraphTests (только pure-CS методы)**

```csharp
using NUnit.Framework;
using UnityEngine;

public class SceneGraphTests
{
    [Test]
    public void CaptureSnapshot_EmptyGraph_ReturnsV2WithEmptyNodes()
    {
        // SceneGraph требует IAssetRegistry/IInteractableFactory/AppStorage — для CaptureSnapshot достаточно null + bus
        // CaptureSnapshot не использует injected services, поэтому стабы могут быть null
        var bus = new EventBus();
        var sut = new SceneGraph(bus, null, null, null);

        var snap = sut.CaptureSnapshot("scene-1", "Scene", "2026-05-17");

        Assert.AreEqual(2,         snap.SchemaVersion);
        Assert.AreEqual("scene-1", snap.SceneId);
        Assert.AreEqual("Scene",   snap.DisplayName);
        Assert.IsNotNull(snap.Nodes);
        Assert.AreEqual(0,         snap.Nodes.Count);
    }
}
```

- [ ] **Step 8.3: Зарегистрировать SceneGraph с новыми зависимостями**

`SceneGraph` уже зарегистрирован в `VrEditingSceneScope` и `SandboxSceneScope`. Но теперь его конструктор требует `IAssetRegistry`, `IInteractableFactory`, `AppStorage`. Все они уже зарегистрированы (Registry — в Task 4, factory — `SelectionInteractorFactory` через `AsImplementedInterfaces`, AppStorage — в RootLifetimeScope). Изменения в scope не нужны.

- [ ] **Step 8.4: Run tests, verify PASS**

Test Runner: `SceneGraphTests.CaptureSnapshot_EmptyGraph_ReturnsV2WithEmptyNodes` — зелёный. `AssetRegistryTests`, `SelectionManagerTests`, `SceneSerializerTests` — все ещё зелёные.

- [ ] **Step 8.5: Проверить компиляцию**

Console: ошибки про `AssetSpawner.AddNode` и `AssetImporter` сохраняются — это норма, починим в Task 9, 10.

- [ ] **Step 8.6: Suggested commit**

(commit можно отложить до Task 10, когда компиляция станет чистой; либо закоммитить с пометкой WIP)

```
feat(scene-composition): rewrite SceneGraph with [Spawned] root, rebuild on SceneOpened, snapshot capture (WIP — AssetSpawner update follows)
```

---

### Task 9: AssetSpawner update

**Files:**
- Modify: `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs`

- [ ] **Step 9.1: Переписать AssetSpawner**

```csharp
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus              _bus;
    private readonly SceneGraph            _graph;
    private readonly IInteractableFactory  _factory;

    public AssetSpawner(EventBus bus, SceneGraph graph, IInteractableFactory factory)
    {
        _bus     = bus;
        _graph   = graph;
        _factory = factory;
    }

    public void Start() =>
        _bus.Subscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    public void Dispose() =>
        _bus.Unsubscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
        _ = SpawnCoreAsync(e);

    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var go = await e.Asset.SpawnAsync(e.Position, e.Rotation, CancellationToken.None);
            _factory.MakeInteractable(go, e.Asset.Capabilities);
            var assetRef = new AssetRef
            {
                Source  = e.Asset is BuiltinLabAsset  ? AssetSource.Builtin
                        : e.Asset is ImportedLabAsset ? AssetSource.Imported
                        : AssetSource.Saved,
                AssetId = e.Asset.Id,
            };
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }
}
```

- [ ] **Step 9.2: Проверить компиляцию**

Ошибка остаётся только в `AssetImporter` — Task 10.

---

### Task 10: AssetImporter legacy cleanup

**Files:**
- Modify: `Assets/_App/Subsystems/AssetBrowser/AssetImporter.cs`

- [ ] **Step 10.1: Убрать строки с _interactableFactory и _sceneGraph**

В `AssetImporter.cs`:
- Удалить `_interactableFactory.MakeInteractable(instance);`
- Удалить `_sceneGraph.AddNode(instance);`
- Поля `_sceneGraph` и `_interactableFactory` можно оставить (могут использоваться в будущем), но безопаснее удалить вместе с параметрами конструктора, чтобы не висели мёртвые ссылки.

Финальная версия (минимальная):

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AssetImporter
{
    private readonly DemoAssetCatalog _catalog;
    private readonly AppStorage       _storage;

    public AssetImporter(DemoAssetCatalog catalog, AppStorage storage)
    {
        _catalog = catalog;
        _storage = storage;
    }

    public async Task<(GameObject Instance, AssetEntry Entry)> ImportAsync(
        string filePath, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(filePath);

        if (!_catalog.TryFind(fileName, out var demoEntry))
        {
            Debug.LogWarning($"AssetImporter: '{fileName}' not in DemoAssetCatalog");
            return (null, default);
        }

        await Task.Yield();

        var instance = UnityEngine.Object.Instantiate(
            demoEntry.Prefab, Vector3.zero, Quaternion.identity);
        instance.name = Path.GetFileNameWithoutExtension(fileName);

        var assetEntry = new AssetEntry
        {
            AssetId      = Guid.NewGuid().ToString("N")[..8],
            Type         = demoEntry.Type,
            DisplayName  = instance.name,
            RelativePath = $"Models/{fileName}",
            Icon         = demoEntry.Icon,
        };

        return (instance, assetEntry);
    }
}
```

- [ ] **Step 10.2: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 10.3: Run all tests, verify GREEN**

Test Runner: `AssetRegistryTests`, `SelectionManagerTests`, `SceneSerializerTests`, `SceneGraphTests`, `CommandStackTests`, `PathProviderTests` — все зелёные.

- [ ] **Step 10.4: Suggested commit**

```
refactor(asset-browser): remove legacy graph/factory wiring from AssetImporter; AssetSpawner passes AssetRef + capabilities
```

---

### Task 11: SceneAutoSaver + DI registration

**Files:**
- Create: `Assets/_App/Subsystems/SceneComposition/SceneAutoSaver.cs`
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`

- [ ] **Step 11.1: Создать SceneAutoSaver**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class SceneAutoSaver : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;
    private readonly AppStorage _storage;

    public SceneAutoSaver(EventBus bus, SceneGraph graph, AppStorage storage)
    {
        _bus     = bus;
        _graph   = graph;
        _storage = storage;
    }

    public void Start()   => _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    public void Dispose() => _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    private void OnModeChanged(ModeChangedEvent e)
    {
        if (e.PreviousMode == AppMode.VrEditing && e.CurrentMode != AppMode.VrEditing)
            _ = SaveCurrentAsync();
    }

    private async Task SaveCurrentAsync()
    {
        try
        {
            var activeId = _storage.ActiveSceneId;
            if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;
            var cached = await _storage.LoadSceneAsync(activeId, CancellationToken.None);
            if (cached == null) return;
            var snap = _graph.CaptureSnapshot(activeId, cached.DisplayName, cached.CreatedAt);
            await _storage.SaveSceneAsync(snap, CancellationToken.None);
            _bus.Publish(new SceneClosedEvent());
        }
        catch (Exception ex)
        {
            Debug.LogError($"SceneAutoSaver failed: {ex}");
        }
    }
}
```

- [ ] **Step 11.2: Зарегистрировать в VrEditingSceneScope**

В `VrEditingSceneScope.Configure()`, после строки регистрации `SceneGraph`, добавить:

```csharp
builder.Register<SceneAutoSaver>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

(`AsImplementedInterfaces` нужен для активации `IStartable`/`IDisposable`.)

В Sandbox **НЕ добавляем** — Sandbox не сохраняется.

- [ ] **Step 11.3: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 11.4: Suggested commit**

```
feat(scene-composition): SceneAutoSaver writes SceneData on VrEditing→other mode transition
```

---

## Phase 3: Interactor pipeline

### Task 12: IInteractableFactory signature + SelectionInteractorFactory + Selectable

**Files:**
- Modify: `Assets/_App/_Shared/Interfaces/IInteractableFactory.cs`
- Create: `Assets/_App/Subsystems/VrInteraction/Selectable.cs`
- Modify: `Assets/_App/Subsystems/VrInteraction/SelectionInteractorFactory.cs`

- [ ] **Step 12.1: Обновить IInteractableFactory**

```csharp
using UnityEngine;

public interface IInteractableFactory
{
    void MakeInteractable(GameObject go, AssetCapabilities capabilities);
}
```

- [ ] **Step 12.2: Создать Selectable**

Файл `Assets/_App/Subsystems/VrInteraction/Selectable.cs`:

```csharp
using UnityEngine;

public class Selectable : MonoBehaviour
{
    private SceneNode _node;
    private Outline   _outline;

    public string NodeId => _node?.NodeId;
    public SceneNode Node => _node;

    public void Init(SceneNode node) => _node = node;

    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.InSet:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.55f, 0f);
                _outline.OutlineWidth = 4f;
                break;
            case SelectionVisual.Active:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth = 6f;
                break;
        }
    }

    private void EnsureOutline()
    {
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
    }
}
```

**Важно:** `Outline` — класс из QuickOutline пакета. Чтобы он был виден из сборки `Subsystems.VrInteraction`, надо создать `QuickOutline.asmdef` (Task 13.5) и сослаться на неё.

- [ ] **Step 12.3: Обновить SelectionInteractorFactory**

```csharp
using UnityEngine;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly ISelectionManager _selectionManager;

    public SelectionInteractorFactory(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public void MakeInteractable(GameObject go, AssetCapabilities capabilities)
    {
        if ((capabilities & AssetCapabilities.Selectable) == 0)
            return;

        if (go.GetComponentInChildren<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var sn = go.GetComponent<SceneNode>();
        var sel = go.GetComponent<Selectable>() ?? go.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        var si = go.GetComponent<SelectionInteractor>() ?? go.AddComponent<SelectionInteractor>();
        si.Construct(_selectionManager);
    }
}
```

**Замечание:** `SceneNode` добавляется ДО `Selectable.Init` — но фабрика вызывается из `AssetSpawner` ПЕРЕД `_graph.AddNode`. То есть в момент `MakeInteractable` ноды ещё нет. Решение: фабрика добавляет `Selectable` без `Init`; `SceneGraph.AddNodeInternal` после `AddComponent<SceneNode>` вызывает `selectable.Init(node)`. Обновим Task 14 для этого.

Альтернативно (проще): в `MakeInteractable` искать SceneNode уже после; в текущем порядке (factory → AddNode) лучше добавить отдельный шаг "wire selectable to node" в SceneGraph.

- [ ] **Step 12.4: Обновить SceneGraph.AddNodeInternal для wire**

В `SceneGraph.AddNodeInternal` после `node.Init(...)` добавить:

```csharp
var selectable = go.GetComponent<Selectable>();
if (selectable != null) selectable.Init(node);
```

- [ ] **Step 12.5: Проверить компиляцию**

Console будет ошибка про `Outline` (тип неизвестен) — это разрешится в Task 13 после QuickOutline.asmdef.

---

### Task 13: QuickOutline asmdef + VrInteraction ref

**Files:**
- Create: `Assets/UnityPacks/QuickOutline/Scripts/QuickOutline.asmdef`
- Modify: `Assets/_App/Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef`

- [ ] **Step 13.1: Создать QuickOutline.asmdef**

```json
{
    "name": "QuickOutline",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 13.2: Добавить QuickOutline в Subsystems.VrInteraction.asmdef**

Прочитать существующий файл, добавить `"QuickOutline"` в массив `references`. Полный JSON должен включать существующие refs (XR Toolkit и т.д.) + `"QuickOutline"`.

- [ ] **Step 13.3: Проверить компиляцию**

Console: 0 errors. `Outline` теперь виден из VrInteraction.

- [ ] **Step 13.4: Suggested commit**

```
feat(vr-interaction): Selectable component + capability-aware factory; QuickOutline.asmdef
```

---

### Task 14: SelectionInteractor → Toggle

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/SelectionInteractor.cs`

- [ ] **Step 14.1: Обновить OnSelectEntered**

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VContainer;

[RequireComponent(typeof(Collider))]
public class SelectionInteractor : XRSimpleInteractable
{
    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        var selectable = GetComponentInParent<Selectable>();
        if (selectable != null && _selectionManager != null)
            _selectionManager.Toggle(selectable.NodeId);
    }
}
```

- [ ] **Step 14.2: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 14.3: Suggested commit**

```
feat(vr-interaction): SelectionInteractor uses SelectionManager.Toggle for multi-select
```

---

### Task 15: SelectionVisualSync + DI

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs`
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/SandboxSceneScope.cs`

- [ ] **Step 15.1: Создать SelectionVisualSync**

```csharp
using System;
using System.Collections.Generic;
using VContainer.Unity;

public class SelectionVisualSync : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;

    public SelectionVisualSync(EventBus bus, SceneGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void Start()   => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        var activeId = e.SelectedNodeId;
        var set      = e.SelectedNodeIds == null ? new HashSet<string>() : new HashSet<string>(e.SelectedNodeIds);
        foreach (var pair in _graph.Nodes)
        {
            var sel = pair.Value.GetComponent<Selectable>();
            if (sel == null) continue;
            var state = pair.Key == activeId
                ? SelectionVisual.Active
                : set.Contains(pair.Key) ? SelectionVisual.InSet
                                         : SelectionVisual.None;
            sel.SetVisualState(state);
        }
    }
}
```

- [ ] **Step 15.2: Зарегистрировать в обоих scene scopes**

В `VrEditingSceneScope.Configure()` и `SandboxSceneScope.Configure()` добавить:

```csharp
builder.Register<SelectionVisualSync>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 15.3: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 15.4: Suggested commit**

```
feat(vr-interaction): SelectionVisualSync applies outline colors via QuickOutline on selection change
```

---

### Task 16: WorldClickCatcher + DI

**Files:**
- Create: `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs`
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/SandboxSceneScope.cs`

- [ ] **Step 16.1: Создать WorldClickCatcher**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class WorldClickCatcher : MonoBehaviour
{
    [SerializeField] private XRRayInteractor _leftRay;
    [SerializeField] private XRRayInteractor _rightRay;
    [SerializeField] private InputActionReference _leftSelectAction;
    [SerializeField] private InputActionReference _rightSelectAction;

    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager) => _selectionManager = selectionManager;

    private void OnEnable()
    {
        if (_leftSelectAction != null)  _leftSelectAction.action.performed  += OnLeft;
        if (_rightSelectAction != null) _rightSelectAction.action.performed += OnRight;
    }

    private void OnDisable()
    {
        if (_leftSelectAction != null)  _leftSelectAction.action.performed  -= OnLeft;
        if (_rightSelectAction != null) _rightSelectAction.action.performed -= OnRight;
    }

    private void OnLeft(InputAction.CallbackContext _)  => CheckRay(_leftRay);
    private void OnRight(InputAction.CallbackContext _) => CheckRay(_rightRay);

    private void CheckRay(XRRayInteractor ray)
    {
        if (ray == null || _selectionManager == null) return;
        if (ray.TryGetCurrent3DRaycastHit(out var hit))
        {
            if (hit.collider.GetComponentInParent<Selectable>() == null
                && hit.collider.GetComponentInParent<UnityEngine.UI.Graphic>() == null)
                _selectionManager.Clear();
        }
        else
        {
            _selectionManager.Clear();
        }
    }
}
```

- [ ] **Step 16.2: Зарегистрировать поиск + инъекцию в scene scopes**

В `VrEditingSceneScope.Configure()` и `SandboxSceneScope.Configure()` добавить:

```csharp
var catcher = Object.FindAnyObjectByType<WorldClickCatcher>(FindObjectsInactive.Include);
if (catcher != null)
    builder.RegisterBuildCallback(c => c.Inject(catcher));
```

- [ ] **Step 16.3: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 16.4: Suggested commit**

```
feat(vr-interaction): WorldClickCatcher clears selection on miss/UI-miss
```

---

## Phase 4: Outliner / Inspector UI

### Task 17: UserPanel.SwapContext scope-aware inject

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs:205-225`

- [ ] **Step 17.1: Переписать SwapContext**

Заменить метод `SwapContext`:

```csharp
private void SwapContext(AppMode mode)
{
    if (_currentContext != null)
    {
        Destroy(_currentContext);
        _currentContext = null;
    }
    if (_contextSlot == null) return;

    foreach (var entry in _contextMenus)
    {
        if (entry.Mode == mode && entry.Prefab != null)
        {
            _currentContext = Instantiate(entry.Prefab, _contextSlot);

            VContainer.Unity.LifetimeScope scope = mode switch
            {
                AppMode.VrEditing => VContainer.Unity.LifetimeScope.Find<VrEditingSceneScope>(),
                AppMode.Sandbox   => VContainer.Unity.LifetimeScope.Find<SandboxSceneScope>(),
                _                 => VContainer.Unity.LifetimeScope.Find<RootLifetimeScope>(),
            };
            scope?.Container.InjectGameObject(_currentContext);

            _currentContext.transform.localPosition = Vector3.zero;
            _currentContext.transform.localRotation = Quaternion.identity;
            break;
        }
    }
}
```

`UserPanel.cs` сейчас в asmdef `_App` (через включение в `_App` папку). `VrEditingSceneScope` и `SandboxSceneScope` — тоже в `_App` (Bootstrap). Доступ есть без новых references.

- [ ] **Step 17.2: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 17.3: Suggested commit**

```
feat(spatial-ui): UserPanel.SwapContext injects via scene-aware LifetimeScope
```

---

### Task 18: SceneOutlinerRow component

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneOutlinerRow.cs`

- [ ] **Step 18.1: Создать SceneOutlinerRow**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SceneOutlinerRow : MonoBehaviour
{
    [SerializeField] private TMP_Text      _label;
    [SerializeField] private Image         _highlight;
    [SerializeField] private LayoutElement _indentSpacer;
    [SerializeField] private Button        _button;

    public string NodeId { get; private set; }

    public void Bind(SceneNode node, float indentPx, Action onClick)
    {
        NodeId = node.NodeId;
        _label.text = node.DisplayName;
        if (_indentSpacer != null) _indentSpacer.preferredWidth = indentPx;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetVisualState(SelectionVisual state)
    {
        if (_highlight == null) return;
        _highlight.enabled = state != SelectionVisual.None;
        _highlight.color = state switch
        {
            SelectionVisual.Active => new Color(1f, 0.95f, 0.15f, 0.35f),
            SelectionVisual.InSet  => new Color(1f, 0.55f,  0f,   0.25f),
            _                      => Color.clear,
        };
    }
}
```

- [ ] **Step 18.2: Проверить компиляцию**

Console: 0 errors.

---

### Task 19: SceneOutlinerView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneOutlinerView.cs`

- [ ] **Step 19.1: Создать SceneOutlinerView**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public class SceneOutlinerView : MonoBehaviour
{
    [SerializeField] private Transform        _rowsRoot;
    [SerializeField] private SceneOutlinerRow _rowPrefab;
    [SerializeField] private float            _indentPx = 16f;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
    {
        _bus       = bus;
        _graph     = graph;
        _selection = selection;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        Rebuild();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnModified(SceneModifiedEvent _)              => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _)   => ApplyHighlight();

    private void Rebuild()
    {
        if (_rowsRoot == null || _rowPrefab == null || _graph == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

        var byParent = new Dictionary<string, List<SceneNode>>();
        foreach (var pair in _graph.Nodes)
        {
            var p = GetParentId(pair.Value) ?? "";
            if (!byParent.TryGetValue(p, out var list))
                byParent[p] = list = new List<SceneNode>();
            list.Add(pair.Value);
        }
        AddRowsRecursive(null, 0, byParent);
        ApplyHighlight();
    }

    private string GetParentId(SceneNode n)
    {
        var p = n.transform.parent;
        if (p == null) return null;
        var pn = p.GetComponent<SceneNode>();
        return pn != null ? pn.NodeId : null;
    }

    private void AddRowsRecursive(string parentId, int depth,
                                   Dictionary<string, List<SceneNode>> byParent)
    {
        if (!byParent.TryGetValue(parentId ?? "", out var children)) return;
        foreach (var node in children)
        {
            var row = Instantiate(_rowPrefab, _rowsRoot);
            row.Bind(node, depth * _indentPx, () => _selection.Toggle(node.NodeId));
            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

    private void ApplyHighlight()
    {
        if (_rowsRoot == null || _selection == null) return;
        var active = _selection.ActiveId;
        var set    = new HashSet<string>(_selection.SelectedIds);
        foreach (var row in _rowsRoot.GetComponentsInChildren<SceneOutlinerRow>())
        {
            var state = row.NodeId == active ? SelectionVisual.Active
                      : set.Contains(row.NodeId) ? SelectionVisual.InSet
                                                 : SelectionVisual.None;
            row.SetVisualState(state);
        }
    }
}
```

- [ ] **Step 19.2: Проверить компиляцию**

Console: 0 errors.

---

### Task 20: SceneInspectorView

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SceneInspectorView.cs`

- [ ] **Step 20.1: Создать SceneInspectorView**

```csharp
using TMPro;
using UnityEngine;
using VContainer;

public class SceneInspectorView : MonoBehaviour
{
    [SerializeField] private GameObject     _emptyState;
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
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
    {
        _bus       = bus;
        _graph     = graph;
        _selection = selection;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null) _nameField.onEndEdit.AddListener(OnNameChanged);
        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null) _nameField.onEndEdit.RemoveListener(OnNameChanged);
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void Refresh()
    {
        if (_selection == null || _graph == null) return;
        var activeId = _selection.ActiveId;
        _bound = string.IsNullOrEmpty(activeId) ? null : _graph.GetNode(activeId);
        var has = _bound != null;
        if (_emptyState != null) _emptyState.SetActive(!has);
        if (_content    != null) _content.SetActive(has);
        if (!has) return;
        if (_nameField     != null) _nameField.SetTextWithoutNotify(_bound.DisplayName);
        if (_typeLabel     != null) _typeLabel.text     = $"Type: {_bound.AssetRef}";
        if (_positionLabel != null) _positionLabel.text = $"Pos: {_bound.transform.position:F2}";
        if (_rotationLabel != null) _rotationLabel.text = $"Rot: {_bound.transform.rotation.eulerAngles:F1}";
        if (_scaleLabel    != null) _scaleLabel.text    = $"Scale: {_bound.transform.localScale:F2}";
    }

    private void OnNameChanged(string newName)
    {
        if (_bound == null || string.IsNullOrWhiteSpace(newName)) return;
        _bound.SetDisplayName(newName.Trim());
        _bus?.Publish(new SceneModifiedEvent());
    }
}
```

- [ ] **Step 20.2: Проверить компиляцию**

Console: 0 errors.

- [ ] **Step 20.3: Suggested commit**

```
feat(spatial-ui): SceneOutlinerView + SceneOutlinerRow + SceneInspectorView (UI components)
```

---

## Phase 5: Ручная работа в Unity Editor

> Все шаги ниже выполняются **вручную в Unity Editor**. Агент не может их автоматизировать (prefab-edits в Unity требуют интерактивной правки). Каждый шаг — отдельный коммит после ручной правки.

### Task 21: MANUAL — Set Capabilities на BuiltinAssetLibrary entries

**Files:**
- Manual edit: `Assets/_App/DemoAssets/BuiltinAssetLibrary.asset` (или где он лежит — найти через Project view)

- [ ] **Step 21.1:** В Project view найти `BuiltinAssetLibrary.asset` (через Find: `t:BuiltinAssetLibrary`).
- [ ] **Step 21.2:** Кликнуть на ассет. В Inspector у каждой entry в `_entries` появилось поле `Capabilities`. Выставить `Selectable | Movable` (оба галки).
- [ ] **Step 21.3:** Сохранить (Ctrl+S).
- [ ] **Step 21.4:** Suggested commit:

```
chore(demo-assets): set Selectable|Movable capabilities on all builtin entries
```

---

### Task 22: MANUAL — Создать SceneOutlinerRow prefab

**Files:**
- Manual create: `Assets/_App/Subsystems/SpatialUi/Prefabs/Rows/SceneOutlinerRow.prefab`

- [ ] **Step 22.1:** В сцене Bootstrap создать UI > Button. Переименовать в `SceneOutlinerRow`.
- [ ] **Step 22.2:** Внутри Button:
  - Добавить пустой child `Indent` с компонентом `LayoutElement` (preferredWidth = 0).
  - Добавить `Image` (overlay для highlight) — enabled = false, color = Clear.
  - Добавить TMP > Text (TMP) для label.
  - Layout: `Horizontal Layout Group` на root row, чтобы Indent → Label.
- [ ] **Step 22.3:** Повесить компонент `SceneOutlinerRow` (Add Component → SceneOutlinerRow).
  - Wire поля: `_label` → TMP Text, `_highlight` → Image, `_indentSpacer` → LayoutElement, `_button` → Button (root).
- [ ] **Step 22.4:** Drag в `Assets/_App/Subsystems/SpatialUi/Prefabs/Rows/` (создать папку, если нет). Удалить из сцены.
- [ ] **Step 22.5:** Suggested commit:

```
feat(spatial-ui): SceneOutlinerRow prefab
```

---

### Task 23: MANUAL — Добавить Outliner + Inspector в ContextMenu prefabs

**Files:**
- Manual edit: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_VrEditing.prefab`
- Manual edit: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_Sandbox.prefab` (создать если нет — копией VrEditing)

- [ ] **Step 23.1:** Открыть `UserPanel_ContextMenu_VrEditing.prefab` для редактирования.
- [ ] **Step 23.2:** Добавить child `Outliner`:
  - `ScrollRect` + `Vertical Layout Group` на content
  - Empty header (TMP "Outliner")
  - Content (Transform) — `_rowsRoot` для view
  - Повесить компонент `SceneOutlinerView`. Wire `_rowsRoot` → Content, `_rowPrefab` → `SceneOutlinerRow.prefab` из Task 22.
- [ ] **Step 23.3:** Добавить child `Inspector` (под Outliner):
  - Header TMP "Inspector"
  - Empty state `EmptyState` (Text "Nothing selected") — disabled by default
  - Content `Content` с полями:
    - `NameField` (TMP_InputField + повесить `VrInputFieldProxy` тоже)
    - `TypeLabel` (TMP Text)
    - `PositionLabel`, `RotationLabel`, `ScaleLabel` (TMP Text)
  - Повесить компонент `SceneInspectorView`. Wire все поля.
- [ ] **Step 23.4:** Сохранить prefab.
- [ ] **Step 23.5:** Скопировать prefab для Sandbox: дублировать как `UserPanel_ContextMenu_Sandbox.prefab` (если ещё не существует — см. UserPanel.cs `_contextMenus`, какой prefab привязан к AppMode.Sandbox).
- [ ] **Step 23.6:** Проверить в `UserPanel.prefab` массив `_contextMenus`: должен быть entry для `AppMode.Sandbox` → новый prefab.
- [ ] **Step 23.7:** Suggested commit:

```
feat(spatial-ui): Outliner + Inspector inside UserPanel_ContextMenu_{VrEditing,Sandbox} prefabs
```

---

### Task 24: MANUAL — WorldClickCatcher на XR Origin

**Files:**
- Manual edit: XR Origin prefab/scene-object в VrEditing scene
- Manual edit: то же в Sandbox scene

- [ ] **Step 24.1:** Открыть scene-prefab `VrEditing` (`Assets/.../Scenes/VrEditing.unity` или соответствующий).
- [ ] **Step 24.2:** Найти `XR Origin` GameObject.
- [ ] **Step 24.3:** Add Component → `WorldClickCatcher`.
- [ ] **Step 24.4:** Wire поля:
  - `_leftRay` → XRRayInteractor компонент с левого контроллера
  - `_rightRay` → XRRayInteractor с правого
  - `_leftSelectAction` → InputActionReference на trigger left (см. InputBindings subsystem или InputActions asset)
  - `_rightSelectAction` → то же для right
- [ ] **Step 24.5:** Сохранить scene.
- [ ] **Step 24.6:** То же для Sandbox scene.
- [ ] **Step 24.7:** Suggested commit:

```
feat(vr-interaction): WorldClickCatcher wired on XR Origin in VrEditing + Sandbox scenes
```

---

## Phase 6: Manual playmode validation

### Task 25: Manual playmode test sweep

> Эти шаги — финальная валидация. Проводятся в VR (Quest 3) или в XR Simulator (если есть). Каждый шаг — ожидаемый результат; если не сходится — диагностика и фикс.

- [ ] **Step 25.1:** **Enter Play Mode. Перейти в VrEditing → создать новую сцену → наспаунить 3 ассета.**
  - Expected: все 3 видны, в Hierarchy под `[Spawned]`, Outliner показывает 3 строки с именами файлов.
- [ ] **Step 25.2:** **Click trigger по одному ассету.**
  - Expected: жёлтый outline на 3D, жёлтая подсветка соответствующей строки Outliner, Inspector показывает имя/тип/transform.
- [ ] **Step 25.3:** **Click по второму ассету.**
  - Expected: оба выделены — первый orange, второй yellow. Active в Inspector = второй.
- [ ] **Step 25.4:** **Click по первому ещё раз.**
  - Expected: первый убран из выделения, второй остался yellow. Inspector — второй.
- [ ] **Step 25.5:** **Click trigger в пустоту.**
  - Expected: выделение очищено, Inspector → "Nothing selected".
- [ ] **Step 25.6:** **Click trigger по кнопке UserPanel (например, Settings).**
  - Expected: выделение НЕ сбрасывается. Только активируется кнопка.
- [ ] **Step 25.7:** **Click на ассет → в Inspector NameField → клавиатура → ввести "MyChair" → Submit.**
  - Expected: имя обновляется в Outliner строке, gameObject.name тоже.
- [ ] **Step 25.8:** **Exit в MainMenu, потом снова открыть ту же сцену.**
  - Expected: все 3 ассета снова в сцене с теми же positions, "MyChair" сохранилось. В Console — warning про migrating если сцена была v1.
- [ ] **Step 25.9:** **Sandbox: спавн 2 ассетов → exit → опять зайти в Sandbox.**
  - Expected: Sandbox пуст (in-memory, не сохраняется).
- [ ] **Step 25.10:** **Создать НОВУЮ сцену → спавн → exit → открыть СТАРУЮ сцену.**
  - Expected: старая показывает свои ноды, не от новой.
- [ ] **Step 25.11:** **Если все шаги пройдены — Suggested final commit:**

```
test(vr): manual playmode validation pass for scene-objects+selection+outliner
```

---

## Summary file map (для удобства code review)

| Файл | Изменение | Task |
|---|---|---|
| `_Shared/Data/AssetCapabilities.cs` | new | 1 |
| `_Shared/Data/AssetSource.cs` | new | 1 |
| `_Shared/Data/AssetRef.cs` | new | 1 |
| `_Shared/Data/SelectionVisual.cs` | new | 1 |
| `_Shared/Interfaces/ILabAsset.cs` | edit (Capabilities) | 2 |
| `_Shared/Interfaces/ISelectionManager.cs` | edit (Toggle/Clear) | 3 |
| `_Shared/Interfaces/ISceneGraph.cs` | edit (AddNode signature) | 7 |
| `_Shared/Interfaces/IInteractableFactory.cs` | edit (capabilities param) | 12 |
| `_Shared/Interfaces/IAssetRegistry.cs` | new | 4 |
| `_Shared/Events/AppEvents.cs` | edit (SelectionChangedEvent expanded) | 3 |
| `Subsystems/AssetBrowser/Data/{Builtin,Imported,Saved}LabAsset.cs` | edit (Capabilities field) | 2 |
| `Subsystems/AssetBrowser/AssetRegistry.cs` | new | 4 |
| `Subsystems/AssetBrowser/AssetSpawner.cs` | edit | 9 |
| `Subsystems/AssetBrowser/AssetImporter.cs` | edit (legacy cleanup) | 10 |
| `Subsystems/SceneComposition/SelectionManager.cs` | edit (multi-select) | 5 |
| `Subsystems/SceneComposition/SceneNode.cs` | edit | 7 |
| `Subsystems/SceneComposition/SceneGraph.cs` | rewrite | 8, 12 |
| `Subsystems/SceneComposition/SceneAutoSaver.cs` | new | 11 |
| `Subsystems/SceneComposition/Tests/SelectionManagerTests.cs` | new | 5 |
| `Subsystems/SceneComposition/Tests/SceneGraphTests.cs` | new | 8 |
| `Subsystems/SceneComposition/Tests/AssetRegistryTests.cs` | new | 4 |
| `Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef` | edit (add AssetBrowser ref) | 4 |
| `Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef` | edit (add StorageCore ref) | 8 |
| `Subsystems/StorageCore/Data/SceneData.cs` | edit (v2) | 6 |
| `Subsystems/StorageCore/Data/NodeData.cs` | new | 6 |
| `Subsystems/StorageCore/SceneSerializer.cs` | edit (migration) | 6 |
| `Subsystems/StorageCore/Tests/SceneSerializerTests.cs` | edit (v1→v2 test) | 6 |
| `Subsystems/VrInteraction/Selectable.cs` | new | 12 |
| `Subsystems/VrInteraction/SelectionInteractor.cs` | edit (Toggle) | 14 |
| `Subsystems/VrInteraction/SelectionInteractorFactory.cs` | edit | 12 |
| `Subsystems/VrInteraction/SelectionVisualSync.cs` | new | 15 |
| `Subsystems/VrInteraction/WorldClickCatcher.cs` | new | 16 |
| `Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef` | edit (QuickOutline ref) | 13 |
| `UnityPacks/QuickOutline/Scripts/QuickOutline.asmdef` | new | 13 |
| `Subsystems/SpatialUi/UI_Scripts/SceneOutlinerView.cs` | new | 19 |
| `Subsystems/SpatialUi/UI_Scripts/SceneOutlinerRow.cs` | new | 18 |
| `Subsystems/SpatialUi/UI_Scripts/SceneInspectorView.cs` | new | 20 |
| `Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | edit (SwapContext) | 17 |
| `Subsystems/SpatialUi/Prefabs/Rows/SceneOutlinerRow.prefab` | new (manual) | 22 |
| `Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_VrEditing.prefab` | edit (manual) | 23 |
| `Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_Sandbox.prefab` | new/edit (manual) | 23 |
| `Bootstrap/RootLifetimeScope.cs` | edit (AssetRegistry) | 4 |
| `Bootstrap/VrEditingSceneScope.cs` | edit (AutoSaver, VisualSync, ClickCatcher inject) | 11, 15, 16 |
| `Bootstrap/SandboxSceneScope.cs` | edit (VisualSync, ClickCatcher inject) | 15, 16 |
| `DemoAssets/BuiltinAssetLibrary.asset` | edit Capabilities (manual) | 21 |
| Scene prefabs (XR Origin) | add WorldClickCatcher (manual) | 24 |
