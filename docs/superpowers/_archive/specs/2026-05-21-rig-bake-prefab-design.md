# Rig Bake Into Prefab — Design Spec

**Date:** 2026-05-21
**Status:** Design approved, ready for implementation plan
**Supersedes parts of:** `2026-05-20-rig-interaction-polish.md` (changes the spawn-time wiring to bake-time)

## Goal

Move all rig-interaction wiring (proxy bones, Selectable, XRPromeonInteractable, SceneNode) from **runtime spawn-time** into **edit-time prefab bake**. Library prefabs ship ready-to-go. `AssetSpawner` becomes a thin orchestrator: Instantiate → register in SceneGraph → rewrite bone NodeIds → inject DI. No factories.

## Why

Current runtime flow (`AssetSpawner` → `IInteractableFactory.MakeInteractable` → `IRigRuntime.ApplyDefinition` → `PromeonProxyRigBuilder.Rebuild` → walk `ProxyGOs` → `BoneInteractableFactory.MakeBoneInteractable`) is fragile:

- Hard to inspect/debug in editor — proxies exist only in play mode.
- Race conditions between DI registration order and rig wiring.
- Two separate factories (`BoneInteractableFactory`, `SelectionInteractorFactory`) that essentially do the same thing for different GO types.
- Empty `ProxyGOs` at the moment `SceneInspectorView` reads `AreBonesInteractive` → Show Bones toggle reads false → user clicks → still empty → "nothing happens".

With bake-time wiring, the prefab is a snapshot of a fully-functional interactable rig. Runtime just hands it to the DI container.

## Architecture

### Bake Flow (edit time)

**Trigger:** "Rebuild" button on `PromeonProxyRigBuilder` inspector, with the prefab open in Prefab Mode. Followed by `Ctrl+S` to persist.

**What `Rebuild()` does:**

1. `DestroyImmediate` of previous `ProxyRig` GO (existing behavior).
2. Build new `ProxyRig` hierarchy with one `proxy_{boneName}` GO per bone (existing behavior — `BuildProxyHierarchy`).
3. On each proxy GO add:
   - `MeshFilter` + procedurally generated diamond `Mesh`
   - `MeshRenderer` + `_boneMaterial` reference (Outline disabled)
   - `Outline` (disabled, color = `_boneOutlineColorDefault`)
   - `MeshCollider` (convex) or `CapsuleCollider` (disabled)
   - `SceneNode` (NodeId initially set to **just the bone name**, e.g. `"pelvis"`)
   - `BoneSceneNodeMarker` (new empty MonoBehaviour — marks this SceneNode as a bone for spawn-time rewrite)
   - `Selectable`
   - `XRPromeonInteractable`
4. `BoneFollower` on the original bone Transform → references proxy Transform (existing behavior).
5. Final `SetBonesInteractive(false)` — bones hidden by default, user opts in via Show Bones toggle.

**Persisted in prefab:** `ProxyRig` GO + all proxy GOs + all components above + sub-asset `Mesh` instances (Unity auto-stores procedural meshes as prefab sub-assets).

**Not persisted:** DI service references (`ISelectionManager`, `GizmoController`) — resolved at spawn. Final bone NodeIds in `bone:{rigId}:{boneName}` format — rewritten at spawn.

**Builder runtime behavior change:**
- `Awake()` no longer calls `Rebuild()`. The proxies already exist in the prefab.
- New `OnEnable()`: if `_proxyRoot == null`, find `transform.Find("ProxyRig")` and populate `_proxyGOs` from its descendants. Idempotent.
- `Rebuild()` is editor-only (called from `PromeonProxyRigBuilderEditor`).
- `Construct(EventBus)` `[Inject]` method replaces the explicit `SetEventBus(bus)` — VContainer fills it via `InjectGameObject`.

### Spawn Flow (runtime)

`AssetSpawner.SpawnCoreAsync` collapses to:

```csharp
var go = await asset.SpawnAsync(position, rotation, CancellationToken.None);
var rigNode = _graph.AddNode(go, assetRef, asset.DisplayName);
RewriteBoneNodeIds(go, rigNode.NodeId);
_resolver.InjectGameObject(go);
```

