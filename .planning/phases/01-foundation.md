# Phase 1: Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete project scaffold — folder structure, assembly definitions, VContainer scope chain, and a working XR Simulator session in VrEditing scene.

**Architecture:** Three-scene structure (Bootstrap → MainMenu → VrEditing) with three VContainer LifetimeScopes (Root/Scene/Feature). XRI provides XR Origin, Ray Interactor, and XR Simulator support. Shared event types and EventBus declared in `_Shared`. All subsystem asmdefs created empty, ready for later phases.

**Tech Stack:** Unity 6000.3.7f1, VContainer 1.18.0, XR Interaction Toolkit 3.x, OpenXR 1.16.1, URP 17.3.0

---

## File Map

**Create:**
- `Packages/manifest.json` — add XRI
- `Assets/_Shared/_Shared.asmdef`
- `Assets/Subsystems/{Name}/Subsystems.{Name}.asmdef` × 13
- `Assets/Editor/PromeonLab.Editor.asmdef`
- `Assets/_Shared/Events/EventBus.cs`
- `Assets/_Shared/Events/AppEvents.cs`
- `Assets/_Shared/Models/AppMode.cs`
- `Assets/_Shared/Models/ErrorLevel.cs`
- `Assets/_App/Bootstrap/RootLifetimeScope.cs`
- `Assets/_App/Bootstrap/MainMenuSceneScope.cs`
- `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- `Assets/_App/Bootstrap/AppBootstrap.cs`

**Unity Editor (no code):**
- `Assets/Scenes/Bootstrap.unity`
- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/VrEditing.unity`

---

## Task 1: Add XR Interaction Toolkit

**Files:** `Packages/manifest.json`

- [ ] Add to `"dependencies"` in `Packages/manifest.json`:
  ```json
  "com.unity.xr.interaction.toolkit": "3.0.7",
  ```

- [ ] Open Unity — wait for import to complete (Package Manager resolves all XRI dependencies automatically)

- [ ] Import samples via Window → Package Manager → XR Interaction Toolkit → Samples tab:
  - **XR Device Simulator** — enables keyboard/mouse controller emulation
  - **Starter Assets** — provides default Input Actions asset

- [ ] Commit:
  ```bash
  git add Packages/
  git commit -m "feat: add XR Interaction Toolkit 3.x"
  ```

---

## Task 2: Create Folder Structure

**Files:** directories only

- [ ] Run from project root (PowerShell or bash):
  ```bash
  mkdir -p Assets/_App/Bootstrap Assets/_App/Scenes Assets/_App/DemoAssets
  mkdir -p Assets/_Shared/Events Assets/_Shared/Interfaces Assets/_Shared/Models Assets/_Shared/UI
  mkdir -p Assets/Subsystems/StorageCore/Data Assets/Subsystems/StorageCore/Tests
  mkdir -p Assets/Subsystems/AssetBrowser/Data Assets/Subsystems/AssetBrowser/UI Assets/Subsystems/AssetBrowser/Tests
  mkdir -p Assets/Subsystems/SceneComposition/Data Assets/Subsystems/SceneComposition/UI Assets/Subsystems/SceneComposition/Tests
  mkdir -p Assets/Subsystems/EnvironmentMapping/Data
  mkdir -p Assets/Subsystems/RigBuilder/Data Assets/Subsystems/RigBuilder/UI Assets/Subsystems/RigBuilder/Tests
  mkdir -p Assets/Subsystems/AnimationAuthoring/Data Assets/Subsystems/AnimationAuthoring/UI Assets/Subsystems/AnimationAuthoring/Tests
  mkdir -p Assets/Subsystems/AnimationPlayback/Data Assets/Subsystems/AnimationPlayback/Tests
  mkdir -p Assets/Subsystems/ExportPipeline/Data
  mkdir -p Assets/Subsystems/InputBindings/Data
  mkdir -p Assets/Subsystems/ModeOrchestrator/Data
  mkdir -p Assets/Subsystems/VrInteraction/Data Assets/Subsystems/VrInteraction/Tests
  mkdir -p Assets/Subsystems/SpatialUi/Data Assets/Subsystems/SpatialUi/UI
  mkdir -p Assets/Subsystems/ErrorHandling
  mkdir -p Assets/Editor Assets/Resources
  ```

