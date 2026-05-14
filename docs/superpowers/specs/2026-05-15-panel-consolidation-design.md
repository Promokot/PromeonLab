# Panel Consolidation — Design Spec

**Date:** 2026-05-15  
**Scope:** Move all panel scripts into `SpatialUi`; introduce minimal interface contracts so `SpatialUi.asmdef` depends only on `_Shared`.

---

## Goal

All MonoBehaviour panel controllers live in `Assets/_App/Subsystems/SpatialUi/UI/`. No panel C# file exists outside this folder. `SpatialUi.asmdef` references only `_Shared` — no direct subsystem assembly references.

---

## What Changes

### New interfaces — `_Shared/Interfaces/`

| File | Interface | Methods/Properties |
|---|---|---|
| `ISelectionManager.cs` | `ISelectionManager` | `string SelectedNodeId { get; }` · `void Select(string nodeId)` · `event Action<string> SelectionChanged` |
| `ISceneGraph.cs` | `ISceneGraph` | `SceneNode GetNode(string nodeId)` · `void AddNode(GameObject go)` · `void RemoveNode(string nodeId)` |
| `IRigRuntime.cs` | `IRigRuntime` | `RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)` · `void ApplyDefinition(RigDefinition def, SkinnedMeshRenderer smr)` |
| `ICommandStack.cs` | `ICommandStack` | `void Push(ICommand cmd)` · `void Undo()` |

> `ICommand` already exists in `_Shared/Interfaces/ICommand.cs`.  
> `SceneNode` is a MonoBehaviour in `SceneComposition` — stays there; `ISceneGraph.GetNode` returns it directly since SceneNode is accessed via GameObject anyway. Actually, to keep SpatialUi free of SceneComposition types, ISceneGraph.GetNode returns `GameObject` instead of `SceneNode`.

### Revised `ISceneGraph`

```csharp
public interface ISceneGraph
{
    GameObject GetNode(string nodeId);
    void AddNode(GameObject go);
    void RemoveNode(string nodeId);
}
```

### Data models moved to `_Shared/Models/`

| From | To |
|---|---|
| `RigBuilder/Data/RigDefinition.cs` | `_Shared/Models/RigDefinition.cs` |
| `RigBuilder/Data/BoneRecord.cs` | `_Shared/Models/BoneRecord.cs` |
| `RigBuilder/Data/IkChainRecord.cs` | `_Shared/Models/IkChainRecord.cs` |

### Panel scripts moved to `SpatialUi/UI/`

| From | To |
|---|---|
| `RigBuilder/UI/BoneInspectorPanel.cs` | `SpatialUi/UI/BoneInspectorPanel.cs` |
| `RigBuilder/UI/IkSetupWizard.cs` | `SpatialUi/UI/IkSetupWizard.cs` |
| `SceneComposition/UI/PropertyPanel.cs` | `SpatialUi/UI/PropertyPanel.cs` |

### Concrete classes implement interfaces

| Class | Implements |
|---|---|
| `SelectionManager` | `ISelectionManager` |
| `SceneGraph` | `ISceneGraph` |
| `RigRuntime` | `IRigRuntime` |
| `CommandStack` | `ICommandStack` |

### Assembly definition changes

| Asmdef | Change |
|---|---|
| `SpatialUi.asmdef` | No new references — `_Shared` already there |
| `RigBuilder.asmdef` | Remove `Subsystems.SpatialUi` reference; remove `Subsystems.SceneComposition` if present |
| `SceneComposition.asmdef` | Remove `Subsystems.SpatialUi` reference if present |

### Injection signature updates

Panels currently inject concrete types. After move they inject interfaces:

```csharp
// Before (BoneInspectorPanel)
public void Construct(RigRuntime rigRuntime, SelectionManager selectionManager, SceneGraph sceneGraph, IkSetupWizard ikWizard)

// After
public void Construct(IRigRuntime rigRuntime, ISelectionManager selectionManager, ISceneGraph sceneGraph, IkSetupWizard ikWizard)
```

VContainer binds via `.AsImplementedInterfaces()` — no scope file changes needed if concrete types are already registered with that call. Where they aren't, add `.AsImplementedInterfaces()`.

---

## What Does NOT Change

- `MainMenuPanel`, `ScenePickerPanel`, `ToolbarPanel` — already in `SpatialUi/UI/`, no move needed
- `RigRuntime.cs`, `SceneGraph.cs`, `SelectionManager.cs`, `CommandStack.cs` — stay in their subsystems; only add `implements` clause
- `BoneProxy.cs` — stays in `RigBuilder/` (not a panel)
- `RigSerializer.cs` — stays in `RigBuilder/`
- No Unity Editor prefab changes — panel prefabs still reference the same MonoBehaviour type names

---

## Scope Boundary

This spec covers only the consolidation refactor. Asset Library and Animation Authoring are separate specs.