`RewriteBoneNodeIds(GameObject root, string rigNodeId)`:
```csharp
foreach (var marker in root.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true))
{
    var sn = marker.GetComponent<SceneNode>();
    if (sn == null) continue;
    var boneName = sn.NodeId;  // baked as just the bone name
    sn.SetNodeId($"bone:{rigNodeId}:{boneName}");
    _graph.AddTransientNode(sn);
}
```

Order matters: rewrite **before** `InjectGameObject` so any `[Inject]`-driven init code that reads `SceneNode.NodeId` sees the final value.

`IObjectResolver.InjectGameObject(go)` walks every `MonoBehaviour` in the hierarchy and invokes `[Inject]` methods. `XRPromeonInteractable.Construct(ISelectionManager, GizmoController)` and `PromeonProxyRigBuilder.Construct(EventBus)` fire automatically.

### Component Self-Init

**`XRPromeonInteractable`:**
- `[Inject] public void Construct(ISelectionManager, GizmoController)` — DI.
- `Awake()`: collider discovery.
  - Default: `_colliders = GetComponents<Collider>()` (own GO only).
  - If `[SerializeField] private bool _includeChildColliders` is `true`: `GetComponentsInChildren<Collider>(true)` instead.
- `RegisterColliders(Collider[])` remains as a public override path (used by editor-baking utilities; not needed at runtime spawn).

**`Selectable`:**
- `Awake()`: `_sceneNode = GetComponent<SceneNode>()`.
- Public `Init(SceneNode)` removed — there is one path (auto-discovery on Awake).
- Optional `[SerializeField] private SceneNode _sceneNode` for visibility in inspector (auto-filled by Awake if null).

**`SceneNode`:**
- Existing API preserved (`NodeId`, `DisplayName`, `AssetRef`, `Init(...)`).
- New: `public void SetNodeId(string newId)` — used by `AssetSpawner.RewriteBoneNodeIds`.
- `NodeId` field remains `[SerializeField]` for prefab persistence.

**`BoneSceneNodeMarker` (new):**
- Empty `MonoBehaviour`. Marker only.
- Added by builder during `BuildProxyNode`.
- Consumed by `AssetSpawner.RewriteBoneNodeIds`.

**`PromeonProxyRigBuilder`:**
- `[Inject] public void Construct(EventBus bus)` — replaces `SetEventBus(bus)`.
- `OnEnable()` repopulates `_proxyGOs` from baked `ProxyRig` if empty.
- `Rebuild()` editor-only entry point.

### SceneGraph: Transient Nodes

`SceneGraph` gains a parallel dictionary for "invisible-to-outliner" nodes (bones):

```csharp
private readonly Dictionary<string, SceneNode> _nodes          = new();
private readonly Dictionary<string, SceneNode> _transientNodes = new();

public SceneNode AddNode(GameObject go, AssetRef assetRef, string displayName) { /* unchanged: _nodes, publishes SceneModifiedEvent */ }

public void AddTransientNode(SceneNode sn)
{
    if (sn == null || string.IsNullOrEmpty(sn.NodeId)) return;
    _transientNodes[sn.NodeId] = sn;
    // No SceneModifiedEvent — outliner does not need to rebuild for bones.
}

public SceneNode GetNode(string nodeId)
{
    if (_nodes.TryGetValue(nodeId, out var n)) return n;
    if (_transientNodes.TryGetValue(nodeId, out var t))
    {
        if (t == null) { _transientNodes.Remove(nodeId); return null; }
        return t;
    }
    return null;
}

public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes; // outliner enumerates only visible nodes
```

Cleanup is **lazy**: `GetNode` removes stale entries when it encounters a destroyed `SceneNode`. When `RemoveNode(rigNodeId)` destroys the rig GO, child proxies die with it; their `_transientNodes` entries become "fake-null" and get pruned on next `GetNode` access.

### Inspector & Selection

**`SceneInspectorView.Construct(EventBus, SceneGraph, ISelectionManager)`** — `IBoneInteractableFactory` parameter removed.

`BindBone` resolves bone Transform via `_graph.GetNode(boneNodeId)?.transform` (replaces previous `_boneFactory.GetBoneTransform`).