- [ ] Switch to Unity Editor → right-click in Project → Refresh (Ctrl+R) to import .meta files

- [ ] Commit:
  ```bash
  git add Assets/
  git commit -m "feat: create project folder structure"
  ```

---

## Task 3: Create Assembly Definitions

**Files:** `.asmdef` JSON files

- [ ] Create `Assets/_Shared/_Shared.asmdef`:
  ```json
  {
      "name": "_Shared",
      "references": ["VContainer"],
      "includePlatforms": [],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": false,
      "precompiledReferences": [],
      "autoReferenced": true,
      "defineConstraints": [],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```

- [ ] Create `Assets/Subsystems/StorageCore/Subsystems.StorageCore.asmdef`:
  ```json
  { "name": "Subsystems.StorageCore", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/AssetBrowser/Subsystems.AssetBrowser.asmdef`:
  ```json
  { "name": "Subsystems.AssetBrowser", "references": ["_Shared", "VContainer", "SimpleFileBrowser.Runtime"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef`:
  ```json
  { "name": "Subsystems.SceneComposition", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/EnvironmentMapping/Subsystems.EnvironmentMapping.asmdef`:
  ```json
  { "name": "Subsystems.EnvironmentMapping", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef`:
  ```json
  { "name": "Subsystems.RigBuilder", "references": ["_Shared", "VContainer", "Unity.Animation.Rigging"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/AnimationAuthoring/Subsystems.AnimationAuthoring.asmdef`:
  ```json
  { "name": "Subsystems.AnimationAuthoring", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/AnimationPlayback/Subsystems.AnimationPlayback.asmdef`:
  ```json
  { "name": "Subsystems.AnimationPlayback", "references": ["_Shared", "VContainer", "Unity.Animation.Rigging"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/ExportPipeline/Subsystems.ExportPipeline.asmdef`:
  ```json
  { "name": "Subsystems.ExportPipeline", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/InputBindings/Subsystems.InputBindings.asmdef`:
  ```json
  { "name": "Subsystems.InputBindings", "references": ["_Shared", "VContainer", "Unity.InputSystem"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/ModeOrchestrator/Subsystems.ModeOrchestrator.asmdef`:
  ```json
  { "name": "Subsystems.ModeOrchestrator", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef`:
  ```json
  { "name": "Subsystems.VrInteraction", "references": ["_Shared", "VContainer", "Unity.XR.Interaction.Toolkit", "Unity.InputSystem"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/SpatialUi/Subsystems.SpatialUi.asmdef`:
  ```json
  { "name": "Subsystems.SpatialUi", "references": ["_Shared", "VContainer", "Unity.XR.Interaction.Toolkit"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Subsystems/ErrorHandling/Subsystems.ErrorHandling.asmdef`:
  ```json
  { "name": "Subsystems.ErrorHandling", "references": ["_Shared", "VContainer"], "autoReferenced": false }
  ```

- [ ] Create `Assets/Editor/PromeonLab.Editor.asmdef`:
  ```json
  { "name": "PromeonLab.Editor", "references": ["_Shared"], "includePlatforms": ["Editor"], "excludePlatforms": [], "autoReferenced": false }
  ```

- [ ] Switch to Unity → verify no compile errors (assemblies are empty — only .asmdef files exist)

- [ ] Commit:
  ```bash
  git add Assets/
  git commit -m "feat: add assembly definitions for all subsystems"
  ```

---

## Task 4: Shared Types

**Files:** `_Shared/Events/EventBus.cs`, `_Shared/Events/AppEvents.cs`, `_Shared/Models/AppMode.cs`, `_Shared/Models/ErrorLevel.cs`

