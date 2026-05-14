# Phase 5: SceneComposition + VrInteraction — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Objects in the VR scene can be selected with the ray interactor; translate/rotate/scale gizmos manipulate the selected object; PropertyPanel displays current transforms; Ctrl+Z undoes the last transform.

**Architecture:** `SceneGraph` is the authoritative list of scene nodes. `SelectionManager` tracks the selected node and publishes `SelectionChangedEvent`. `SelectionInteractor` is an XRI `XRBaseInteractable` wrapper that notifies `SelectionManager` on select. `GizmoController` spawns XRI-grabbable handles around the selection. `CommandStack` records `TransformCommand` objects for undo. All services registered in `VrEditingSceneScope`.

**Tech Stack:** XRI `XRSimpleInteractable`, `IXRSelectInteractor`, VContainer constructor injection, Unity `Transform` API

---

## File Map

**Create:**
- `Assets/Subsystems/SceneComposition/SceneNode.cs`
- `Assets/Subsystems/SceneComposition/SceneGraph.cs`
- `Assets/Subsystems/SceneComposition/SelectionManager.cs`
- `Assets/_Shared/Interfaces/ICommand.cs`
- `Assets/Subsystems/SceneComposition/Data/CommandStack.cs`
- `Assets/Subsystems/SceneComposition/Data/TransformCommand.cs`
- `Assets/Subsystems/SceneComposition/UI/PropertyPanel.cs`
- `Assets/Subsystems/VrInteraction/SelectionInteractor.cs`
- `Assets/Subsystems/VrInteraction/GizmoController.cs`
- `Assets/Subsystems/VrInteraction/GizmoHandle.cs`
- `Assets/_App/Bootstrap/UndoKeyHandler.cs`
- `Assets/Subsystems/SceneComposition/Tests/CommandStackTests.cs`

**Modify:**
- `Assets/_App/Bootstrap/VrEditingSceneScope.cs` — register all above + move AssetImporter here
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — remove AssetImporter registration (moved to Scene scope)
- `Assets/Subsystems/AssetBrowser/AssetImporter.cs` — add SceneGraph + IObjectResolver params, register scene scope

---

## Task 1: SceneNode + SceneGraph

**Files:** `SceneComposition/SceneNode.cs`, `SceneComposition/SceneGraph.cs`

- [ ] Create `Assets/Subsystems/SceneComposition/SceneNode.cs`:
  ```csharp
  using UnityEngine;

  public class SceneNode : MonoBehaviour
  {
      public string NodeId { get; private set; }
      public bool IsVisible { get; private set; } = true;
      public bool IsLocked  { get; private set; }

      public void Init(string nodeId)
      {
          NodeId = nodeId;
      }

      public void SetVisible(bool visible)
      {
          IsVisible = visible;
          gameObject.SetActive(visible);
      }

      public void SetLocked(bool locked) => IsLocked = locked;
  }
  ```

- [ ] Create `Assets/Subsystems/SceneComposition/SceneGraph.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using VContainer.Unity;

  public class SceneGraph : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private readonly Dictionary<string, SceneNode> _nodes = new();

      public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

      public SceneGraph(EventBus bus) => _bus = bus;

      public void Start() =>
          _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

      public void Dispose() =>
          _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);

      public SceneNode AddNode(GameObject go)
      {
          var nodeId = System.Guid.NewGuid().ToString("N")[..8];
          var node   = go.AddComponent<SceneNode>();
          node.Init(nodeId);
          _nodes[nodeId] = node;
          _bus.Publish(new SceneModifiedEvent());
          return node;
      }

      public void RemoveNode(string nodeId)
      {
          if (!_nodes.TryGetValue(nodeId, out var node)) return;
          _nodes.Remove(nodeId);
          Object.Destroy(node.gameObject);
          _bus.Publish(new SceneModifiedEvent());
      }

      public SceneNode GetNode(string nodeId) =>
          _nodes.TryGetValue(nodeId, out var n) ? n : null;

      private void OnSceneOpened(SceneOpenedEvent _)
      {
          // Future: load nodes from SceneData
      }
  }
  ```