**`SelectionInteractorFactory` deleted.** It is no longer registered in `SandboxSceneScope` / `VrEditingSceneScope`.

**`BoneInteractableFactory` deleted.** Same — DI registration removed.

**`IInteractableFactory` and `IBoneInteractableFactory` deleted** from `_Shared/Interfaces/`.

**`AssetSpawner`** constructor becomes `(EventBus bus, SceneGraph graph, IObjectResolver resolver)` — no `IInteractableFactory`, no `IRigRuntime`. Capability `Rig` flag remains in the enum (kept for future import-wizard logic) but is not read by spawn flow.

### Collider Strategy

**Per-XRPromeonInteractable invariant:**
> An `XRPromeonInteractable` owns only the collider(s) sitting on its own GameObject by default.

| Prefab type | Root | Each proxy | Owner |
|---|---|---|---|
| Non-rig (bush, prop) | `BoxCollider` (assigned manually in builtin; auto-added by import wizard) + `Selectable` + `XRPromeonInteractable` | — | Root owns its `BoxCollider`. |
| Rig (Crush Dummy) | `BoxCollider` covering the body + `Selectable` + `XRPromeonInteractable` | `MeshCollider` (convex) / `CapsuleCollider` + `Selectable` + `XRPromeonInteractable` | Root owns its own; each proxy owns its own. |

**`_includeChildColliders` flag:** kept as an escape hatch (`[SerializeField] private bool _includeChildColliders = false`) — for legacy or unusual prefabs where collider sits on a child GO. Default `false`. Builtin prefabs are structured so this stays off.

**Show Bones interaction layering:**

When `PromeonProxyRigBuilder.SetBonesInteractive(true)`:
1. Each proxy enables its own `MeshRenderer`, `Outline`, `Collider`.
2. The **root's** collider gets disabled. This prevents the body-spanning `BoxCollider` from intercepting raycasts intended for the bones inside it.

To do this, the builder needs a reference to the root's collider. Add `[SerializeField] private Collider _rootCollider` on `PromeonProxyRigBuilder`. User assigns it in the prefab inspector (it's the BoxCollider on the root rig GO). `SetBonesInteractive(true)` → `_rootCollider.enabled = false`; `SetBonesInteractive(false)` → `_rootCollider.enabled = true`.

Note: `_rootCollider` is optional. If null (e.g. non-rig prefab using builder for preview, or builder used in a test), the bones-interactive call simply skips that step.

## Migration Touchpoints

### Files to modify

- `PromeonProxyRigBuilder.cs` — add `_rootCollider` field, `[Inject] Construct(EventBus)`, `OnEnable` repopulation, builder bake updates (add Selectable + XRPromeonInteractable + BoneSceneNodeMarker per proxy), `SetBonesInteractive` toggles `_rootCollider.enabled`.
- `Selectable.cs` — auto-discover SceneNode in Awake; drop `Init(SceneNode)`.
- `XRPromeonInteractable.cs` — `Awake` self-discovers colliders; `_includeChildColliders` field; `Construct` already exists, mark with `[Inject]`.
- `SceneNode.cs` — add `SetNodeId(string)`.
- `SceneGraph.cs` — add `_transientNodes`, `AddTransientNode`, update `GetNode`, keep `Nodes` returning only `_nodes`.
- `AssetSpawner.cs` — simplified flow + `RewriteBoneNodeIds`. Drop `IInteractableFactory` and rig-related dependencies. Inject `IObjectResolver`.
- `SceneInspectorView.cs` — drop `IBoneInteractableFactory` dep; use `_graph.GetNode` for bone transform.
- `SandboxSceneScope.cs` / `VrEditingSceneScope.cs` — drop registrations of `SelectionInteractorFactory`, `BoneInteractableFactory`, `IInteractableFactory`, `IBoneInteractableFactory`.

### Files to add

- `BoneSceneNodeMarker.cs` — empty MonoBehaviour, lives in `Subsystems/RigBuilder/`.

### Files to delete

- `Subsystems/VrInteraction/BoneInteractableFactory.cs`
- `Subsystems/VrInteraction/SelectionInteractorFactory.cs`
- `_Shared/Interfaces/IInteractableFactory.cs`
- `_Shared/Interfaces/IBoneInteractableFactory.cs`

