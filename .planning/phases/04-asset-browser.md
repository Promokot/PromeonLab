# Phase 4: AssetBrowser + Model Loading — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SimpleFileBrowser opens from ToolbarPanel; user picks a filename; a pre-bundled skinned mesh prefab matching that filename instantiates in the VR scene and is registered in AppStorage.

**Architecture:** `DemoAssetCatalog` ScriptableObject maps known filenames to prefabs. `AssetImporter` looks up the catalog — if matched, instantiates the prefab; otherwise shows a "not found" toast. `AssetBrowserPanel` provides a list view of imported assets per scene. Pre-bundled FBX models are imported into Unity as prefabs in `Assets/_App/DemoAssets/`.

**Tech Stack:** `SimpleFileBrowser` API, Unity `Instantiate`, `ScriptableObject`, VContainer injection

---

## File Map

**Create:**
- `Assets/_Shared/Models/AssetType.cs`
- `Assets/_Shared/Models/AssetEntry.cs`
- `Assets/Subsystems/StorageCore/Data/AssetCatalogData.cs`
- `Assets/Subsystems/AssetBrowser/Data/DemoAssetCatalog.cs`
- `Assets/Subsystems/AssetBrowser/AssetImporter.cs`
- `Assets/Subsystems/AssetBrowser/AssetBrowserController.cs`
- `Assets/Subsystems/AssetBrowser/UI/AssetBrowserPanel.cs`

**Modify:**
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register AssetImporter
- `Assets/Subsystems/SpatialUi/UI/ToolbarPanel.cs` — add "Import" button handler

**Unity Editor:**
- Import 2–3 test FBX files with skeletons into `Assets/_App/DemoAssets/`
- Create `DemoAssetCatalog.asset`
- Create AssetBrowserPanel prefab

---

## Task 1: Shared Asset Models

**Files:** `_Shared/Models/AssetType.cs`, `_Shared/Models/AssetEntry.cs`

- [ ] Create `Assets/_Shared/Models/AssetType.cs`:
  ```csharp
  public enum AssetType { Model, Material, Texture, Video, Audio, Rig, Pose }
  ```

- [ ] Create `Assets/_Shared/Models/AssetEntry.cs`:
  ```csharp
  using System;
  using UnityEngine;

  [Serializable]
  public class AssetEntry
  {
      public string AssetId;
      public AssetType Type;
      public string RelativePath;
      public string DisplayName;
      public Sprite Icon;          // null = use type default icon
  }
  ```

- [ ] Create `Assets/Subsystems/StorageCore/Data/AssetCatalogData.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  [Serializable]
  public class AssetCatalogData
  {
      public int SchemaVersion = 1;
      public string SceneId;
      public List<AssetEntry> Entries = new();
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/_Shared/Models/ Assets/Subsystems/StorageCore/Data/AssetCatalogData.cs
  git commit -m "feat: add AssetType, AssetEntry, AssetCatalogData to shared models"
  ```

---

## Task 2: DemoAssetCatalog ScriptableObject

**Files:** `AssetBrowser/Data/DemoAssetCatalog.cs`

- [ ] Create `Assets/Subsystems/AssetBrowser/Data/DemoAssetCatalog.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  [CreateAssetMenu(fileName = "DemoAssetCatalog", menuName = "PromeonLab/DemoAssetCatalog")]
  public class DemoAssetCatalog : ScriptableObject
  {
      [System.Serializable]
      public struct DemoEntry
      {
          public string FileName;     // e.g. "Mannequin.fbx"
          public GameObject Prefab;   // pre-imported skinned mesh prefab
          public AssetType Type;
          public Sprite Icon;
      }

      [SerializeField] private List<DemoEntry> _entries = new();

      public bool TryFind(string fileName, out DemoEntry entry)
      {
          foreach (var e in _entries)
          {
              if (string.Equals(e.FileName, fileName, System.StringComparison.OrdinalIgnoreCase))
              {
                  entry = e;
                  return true;
              }
          }
          entry = default;
          return false;
      }

      public IReadOnlyList<DemoEntry> AllEntries => _entries;
  }
  ```

