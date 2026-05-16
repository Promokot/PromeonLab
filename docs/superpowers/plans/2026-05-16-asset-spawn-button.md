# Asset Spawn Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Spawn button to AssetBrowserModule that places the selected asset 1.2 m in front of the player at floor level (Y=0), registered in SceneGraph via an event.

**Architecture:** `AssetBrowserModule` (Root scope) publishes `AssetSpawnRequestedEvent` on the shared `EventBus`. `AssetSpawner` (scene scope: VrEditing and Sandbox) subscribes, calls `ILabAsset.SpawnAsync()`, and registers the result with `SceneGraph`. Cross-scope communication flows exclusively through `EventBus` — no direct calls across scope boundaries.

**Tech Stack:** Unity 6000.3.7f1 · C# · VContainer · OpenXR · TMPro · UnityEngine.UI

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/_App/_Shared/Events/AppEvents.cs` | Add `AssetSpawnRequestedEvent` struct |
| Create | `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs` | Subscribe to event, spawn asset, register in SceneGraph |
| Modify | `Assets/_App/Subsystems/SpatialUi/UI/AssetBrowserModule.cs` | Add `_spawnButton`, track `_selectedAsset`, publish event |
| Modify | `Assets/_App/Bootstrap/VrEditingSceneScope.cs` | Register `AssetSpawner` |
| Create | `Assets/_App/Bootstrap/SandboxSceneScope.cs` | Mirror of VrEditingSceneScope without UnsavedChangesGuard |

---

### Task 1: Add `AssetSpawnRequestedEvent`

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`

- [ ] **Step 1: Read the file**

  Open `Assets/_App/_Shared/Events/AppEvents.cs` and confirm it currently ends with `PlayerSpawnRequestedEvent`.

- [ ] **Step 2: Add the new event struct**

  Append one line to `AppEvents.cs`:

  ```csharp
  public struct AssetSpawnRequestedEvent { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
  ```

  Final file should look like:
  ```csharp
  public struct SceneOpenedEvent       { public string SceneId; }
  public struct SceneModifiedEvent     { }
  public struct SceneClosedEvent       { }
  public struct AssetImportedEvent     { public string AssetId; }
  public struct SelectionChangedEvent  { public string SelectedNodeId; }
  public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
  public struct FrameChangedEvent      { public int Frame; }
  public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
  public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
  public struct SceneSelectedEvent          { public string SceneId; public string DisplayName; }
  public struct PlayerSpawnRequestedEvent   { public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
  public struct AssetSpawnRequestedEvent    { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
  ```

- [ ] **Step 3: Check Unity console for compilation errors**

  Switch to Unity Editor. In the Console window, confirm no errors appear after the domain reload. `AssetSpawnRequestedEvent` references `ILabAsset` (already defined in `_Shared/Interfaces/ILabAsset.cs`) — no additional using statements needed.

- [ ] **Step 4: Commit**

  ```
  git add "Assets/_App/_Shared/Events/AppEvents.cs"
  git commit -m "feat: add AssetSpawnRequestedEvent"
  ```

---

### Task 2: Create `AssetSpawner`

**Files:**
- Create: `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs`

- [ ] **Step 1: Create the file**

  Create `Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs` with the following content:

  ```csharp
  using System;
  using System.Threading;
  using UnityEngine;
  using VContainer.Unity;

  public class AssetSpawner : IStartable, IDisposable
  {
      private readonly EventBus   _bus;
      private readonly SceneGraph _graph;

      public AssetSpawner(EventBus bus, SceneGraph graph)
      {
          _bus   = bus;
          _graph = graph;
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
              _graph.AddNode(go);
          }
          catch (Exception ex)
          {
              Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
          }
      }
  }
  ```

- [ ] **Step 2: Check Unity console for compilation errors**

  Switch to Unity Editor, wait for domain reload, confirm no errors. `SceneGraph` is a concrete class registered `.AsSelf()` in `VrEditingSceneScope` — injection will resolve correctly once registered in Task 4.

- [ ] **Step 3: Commit**

  ```
  git add "Assets/_App/Subsystems/AssetBrowser/AssetSpawner.cs"
  git commit -m "feat: add AssetSpawner — subscribes to AssetSpawnRequestedEvent and adds to SceneGraph"
  ```

---

