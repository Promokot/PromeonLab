# Asset Browser — Design Spec
**Date:** 2026-05-16

## Overview

Redesign of the asset browser: from a scene-node list to a full asset catalog with three libraries, grid UI as a UserPanel module, and VR drag-and-drop spawning.

---

## 1. Data Model

### `ILabAsset` — `_Shared/Interfaces/ILabAsset.cs`
```csharp
public interface ILabAsset
{
    string           Id          { get; }
    string           DisplayName { get; }
    AssetType        Type        { get; }
    Sprite           Icon        { get; }
    Task<GameObject> SpawnAsync(Vector3 position, Quaternion rotation, CancellationToken ct);
}
```

### Concrete implementations — `AssetBrowser/Data/`

| Class | Unique fields | SpawnAsync |
|---|---|---|
| `BuiltinLabAsset` | `GameObject Prefab` | `Object.Instantiate(Prefab, pos, rot)` |
| `ImportedLabAsset` | `string FilePath` | via `AssetImporter` |
| `SavedLabAsset` | `string AssetId` | look-up in StorageCore; exact mechanism depends on saved asset type (rig, pose, etc.) — implemented per subtype |

`BuiltinLabAsset` — `[Serializable]` struct inside `BuiltinAssetLibrary` SO, assigned manually in the editor. Replaces / wraps `DemoAssetCatalog`.

`ImportedLabAsset` and `SavedLabAsset` — plain C# classes, serialized to JSON.

### `IAssetLibrary` — `_Shared/Interfaces/IAssetLibrary.cs`
```csharp
public interface IAssetLibrary
{
    IReadOnlyList<ILabAsset> Assets  { get; }
    Task LoadAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void Add(ILabAsset asset);
    void Remove(string id);
}
```

---

## 2. Library Storage

### `BuiltinAssetLibrary` (ScriptableObject)
- Assigned manually in the editor; wraps/replaces `DemoAssetCatalog`.
- `LoadAsync` / `SaveAsync` are no-ops — data is in the SO asset.
- Registered in DI via `builder.RegisterInstance(_builtinLibrary)`.

### `ImportedAssetLibrary` (C# class)
- Serialized via `AppStorage` to:
  `persistentDataPath/asset-library/imported.json`
- Stores: `Id`, `DisplayName`, `Type`, `FilePath`.
- Icon is not serialized — default icon by type at load time.

### `SavedAssetLibrary` (C# class)
- Serialized to:
  `persistentDataPath/asset-library/saved.json`
- Stores: `Id`, `DisplayName`, `Type`, `AssetId`.
- Populated when the user saves an in-app-created asset.

Both JSON libraries are **global** (not per-scene), alongside `scenes/` in the root of `persistentDataPath`. Paths are built via `PathProvider`:
```csharp
public string ImportedLibraryPath => Path.Combine(_root, "asset-library", "imported.json");
public string SavedLibraryPath    => Path.Combine(_root, "asset-library", "saved.json");
```

---

## 3. DI Registration (`RootLifetimeScope`)

Each library is registered under its concrete type so `AssetBrowserModule` can inject all three unambiguously:

```csharp
builder.RegisterInstance(_builtinLibrary);           // SO, set in inspector
builder.Register<ImportedAssetLibrary>(Lifetime.Singleton);
builder.Register<SavedAssetLibrary>(Lifetime.Singleton);
```

`ImportedAssetLibrary` and `SavedAssetLibrary` implement `IAsyncStartable` — `LoadAsync` is called automatically by VContainer on app start.

---

## 4. UI Structure

### UserPanel module system

`AssetBrowserModule` is a **MonoBehaviour child of the UserPanel prefab**, not a standalone `SpatialPanel`. Pattern mirrors `SettingsModule`: `Show()` / `Hide()` / `Toggle()` with `CanvasGroup` slide animation.

UserPanel gets an **"Assets" button** → calls `_assetBrowserModule.Toggle()`. Only one module open at a time — opening Assets closes Settings and vice versa.

**Context menus refactor (done alongside):** `UserPanel_ContextMenu_VrEditing`, `_ArMapping`, `_Sandbox` → renamed to `ContextModule_VrEditing`, `ContextModule_ArMapping`, `ContextModule_Sandbox`. Become permanent children of UserPanel prefab; `SwapContext` (Instantiate/Destroy loop) removed. UserPanel shows/hides the correct context module by `AppMode`.

