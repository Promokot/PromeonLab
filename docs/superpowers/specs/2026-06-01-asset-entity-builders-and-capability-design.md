# Asset Entity Builders & Interaction Capability — Design Spec

**Date:** 2026-06-01
**Status:** Approved (design); implementation sliced (see §10)
**Related:** [[asset-import-pipeline-design 2026-05-31]] (Source→Asset→Node model, glTFast import), [[project_outline_system]], [[project_interaction_layer_priority]]

---

## 1. Problem

Runtime-imported assets (glTF objects, reference images) spawn into the scene but are **not selectable and show no outline**. Built-in library assets work fine.

**Root cause:** selectability/outline is provided by components — `SceneNode` + `Selectable` + `XRPromeonInteractable` + a collider on interaction layer `SceneObjects` — that are **authored into the built-in prefabs ahead of time** ("baked"). `SceneGraph.AddNodeInternal` (`SceneGraph.cs:96-102`) only *re-uses* a pre-attached `SceneNode` and stamps its `NodeId`; it does not assign interaction capability. Runtime imports are materialized by glTFast / `CreatePrimitive` and never pass through any bake step, so they arrive without those components:

- `SelectionVisualSync.OnSelectionChanged` (`SelectionVisualSync.cs:18-28`) looks for a `Selectable` on each node → none → no outline.
- No `XRPromeonInteractable` + no collider on the right layer → no in-scene tap-select.
- Gizmo still works because `GizmoActivator` targets the node transform by `NodeId` (selected via the outliner) and adds its own outline.

The fix is not a one-off patch but a small architecture: define *where and when* an asset acquires its scene-interaction capability, and make that path identical for built-in and imported assets.

## 2. Guiding principle — build once, restore many

A builder must **never run its decision logic inside the live scene**, so an entity's representation can never drift between spawns. Builders run **once** and record their decisions; the scene only **restores** from that record.

Two layers, with a serializable contract (`AssetEntityRecipe`) between them:

| Layer | Runs | Output |
|---|---|---|
| **Build** | once — at import (runtime) OR via an editor button | the *processed form* |
| **Restore** | every spawn / scene-load | a materialized `GameObject` (no decisions) |

The *processed form* differs only by **persistence medium**, never by logic:

- **Built-in (editor-time Build):** a saved **prefab** — Unity serializes geometry + components. Restore = `Instantiate(prefab)`.
- **Imported (runtime Build):** a **JSON `AssetEntityRecipe`** stored beside the raw source (a runtime GameObject cannot be serialized to a reusable asset on-device — no `AssetDatabase`/`PrefabUtility` on Quest). Restore = reload geometry from source + **apply the recipe** (pure application, no measurement/decisions).

This is why Restore *adds components* for imports yet still honours the principle: every decision (collider size, layer, component set, child offsets, bone descriptor) was fixed at Build time and merely replayed.

## 3. Components & contracts

### `IAssetEntityBuilder` (replaces `IAssetSpawner`)
One per `AssetType`. Dispatched by type.

```csharp
public interface IAssetEntityBuilder
{
    AssetType HandledType { get; }

    // Once. Inspects the raw source, makes all decisions, returns a serializable recipe.
    // Throws/falls back gracefully on invalid input (see §7).
    Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct);

    // Many. Deterministically materializes the entity. No decisions.
    //   Builtin source  → Instantiate the pre-baked prefab (recipe ignored).
    //   Imported source → load geometry from source + apply recipe via InteractionCapability.
    Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct);
}
```

