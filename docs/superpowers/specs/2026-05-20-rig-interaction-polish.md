# Rig Interaction Polish Design

> **Status:** Approved — ready for implementation

**Goal:** Make rig proxy bones selectable in VR with their own inspector view, toggleable visibility/interactability from the rig's inspector, and a visual highlight when selected.

**Approach:** Each `proxy_X` GameObject built by `PromeonInteractableRigBuilder` gets a `SceneNode` component with a deterministic NodeId (`bone:{rigNodeId}:{boneName}`) so it plugs into the existing VR selection pipeline. Bone SceneNodes are **not** registered in `SceneGraph._nodes`, so the outliner stays clean — bone selection works only via direct VR interaction and inspector. A new `BoneInteractableFactory` wires `XRPromeonInteractable + Selectable + DI` onto the proxies after `Rebuild`. `SceneInspectorView` gains a 4th state `Bone` plus a `Show Bones` toggle that appears when a rig is selected.

**Tech Stack:** Unity 6, VContainer, MessagePipe-style EventBus, QuickOutline.

---

## Why

Currently the proxy skeleton hierarchy renders correctly and bones follow their proxies in VR, but the proxies have no `XRPromeonInteractable` and no `SceneNode`. There's no way to grab a single bone in VR or open a bone-specific inspector. This work closes that gap without polluting the outliner (which lists scene nodes by parent transform) or the `SceneGraph._nodes` dictionary (which serializes to disk).

Constraints from prior discussion:
- Bones must NOT appear in the outliner
- Selecting a bone opens a "bone version" of the inspector (a new state inside the existing `SceneInspectorView`)
- Show Bones is controlled from the rig's inspector
- Selected bone changes color (initial implementation: Outline color)

---

## Architecture

### NodeId Convention

Bone NodeId format: `bone:{rigNodeId}:{boneName}`. Example: `bone:a1b2c3d4:Spine_01`. Deterministic across Rebuilds for the same rig + bone — selection-state survives a Rebuild call.

Detection: `selection.ActiveId.StartsWith("bone:")` → bone selection. NodeId can be split on `:` into prefix, rigNodeId, boneName.

### Component Layout

A proxy bone GO after `Rebuild`:

```
proxy_Spine_01  (GameObject)
├── Transform                     (position/rotation drive bone via BoneFollower on the original bone)
├── MeshFilter                    (per-bone diamond mesh from BuildCombinedDiamondMesh)
├── MeshRenderer
├── MeshCollider                  (sharedMesh = the diamond mesh)
├── Outline                       (QuickOutline; OutlineColor set per selection state)
├── SceneNode                     (NEW — Init'd with NodeId="bone:...:Spine_01")
├── XRPromeonInteractable         (NEW — registered colliders + DI'd ISelectionManager/GizmoController)
└── Selectable                    (NEW — Init'd with the SceneNode reference)
```

