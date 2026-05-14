# Phase 3: StorageCore + MainMenu — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create/open a named scene; scene metadata persists to `scene.json` on disk; `ScenePickerView` lists existing scenes and handles create/open/delete.

**Architecture:** `PathProvider` is the single authority for all file paths — no manual string concatenation elsewhere. `AppStorage` holds the in-memory scene cache and exposes async load/save. `SceneSerializer` handles JSON round-trips. `UnsavedChangesGuard` listens to `SceneModifiedEvent` and blocks navigation without confirmation. All services are registered in `RootLifetimeScope`.

**Tech Stack:** `System.IO`, `UnityEngine.JsonUtility`, `Application.persistentDataPath`, `async/await`

---

## File Map

**Create:**
- `Assets/Subsystems/StorageCore/PathProvider.cs`
- `Assets/Subsystems/StorageCore/Data/SceneData.cs`
- `Assets/Subsystems/StorageCore/SceneSerializer.cs`
- `Assets/Subsystems/StorageCore/AppStorage.cs`
- `Assets/Subsystems/StorageCore/UnsavedChangesGuard.cs`
- `Assets/Subsystems/StorageCore/Tests/PathProviderTests.cs`
- `Assets/Subsystems/StorageCore/Tests/SceneSerializerTests.cs`
- `Assets/Subsystems/AssetBrowser/UI/ScenePickerView.cs` *(lives in AssetBrowser per spec)*

**Modify:**
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register PathProvider, AppStorage

---

## Task 1: PathProvider + Tests

**Files:** `StorageCore/PathProvider.cs`, `StorageCore/Tests/PathProviderTests.cs`

- [ ] Write failing test first — create `Assets/Subsystems/StorageCore/Tests/PathProviderTests.cs`:
  ```csharp
  using NUnit.Framework;

  public class PathProviderTests
  {
      private PathProvider _sut;

      [SetUp]
      public void SetUp() => _sut = new PathProvider("/data");

      [Test]
      public void SceneRoot_ReturnsExpectedPath()
      {
          Assert.AreEqual("/data/scenes/scene-01", _sut.SceneRoot("scene-01"));
      }

      [Test]
      public void SceneJson_ReturnsExpectedPath()
      {
          Assert.AreEqual("/data/scenes/scene-01/scene.json", _sut.SceneJson("scene-01"));
      }

      [Test]
      public void AssetPath_ReturnsExpectedPath()
      {
          Assert.AreEqual("/data/scenes/scene-01/assets/Models/mesh.fbx",
              _sut.AssetPath("scene-01", "Models/mesh.fbx"));
      }
  }
  ```

- [ ] Run test in Unity Test Runner → Window → General → Test Runner → Edit Mode → Run All → expect 3 failures ("PathProvider not found")

- [ ] Create `Assets/Subsystems/StorageCore/PathProvider.cs`:
  ```csharp
  using System.IO;
  using UnityEngine;

  public class PathProvider
  {
      private readonly string _root;

      public PathProvider() : this(Application.persistentDataPath) { }

      // Testable constructor
      public PathProvider(string root) => _root = root;

      public string SceneRoot(string sceneId) =>
          Path.Combine(_root, "scenes", sceneId);

      public string SceneJson(string sceneId) =>
          Path.Combine(SceneRoot(sceneId), "scene.json");

      public string AssetCatalogJson(string sceneId) =>
          Path.Combine(SceneRoot(sceneId), "asset-catalog.json");

      public string AssetPath(string sceneId, string relativePath) =>
          Path.Combine(SceneRoot(sceneId), "assets", relativePath);

      public string ExportDir(string sceneId) =>
          Path.Combine(SceneRoot(sceneId), "export");

      public string ScenesRoot() =>
          Path.Combine(_root, "scenes");
  }
  ```