- [ ] Wire `AssetImporter.ImportAsync` to register imported GO in SceneGraph. Update `AssetImporter.cs`:
  ```csharp
  // Add constructor parameter:
  private readonly SceneGraph _sceneGraph;

  public AssetImporter(DemoAssetCatalog catalog, AppStorage storage, EventBus bus, SceneGraph sceneGraph)
  {
      _catalog    = catalog;
      _storage    = storage;
      _bus        = bus;
      _sceneGraph = sceneGraph;
  }

  // In ImportAsync, after Instantiate:
  _sceneGraph.AddNode(instance);
  ```

  > Note: SceneGraph is in Scoped lifetime (VrEditingSceneScope) but AssetImporter is Singleton (RootLifetimeScope). Use an interface `ISceneNodeReceiver` in `_Shared/Interfaces/` to decouple them, or inject SceneGraph lazily. For the demo, simplest approach: make AssetImporter Scoped and register it in VrEditingSceneScope instead.

  Update `RootLifetimeScope.cs` — remove `AssetImporter` registration.
  Update `VrEditingSceneScope.cs` — add:
  ```csharp
  builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  builder.Register<AssetImporter>(Lifetime.Scoped);
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SceneComposition/SceneNode.cs Assets/Subsystems/SceneComposition/SceneGraph.cs Assets/Subsystems/AssetBrowser/AssetImporter.cs
  git commit -m "feat: add SceneNode, SceneGraph; wire AssetImporter to register nodes"
  ```

---

## Task 2: SelectionManager

**Files:** `SceneComposition/SelectionManager.cs`

- [ ] Create `Assets/Subsystems/SceneComposition/SelectionManager.cs`:
  ```csharp
  using VContainer.Unity;

  public class SelectionManager : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private string _selectedNodeId;

      public string SelectedNodeId => _selectedNodeId;

      public SelectionManager(EventBus bus) => _bus = bus;

      public void Start() { }
      public void Dispose() { }

      public void Select(string nodeId)
      {
          if (_selectedNodeId == nodeId) return;
          _selectedNodeId = nodeId;
          _bus.Publish(new SelectionChangedEvent { SelectedNodeId = nodeId });
      }

      public void Deselect()
      {
          if (_selectedNodeId == null) return;
          _selectedNodeId = null;
          _bus.Publish(new SelectionChangedEvent { SelectedNodeId = null });
      }
  }
  ```

- [ ] Add to `VrEditingSceneScope`:
  ```csharp
  builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/SceneComposition/SelectionManager.cs
  git commit -m "feat: add SelectionManager publishing SelectionChangedEvent"
  ```

---

## Task 3: CommandStack + TransformCommand + Tests

**Files:** `ICommand.cs`, `CommandStack.cs`, `TransformCommand.cs`, `CommandStackTests.cs`

- [ ] Write failing tests first — create `Assets/Subsystems/SceneComposition/Tests/CommandStackTests.cs`:
  ```csharp
  using NUnit.Framework;
  using UnityEngine;

  public class CommandStackTests
  {
      private CommandStack _sut;

      [SetUp]
      public void SetUp() => _sut = new CommandStack(maxHistory: 5);

      [Test]
      public void Undo_AfterExecute_CallsUndo()
      {
          int undoCalls = 0;
          var cmd = new TestCommand(onUndo: () => undoCalls++);
          _sut.Execute(cmd);
          _sut.Undo();
          Assert.AreEqual(1, undoCalls);
      }

      [Test]
      public void Undo_EmptyStack_DoesNotThrow()
      {
          Assert.DoesNotThrow(() => _sut.Undo());
      }

      [Test]
      public void Execute_ExceedsMaxHistory_DropsOldest()
      {
          int undoCalls = 0;
          var oldest = new TestCommand(onUndo: () => undoCalls++);
          _sut.Execute(oldest);
          for (int i = 0; i < 5; i++)
              _sut.Execute(new TestCommand(onUndo: () => { }));
          // Undo all 5 recent commands
          for (int i = 0; i < 5; i++) _sut.Undo();
          // oldest was dropped, so its undo should not have been called
          Assert.AreEqual(0, undoCalls);
      }

      private class TestCommand : ICommand
      {
          private readonly System.Action _onUndo;
          public TestCommand(System.Action onUndo) => _onUndo = onUndo;
          public void Execute() { }
          public void Undo() => _onUndo();
      }
  }
  ```