`PromeonInteractableRigBuilder` adds `SceneNode` directly (it's a no-DI component). `XRPromeonInteractable` + `Selectable` are added by a new `BoneInteractableFactory` after the geometry is built (because they require DI).

### Build & Wire Order

`RigRuntime.ApplyDefinition` orchestrates:
1. Find or add `PromeonInteractableRigBuilder` to the rig SMR's parent
2. Resolve the rig's `SceneNode` (the user-selected rig node)
3. Call `boneRenderer.SetRigNodeId(rigNode.NodeId)` so the builder knows the namespace for bone IDs
4. Call `boneRenderer.SetEventBus(_bus)` so the builder can subscribe to `SelectionChangedEvent` for color highlight
5. Call `boneRenderer.SetTransforms(...)` and `boneRenderer.Rebuild()`
6. Walk `boneRenderer.ProxyGOs` and call `_boneInteractableFactory.MakeBoneInteractable(proxyGo)` for each

### Bone Selection Flow

```
User clicks bone proxy in VR
  → XRPromeonInteractable.ProcessInteractable → _selectionManager.Select(bone NodeId)
  → SelectionManager publishes SelectionChangedEvent
  → SceneInspectorView.OnSelectionChanged → Refresh() → state=Bone, shows bone content
  → PromeonInteractableRigBuilder.OnSelectionChanged → updates Outline.OutlineColor on each proxy
```

`XRPromeonInteractable.Awake` already does `GetComponentInParent<SceneNode>()`. Because the proxy GO has its own SceneNode, the call returns the bone's SceneNode (closest ancestor including self), not the rig's SceneNode further up. Selection targets the bone correctly.

### Show Bones Toggle

`PromeonInteractableRigBuilder` exposes:
```csharp
public void SetBonesInteractive(bool enabled)
```
Iterates `_proxyGOs`, toggles `MeshRenderer.enabled`, `Outline.enabled`, and the collider's `enabled` for each.

Default state after `Rebuild`: **bones disabled** (renderer/outline/collider off). User must explicitly turn the toggle ON.

The toggle lives in `SceneInspectorView` (Single state). Visible only when the selected scene node has a `PromeonInteractableRigBuilder` somewhere in its hierarchy (i.e., it's a rig). On change, calls `rigBuilder.SetBonesInteractive(value)`.

### Inspector States

`SceneInspectorView.Refresh()` state logic:

| Condition | State | UI |
|---|---|---|
| `count == 0` | Empty | `_emptyState` shown |
| `count > 1` | Multi | `_multiState` shown |
| `count == 1`, ActiveId starts with `"bone:"` | **Bone** (new) | `_boneState` shown |
| `count == 1`, rig (has `PromeonInteractableRigBuilder`) | Single (rig) | `_content` shown + `_showBonesToggle` visible |
| `count == 1`, other | Single (normal) | `_content` shown + `_showBonesToggle` hidden |

`_boneState` content (minimal for this phase):
- Bone name label (parsed from NodeId)
- Parent rig name label (looked up via rigNodeId)
- Placeholder text "Bone-specific tools coming soon" (keyframe tracks are future work)

### Color Highlight

`PromeonInteractableRigBuilder` adds:
```csharp
[SerializeField] private Color _boneOutlineColorDefault  = Color.white;
[SerializeField] private Color _boneOutlineColorSelected = new Color(1f, 0.5f, 0f); // orange
```

Subscribes to `SelectionChangedEvent` after `SetEventBus` is called (or in `OnEnable` if EventBus is already set). In the handler:
```csharp
var selected = new HashSet<string>(evt.SelectedNodeIds ?? Array.Empty<string>());
foreach (var go in _proxyGOs)
{
    if (go == null) continue;
    var sn = go.GetComponent<SceneNode>();
    var outline = go.GetComponent<Outline>();
    if (sn == null || outline == null) continue;
    outline.OutlineColor = selected.Contains(sn.NodeId)
        ? _boneOutlineColorSelected
        : _boneOutlineColorDefault;
}
```

Unsubscribes in `OnDestroy`.

### BoneInteractableFactory

New service mirroring `SelectionInteractorFactory` but specialized for proxies:

```csharp
public interface IBoneInteractableFactory
{
    void MakeBoneInteractable(GameObject proxyGo);
}

public class BoneInteractableFactory : IBoneInteractableFactory
{
    private readonly ISelectionManager _selectionManager;
    private readonly IObjectResolver   _resolver;
    private GizmoController            _gizmoCached;

    public BoneInteractableFactory(ISelectionManager selectionManager, IObjectResolver resolver) { ... }

    public void MakeBoneInteractable(GameObject proxyGo)
    {
        // Proxy already has its mesh collider and a SceneNode from the rig builder.
        var sn = proxyGo.GetComponent<SceneNode>();
        var existing = proxyGo.GetComponentsInChildren<Collider>(includeInactive: true);

        var sel = proxyGo.GetComponent<Selectable>() ?? proxyGo.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        _gizmoCached ??= _resolver.Resolve<GizmoController>();

        var xri = proxyGo.GetComponent<XRPromeonInteractable>() ?? proxyGo.AddComponent<XRPromeonInteractable>();
        xri.RegisterColliders(existing);
        xri.Construct(_selectionManager, _gizmoCached);
    }
}
```

Registered as scoped in `VrEditingSceneScope` and `SandboxSceneScope` (where the existing `SelectionInteractorFactory` is registered):

```csharp
builder.Register<BoneInteractableFactory>(Lifetime.Scoped).AsImplementedInterfaces();
```

### `PromeonInteractableRigBuilder` Public Additions

```csharp
public IReadOnlyList<GameObject> ProxyGOs => _proxyGOs;

public void SetRigNodeId(string rigNodeId);          // stored; used to build bone NodeIds
public void SetEventBus(EventBus bus);               // stored; subscribed for selection highlight
public void SetBonesInteractive(bool enabled);       // toggles renderer/outline/collider on all proxies
```

`Rebuild()` uses `_rigNodeId` to construct bone NodeIds when adding `SceneNode` to each proxy. If `_rigNodeId` is empty, falls back to `"rig"` for the namespace (so manual Inspector "Rebuild" still works during edit-time testing).

---

## Files

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Add `_rigNodeId`, `_eventBus`, color fields, `SetRigNodeId`, `SetEventBus`, `SetBonesInteractive`, SceneNode-add in `BuildProxyNode`, SelectionChangedEvent subscription, `ProxyGOs` getter |
| `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` | Inject `IBoneInteractableFactory` + `EventBus`; wire `SetRigNodeId`/`SetEventBus` + walk `ProxyGOs` calling `MakeBoneInteractable` after `Rebuild` |
| `Assets/_App/Subsystems/VrInteraction/IBoneInteractableFactory.cs` | NEW interface |
| `Assets/_App/Subsystems/VrInteraction/BoneInteractableFactory.cs` | NEW implementation (mirrors `SelectionInteractorFactory`) |
| `Assets/_App/Bootstrap/VrEditingSceneScope.cs` | Register `BoneInteractableFactory` |
| `Assets/_App/Bootstrap/SandboxSceneScope.cs` | Register `BoneInteractableFactory` |
| `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` | Add `_boneState`, `_showBonesToggle`, `_boneNameLabel`, `_boneParentRigLabel`; state-detection logic |
| `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/SceneInspectorModule.prefab` | MANUAL: add `BoneState` GameObject (name + rig labels), add `ShowBonesToggle` GameObject under `_content` |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | Tests for NodeId format, SceneNode presence, `SetBonesInteractive` behavior |

---

## Tests

### New EditMode tests in `PromeonInteractableRigBuilderTests.cs`

- `BuildProxyHierarchy_BoneNodeId_FollowsBoneFormat` — after Rebuild with `SetRigNodeId("rig1")`, `proxy_pelvis.GetComponent<SceneNode>().NodeId == "bone:rig1:pelvis"`
- `BuildProxyHierarchy_BoneNodeId_NoRigId_UsesDefaultNamespace` — Rebuild without `SetRigNodeId`, NodeId starts with `"bone:rig:"`
- `BuildProxyHierarchy_AddsSceneNodeToEachProxy` — each `proxy_X` has a `SceneNode` component
- `SetBonesInteractive_TogglesRendererOutlineCollider` — after Rebuild, `SetBonesInteractive(false)` → renderer.enabled, outline.enabled, collider.enabled all false; `SetBonesInteractive(true)` → all true

Manual VR testing covers:
- Grabbing a bone in VR fires `Select(boneNodeId)`
- Inspector switches to Bone state with bone name shown
- Selecting a rig shows the Show Bones toggle; toggling it makes proxies appear/disappear and become grab-able / non-grab-able
- Color highlight: selected bone shows orange Outline, others stay white

---

## Cleanup Contract

`PromeonInteractableRigBuilder.DestroyBoneGOs` already destroys `_proxyRoot.gameObject` (cascade destroys all proxies and their SceneNode + XRPromeonInteractable + Selectable components). `_proxyMeshes`, `_proxyGOs`, `_followers` clear as today.

`SelectionManager` does not know about bone NodeIds specifically — if a bone is selected and then destroyed (via Rebuild), the next `SelectionChangedEvent` will arrive with the stale ID still in `SelectedIds`. The inspector lookup of the bone will simply fail (no GO with that NodeId in scene), the inspector falls back to Empty state. Acceptable — no defensive cleanup needed.

---

## Known Limitations / Out of Scope

- **Keyframe tracks per bone:** explicitly future work. The `BoneState` shows a placeholder.
- **MaterialPropertyBlock-based bone color change:** chose Outline color for simplicity. If the user wants the diamond's material color to change too, that's a follow-up using `MaterialPropertyBlock` on the MeshRenderer.
- **Bone color when "show bones" is OFF:** since the renderer is disabled, color is invisible. No edge case to handle.
- **Multiple rigs in scene:** each `PromeonInteractableRigBuilder` instance subscribes to `SelectionChangedEvent` independently. Only one rig has bones interactive at a time in practice (the one with show-bones ON), but each rig will iterate its own proxies on each event. Negligible cost.
- **Asset import workflow (Sub-project B):** not in scope. Bone wiring happens at Rebuild time inside RigRuntime — this is OK because proxies are internal infrastructure, not user assets.