**Global panels** (not tied to UserPanel) keep `SpatialPanel` + `PanelRegistry` registration: `ToolbarPanel`, `PropertyPanel`, future world-fixed panels.

`PanelId.AssetBrowser` removed from the enum.

### AssetBrowserModule layout

```
[ Library Tabs ]  [ Asset Grid             ] [ Properties     ]
  Встроенные  →   [card][card][card]     [+]   Name: ...
  Импортир.   →   [card][card][card]           Type: Model
  Сохранённые →   [card][card]                 Icon
                                               [override zone]
```

- **Left column** — tab buttons, one per library; switches the active `IAssetLibrary`
- **Grid** — `ScrollRect` with `GridLayoutGroup`, populated with `LabAssetCard` prefabs
- **[+] button** — top-right of grid; opens `SimpleFileBrowser` → creates `ImportedLabAsset`, adds to `ImportedAssetLibrary`, saves
- **Right column** — `AssetPropertiesView`; appears on tile select, hidden by default

### `LabAssetCard` — `SpatialUi/UI/LabAssetCard.cs`

```
┌──────────────┐
│              │
│     Icon     │
│   [type]     │  ← small type badge (optional)
├──────────────┤
│  DisplayName │
└──────────────┘
```

MonoBehaviour with `Bind(ILabAsset asset)`. Fires `Selected` event on click → `AssetBrowserModule` opens `AssetPropertiesView`. Also XRI Interactable for drag-and-drop (see Section 5).

Replaces the old `AssetBrowserItem` (deleted).

### `AssetPropertiesView` — `SpatialUi/UI/AssetPropertiesView.cs`

Base MonoBehaviour: shows Name + Type + Icon. `virtual void Bind(ILabAsset asset)` — overridden by type-specific subclasses (`ModelPropertiesView`, `RigPropertiesView`, etc.) when needed. `AssetBrowserModule` picks the right prefab from a `[SerializeField] PropertiesViewEntry[]` array (AssetType → prefab mapping), instantiates it in the properties slot, and calls `Bind`.

---

## 5. Drag-and-Drop

`LabAssetCard` carries a `LabAssetCardDragHandler` component that encapsulates all drag logic, keeping the card's visual MonoBehaviour clean.

**On grab start:** card shows visual feedback (slight scale-up).

**On release (`selectExited`):**
- If released **outside the panel bounds** → spawn:
  - *Ray grab:* raycast from controller at release moment; hit point used as spawn position; fallback to fixed distance in front of player.
  - *Near grab:* controller world position at release.
  - Calls `_asset.SpawnAsync(spawnPoint, rotation, ct)`.
- If released **inside the panel** → no spawn; card stays in place (it is a UI element, not moved).

`LabAssetCardDragHandler` has no DI injections; it receives the `ILabAsset` reference from `LabAssetCard.Bind()` at initialization.

---

## 6. Files Changed / Created

| Action | Path |
|---|---|
| Create | `_Shared/Interfaces/ILabAsset.cs` |
| Create | `_Shared/Interfaces/IAssetLibrary.cs` |
| Create | `AssetBrowser/Data/BuiltinLabAsset.cs` |
| Create | `AssetBrowser/Data/ImportedLabAsset.cs` |
| Create | `AssetBrowser/Data/SavedLabAsset.cs` |
| Create | `AssetBrowser/BuiltinAssetLibrary.cs` (SO) |
| Create | `AssetBrowser/ImportedAssetLibrary.cs` |
| Create | `AssetBrowser/SavedAssetLibrary.cs` |
| Modify | `StorageCore/PathProvider.cs` — add two library paths |
| Replace | `SpatialUi/UI/AssetBrowserPanel.cs` → `AssetBrowserModule.cs` |
| Replace | `SpatialUi/UI/AssetBrowserItem.cs` → `LabAssetCard.cs` |
| Create | `SpatialUi/UI/LabAssetCardDragHandler.cs` |
| Create | `SpatialUi/UI/AssetPropertiesView.cs` |
| Modify | `SpatialUi/UI/UserPanel.cs` — add module toggle logic |
| Rename | `UserPanel_ContextMenu_*.cs` → `ContextModule_*.cs` |
| Modify | `_Shared/Models/PanelId.cs` — remove AssetBrowser |
| Modify | `Bootstrap/RootLifetimeScope` — register libraries |
| Modify | `SpatialUi/UI/ToolbarPanel.cs` — remove or rewire `_openAssetBrowserButton` |
