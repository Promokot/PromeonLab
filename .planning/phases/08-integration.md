# Phase 8: Integration, Stubs, Polish — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Full golden path runs end-to-end without crashes. AR Mapping and Export show "Coming Soon" panels. Errors surface as toast notifications. The demo is presentable.

**Architecture:** `ErrorDispatcher` is a Singleton service that routes `ErrorOccurredEvent` to the UI (toast) and the Unity Console. `ComingSoonPanel` is a reusable `SpatialPanel` with a label. `ToastNotification` is a pooled UI element in `ToolbarPanel` area. All subsystems publish errors via `ErrorBus`; no silent swallowing.

---

## File Map

**Create:**
- `Assets/Subsystems/ErrorHandling/ErrorDispatcher.cs`
- `Assets/Subsystems/ErrorHandling/ErrorRecord.cs`
- `Assets/Subsystems/SpatialUi/UI/ToastNotification.cs`
- `Assets/Subsystems/SpatialUi/UI/ComingSoonPanel.cs`

**Modify:**
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register ErrorDispatcher
- `Assets/Subsystems/AssetBrowser/AssetImporter.cs` — wrap async in try-catch → ErrorDispatcher
- `Assets/Subsystems/StorageCore/AppStorage.cs` — wrap async in try-catch → ErrorDispatcher

---

## Task 1: ErrorDispatcher

**Files:** `ErrorHandling/ErrorDispatcher.cs`, `ErrorHandling/ErrorRecord.cs`

- [ ] Create `Assets/Subsystems/ErrorHandling/ErrorRecord.cs`:
  ```csharp
  using System;

  [Serializable]
  public class ErrorRecord
  {
      public DateTime Timestamp;
      public ErrorLevel Level;
      public string Message;
      public string StackTrace;
  }
  ```

- [ ] Create `Assets/Subsystems/ErrorHandling/ErrorDispatcher.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using VContainer.Unity;

  public class ErrorDispatcher : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private readonly List<ErrorRecord> _log = new();

      public IReadOnlyList<ErrorRecord> Log => _log;

      public ErrorDispatcher(EventBus bus) => _bus = bus;

      public void Start() => _bus.Subscribe<ErrorOccurredEvent>(OnError);
      public void Dispose() => _bus.Unsubscribe<ErrorOccurredEvent>(OnError);

      public void Dispatch(ErrorLevel level, string message, string stackTrace = null)
      {
          var record = new ErrorRecord
          {
              Timestamp  = System.DateTime.UtcNow,
              Level      = level,
              Message    = message,
              StackTrace = stackTrace ?? System.Environment.StackTrace
          };
          _log.Add(record);

          switch (level)
          {
              case ErrorLevel.Warning:  Debug.LogWarning($"[Warning] {message}"); break;
              case ErrorLevel.Error:    Debug.LogError($"[Error] {message}");     break;
              case ErrorLevel.Critical: Debug.LogError($"[CRITICAL] {message}"); break;
          }

          _bus.Publish(new ErrorOccurredEvent { Level = level, Message = message });
      }

      private void OnError(ErrorOccurredEvent e)
      {
          // Already logged via Dispatch; this subscription is for UI listeners
      }
  }
  ```

- [ ] Register in `RootLifetimeScope.cs`:
  ```csharp
  builder.Register<ErrorDispatcher>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/ErrorHandling/ Assets/_App/Bootstrap/RootLifetimeScope.cs
  git commit -m "feat: add ErrorDispatcher with ErrorRecord log"
  ```

---

## Task 2: Toast Notification UI

**Files:** `SpatialUi/UI/ToastNotification.cs`

- [ ] Create `Assets/Subsystems/SpatialUi/UI/ToastNotification.cs`:
  ```csharp
  using System.Collections;
  using UnityEngine;
  using TMPro;
  using VContainer;
  using VContainer.Unity;

  public class ToastNotification : MonoBehaviour, IStartable, IDisposable
  {
      [SerializeField] private TMP_Text _messageText;
      [SerializeField] private float _displaySeconds = 3f;

      private EventBus _bus;

      [Inject]
      public void Construct(EventBus bus) => _bus = bus;

      public void Start() => _bus.Subscribe<ErrorOccurredEvent>(OnError);
      public void Dispose() => _bus.Unsubscribe<ErrorOccurredEvent>(OnError);

      private void OnError(ErrorOccurredEvent e) => Show(e.Message);

      public void Show(string message)
      {
          gameObject.SetActive(true);
          _messageText.text = message;
          StopAllCoroutines();
          StartCoroutine(HideAfterDelayRoutine());
      }

      private IEnumerator HideAfterDelayRoutine()
      {
          yield return new WaitForSeconds(_displaySeconds);
          gameObject.SetActive(false);
      }
  }
  ```