### Task 3: Update `AssetBrowserModule`

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI/AssetBrowserModule.cs`

The module needs: a `_spawnButton` SerializeField, a tracked `_selectedAsset`, `EventBus` injected via `Construct()`, spawn-position math on click, and selection/clear state keeping the button in sync.

- [ ] **Step 1: Read the current file**

  Open `Assets/_App/Subsystems/SpatialUi/UI/AssetBrowserModule.cs` and note the current state.

- [ ] **Step 2: Replace the file with the updated version**

  ```csharp
  using System;
  using System.Collections;
  using System.IO;
  using System.Threading;
  using TMPro;
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using SimpleFileBrowser;

  public class AssetBrowserModule : MonoBehaviour
  {
      [SerializeField] private CanvasGroup _canvasGroup;
      [SerializeField] private float       _slideDist = 0.05f;
      [SerializeField] private float       _duration  = 0.25f;

      [Header("Library Tabs")]
      [SerializeField] private Button _builtinTabButton;
      [SerializeField] private Button _importedTabButton;
      [SerializeField] private Button _savedTabButton;

      [Header("Grid")]
      [SerializeField] private Transform    _gridRoot;
      [SerializeField] private LabAssetCard _cardPrefab;
      [SerializeField] private Button       _addButton;
      [SerializeField] private Button       _spawnButton;

      [Header("Properties")]
      [SerializeField] private TMP_Text _propertiesText;

      private BuiltinAssetLibrary  _builtinLibrary;
      private ImportedAssetLibrary _importedLibrary;
      private SavedAssetLibrary    _savedLibrary;
      private EventBus             _bus;

      private IAssetLibrary _activeLibrary;
      private ILabAsset     _selectedAsset;

      private Vector3   _shownLocalPos;
      private Vector3   _hiddenLocalPos;
      private bool      _visible;
      private Coroutine _anim;

      [Inject]
      public void Construct(BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved, EventBus bus)
      {
          _builtinLibrary  = builtin;
          _importedLibrary = imported;
          _savedLibrary    = saved;
          _bus             = bus;
      }

      private void Awake()
      {
          _shownLocalPos  = transform.localPosition;
          _hiddenLocalPos = _shownLocalPos - Vector3.up * _slideDist;

          transform.localPosition     = _hiddenLocalPos;
          _canvasGroup.alpha          = 0f;
          _canvasGroup.interactable   = false;
          _canvasGroup.blocksRaycasts = false;

          _builtinTabButton?.onClick.AddListener(() => SwitchLibrary(_builtinLibrary));
          _importedTabButton?.onClick.AddListener(() => SwitchLibrary(_importedLibrary));
          _savedTabButton?.onClick.AddListener(() => SwitchLibrary(_savedLibrary));
          _addButton?.onClick.AddListener(OnAddClicked);
          _spawnButton?.onClick.AddListener(OnSpawnClicked);

          if (_spawnButton != null) _spawnButton.interactable = false;
      }

      private void Start()
      {
          if (_builtinLibrary != null)
              SwitchLibrary(_builtinLibrary);
      }

      public void Toggle() { if (_visible) Hide(); else Show(); }

      public void Show()
      {
          _visible = true;
          gameObject.SetActive(true);
          if (_anim != null) StopCoroutine(_anim);
          _anim = StartCoroutine(AnimRoutine(true));
      }

      public void Hide()
      {
          if (!_visible) return;
          _visible = false;
          if (_anim != null) StopCoroutine(_anim);
          _anim = StartCoroutine(AnimRoutine(false));
      }

      private void SwitchLibrary(IAssetLibrary library)
      {
          _activeLibrary = library;
          RefreshGrid();
      }

      private void RefreshGrid()
      {
          foreach (Transform child in _gridRoot)
              Destroy(child.gameObject);

          ClearSelection();
          ClearProperties();

          if (_activeLibrary == null || _cardPrefab == null) return;

          foreach (var asset in _activeLibrary.Assets)
          {
              var card = Instantiate(_cardPrefab, _gridRoot);
              card.Bind(asset);
              card.Selected += OnCardSelected;
          }
      }

      private void OnCardSelected(LabAssetCard card)
      {
          _selectedAsset = card.Asset;
          if (_spawnButton != null) _spawnButton.interactable = true;
          ShowProperties(card.Asset);
      }

      private void ClearSelection()
      {
          _selectedAsset = null;
          if (_spawnButton != null) _spawnButton.interactable = false;
      }

      private void ShowProperties(ILabAsset asset)
      {
          if (_propertiesText == null) return;
          _propertiesText.text =
              $"Name: {asset.DisplayName}\n" +
              $"Type: {asset.Type}";
      }

      private void ClearProperties()
      {
          if (_propertiesText != null)
              _propertiesText.text = string.Empty;
      }

      private void OnSpawnClicked()
      {
          if (_selectedAsset == null || _bus == null) return;

          var cam = Camera.main?.transform;
          if (cam == null) return;

          var fwd = cam.forward;
          fwd.y = 0f;
          if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
          else fwd.Normalize();

          var pos = new Vector3(
              cam.position.x + fwd.x * 1.2f,
              0f,
              cam.position.z + fwd.z * 1.2f);

          _bus.Publish(new AssetSpawnRequestedEvent
          {
              Asset    = _selectedAsset,
              Position = pos,
              Rotation = Quaternion.identity,
          });
      }

      private void OnAddClicked()
      {
          FileBrowser.ShowLoadDialog(
              onSuccess:      paths => _ = HandleImportAsync(paths[0]),
              onCancel:       () => { },
              pickMode:       FileBrowser.PickMode.Files,
              title:          "Import Asset",
              loadButtonText: "Import"
          );
      }

      private async System.Threading.Tasks.Task HandleImportAsync(string filePath)
      {
          var asset = new ImportedLabAsset(
              id:          Guid.NewGuid().ToString("N")[..8],
              displayName: Path.GetFileNameWithoutExtension(filePath),
              type:        AssetType.Model,
              filePath:    filePath
          );

          _importedLibrary.Add(asset);
          await _importedLibrary.SaveAsync(CancellationToken.None);

          if (_activeLibrary == _importedLibrary)
              RefreshGrid();
      }

      private IEnumerator AnimRoutine(bool show)
      {
          var startAlpha = _canvasGroup.alpha;
          var endAlpha   = show ? 1f : 0f;
          var startPos   = transform.localPosition;
          var endPos     = show ? _shownLocalPos : _hiddenLocalPos;

          _canvasGroup.interactable   = show;
          _canvasGroup.blocksRaycasts = show;

          float t = 0f;
          while (t < _duration)
          {
              t += Time.deltaTime;
              var p = Mathf.Clamp01(t / _duration);
              _canvasGroup.alpha      = Mathf.Lerp(startAlpha, endAlpha, p);
              transform.localPosition = Vector3.Lerp(startPos, endPos, p);
              yield return null;
          }

          _canvasGroup.alpha      = endAlpha;
          transform.localPosition = endPos;

          if (!show) gameObject.SetActive(false);
      }
  }
  ```

- [ ] **Step 3: Check Unity console for compilation errors**

  Switch to Unity Editor, wait for domain reload, confirm no errors. The new `EventBus` parameter in `Construct()` is resolved from Root scope — `RootLifetimeScope` already registers `EventBus` as a singleton.

- [ ] **Step 4: Assign `_spawnButton` in the Inspector**

  In the Unity Editor, find the `AssetBrowserModule` GameObject in the scene. In the Inspector, assign the Spawn button UI element to the `_spawnButton` field.

- [ ] **Step 5: Commit**

  ```
  git add "Assets/_App/Subsystems/SpatialUi/UI/AssetBrowserModule.cs"
  git commit -m "feat: add spawn button and selected-asset tracking to AssetBrowserModule"
  ```

---

### Task 4: Register `AssetSpawner` in `VrEditingSceneScope`

**Files:**
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`