- [ ] Run tests → 3 failures

- [ ] Create `Assets/_Shared/Interfaces/ICommand.cs`:
  ```csharp
  public interface ICommand
  {
      void Execute();
      void Undo();
  }
  ```

- [ ] Create `Assets/Subsystems/SceneComposition/Data/CommandStack.cs`:
  ```csharp
  using System.Collections.Generic;

  public class CommandStack
  {
      private readonly int _maxHistory;
      private readonly LinkedList<ICommand> _history = new();

      public CommandStack(int maxHistory = 30) => _maxHistory = maxHistory;

      public void Execute(ICommand command)
      {
          command.Execute();
          _history.AddLast(command);
          if (_history.Count > _maxHistory)
              _history.RemoveFirst();
      }

      public void Undo()
      {
          if (_history.Count == 0) return;
          var cmd = _history.Last.Value;
          _history.RemoveLast();
          cmd.Undo();
      }
  }
  ```

- [ ] Create `Assets/Subsystems/SceneComposition/Data/TransformCommand.cs`:
  ```csharp
  using UnityEngine;

  public class TransformCommand : ICommand
  {
      private readonly Transform _target;
      private readonly Vector3 _newPosition;
      private readonly Quaternion _newRotation;
      private readonly Vector3 _newScale;
      private readonly Vector3 _oldPosition;
      private readonly Quaternion _oldRotation;
      private readonly Vector3 _oldScale;

      public TransformCommand(Transform target, Vector3 newPos, Quaternion newRot, Vector3 newScale)
      {
          _target      = target;
          _oldPosition = target.position;
          _oldRotation = target.rotation;
          _oldScale    = target.localScale;
          _newPosition = newPos;
          _newRotation = newRot;
          _newScale    = newScale;
      }

      public void Execute()
      {
          _target.position   = _newPosition;
          _target.rotation   = _newRotation;
          _target.localScale = _newScale;
      }

      public void Undo()
      {
          _target.position   = _oldPosition;
          _target.rotation   = _oldRotation;
          _target.localScale = _oldScale;
      }
  }
  ```

- [ ] Run tests → 3 passes

- [ ] Add to `VrEditingSceneScope`:
  ```csharp
  builder.Register<CommandStack>(Lifetime.Scoped);
  ```

- [ ] Commit:
  ```bash
  git add Assets/_Shared/Interfaces/ICommand.cs Assets/Subsystems/SceneComposition/Data/ Assets/Subsystems/SceneComposition/Tests/
  git commit -m "feat: add CommandStack, TransformCommand, ICommand with passing tests"
  ```

---

## Task 4: SelectionInteractor (XRI)

**Files:** `VrInteraction/SelectionInteractor.cs`

- [ ] Create `Assets/Subsystems/VrInteraction/SelectionInteractor.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.XR.Interaction.Toolkit;
  using UnityEngine.XR.Interaction.Toolkit.Interactables;
  using VContainer;

  [RequireComponent(typeof(Collider))]
  public class SelectionInteractor : XRSimpleInteractable
  {
      private SelectionManager _selectionManager;
      private SceneNode _node;

      [Inject]
      public void Construct(SelectionManager selectionManager)
      {
          _selectionManager = selectionManager;
      }

      private void Awake() => _node = GetComponentInParent<SceneNode>();

      protected override void OnSelectEntered(SelectEnterEventArgs args)
      {
          base.OnSelectEntered(args);
          if (_node != null)
              _selectionManager.Select(_node.NodeId);
      }
  }
  ```