- [ ] **In Unity Editor:**
  1. Add a small overlay panel to `ToolbarPanel` prefab (bottom area, TMP_Text, initially hidden)
  2. Attach `ToastNotification` component
  3. Inject via `container.InjectGameObject(toolbarPanelInstance)` or register as component in hierarchy

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/UI/ToastNotification.cs
  git commit -m "feat: ToastNotification — shows ErrorOccurredEvent messages for 3s"
  ```

---

## Task 3: Coming Soon Panels

**Files:** `SpatialUi/UI/ComingSoonPanel.cs`

- [ ] Create `Assets/Subsystems/SpatialUi/UI/ComingSoonPanel.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.UI;
  using TMPro;

  public class ComingSoonPanel : SpatialPanel
  {
      [SerializeField] private TMP_Text _featureLabel;
      [SerializeField] private Button _closeButton;

      private void Awake() =>
          _closeButton.onClick.AddListener(() => SetVisible(false));

      public void SetFeatureName(string name) =>
          _featureLabel.text = $"{name}\n\nThis feature is coming soon.";
  }
  ```

- [ ] **In Unity Editor:**
  1. Create `ComingSoonPanel.prefab` (World Space Canvas, TMP_Text, Close button)
  2. Add to `PanelRegistry.asset`: Id = ComingSoon, VisibleInModes = []  *(opened programmatically only)*

- [ ] In `MainMenuPanel.cs` — add AR Mapping button:
  ```csharp
  [SerializeField] private Button _arMappingButton;

  // In Awake:
  _arMappingButton.onClick.AddListener(OnArMappingClicked);

  private void OnArMappingClicked()
  {
      var panel = _panelManager.GetPanel(PanelId.ComingSoon) as ComingSoonPanel;
      panel?.SetFeatureName("AR Mapping");
      panel?.SetVisible(true);
  }
  ```

- [ ] In `ToolbarPanel.cs` — add Export button:
  ```csharp
  [SerializeField] private Button _exportButton;

  // In Awake:
  _exportButton.onClick.AddListener(OnExportClicked);

  private void OnExportClicked()
  {
      var panel = _panelManager.GetPanel(PanelId.ComingSoon) as ComingSoonPanel;
      panel?.SetFeatureName("Export Pipeline");
      panel?.SetVisible(true);
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/UI/ComingSoonPanel.cs Assets/Subsystems/SpatialUi/UI/MainMenuPanel.cs Assets/Subsystems/SpatialUi/UI/ToolbarPanel.cs
  git commit -m "feat: ComingSoonPanel for AR Mapping and Export — stubs complete"
  ```

---

## Task 4: Error Wrapping in Async Methods

**Files:** `StorageCore/AppStorage.cs`, `AssetBrowser/AssetImporter.cs`

- [ ] Wrap `AppStorage.LoadSceneAsync` with error handling:
  ```csharp
  // Inject ErrorDispatcher into AppStorage constructor:
  private readonly ErrorDispatcher _errors;
  public AppStorage(PathProvider paths, EventBus bus, ErrorDispatcher errors)
  {
      _paths  = paths;
      _bus    = bus;
      _errors = errors;
  }

  public async Task<SceneData> LoadSceneAsync(string sceneId, CancellationToken ct = default)
  {
      try
      {
          // ... existing implementation ...
      }
      catch (System.Exception ex)
      {
          _errors.Dispatch(ErrorLevel.Error, $"Failed to load scene '{sceneId}': {ex.Message}", ex.StackTrace);
          return null;
      }
  }
  ```

- [ ] Wrap `AssetImporter.ImportAsync` similarly:
  ```csharp
  // Inject ErrorDispatcher:
  private readonly ErrorDispatcher _errors;

  // Wrap try-catch around the body of ImportAsync
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/StorageCore/AppStorage.cs Assets/Subsystems/AssetBrowser/AssetImporter.cs
  git commit -m "feat: wrap async methods with ErrorDispatcher error reporting"
  ```

---

## Task 5: Full Golden Path Walkthrough

- [ ] Press Play from Bootstrap → verify each step:

  | Step | Expected |
  |---|---|
  | Bootstrap loads | MainMenu scene loads, ScenePickerView visible |
  | Click "Create Scene" | Scene created, VrEditing loads, ToolbarPanel visible |
  | Click "Assets" | AssetBrowserPanel appears |
  | Click "Import" | SimpleFileBrowser opens |
  | Select catalog model | Model instantiates in scene |
  | Ray click model | PropertyPanel updates with transforms |
  | Drag gizmo | Object moves; Ctrl+Z undoes |
  | Click "Rig" in toolbar | RigBuilderPanel opens |
  | Click "Build Rig" | Skeleton lines appear, bone proxies visible |
  | Ray click bone | Bone selected |
  | Open KeyframeEditor | Timeline panel visible |
  | Scrub to frame 0, click "Set Key" | Keyframe recorded |
  | Scrub to frame 30, move bone, "Set Key" | Second keyframe recorded |
  | Click "Play" | Bone animates; slider moves |
  | Click "Stop" | Bone resets to frame 0 |
  | Click "AR Mapping" (MainMenu) | Coming Soon panel shows |
  | Click "Export" (Toolbar) | Coming Soon panel shows |

- [ ] Fix any blocking issues found during walkthrough

- [ ] Commit with bug fixes:
  ```bash
  git add -u
  git commit -m "fix: golden path walkthrough — address integration issues"
  ```

---

## Phase 8 Verification

- [ ] Golden path table above: all rows pass
- [ ] AR Mapping and Export show Coming Soon panels (not crashes)
- [ ] Triggering a warning (import unknown file) shows toast for 3 seconds
- [ ] `ErrorDispatcher._log` accumulates entries (verify in debugger or Debug.Log)
- [ ] No uncaught exceptions across the full session