- [ ] Run tests again → expect 3 passes

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/StorageCore/PathProvider.cs Assets/Subsystems/StorageCore/Tests/
  git commit -m "feat: add PathProvider with Edit Mode tests"
  ```

---

## Task 2: SceneData + SceneSerializer + Tests

**Files:** `StorageCore/Data/SceneData.cs`, `StorageCore/SceneSerializer.cs`, `StorageCore/Tests/SceneSerializerTests.cs`

- [ ] Create `Assets/Subsystems/StorageCore/Data/SceneData.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  [Serializable]
  public class SceneData
  {
      public int SchemaVersion = 1;
      public string SceneId;
      public string DisplayName;
      public string CreatedAt;
      public List<string> NodeIds = new();   // populated by SceneGraph in Phase 5
  }
  ```

- [ ] Write failing test — add to `Assets/Subsystems/StorageCore/Tests/SceneSerializerTests.cs`:
  ```csharp
  using NUnit.Framework;

  public class SceneSerializerTests
  {
      [Test]
      public void Serialize_ThenDeserialize_RoundTrips()
      {
          var original = new SceneData
          {
              SceneId     = "scene-42",
              DisplayName = "My Scene",
              CreatedAt   = "2026-05-14"
          };

          var json       = SceneSerializer.Serialize(original);
          var result     = SceneSerializer.Deserialize(json);

          Assert.AreEqual("scene-42",  result.SceneId);
          Assert.AreEqual("My Scene",  result.DisplayName);
          Assert.AreEqual(1,           result.SchemaVersion);
      }

      [Test]
      public void Deserialize_NullJson_ReturnsNull()
      {
          Assert.IsNull(SceneSerializer.Deserialize(null));
      }
  }
  ```

- [ ] Run tests → expect 2 failures

- [ ] Create `Assets/Subsystems/StorageCore/SceneSerializer.cs`:
  ```csharp
  using UnityEngine;

  public static class SceneSerializer
  {
      public static string Serialize(SceneData data) =>
          JsonUtility.ToJson(data, prettyPrint: true);

      public static SceneData Deserialize(string json)
      {
          if (string.IsNullOrEmpty(json)) return null;
          return JsonUtility.FromJson<SceneData>(json);
      }
  }
  ```

- [ ] Run tests → expect 2 passes

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/StorageCore/Data/ Assets/Subsystems/StorageCore/SceneSerializer.cs Assets/Subsystems/StorageCore/Tests/
  git commit -m "feat: add SceneData, SceneSerializer with round-trip tests"
  ```

---

## Task 3: AppStorage

**Files:** `StorageCore/AppStorage.cs`

- [ ] Create `Assets/Subsystems/StorageCore/AppStorage.cs`:
  ```csharp
  using System.Collections.Generic;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;
  using UnityEngine;

  public class AppStorage
  {
      private readonly PathProvider _paths;
      private readonly EventBus _bus;
      private readonly Dictionary<string, SceneData> _cache = new();

      private string _activeSceneId;
      public string ActiveSceneId => _activeSceneId;

      public AppStorage(PathProvider paths, EventBus bus)
      {
          _paths = paths;
          _bus   = bus;
      }

      public async Task<SceneData> CreateSceneAsync(string displayName, CancellationToken ct = default)
      {
          var sceneId = System.Guid.NewGuid().ToString("N")[..8];
          var data = new SceneData
          {
              SceneId     = sceneId,
              DisplayName = displayName,
              CreatedAt   = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
          };

          Directory.CreateDirectory(_paths.SceneRoot(sceneId));
          await SaveSceneAsync(data, ct);
          _cache[sceneId] = data;
          return data;
      }

      public async Task<SceneData> LoadSceneAsync(string sceneId, CancellationToken ct = default)
      {
          if (_cache.TryGetValue(sceneId, out var cached)) return cached;

          var path = _paths.SceneJson(sceneId);
          if (!File.Exists(path)) return null;

          var json = await File.ReadAllTextAsync(path, ct);
          var data = SceneSerializer.Deserialize(json);
          _cache[sceneId] = data;
          return data;
      }

      public async Task SaveSceneAsync(SceneData data, CancellationToken ct = default)
      {
          var path = _paths.SceneJson(data.SceneId);
          var json = SceneSerializer.Serialize(data);
          await File.WriteAllTextAsync(path, json, ct);
          _cache[data.SceneId] = data;
      }

      public void OpenScene(SceneData data)
      {
          _activeSceneId = data.SceneId;
          _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
      }

      public void MarkModified() => _bus.Publish(new SceneModifiedEvent());

      public IEnumerable<string> GetAllSceneIds()
      {
          var root = _paths.ScenesRoot();
          if (!Directory.Exists(root)) yield break;
          foreach (var dir in Directory.GetDirectories(root))
              yield return Path.GetFileName(dir);
      }

      public void DeleteScene(string sceneId)
      {
          var root = _paths.SceneRoot(sceneId);
          if (Directory.Exists(root))
              Directory.Delete(root, recursive: true);
          _cache.Remove(sceneId);
      }
  }
  ```

- [ ] Register in `RootLifetimeScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;

  public class RootLifetimeScope : LifetimeScope
  {
      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Singleton);
          builder.Register<PathProvider>(Lifetime.Singleton);
          builder.Register<AppStorage>(Lifetime.Singleton);
          // AssetImporter — Phase 4; AnimationClock — Phase 7
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/StorageCore/AppStorage.cs Assets/_App/Bootstrap/RootLifetimeScope.cs
  git commit -m "feat: add AppStorage with async create/load/save/delete"
  ```

---

## Task 4: UnsavedChangesGuard

**Files:** `StorageCore/UnsavedChangesGuard.cs`