Implementations: `ObjectEntityBuilder`, `ReferenceEntityBuilder` (plain DI services), and `RigEntityBuilder` (the evolved `PromeonProxyRigBuilder`, a `MonoBehaviour`). Because the rig builder is a `MonoBehaviour` and the others are plain services, **they share an interface, not a base class** (C# single inheritance — `MonoBehaviour` already occupies the base).

### `AssetEntityRecipe` (serializable, versioned)
The Build→Restore contract. JsonUtility-friendly (flat, no polymorphism), `schemaVersion` per project convention. Indicative shape (extensible per type):

```csharp
[Serializable]
public class AssetEntityRecipe
{
    public int              schemaVersion;     // migrations only in StorageMigrator
    public AssetType        type;

    // Generic interaction capability
    public bool             selectable;        // true → add Selectable + XRPromeonInteractable
    public InteractionLayer interactionLayer;  // SceneObjects for object/reference

    // Collider (single Box for now; see IColliderStrategy)
    public ColliderKind     colliderKind;      // Box
    public Vector3          colliderCenter;    // local space
    public Vector3          colliderSize;      // local space

    // Reference-specific
    public float            referenceAspect;
    public float            referenceBottomGap;   // 0.5 m (bottom edge above floor)
    public bool             referenceTwoSided;

    // Rig-specific (Slice 2) — bone descriptor; omitted/empty otherwise
}
```

### `InteractionCapability` (static helper)
The single definition of "make this GameObject a selectable scene entity." Used by **both** layers — by editor Build (bakes into the prefab) and by runtime Restore (applies the recipe). Static + parameterized so it works with no DI container (runtime Restore passes the DI-resolved strategy; editor Build passes a default; tests pass a stub):

```csharp
public static class InteractionCapability
{
    // Idempotent: if root already has XRPromeonInteractable (a baked builtin prefab), no-op.
    public static void Attach(GameObject root, InteractionLayer layer, IColliderStrategy collider);
}
```

Concretely attaches to `root` (in this order so Awake-time lookups resolve — see §5):
1. `SceneNode` (re-used if present; `NodeId` stamped later by `SceneGraph`).
2. A collider via the strategy (default: one `BoxCollider` sized to combined child-renderer bounds), placed on interaction layer `layer`.
3. `Selectable` — drives the outline through `SelectionVisualSync`; `Construct(OutlineConfig)` injected later by `InjectGameObject`.
4. `XRPromeonInteractable` + `SetInteractionLayer(layer)` — tap-trigger select, hold move/rotate; `Construct(ISelectionManager, GizmoController)` injected later.

Bone proxies call the same helper with `InteractionLayer.BoneProxies`.

### `IColliderStrategy` (swappable seam)
```csharp
public interface IColliderStrategy { void Apply(GameObject root, out Vector3 center, out Vector3 size); }
```
Default `BoundsBoxColliderStrategy`: one root `BoxCollider` from combined child-renderer bounds in root-local space. DI-registered for runtime; default-constructed in editor. Replaceable later with precise mesh colliders (esp. rigs: skeletal mesh + many bones) by swapping the single DI registration. Because the box sits on `root` (same GameObject as the interactable), `XRPromeonInteractable._includeChildColliders` stays `false`.

## 4. Data flow

### Import (runtime, Build once)
`ImportPipeline.RunImportAsync` orchestrates:
1. `IAssetImportHandler` (keyed by **extension**) copies the raw source into `asset-library/sources/{assetId}.{ext}` and creates the `ImportedLabAsset` record (unchanged from prior slice).
2. `IAssetEntityBuilder` (keyed by the **wizard-chosen type**) `BuildAsync(sourcePath, type)` → `AssetEntityRecipe`. (Object: instantiate glTF into a temp hidden GO, measure bounds, destroy temp. Reference: read image dimensions → aspect.)
3. Recipe persisted with the record (inline on `ImportedLabAsset`, library file `schemaVersion` bumped; migration in `StorageMigrator`).
4. `AssetImportedEvent`.

Import handlers (file intake) and entity builders (type processing) stay **separate units** — different keys, different jobs.

### Spawn & scene-load (Restore many)
Both the fresh-spawn path (`AssetSpawner` → `AssetSpawnerRegistry`) and the scene-reload path (`SceneGraph.OnSceneOpenedAsync:148`) call the **same type-keyed registry**, so both get capability with no duplication:

```
registry.RestoreAsync(asset, recipe, pos, rot)
  → builder(asset.Type).RestoreAsync(...)
       Builtin  → Instantiate(asset.Prefab)            // prefab already complete
       Imported → load source + InteractionCapability.Attach(...) per recipe
  → SceneGraph.AddNode(go, ...)   // re-uses the SceneNode, stamps NodeId
  → resolver.InjectGameObject(go) // wires Selectable / XRPromeonInteractable Construct
```

### Built-in authoring (editor Build)
Each builder exposes an **editor button** ("Bake to Built-in Library"): run `BuildAsync` + attach capability via `InteractionCapability` into a GameObject, save as a prefab, register it in `BuiltinAssetLibrary`. Generalizes the existing `PromeonProxyRigBuilder` "Rebuild" button to all three. This is the editor counterpart of runtime Build.

## 5. Ordering & timing constraints

- `Selectable.Awake` caches `GetComponent<SceneNode>()`; `XRPromeonInteractable.Awake` caches `GetComponentInParent<SceneNode>()`. Therefore **`SceneNode` must exist before those components are added.** `InteractionCapability.Attach` adds `SceneNode` first; `SceneGraph.AddNode` then re-uses it and stamps the runtime `NodeId` (matching the existing "pre-attached SceneNode" contract, `SceneGraph.cs:97-99`).
- DI (`Construct`) is wired by the existing `InjectGameObject` call **after** `AddNode`, so capability components must be attached before that call. Restore attaches them; the existing pipeline order already satisfies this.

## 6. Idempotency / built-in untouched
`InteractionCapability.Attach` no-ops when `root` already carries `XRPromeonInteractable`. Built-in prefabs (complete) are never mutated; their Restore is a plain `Instantiate`. This guarantees the change cannot regress working built-in assets (confirmed scope: only imports are currently broken).

## 7. Error handling
- **Rig type, but source has no skeleton/bones:** `RigEntityBuilder.BuildAsync` detects the absence and **gracefully falls back to an Object recipe** (warning logged), so the asset is still usable as static decoration. A skinned mesh imported as **Object** is valid (decoration), never an error.
- **Unreadable source** (bad glTF / non-image): Build logs an error and the import fails cleanly (no record written) — consistent with current `ImportPipeline` behavior.
- Restore failures for a single node are caught and skipped on scene-load (existing `SceneGraph.OnSceneOpenedAsync:150-154`).

## 8. Persistence layout
```
asset-library/
├── imported.json              (records; now each carries its AssetEntityRecipe; schemaVersion bumped)
└── sources/{assetId}.{ext}    (raw .glb/.gltf/.png/.jpg — unchanged)
```
Built-in recipes do not exist as JSON — the prefab *is* the recipe. (`Saved` library remains out of scope.)

## 9. Testing
- `AssetEntityRecipe` JSON round-trip incl. `schemaVersion`; migration path.
- `InteractionCapability.Attach`: idempotent (skip when `XRPromeonInteractable` present); attaches `SceneNode`+`Selectable`+`XRPromeonInteractable`+collider; sets interaction layer; correct add-order.
- `BoundsBoxColliderStrategy`: box center/size from renderer bounds in local space.
- `ObjectEntityBuilder` / `ReferenceEntityBuilder`: Build produces a recipe with collider/aspect; Restore applies it (pure-logic parts in EditMode; glTF/image load verified in PlayMode or behind a seam).
- `RigEntityBuilder`: no-bones source → Object-fallback recipe + warning.

## 10. Slicing
1. **Foundation + Object/Reference (fixes the reported bug):** `AssetEntityRecipe`, `IAssetEntityBuilder`, `InteractionCapability`, `IColliderStrategy`+`BoundsBoxColliderStrategy`; `ObjectEntityBuilder`+`ReferenceEntityBuilder`; ImportPipeline Build step + recipe persistence; registry Restore dispatch (replaces `IAssetSpawner`); both spawn + reload paths. → imported objects & images become selectable/outlined.
2. **Rig:** `RigEntityBuilder` from `PromeonProxyRigBuilder` (now unblocked) — runtime proxy-rig Build/Restore, bone descriptor in the recipe, graceful no-bones fallback (§7).
3. **Editor bake buttons:** "Bake to Built-in Library" on all three builders → prefab. Auto-registration into `BuiltinAssetLibrary` is *nice-to-have*: if it proves awkward (editor-only library mutation), shipping just the prefab + manual library registration is acceptable.

## 11. Out of scope
- `Saved` library (manual scene save-out) — separate flow.
- Precise/mesh colliders — future `IColliderStrategy` swap.
- NLA / animation, material override profiles beyond the existing `ImportRenderProfile`.
- Lazy "build on first restore" optimization (we Build eagerly at import).

## 12. Naming note
`AssetEntity*` deliberately avoids `Node*`: a "node" already denotes an outliner/scene node (`SceneNode`). The builders' subject is the *library asset's scene entity*, hence `AssetEntity`.
