# Phase 2: SpatialUi + ModeOrchestrator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ToolbarPanel floats body-locked in VR space, ray interactor clicks UGUI buttons, and transitioning MainMenu ↔ VrEditing fires `ModeChangedEvent` and shows/hides panels correctly.

**Architecture:** `ModeOrchestrator` owns the `AppStateMachine` and fires `ModeChangedEvent` via the scene-scoped `EventBus`. `UiPanelManager` subscribes and toggles panels. `SpatialPanel` is a MonoBehaviour base that handles body-locking, billboard rotation, and XRI UI interaction. `ToolbarPanel` extends it with mode/transport buttons. `PanelRegistry` ScriptableObject stores default panel positions.

**Tech Stack:** Unity UGUI, XRI `XRUIInputModule`, VContainer injection via `[Inject]` on MonoBehaviours

---

## File Map

**Create:**
- `Assets/Subsystems/ModeOrchestrator/ModeOrchestrator.cs`
- `Assets/Subsystems/ModeOrchestrator/Data/ModeTransitionGraph.cs`
- `Assets/Subsystems/SpatialUi/SpatialPanel.cs`
- `Assets/Subsystems/SpatialUi/UiPanelManager.cs`
- `Assets/Subsystems/SpatialUi/UI/ToolbarPanel.cs`
- `Assets/Subsystems/SpatialUi/Data/PanelRegistry.cs`
- `Assets/Subsystems/SpatialUi/Data/PanelType.cs`
- `Assets/_Shared/Models/PanelId.cs`

**Modify:**
- `Assets/_App/Bootstrap/MainMenuSceneScope.cs` — register ModeOrchestrator
- `Assets/_App/Bootstrap/VrEditingSceneScope.cs` — register UiPanelManager, SpatialPanel instances

**Unity Editor:**
- Add `XR UI Input Module` to VrEditing scene
- Create `ToolbarPanel` prefab with Canvas + buttons
- Create `PanelRegistry.asset`

---

## Task 1: AppMode Model + PanelId

**Files:** `_Shared/Models/PanelId.cs`

- [ ] Create `Assets/_Shared/Models/PanelId.cs`:
  ```csharp
  public enum PanelId
  {
      Toolbar,
      AssetBrowser,
      Properties,
      RigBuilder,
      KeyframeEditor,
      SceneOutliner,
      ComingSoon
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/_Shared/Models/PanelId.cs
  git commit -m "feat: add PanelId enum to _Shared"
  ```

---

## Task 2: ModeOrchestrator

**Files:** `ModeOrchestrator.cs`, `Data/ModeTransitionGraph.cs`

- [ ] Create `Assets/Subsystems/ModeOrchestrator/Data/ModeTransitionGraph.cs`:
  ```csharp
  using UnityEngine;
  using System.Collections.Generic;

  [CreateAssetMenu(fileName = "ModeTransitionGraph", menuName = "PromeonLab/ModeTransitionGraph")]
  public class ModeTransitionGraph : ScriptableObject
  {
      [System.Serializable]
      public struct Transition { public AppMode From; public AppMode To; }

      [SerializeField] private List<Transition> _allowed = new()
      {
          new Transition { From = AppMode.MainMenu,  To = AppMode.VrEditing  },
          new Transition { From = AppMode.VrEditing, To = AppMode.MainMenu   },
          new Transition { From = AppMode.VrEditing, To = AppMode.ArMapping  },
          new Transition { From = AppMode.ArMapping,  To = AppMode.VrEditing },
      };

      public bool IsAllowed(AppMode from, AppMode to)
      {
          foreach (var t in _allowed)
              if (t.From == from && t.To == to) return true;
          return false;
      }
  }
  ```

- [ ] Create `Assets/Subsystems/ModeOrchestrator/ModeOrchestrator.cs`:
  ```csharp
  using VContainer;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public class ModeOrchestrator
  {
      private readonly EventBus _bus;
      private readonly ModeTransitionGraph _graph;

      private AppMode _current = AppMode.MainMenu;
      public AppMode CurrentMode => _current;

      public ModeOrchestrator(EventBus bus, ModeTransitionGraph graph)
      {
          _bus   = bus;
          _graph = graph;
      }

      public void TransitionTo(AppMode target)
      {
          if (_current == target) return;
          if (!_graph.IsAllowed(_current, target))
          {
              Debug.LogWarning($"Transition {_current} → {target} not allowed");
              return;
          }

          var prev = _current;
          _current = target;

          UnloadCurrentScene(prev);
          LoadScene(target);

          _bus.Publish(new ModeChangedEvent { PreviousMode = prev, CurrentMode = target });
      }

      private void LoadScene(AppMode mode)
      {
          var sceneName = mode switch
          {
              AppMode.MainMenu  => "MainMenu",
              AppMode.VrEditing => "VrEditing",
              _                 => null
          };
          if (sceneName != null)
              SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
      }

      private void UnloadCurrentScene(AppMode mode)
      {
          var sceneName = mode switch
          {
              AppMode.MainMenu  => "MainMenu",
              AppMode.VrEditing => "VrEditing",
              _                 => null
          };
          if (sceneName != null && SceneManager.GetSceneByName(sceneName).isLoaded)
              SceneManager.UnloadSceneAsync(sceneName);
      }
  }
  ```