- [ ] **Step 1: Read the file**

  Open `Assets/_App/Bootstrap/VrEditingSceneScope.cs` and locate the end of `Configure()`.

- [ ] **Step 2: Add the registration**

  At the end of `Configure()`, before the closing brace, add:

  ```csharp
  builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  ```

  The complete `Configure()` method should now end with:

  ```csharp
      builder.Register<AssetImporter>(Lifetime.Scoped);

      var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
      if (undo != null)
          builder.RegisterInstance(undo);

      var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
      if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

      var ikWizard = Object.FindAnyObjectByType<IkSetupWizard>(FindObjectsInactive.Include);
      if (ikWizard != null) builder.RegisterInstance(ikWizard);

      var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
      if (bonePanel != null) builder.RegisterInstance(bonePanel);

      var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
      if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();

      builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
      // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
  }
  ```

- [ ] **Step 3: Check Unity console for compilation errors**

  Switch to Unity Editor, wait for domain reload, confirm no errors.

- [ ] **Step 4: Smoke-test in VrEditing**

  Enter Play mode, navigate to VrEditing, open the UserPanel, click Assets, select a card, press Spawn. Confirm the prefab appears in the scene 1.2 m in front of the camera at Y=0.

- [ ] **Step 5: Commit**

  ```
  git add "Assets/_App/Bootstrap/VrEditingSceneScope.cs"
  git commit -m "feat: register AssetSpawner in VrEditingSceneScope"
  ```