- [ ] **In Unity Editor — prepare demo assets:**
  1. Find or download 2–3 humanoid FBX files with skeletons (Unity's built-in "DefaultAvatar" or any rigged character)
  2. Import into `Assets/_App/DemoAssets/Models/`
  3. For each: select FBX → Inspector → Rig tab → Animation Type = **Humanoid** (or Generic) → Apply
  4. Create a prefab for each: drag the FBX root into scene, adjust scale if needed, drag back to `Assets/_App/DemoAssets/Prefabs/` to save prefab

- [ ] **Create DemoAssetCatalog asset:**
  1. Right-click `Assets/Subsystems/AssetBrowser/Data/` → Create → PromeonLab → DemoAssetCatalog
  2. Name it `DemoAssetCatalog.asset`
  3. Add entries: FileName = "Mannequin.fbx", Prefab = the prefab you made, Type = Model
  4. Repeat for other models

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AssetBrowser/Data/ Assets/_App/DemoAssets/
  git commit -m "feat: add DemoAssetCatalog SO with pre-bundled model prefabs"
  ```

---

## Task 3: AssetImporter

**Files:** `AssetBrowser/AssetImporter.cs`

- [ ] Create `Assets/Subsystems/AssetBrowser/AssetImporter.cs`:
  ```csharp
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;
  using UnityEngine;

  public class AssetImporter
  {
      private readonly DemoAssetCatalog _catalog;
      private readonly AppStorage _storage;
      private readonly EventBus _bus;

      public AssetImporter(DemoAssetCatalog catalog, AppStorage storage, EventBus bus)
      {
          _catalog = catalog;
          _storage = storage;
          _bus     = bus;
      }

      /// <summary>
      /// Called with the path the user selected in SimpleFileBrowser.
      /// Returns the instantiated GameObject or null if not in catalog.
      /// </summary>
      public async Task<GameObject> ImportAsync(string filePath, CancellationToken ct = default)
      {
          var fileName = Path.GetFileName(filePath);

          if (!_catalog.TryFind(fileName, out var entry))
          {
              Debug.LogWarning($"AssetImporter: '{fileName}' not in DemoAssetCatalog");
              _bus.Publish(new ErrorOccurredEvent
              {
                  Level   = ErrorLevel.Warning,
                  Message = $"'{fileName}' is not available in the demo catalog."
              });
              return null;
          }

          await Task.Yield(); // yield so caller can update UI before instantiation

          var instance = Object.Instantiate(entry.Prefab, Vector3.zero, Quaternion.identity);
          instance.name = Path.GetFileNameWithoutExtension(fileName);

          var assetId = System.Guid.NewGuid().ToString("N")[..8];
          var assetEntry = new AssetEntry
          {
              AssetId     = assetId,
              Type        = entry.Type,
              DisplayName = instance.name,
              RelativePath = $"Models/{fileName}",
              Icon        = entry.Icon
          };

          _bus.Publish(new AssetImportedEvent { AssetId = assetId });

          return instance;
      }
  }
  ```

- [ ] Register in `RootLifetimeScope.cs`:
  ```csharp
  // Add to Configure():
  builder.RegisterInstance(_demoAssetCatalog);   // serialized field on scope
  builder.Register<AssetImporter>(Lifetime.Singleton);
  ```

  Update `RootLifetimeScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;
  using UnityEngine;

  public class RootLifetimeScope : LifetimeScope
  {
      [SerializeField] private DemoAssetCatalog _demoAssetCatalog;

      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Singleton);
          builder.Register<PathProvider>(Lifetime.Singleton);
          builder.Register<AppStorage>(Lifetime.Singleton);
          builder.RegisterInstance(_demoAssetCatalog);
          builder.Register<AssetImporter>(Lifetime.Singleton);
      }
  }
  ```

- [ ] In Unity: assign `DemoAssetCatalog.asset` to `RootLifetimeScope._demoAssetCatalog` field in Bootstrap.unity

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AssetBrowser/AssetImporter.cs Assets/_App/Bootstrap/RootLifetimeScope.cs
  git commit -m "feat: add AssetImporter with DemoAssetCatalog lookup"
  ```