- [ ] Register in `Assets/_App/Bootstrap/MainMenuSceneScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;
  using UnityEngine;

  public class MainMenuSceneScope : LifetimeScope
  {
      [SerializeField] private ModeTransitionGraph _transitionGraph;

      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Scoped);
          builder.RegisterInstance(_transitionGraph);
          builder.Register<ModeOrchestrator>(Lifetime.Scoped);
      }
  }
  ```

- [ ] In Unity: create `Assets/Subsystems/ModeOrchestrator/Data/DefaultModeTransitionGraph.asset`
  - Right-click in Project → Create → PromeonLab → ModeTransitionGraph
  - Assign to `MainMenuSceneScope._transitionGraph` field in inspector

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/ModeOrchestrator/ Assets/_App/Bootstrap/MainMenuSceneScope.cs
  git commit -m "feat: add ModeOrchestrator with ModeTransitionGraph SO"
  ```

---

## Task 3: SpatialPanel Base + PanelType

**Files:** `SpatialPanel.cs`, `Data/PanelType.cs`, `Data/PanelRegistry.cs`

- [ ] Create `Assets/Subsystems/SpatialUi/Data/PanelType.cs`:
  ```csharp
  public enum PanelType { BodyLocked, WorldFixed, Free }
  ```

- [ ] Create `Assets/Subsystems/SpatialUi/SpatialPanel.cs`:
  ```csharp
  using UnityEngine;
  using VContainer;

  [RequireComponent(typeof(Canvas))]
  public class SpatialPanel : MonoBehaviour
  {
      [SerializeField] private PanelType _panelType = PanelType.BodyLocked;
      [SerializeField] private bool _billboard = true;
      [SerializeField] private Vector3 _defaultOffset = new Vector3(0, 0, 1.2f);

      public PanelId PanelId { get; private set; }

      private Transform _cameraTransform;

      public void Init(PanelId id, Transform cameraTransform)
      {
          PanelId = id;
          _cameraTransform = cameraTransform;
      }

      private void LateUpdate()
      {
          if (_cameraTransform == null) return;

          if (_panelType == PanelType.BodyLocked)
              FollowCamera();

          if (_billboard)
              FaceCamera();
      }

      private void FollowCamera()
      {
          var cam = _cameraTransform;
          transform.position = cam.position + cam.rotation * _defaultOffset;
      }

      private void FaceCamera()
      {
          var dir = transform.position - _cameraTransform.position;
          if (dir.sqrMagnitude > 0.001f)
              transform.rotation = Quaternion.LookRotation(dir);
      }

      public void SetVisible(bool visible) => gameObject.SetActive(visible);
  }
  ```

- [ ] Create `Assets/Subsystems/SpatialUi/Data/PanelRegistry.cs`:
  ```csharp
  using UnityEngine;
  using System.Collections.Generic;

  [CreateAssetMenu(fileName = "PanelRegistry", menuName = "PromeonLab/PanelRegistry")]
  public class PanelRegistry : ScriptableObject
  {
      [System.Serializable]
      public struct PanelEntry
      {
          public PanelId Id;
          public SpatialPanel Prefab;
          public AppMode[] VisibleInModes;
      }

      [SerializeField] private List<PanelEntry> _panels = new();

      public IReadOnlyList<PanelEntry> Panels => _panels;

      public bool IsVisibleIn(PanelId id, AppMode mode)
      {
          foreach (var entry in _panels)
          {
              if (entry.Id != id) continue;
              foreach (var m in entry.VisibleInModes)
                  if (m == mode) return true;
          }
          return false;
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/
  git commit -m "feat: add SpatialPanel base, PanelType, PanelRegistry SO"
  ```

---

## Task 4: UiPanelManager

**Files:** `UiPanelManager.cs`

- [ ] Create `Assets/Subsystems/SpatialUi/UiPanelManager.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using VContainer;
  using VContainer.Unity;

  public class UiPanelManager : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private readonly PanelRegistry _registry;
      private readonly Transform _cameraTransform;

      private readonly Dictionary<PanelId, SpatialPanel> _panels = new();
      private AppMode _currentMode = AppMode.VrEditing;

      public UiPanelManager(EventBus bus, PanelRegistry registry, Camera mainCamera)
      {
          _bus             = bus;
          _registry        = registry;
          _cameraTransform = mainCamera.transform;
      }

      public void Start()
      {
          _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
          SpawnPanels();
      }

      public void Dispose()
      {
          _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);
      }

      private void SpawnPanels()
      {
          foreach (var entry in _registry.Panels)
          {
              var panel = Object.Instantiate(entry.Prefab);
              panel.Init(entry.Id, _cameraTransform);
              _panels[entry.Id] = panel;
          }
          RefreshVisibility();
      }

      private void OnModeChanged(ModeChangedEvent e)
      {
          _currentMode = e.CurrentMode;
          RefreshVisibility();
      }

      private void RefreshVisibility()
      {
          foreach (var (id, panel) in _panels)
              panel.SetVisible(_registry.IsVisibleIn(id, _currentMode));
      }

      public SpatialPanel GetPanel(PanelId id) =>
          _panels.TryGetValue(id, out var p) ? p : null;
  }
  ```

- [ ] Register in `VrEditingSceneScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;
  using UnityEngine;

  public class VrEditingSceneScope : LifetimeScope
  {
      [SerializeField] private PanelRegistry _panelRegistry;

      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Scoped);
          builder.RegisterInstance(_panelRegistry);
          builder.RegisterInstance(Camera.main);   // injected into UiPanelManager
          builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
          // SceneGraph, SelectionManager — later phases
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/UiPanelManager.cs Assets/_App/Bootstrap/VrEditingSceneScope.cs
  git commit -m "feat: add UiPanelManager, register in VrEditingSceneScope"
  ```

---

## Task 5: ToolbarPanel

**Files:** `UI/ToolbarPanel.cs`

- [ ] Create `Assets/Subsystems/SpatialUi/UI/ToolbarPanel.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;

  public class ToolbarPanel : SpatialPanel
  {
      [SerializeField] private Button _openAssetBrowserButton;
      [SerializeField] private Button _openSceneOutlinerButton;

      private UiPanelManager _panelManager;

      [Inject]
      public void Construct(UiPanelManager panelManager)
      {
          _panelManager = panelManager;
      }

      private void Awake()
      {
          _openAssetBrowserButton.onClick.AddListener(OnAssetBrowserClicked);
          _openSceneOutlinerButton.onClick.AddListener(OnSceneOutlinerClicked);
      }

      private void OnAssetBrowserClicked() =>
          _panelManager.GetPanel(PanelId.AssetBrowser)?.SetVisible(true);

      private void OnSceneOutlinerClicked() =>
          _panelManager.GetPanel(PanelId.SceneOutliner)?.SetVisible(true);
  }
  ```

- [ ] **Create ToolbarPanel prefab (Unity Editor):**
  1. In VrEditing.unity, create GameObject `ToolbarPanel`
  2. Add component `Canvas` → Render Mode: **World Space**, Layer: UI
  3. Add component `ToolbarPanel` (your script)
  4. Add component `GraphicRaycaster`
  5. Add child UI Panel (Image background)
  6. Add two Button children: "Assets" and "Scene"
  7. Wire buttons to `_openAssetBrowserButton` and `_openSceneOutlinerButton` fields
  8. Set `_defaultOffset` on the ToolbarPanel component: `(0, -0.3f, 0.8f)` (below eye level)
  9. Drag to `Assets/Subsystems/SpatialUi/UI/ToolbarPanel.prefab` — save as prefab
  10. Add to `PanelRegistry.asset` entry: Id = Toolbar, VisibleInModes = [VrEditing]

- [ ] **Add XR UI Input Module (Unity Editor — VrEditing.unity):**
  1. Find `EventSystem` in hierarchy (or create one: GameObject → UI → Event System)
  2. Remove `Standalone Input Module` component
  3. Add `XR UI Input Module` component (from XRI package)
  4. On each `XR Ray Interactor` (L and R): verify `Enable UI Interaction` is checked

- [ ] Press Play in VrEditing → verify:
  - ToolbarPanel appears in front of camera
  - Pointing ray at a button highlights it
  - Simulated trigger click (left mouse button by default in XR Simulator) fires button event

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/UI/ Assets/Scenes/VrEditing.unity
  git commit -m "feat: add ToolbarPanel, XR UI Input Module, panel visibility system"
  ```