- [ ] Create `Assets/_Shared/Events/EventBus.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  public class EventBus
  {
      private readonly Dictionary<Type, List<object>> _handlers = new();

      public void Subscribe<T>(Action<T> handler) where T : struct
      {
          var type = typeof(T);
          if (!_handlers.ContainsKey(type))
              _handlers[type] = new List<object>();
          _handlers[type].Add(handler);
      }

      public void Unsubscribe<T>(Action<T> handler) where T : struct
      {
          if (_handlers.TryGetValue(typeof(T), out var list))
              list.Remove(handler);
      }

      public void Publish<T>(T message) where T : struct
      {
          if (!_handlers.TryGetValue(typeof(T), out var list)) return;
          foreach (var handler in list.ToArray())
              ((Action<T>)handler).Invoke(message);
      }
  }
  ```

- [ ] Create `Assets/_Shared/Events/AppEvents.cs`:
  ```csharp
  public struct SceneOpenedEvent  { public string SceneId; }
  public struct SceneModifiedEvent { }
  public struct SceneClosedEvent  { }
  public struct AssetImportedEvent { public string AssetId; }
  public struct SelectionChangedEvent { public string SelectedNodeId; } // null = deselected
  public struct ModeChangedEvent  { public AppMode PreviousMode; public AppMode CurrentMode; }
  public struct FrameChangedEvent { public int Frame; }
  public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
  public struct ErrorOccurredEvent { public ErrorLevel Level; public string Message; }
  ```

- [ ] Create `Assets/_Shared/Models/AppMode.cs`:
  ```csharp
  public enum AppMode { MainMenu, VrEditing, ArMapping, ArPreview, Debug }
  ```

- [ ] Create `Assets/_Shared/Models/ErrorLevel.cs`:
  ```csharp
  public enum ErrorLevel { Warning, Error, Critical }
  ```

- [ ] Switch to Unity → verify `_Shared` assembly compiles without errors

- [ ] Commit:
  ```bash
  git add Assets/_Shared/
  git commit -m "feat: add shared EventBus, event structs, and enums"
  ```

---

## Task 5: VContainer LifetimeScopes

**Files:** `RootLifetimeScope.cs`, `MainMenuSceneScope.cs`, `VrEditingSceneScope.cs`

> VContainer LifetimeScopes are MonoBehaviours attached to GameObjects in Unity scenes. The Root scope lives in Bootstrap.unity. Scene scopes declare their parent type so VContainer chains them automatically.

- [ ] Create `Assets/_App/Bootstrap/RootLifetimeScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;

  public class RootLifetimeScope : LifetimeScope
  {
      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Singleton);
          // PathProvider, AppStorage, AssetImporter, AnimationClock — registered in later phases
      }
  }
  ```

- [ ] Create `Assets/_App/Bootstrap/MainMenuSceneScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;

  public class MainMenuSceneScope : LifetimeScope
  {
      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Scoped);
          // ModeOrchestrator, CommandStack — Phase 2
      }
  }
  ```

- [ ] Create `Assets/_App/Bootstrap/VrEditingSceneScope.cs`:
  ```csharp
  using VContainer;
  using VContainer.Unity;

  public class VrEditingSceneScope : LifetimeScope
  {
      protected override void Configure(IContainerBuilder builder)
      {
          builder.Register<EventBus>(Lifetime.Scoped);
          // SceneGraph, SelectionManager, UiPanelManager, CommandStack — later phases
      }
  }
  ```

- [ ] Create `Assets/_App/Bootstrap/AppBootstrap.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public class AppBootstrap : MonoBehaviour
  {
      private void Start()
      {
          SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
      }
  }
  ```

- [ ] Verify no compile errors in Unity

- [ ] Commit:
  ```bash
  git add Assets/_App/Bootstrap/
  git commit -m "feat: add VContainer LifetimeScope shells and AppBootstrap"
  ```

---

## Task 6: Create Scenes (Unity Editor)

> All steps in Unity Editor.