---

### Task 5: Create `SandboxSceneScope`

**Files:**
- Create: `Assets/_App/Bootstrap/SandboxSceneScope.cs`

The Sandbox scene works identically to VrEditing except there is no `UnsavedChangesGuard` (the scene is intentionally throw-away).

- [ ] **Step 1: Create the file**

  Create `Assets/_App/Bootstrap/SandboxSceneScope.cs`:

  ```csharp
  using VContainer;
  using VContainer.Unity;
  using UnityEngine;

  public class SandboxSceneScope : LifetimeScope
  {
      [SerializeField] private PanelRegistry _panelRegistry;

      protected override void Configure(IContainerBuilder builder)
      {
          builder.RegisterInstance(_panelRegistry);
          builder.RegisterInstance(Camera.main);
          builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
          builder.Register<AssetImporter>(Lifetime.Scoped);

          var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
          if (undo != null)
              builder.RegisterInstance(undo);

          var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
          if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

          var ikWizard = Object.FindAnyObjectByType<IkSetupWizard>(FindObjectsInactive.Include);
          if (ikWizard != null) builder.RegisterInstance(ikWizard);

          var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
          if (bonePanel != null) builder.RegisterInstance(bonePanel);

          var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
          if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();

          builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
      }
  }
  ```

- [ ] **Step 2: Check Unity console for compilation errors**

  Switch to Unity Editor, wait for domain reload, confirm no errors.

- [ ] **Step 3: Add `SandboxSceneScope` to the Sandbox scene**

  In the Unity Editor:
  1. Open the `Sandbox` scene.
  2. Create an empty GameObject named `SandboxSceneScope`.
  3. Add the `SandboxSceneScope` component to it.
  4. Set its `Parent` field to the `RootLifetimeScope` instance (so it inherits Root registrations).
  5. Assign the `_panelRegistry` field (same `PanelRegistry` ScriptableObject used by VrEditing).
  6. Save the scene.

- [ ] **Step 4: Smoke-test in Sandbox**

  Enter Play mode, navigate to Sandbox, open the UserPanel, click Assets, select a card, press Spawn. Confirm the prefab appears 1.2 m in front of the camera at Y=0.

- [ ] **Step 5: Commit**

  ```
  git add "Assets/_App/Bootstrap/SandboxSceneScope.cs"
  git add "Assets/Scenes/Sandbox.unity"
  git commit -m "feat: create SandboxSceneScope with SceneGraph and AssetSpawner"
  ```

---

## Self-Review

**Spec coverage:**
- ✅ `AssetSpawnRequestedEvent` struct added to AppEvents
- ✅ `_spawnButton` SerializeField in AssetBrowserModule
- ✅ `_selectedAsset` tracked; button disabled when nothing selected
- ✅ EventBus injected via Construct (4th param)
- ✅ Spawn position: camera forward projected to XZ plane, Y=0, 1.2 m offset
- ✅ Event published on click
- ✅ `AssetSpawner` subscribes, calls SpawnAsync, registers with SceneGraph
- ✅ Registered in VrEditingSceneScope
- ✅ SandboxSceneScope created, AssetSpawner registered there too

**Placeholder scan:** None found. All code blocks are complete.

**Type consistency:**
- `AssetSpawnRequestedEvent` defined in Task 1, used in Task 2 (subscribe) and Task 3 (publish) — consistent
- `AssetSpawner` created in Task 2, registered in Tasks 4 and 5 — consistent
- `ClearSelection()` introduced in Task 3 (`RefreshGrid` calls it) — defined in same file, no cross-task drift