---

## Task 4: AssetBrowserPanel + SimpleFileBrowser Integration

**Files:** `AssetBrowser/AssetBrowserController.cs`, `AssetBrowser/UI/AssetBrowserPanel.cs`

- [ ] Create `Assets/Subsystems/AssetBrowser/UI/AssetBrowserPanel.cs`:
  ```csharp
  using System.Collections.Generic;
  using System.Threading;
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using TMPro;
  using SimpleFileBrowser;

  public class AssetBrowserPanel : SpatialPanel
  {
      [SerializeField] private Transform _listRoot;
      [SerializeField] private GameObject _assetItemPrefab;
      [SerializeField] private Button _importButton;

      private AssetImporter _importer;
      private EventBus _bus;
      private readonly List<AssetEntry> _entries = new();

      [Inject]
      public void Construct(AssetImporter importer, EventBus bus)
      {
          _importer = importer;
          _bus      = bus;
      }

      private void Start()
      {
          _importButton.onClick.AddListener(OnImportClicked);
          _bus.Subscribe<AssetImportedEvent>(OnAssetImported);
      }

      private void OnDestroy() =>
          _bus.Unsubscribe<AssetImportedEvent>(OnAssetImported);

      private void OnImportClicked()
      {
          FileBrowser.ShowLoadDialog(
              onSuccess: paths => _ = HandleImportAsync(paths[0]),
              onCancel:  () => { },
              pickMode:  FileBrowser.PickMode.Files,
              title:     "Select a model",
              loadButtonText: "Import"
          );
      }

      private async System.Threading.Tasks.Task HandleImportAsync(string path)
      {
          var go = await _importer.ImportAsync(path, CancellationToken.None);
          if (go != null)
              Debug.Log($"Imported: {go.name}");
      }

      private void OnAssetImported(AssetImportedEvent e) => RefreshList();

      private void RefreshList()
      {
          foreach (Transform child in _listRoot)
              Destroy(child.gameObject);
          // Phase 5 will populate _entries from SceneGraph; for now just show import button
      }
  }
  ```

- [ ] **In Unity Editor — create AssetBrowserPanel prefab:**
  1. Create World Space Canvas in VrEditing.unity
  2. Add vertical scroll list, "Import" button
  3. Attach `AssetBrowserPanel` component; wire `_importButton`, `_listRoot`
  4. Save as prefab `Assets/Subsystems/AssetBrowser/UI/AssetBrowserPanel.prefab`
  5. Add to `PanelRegistry.asset`: Id = AssetBrowser, VisibleInModes = [VrEditing]

- [ ] In `ToolbarPanel.cs` — verify "Assets" button opens the panel (already wired in Phase 2)

- [ ] Press Play → click "Assets" in ToolbarPanel → AssetBrowserPanel appears → click "Import" → SimpleFileBrowser opens → select a file matching a catalog entry → model instantiates at origin

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AssetBrowser/ Assets/Scenes/VrEditing.unity
  git commit -m "feat: AssetBrowserPanel with SimpleFileBrowser integration and model loading"
  ```

---

## Phase 4 Verification

- [ ] Clicking "Assets" in ToolbarPanel shows AssetBrowserPanel
- [ ] Clicking "Import" → SimpleFileBrowser opens
- [ ] Selecting a filename that matches a catalog entry → skinned mesh prefab appears at origin in scene
- [ ] Selecting an unrecognized filename → `ErrorOccurredEvent` published (verify with `Debug.Log` in subscriber)
- [ ] `AssetImportedEvent` fires (verify with `Debug.Log`)