### Prefab edits (manual, in Unity Editor)

User performs these once per rig prefab in Prefab Mode:

1. **Rig prefabs (Crush Dummy + future ригi):**
   - Open prefab.
   - On the root GO, add `Selectable`, `XRPromeonInteractable`, `BoxCollider` (sized to the body bounds), `SceneNode` (NodeId stays empty — assigned at spawn by `SceneGraph.AddNode`).
   - On the `PromeonProxyRigBuilder` inspector, assign `_rootCollider` = the BoxCollider above.
   - Click **Rebuild**. Save (`Ctrl+S`).
   - Result: `ProxyRig` hierarchy with all components baked in.

2. **Non-rig prefabs (bush, props):**
   - Add `Selectable`, `XRPromeonInteractable`, a `BoxCollider` (on root), `SceneNode` to the prefab.
   - Save.

3. **DemoAssetCatalog entries:** the `Rig` capability flag is no longer required for spawn (kept in enum for future import-wizard logic). Existing settings can stay as-is or be cleaned up.

### Import wizard (future, out of scope for this spec)

When user imports an external model:
1. Add `BoxCollider` to root automatically.
2. Add `Selectable`, `XRPromeonInteractable`, `SceneNode` to root.
3. If model has SkinnedMeshRenderer (= rig): add `PromeonProxyRigBuilder`, run `Rebuild()`, save as prefab.
4. Otherwise: save as non-rig prefab.

This will live in a separate spec — see `Sub-project B` placeholder.

## Testing

Existing `PromeonProxyRigBuilderTests` continue to validate proxy geometry/hierarchy in edit-mode (test creates rig + GO ad-hoc, calls Rebuild, asserts).

New test cases:
- `BuildProxyHierarchy_AddsBoneSceneNodeMarkerToEachProxy` — every proxy has `BoneSceneNodeMarker`.
- `BuildProxyHierarchy_AddsSelectableAndXRInteractableToEachProxy` — verify components present.
- `BuildProxyHierarchy_SceneNodeId_IsBoneNameOnly_AtBakeTime` — SceneNode.NodeId == `"pelvis"`, not `"bone:rig:pelvis"`.

`SceneGraphTests`:
- `AddTransientNode_DoesNotPublishSceneModified` — verify no event.
- `GetNode_FindsTransientNode` — round-trip lookup.
- `GetNode_CleansStaleTransientEntry_WhenDestroyed` — destroy bone GO, GetNode returns null and removes entry.

`AssetSpawner` integration test (play mode):
- Spawn a baked rig prefab → assert SceneNode on root has runtime NodeId → all bone proxy SceneNodes have rewritten NodeId in `bone:{rigNodeId}:{boneName}` format → SceneGraph.GetNode returns each bone.

## Open Questions — resolved

| Question | Decision |
|---|---|
| What gets baked? | All visual + interaction components, plus SceneNode + BoneSceneNodeMarker. |
| How are DI deps wired post-spawn? | `IObjectResolver.InjectGameObject(spawned)`. |
| How are bone NodeIds made unique per instance? | Rewritten by `AssetSpawner` post-spawn using `BoneSceneNodeMarker`. |
| Bone visibility default? | Off — user opts in via Show Bones toggle. |
| Root rig collider strategy? | Manual `BoxCollider` on root, disabled by builder when Show Bones ON. |
| Non-rig collider sourcing? | Default own-GO only. `_includeChildColliders` opt-in for unusual cases. |
| Fate of factories? | Deleted (`BoneInteractableFactory`, `SelectionInteractorFactory`, `IInteractableFactory`, `IBoneInteractableFactory`). |
| Fate of `IRigRuntime` / `RigRuntime`? | Kept — needed by `IkSetupWizard`, `BoneInspectorPanel`, and future import wizard. No longer called from spawn flow. |
| Outliner sees bones? | No — bones live in `SceneGraph._transientNodes`, outliner enumerates only `_nodes`. |

## Out of Scope

- Import wizard (Sub-project B).
- Animation track UI on bones (mentioned earlier as a future need).
- Bone hierarchy mutations at runtime (add/remove bones from a spawned rig).
- Non-rig prefab batch editing (each is touched once, manually).