- [ ] **Bootstrap.unity**
  1. File → New Scene → Basic → Save as `Assets/Scenes/Bootstrap.unity`
  2. Remove default camera and light
  3. Create empty GameObject `[RootScope]` → add component `RootLifetimeScope`
  4. Create empty GameObject `[Bootstrap]` → add component `AppBootstrap`
  5. Save (Ctrl+S)

- [ ] **MainMenu.unity**
  1. File → New Scene → Basic → Save as `Assets/Scenes/MainMenu.unity`
  2. Remove default objects
  3. Create `[SceneScope]` → add `MainMenuSceneScope`
  4. In inspector: set **Parent** to `RootLifetimeScope` (dropdown on the component)
  5. Save

- [ ] **VrEditing.unity**
  1. File → New Scene → Basic → Save as `Assets/Scenes/VrEditing.unity`
  2. Remove default objects
  3. Create `[SceneScope]` → add `VrEditingSceneScope`, set **Parent** to `RootLifetimeScope`
  4. Save (XR Origin added next task)

- [ ] File → Build Settings → drag all 3 scenes in order: Bootstrap (0), MainMenu (1), VrEditing (2)

- [ ] Commit:
  ```bash
  git add Assets/Scenes/ ProjectSettings/
  git commit -m "feat: create Bootstrap, MainMenu, VrEditing scenes"
  ```

---

## Task 7: XR Origin + XR Simulator (Unity Editor)

> All steps in VrEditing.unity

- [ ] Open `Assets/Scenes/VrEditing.unity`

- [ ] Add XR Interaction Manager: GameObject → XR → Interaction Manager → rename `[XR Interaction Manager]`

- [ ] Add XR Rig: GameObject → XR → XR Origin (XR Rig)
  - Verify hierarchy: `XR Origin` → `Camera Offset` → `Main Camera`
  - Left and Right Hand controllers should appear as children

- [ ] On **Main Camera**: verify `Tracked Pose Driver (Input System)` component exists; set Tag = `MainCamera`

- [ ] On **Left Hand Controller** and **Right Hand Controller**:
  1. Add `XR Ray Interactor` component
  2. Add `XR Interactor Line Visual` component
  3. Add `Line Renderer` component (if not added automatically)

- [ ] Add XR Device Simulator: drag `Assets/Samples/XR Interaction Toolkit/.../XR Device Simulator/XR Device Simulator.prefab` into the scene

- [ ] Add Directional Light: GameObject → Light → Directional Light (Rotation: 50, -30, 0)

- [ ] **Press Play** → move mouse in Game view → verify head rotation, no console errors

- [ ] Save + commit:
  ```bash
  git add Assets/Scenes/VrEditing.unity
  git commit -m "feat: configure XR Origin, Ray Interactors, XR Device Simulator"
  ```

---

---

## Task 9: Create XRI Interaction Layers (Unity Editor)

> The project uses 4 custom physics layers for XRI raycasting. Must be set up before Phase 5.

- [ ] Edit → Project Settings → Tags and Layers → add to Layers:
  - Layer 8: `SceneObjects`
  - Layer 9: `UiPanels`
  - Layer 10: `GizmoHandles`
  - Layer 11: `BoneProxies`

- [ ] On each `XR Ray Interactor` (L and R) in VrEditing.unity:
  - Set **Interaction Layer Mask** to include: SceneObjects, UiPanels, GizmoHandles, BoneProxies

- [ ] Commit:
  ```bash
  git add ProjectSettings/TagManager.asset Assets/Scenes/VrEditing.unity
  git commit -m "feat: add XRI interaction layers (SceneObjects, UiPanels, GizmoHandles, BoneProxies)"
  ```

---

## Phase 1 Verification

- [ ] `_Shared` assembly and all 13 subsystem asmdefs compile (zero errors in Console)
- [ ] Playing Bootstrap.unity loads MainMenu additively (no exceptions)
- [ ] Playing VrEditing.unity: XR Simulator responds to mouse, ray interactor line renders, no errors
- [ ] VContainer scopes initialize without "no registration" exceptions