---

## Task 6: Mode Transition Button (MainMenu)

**Files:** minimal `MainMenuPanel.cs` for transition testing

- [ ] Create `Assets/Subsystems/SpatialUi/UI/MainMenuPanel.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;

  public class MainMenuPanel : MonoBehaviour
  {
      [SerializeField] private Button _openEditorButton;

      private ModeOrchestrator _orchestrator;

      [Inject]
      public void Construct(ModeOrchestrator orchestrator)
      {
          _orchestrator = orchestrator;
      }

      private void Awake() =>
          _openEditorButton.onClick.AddListener(() => _orchestrator.TransitionTo(AppMode.VrEditing));
  }
  ```

- [ ] Create a minimal UI Canvas in `MainMenu.unity` with "Open Editor" button, attach `MainMenuPanel`

- [ ] Press Play from Bootstrap → verify: clicking "Open Editor" unloads MainMenu, loads VrEditing, ToolbarPanel appears

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SpatialUi/UI/MainMenuPanel.cs Assets/Scenes/MainMenu.unity
  git commit -m "feat: add MainMenuPanel with mode transition button"
  ```

---

## Phase 2 Verification

- [ ] Playing Bootstrap.unity → MainMenu loads, "Open Editor" button visible and clickable
- [ ] Clicking "Open Editor" → VrEditing loads, ToolbarPanel appears body-locked in front of camera
- [ ] Ray interactor line renders; hovering a button highlights it; clicking fires the event
- [ ] `ModeChangedEvent` fires (add `Debug.Log` in UiPanelManager.OnModeChanged to verify)
- [ ] No VContainer injection errors in Console