> `XRSimpleInteractable` is in `UnityEngine.XR.Interaction.Toolkit.Interactables` namespace in XRI 3.x.

- [ ] After importing a model, add `SelectionInteractor` to the root of each instantiated prefab. Update `AssetImporter.ImportAsync`:
  ```csharp
  // After Instantiate:
  var collider = instance.GetComponentInChildren<Collider>();
  if (collider == null)
      collider = instance.AddComponent<BoxCollider>();
  instance.AddComponent<SelectionInteractor>();
  ```

  > VContainer cannot inject into dynamically instantiated MonoBehaviours automatically. Inject manually:
  ```csharp
  var si = instance.AddComponent<SelectionInteractor>();
  _container.InjectGameObject(instance);  // requires IObjectResolver injected into AssetImporter
  ```

  Add `IObjectResolver _container` to `AssetImporter` constructor and registration:
  ```csharp
  // In AssetImporter constructor:
  private readonly IObjectResolver _container;
  public AssetImporter(..., IObjectResolver container) { _container = container; }
  // VContainer auto-injects IObjectResolver — no extra registration needed
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/VrInteraction/SelectionInteractor.cs Assets/Subsystems/AssetBrowser/AssetImporter.cs
  git commit -m "feat: add SelectionInteractor, wire XRI select to SelectionManager"
  ```

---

## Task 5: GizmoController + PropertyPanel

**Files:** `VrInteraction/GizmoController.cs`, `SceneComposition/UI/PropertyPanel.cs`

- [ ] Create `Assets/Subsystems/VrInteraction/GizmoController.cs`:
  ```csharp
  using UnityEngine;
  using VContainer.Unity;

  public class GizmoController : IStartable, IDisposable
  {
      private readonly EventBus _bus;
      private readonly CommandStack _commands;
      private readonly SceneGraph _sceneGraph;

      private SceneNode _target;
      private Vector3 _dragStartPos;

      public GizmoController(EventBus bus, CommandStack commands, SceneGraph sceneGraph)
      {
          _bus       = bus;
          _commands  = commands;
          _sceneGraph = sceneGraph;
      }

      public void Start() =>
          _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);

      public void Dispose() =>
          _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

      private void OnSelectionChanged(SelectionChangedEvent e)
      {
          _target = e.SelectedNodeId != null ? _sceneGraph.GetNode(e.SelectedNodeId) : null;
          // Gizmo handle spawn/despawn handled via prefab in scene (Phase 5 editor setup)
      }

      /// Called by gizmo handle prefab when drag ends (see GizmoHandle MonoBehaviour below)
      public void CommitMove(Transform target, Vector3 newPosition)
      {
          var cmd = new TransformCommand(target, newPosition, target.rotation, target.localScale);
          _commands.Execute(cmd);
      }
  }
  ```

- [ ] Create `Assets/Subsystems/VrInteraction/GizmoHandle.cs` — a grabbable handle that calls `GizmoController.CommitMove` on release:
  ```csharp
  using UnityEngine;
  using UnityEngine.XR.Interaction.Toolkit;
  using UnityEngine.XR.Interaction.Toolkit.Interactables;
  using VContainer;

  public class GizmoHandle : XRGrabInteractable
  {
      private GizmoController _controller;
      private Transform _target;
      private Vector3 _startPos;

      [Inject]
      public void Construct(GizmoController controller) => _controller = controller;

      public void SetTarget(Transform target) => _target = target;

      protected override void OnSelectEntered(SelectEnterEventArgs args)
      {
          base.OnSelectEntered(args);
          _startPos = _target != null ? _target.position : Vector3.zero;
      }

      protected override void OnSelectExited(SelectExitEventArgs args)
      {
          base.OnSelectExited(args);
          if (_target != null)
              _controller.CommitMove(_target, _target.position);
      }
  }
  ```