- [ ] Create `Assets/Subsystems/StorageCore/UnsavedChangesGuard.cs`:
  ```csharp
  using VContainer.Unity;

  public class UnsavedChangesGuard : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private bool _isDirty;

      public bool IsDirty => _isDirty;

      public UnsavedChangesGuard(EventBus bus) => _bus = bus;

      public void Start()
      {
          _bus.Subscribe<SceneModifiedEvent>(OnModified);
          _bus.Subscribe<SceneOpenedEvent>(OnOpened);
      }

      public void Dispose()
      {
          _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
          _bus.Unsubscribe<SceneOpenedEvent>(OnOpened);
      }

      public bool CanNavigate() => !_isDirty;

      public void ClearDirty() => _isDirty = false;

      private void OnModified(SceneModifiedEvent _) => _isDirty = true;
      private void OnOpened(SceneOpenedEvent _)    => _isDirty = false;
  }
  ```

- [ ] Add to `MainMenuSceneScope.cs` and `VrEditingSceneScope.cs`:
  ```csharp
  builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/StorageCore/UnsavedChangesGuard.cs
  git commit -m "feat: add UnsavedChangesGuard listening to SceneModifiedEvent"
  ```

---

## Task 5: ScenePickerView (MainMenu UI)

**Files:** `AssetBrowser/UI/ScenePickerView.cs`

- [ ] Create `Assets/Subsystems/AssetBrowser/UI/ScenePickerView.cs`:
  ```csharp
  using System.Collections.Generic;
  using System.Threading;
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using TMPro;

  public class ScenePickerView : MonoBehaviour
  {
      [SerializeField] private Transform _listRoot;
      [SerializeField] private GameObject _sceneItemPrefab;
      [SerializeField] private Button _createButton;
      [SerializeField] private TMP_InputField _nameInput;

      private AppStorage _storage;
      private ModeOrchestrator _orchestrator;

      [Inject]
      public void Construct(AppStorage storage, ModeOrchestrator orchestrator)
      {
          _storage      = storage;
          _orchestrator = orchestrator;
      }

      private void Start()
      {
          _createButton.onClick.AddListener(OnCreateClicked);
          Refresh();
      }

      private void Refresh()
      {
          foreach (Transform child in _listRoot)
              Destroy(child.gameObject);

          foreach (var sceneId in _storage.GetAllSceneIds())
              SpawnSceneItem(sceneId);
      }

      private void SpawnSceneItem(string sceneId)
      {
          var item = Instantiate(_sceneItemPrefab, _listRoot);
          var label = item.GetComponentInChildren<TMP_Text>();
          if (label != null) label.text = sceneId;

          var btn = item.GetComponentInChildren<Button>();
          if (btn != null)
              btn.onClick.AddListener(async () =>
              {
                  var data = await _storage.LoadSceneAsync(sceneId, CancellationToken.None);
                  if (data != null)
                  {
                      _storage.OpenScene(data);
                      _orchestrator.TransitionTo(AppMode.VrEditing);
                  }
              });
      }

      private async void OnCreateClicked()
      {
          var name = _nameInput.text;
          if (string.IsNullOrWhiteSpace(name)) name = "New Scene";
          var data = await _storage.CreateSceneAsync(name, CancellationToken.None);
          _storage.OpenScene(data);
          _orchestrator.TransitionTo(AppMode.VrEditing);
      }
  }
  ```

> Note: `async void` on `OnCreateClicked` is acceptable per convention since it's a Unity lifecycle/event entry point. Wrap with try-catch in Phase 8 when ErrorDispatcher is ready.

- [ ] **In Unity Editor — MainMenu.unity:**
  1. Add World Space Canvas to scene
  2. Add title text, name input field, "Create Scene" button, and a vertical scroll list
  3. Add `ScenePickerView` component, wire fields
  4. Create a simple scene item prefab: label + "Open" button → save as `Assets/Subsystems/AssetBrowser/UI/SceneItemPrefab.prefab`

- [ ] Press Play from Bootstrap → create a scene, verify `scene.json` written to `Application.persistentDataPath/scenes/{id}/`

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AssetBrowser/UI/ Assets/Scenes/MainMenu.unity
  git commit -m "feat: add ScenePickerView — create/open scenes persist to disk"
  ```

---

## Phase 3 Verification

- [ ] PathProvider tests: 3 passing in Edit Mode Test Runner
- [ ] SceneSerializer tests: 2 passing
- [ ] Playing Bootstrap: create scene → `scene.json` appears in `persistentDataPath/scenes/{id}/`
- [ ] Listing scenes: existing scenes appear in ScenePickerView
- [ ] Opening a scene → `SceneOpenedEvent` fires (add `Debug.Log` to verify), VrEditing loads