- [ ] Create `Assets/Subsystems/SceneComposition/UI/PropertyPanel.cs`:
  ```csharp
  using UnityEngine;
  using VContainer;
  using VContainer.Unity;
  using TMPro;

  public class PropertyPanel : SpatialPanel, IStartable, IDisposable
  {
      [SerializeField] private TMP_Text _positionText;
      [SerializeField] private TMP_Text _rotationText;
      [SerializeField] private TMP_Text _scaleText;

      private EventBus _bus;
      private SceneGraph _sceneGraph;

      [Inject]
      public void Construct(EventBus bus, SceneGraph sceneGraph)
      {
          _bus        = bus;
          _sceneGraph = sceneGraph;
      }

      public void Start() => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
      public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

      private void OnSelectionChanged(SelectionChangedEvent e)
      {
          if (e.SelectedNodeId == null)
          {
              ClearDisplay();
              return;
          }
          var node = _sceneGraph.GetNode(e.SelectedNodeId);
          if (node == null) return;
          var t = node.transform;
          _positionText.text = $"Pos: {t.position:F2}";
          _rotationText.text = $"Rot: {t.eulerAngles:F1}";
          _scaleText.text    = $"Scl: {t.localScale:F2}";
      }

      private void ClearDisplay()
      {
          _positionText.text = "Pos: —";
          _rotationText.text = "Rot: —";
          _scaleText.text    = "Scl: —";
      }
  }
  ```

- [ ] Add to `VrEditingSceneScope`:
  ```csharp
  builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  ```

- [ ] **In Unity Editor:**
  1. Create `PropertyPanel` prefab (World Space Canvas with 3 TMP_Text labels)
  2. Add to `PanelRegistry.asset`: Id = Properties, VisibleInModes = [VrEditing]
  3. Create a simple `GizmoHandles` prefab (3 XRI grab-interactable arrow shapes for X/Y/Z, each with `GizmoHandle` component)
  4. Subscribe `GizmoHandle` to receive injection via `container.InjectGameObject(gizmoInstance)` when spawned

- [ ] Add keyboard shortcut for Undo: in `VrEditingSceneScope`, register a MonoBehaviour that calls `commandStack.Undo()` on `Ctrl+Z`:
  ```csharp
  // Assets/_App/Bootstrap/UndoKeyHandler.cs
  using UnityEngine;
  using VContainer;

  public class UndoKeyHandler : MonoBehaviour
  {
      private CommandStack _commandStack;

      [Inject]
      public void Construct(CommandStack commandStack) => _commandStack = commandStack;

      private void Update()
      {
          if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
              && Input.GetKeyDown(KeyCode.Z))
              _commandStack.Undo();
      }
  }
  ```
  Add to VrEditing scene; register in scope using `builder.RegisterComponentInHierarchy<UndoKeyHandler>()`.

- [ ] Press Play → import model → click on it → PropertyPanel updates → drag gizmo handle → move object → Ctrl+Z → object returns to previous position

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/VrInteraction/ Assets/Subsystems/SceneComposition/UI/ Assets/_App/Bootstrap/UndoKeyHandler.cs
  git commit -m "feat: GizmoController, PropertyPanel, UndoKeyHandler — selection and transform editing"
  ```

---

## Phase 5 Verification

- [ ] CommandStack tests: 3 passing
- [ ] Import model → ray click → `SelectionChangedEvent` logs → PropertyPanel updates
- [ ] Drag gizmo handle → object moves → Ctrl+Z → object returns to prior position
- [ ] `SceneModifiedEvent` fires when node added (verify `UnsavedChangesGuard.IsDirty` becomes true)
