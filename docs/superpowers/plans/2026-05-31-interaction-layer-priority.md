# Interaction Layer Priority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the XR ray select the highest-priority interaction layer it passes through (Gizmo > Bone > Selectable) with distance breaking ties only within the same layer, so a gizmo handle behind the floor or a bone behind the body mesh becomes reachable.

**Architecture:** A scene-scoped `RayInteractionResolver` raycasts against a mask of three interaction layers and returns the prioritized winning collider. The two ray-driven interactables (`XRPromeonInteractable`, `GizmoHandle`) replace their nearest-hit `IsPrimaryFor` check with a call into the resolver. Layer assignment is centralized behind one enum + one `GameObject` extension; runtime spawn sites call the extension, prefab-authored objects use an `InteractionLayerTag` component. The priority-pick logic is a pure static function (fully unit-tested); physics and layer wiring are thin glue verified in VR.

**Tech Stack:** Unity 6000.3.7f1, C#, OpenXR + XR Interaction Toolkit 3.x (`NearFarInteractor`/`XRRayInteractor`), VContainer DI, NUnit (Unity Test Runner). Spec: `docs/superpowers/specs/2026-05-31-interaction-layer-priority-design.md`.

---

## File Structure

**Create (all in `Assets/_App/Scripts/VrInteraction/`):**
- `Data/InteractionLayer.cs` — `enum InteractionLayer { GizmoHandles, BoneProxies, SceneObjects }` (names match existing Unity layers). Declaration order = priority (index 0 = highest). Single source of truth.
- `InteractionLayers.cs` — static class. Maps the enum to Unity layer indices (cached), builds the combined raycast `Mask`, exposes `Priority`, `TryGetPriority`, and the pure `PickWinnerIndex` selection function.
- `GameObjectInteractionLayerExtensions.cs` — `SetInteractionLayer(this GameObject, InteractionLayer)` extension; the one funnel every script site uses to assign a layer.
- `InteractionLayerTag.cs` — `MonoBehaviour` for prefab-authored objects; applies its serialized layer on `Awake` and `OnValidate`.
- `RayInteractionResolver.cs` — scene-scoped service; `ResolvePrimary(Ray, float) -> Collider`.

**Create (test):**
- `Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs` — unit tests for `PickWinnerIndex`, the layer mapping, and the extension.

**Modify:**
- `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs` — inject resolver, rewrite `IsPrimaryFor`.
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoHandle.cs` — receive resolver via `Bind`, rewrite `IsPrimaryFor`.
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` — inject resolver, pass to handles via `Bind`, assign `Gizmo` layer in `Spawn`, remove the target-collider disable.
- `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs` — assign `Bone` layer when a proxy is built.
- `Assets/_App/Scripts/SceneComposition/SceneGraph.cs` — assign `Selectable` layer on load-spawned objects.
- `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs` — assign `Selectable` layer on user-spawned objects.
- `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs` + `SandboxSceneScope.cs` — register `RayInteractionResolver`.

**Editor/MCP (no code file):**
- `ProjectSettings/TagManager.asset` — three Unity layers already exist (`GizmoHandles`=14, `BoneProxies`=15, `SceneObjects`=13); no creation needed (see Task 1).
- Editor-authored interactable prefabs — add `InteractionLayerTag` set to `SceneObjects`.

---

## Task 1: Confirm the interaction layers exist (DONE — no creation needed)

**Decision (2026-05-31):** the project already defines the three intended layers (they were
pre-declared in `Documentation/architecture_context.md` but never wired). The user reshuffled
their slots. Live layout (read from `mcpforunity://project/layers`):

| Index | Layer |
|---|---|
| 7 | UiPanels (UI — out of scope) |
| 13 | SceneObjects |
| 14 | GizmoHandles |
| 15 | BoneProxies |

We **reuse** these layers rather than create `Gizmo`/`Bone`/`Selectable`. The `InteractionLayer`
enum (Task 2) is named to match the Unity layer names exactly, so `layer.ToString()` yields the
layer name directly — no mapping table, no new layers.

Audit result (controller-verified): no object in `Assets/_App/Content/**` or `Assets/_App/Scenes/**`
sits on any non-zero interaction layer, so the slot reshuffle broke no serialized assignments.
`UiPanels` (now index 7) is referenced only in a doc, never by code or a serialized mask — harmless.

Nothing to create. Code looks layers up by name, never by hardcoded index, so future slot moves stay safe.

---

## Task 2: `InteractionLayer` enum + `InteractionLayers` mapping & priority logic

The pure `PickWinnerIndex` is the real risk-bearing logic and is fully unit-tested. The Unity-layer mapping is also tested (the layers exist after Task 1).

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/Data/InteractionLayer.cs`
- Create: `Assets/_App/Scripts/VrInteraction/InteractionLayers.cs`
- Test: `Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs`

- [ ] **Step 1: Create the enum**

`Assets/_App/Scripts/VrInteraction/Data/InteractionLayer.cs`:

```csharp
/// Interaction layers in priority order. Declaration order IS the priority:
/// index 0 (GizmoHandles) is highest. Each name MUST match an existing Unity layer
/// ("GizmoHandles"/"BoneProxies"/"SceneObjects") so layer.ToString() resolves directly.
public enum InteractionLayer
{
    GizmoHandles,
    BoneProxies,
    SceneObjects,
}
```

- [ ] **Step 2: Write the failing tests**

`Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class InteractionPriorityTests
{
    // --- PickWinnerIndex: pure priority-then-distance selection ---

    [Test]
    public void PickWinnerIndex_Empty_ReturnsMinusOne()
    {
        var idx = InteractionLayers.PickWinnerIndex(new int[0], new float[0]);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void PickWinnerIndex_SingleHit_ReturnsZero()
    {
        var idx = InteractionLayers.PickWinnerIndex(new[] { 2 }, new[] { 5f });
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void PickWinnerIndex_HigherPriorityWins_EvenWhenFarther()
    {
        // priorities: index0 = Selectable(2) near, index1 = Gizmo(0) far.
        var priorities = new[] { 2, 0 };
        var distances  = new[] { 1f, 9f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(1, idx, "Gizmo (priority 0) must win over Selectable (2) regardless of distance");
    }

    [Test]
    public void PickWinnerIndex_SameLayer_NearestWins()
    {
        var priorities = new[] { 2, 2, 2 };
        var distances  = new[] { 4f, 1.5f, 8f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(1, idx, "within one layer the smallest distance wins");
    }

    [Test]
    public void PickWinnerIndex_MixedLayers_PicksNearestWithinTopLayer()
    {
        // Bone(1) at 6 and 2; Selectable(2) at 1. Bone wins; nearest bone is index2.
        var priorities = new[] { 2, 1, 1 };
        var distances  = new[] { 1f, 6f, 2f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(2, idx);
    }

    // --- Unity layer mapping (layers created in Task 1 exist in the project) ---

    [Test]
    public void UnityLayer_ResolvesAllThree()
    {
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.GizmoHandles), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.BoneProxies), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.SceneObjects), 0);
    }

    [Test]
    public void Mask_IncludesAllThreeLayers()
    {
        int mask = InteractionLayers.Mask;
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.GizmoHandles)));
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.BoneProxies)));
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.SceneObjects)));
    }

    [Test]
    public void TryGetPriority_MapsUnityLayerBackToEnumPriority()
    {
        Assert.IsTrue(InteractionLayers.TryGetPriority(
            InteractionLayers.UnityLayer(InteractionLayer.BoneProxies), out var p));
        Assert.AreEqual((int)InteractionLayer.BoneProxies, p);

        // A layer that is not one of the three returns false (0 = Default layer).
        Assert.IsFalse(InteractionLayers.TryGetPriority(0, out _));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run via Unity Test Runner (or MCP `mcp__unityMCP__run_tests` with `test_mode: "EditMode"`, filtering class `InteractionPriorityTests`).
Expected: FAIL — `InteractionLayers` does not exist (compile error / all tests fail).

- [ ] **Step 4: Implement `InteractionLayers`**

`Assets/_App/Scripts/VrInteraction/InteractionLayers.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// Central mapping between the InteractionLayer enum, Unity physics layers, and
/// raycast priority. Priority = enum declaration order (Gizmo = 0 = highest).
public static class InteractionLayers
{
    private const int NotCached = -2; // -1 is "layer not found"; -2 = not yet looked up.

    private static readonly int[] _unityLayers = { NotCached, NotCached, NotCached };
    private static int _mask = NotCached;

    /// Raycast priority of a layer: smaller wins. Equals the enum's integer value.
    public static int Priority(InteractionLayer layer) => (int)layer;

    /// Unity physics layer index for the given interaction layer (cached). -1 if the
    /// named layer was never created in ProjectSettings.
    public static int UnityLayer(InteractionLayer layer)
    {
        int i = (int)layer;
        if (_unityLayers[i] == NotCached)
            _unityLayers[i] = LayerMask.NameToLayer(layer.ToString());
        return _unityLayers[i];
    }

    /// Combined raycast LayerMask of all interaction layers that exist (cached).
    public static int Mask
    {
        get
        {
            if (_mask == NotCached)
            {
                _mask = 0;
                foreach (InteractionLayer l in System.Enum.GetValues(typeof(InteractionLayer)))
                {
                    int unity = UnityLayer(l);
                    if (unity >= 0) _mask |= 1 << unity;
                }
            }
            return _mask;
        }
    }

    /// Maps a Unity physics layer index back to an interaction-layer priority.
    /// Returns false if the layer is not one of the interaction layers.
    public static bool TryGetPriority(int unityLayer, out int priority)
    {
        foreach (InteractionLayer l in System.Enum.GetValues(typeof(InteractionLayer)))
        {
            if (UnityLayer(l) == unityLayer && unityLayer >= 0)
            {
                priority = Priority(l);
                return true;
            }
        }
        priority = 0;
        return false;
    }

    /// Pure selection: given parallel lists of priorities (smaller = higher) and
    /// distances, return the index of the winner — highest priority, nearest within
    /// ties. Returns -1 when the lists are empty. The lists must be the same length.
    public static int PickWinnerIndex(IReadOnlyList<int> priorities, IReadOnlyList<float> distances)
    {
        int best = -1;
        for (int i = 0; i < priorities.Count; i++)
        {
            if (best < 0
                || priorities[i] < priorities[best]
                || (priorities[i] == priorities[best] && distances[i] < distances[best]))
            {
                best = i;
            }
        }
        return best;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run `InteractionPriorityTests` (EditMode).
Expected: PASS — all 8 tests green.

- [ ] **Step 6: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/Data/InteractionLayer.cs Assets/_App/Scripts/VrInteraction/InteractionLayers.cs Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs
git commit -m "feat: InteractionLayer enum + priority/layer mapping with tests"
```

---

## Task 3: `GameObject.SetInteractionLayer` extension

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/GameObjectInteractionLayerExtensions.cs`
- Test: `Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs` (add to existing file)

- [ ] **Step 1: Add the failing test**

Append these tests to `InteractionPriorityTests.cs` (inside the class):

```csharp
    [Test]
    public void SetInteractionLayer_AssignsNamedUnityLayer()
    {
        var go = new GameObject("layered");
        try
        {
            go.SetInteractionLayer(InteractionLayer.GizmoHandles);
            Assert.AreEqual(LayerMask.NameToLayer("GizmoHandles"), go.layer);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void SetInteractionLayer_Selectable_AssignsSelectableLayer()
    {
        var go = new GameObject("layered2");
        try
        {
            go.SetInteractionLayer(InteractionLayer.SceneObjects);
            Assert.AreEqual(LayerMask.NameToLayer("SceneObjects"), go.layer);
        }
        finally { Object.DestroyImmediate(go); }
    }
```

- [ ] **Step 2: Run to verify it fails**

Run `InteractionPriorityTests` (EditMode).
Expected: FAIL — `SetInteractionLayer` extension does not exist (compile error).

- [ ] **Step 3: Implement the extension**

`Assets/_App/Scripts/VrInteraction/GameObjectInteractionLayerExtensions.cs`:

```csharp
using UnityEngine;

/// The single funnel for assigning an interaction layer to the GameObject that
/// carries the collider. Maps the enum to its Unity layer via InteractionLayers.
public static class GameObjectInteractionLayerExtensions
{
    public static void SetInteractionLayer(this GameObject go, InteractionLayer layer)
    {
        if (go == null) return;
        int unity = InteractionLayers.UnityLayer(layer);
        if (unity < 0)
        {
            Debug.LogError($"SetInteractionLayer: Unity layer '{layer}' is missing — create it in " +
                           $"ProjectSettings > Tags and Layers. Leaving '{go.name}' on its current layer.");
            return;
        }
        go.layer = unity;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run `InteractionPriorityTests` (EditMode).
Expected: PASS — the two new tests green (and all prior tests still pass).

- [ ] **Step 5: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/GameObjectInteractionLayerExtensions.cs Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs
git commit -m "feat: SetInteractionLayer GameObject extension with tests"
```

---

## Task 4: `InteractionLayerTag` component

For prefab-authored interactables (e.g. the toilet) — drop the component, pick the layer, it applies on Awake.

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/InteractionLayerTag.cs`

- [ ] **Step 1: Implement the component**

`Assets/_App/Scripts/VrInteraction/InteractionLayerTag.cs`:

```csharp
using UnityEngine;

/// Prefab-authored interaction-layer assignment. Applies its layer to this
/// GameObject at runtime (Awake) and in the editor (OnValidate) for visibility.
[AddComponentMenu("PromeonLab/Interaction Layer Tag")]
public class InteractionLayerTag : MonoBehaviour
{
    [SerializeField] private InteractionLayer _layer = InteractionLayer.SceneObjects;

    private void Awake() => gameObject.SetInteractionLayer(_layer);

    private void OnValidate()
    {
        // Editor-time convenience: keep the GameObject's layer in sync while authoring.
        // Only assigns if the named layer exists (NameToLayer >= 0); harmless no-op otherwise.
        if (InteractionLayers.UnityLayer(_layer) >= 0)
            gameObject.SetInteractionLayer(_layer);
    }
}
```

- [ ] **Step 2: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error) after the domain reload.
Expected: no compile errors; `InteractionLayerTag` becomes available as a component.

- [ ] **Step 3: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/InteractionLayerTag.cs
git commit -m "feat: InteractionLayerTag component for prefab-authored layers"
```

---

## Task 5: `RayInteractionResolver` + scene-scope registration

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/RayInteractionResolver.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`
- Test: `Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs` (add one integration test)

- [ ] **Step 1: Add the failing integration test**

Append to `InteractionPriorityTests.cs` (inside the class). This builds real colliders on the real layers and rays through them:

```csharp
    [Test]
    public void Resolver_HigherLayerBehindLowerLayer_PicksHigherLayer()
    {
        var near = GameObject.CreatePrimitive(PrimitiveType.Cube); // Selectable, near
        var far  = GameObject.CreatePrimitive(PrimitiveType.Cube); // Gizmo, far
        try
        {
            near.transform.position = new Vector3(0, 0, 2);
            far.transform.position  = new Vector3(0, 0, 5);
            near.SetInteractionLayer(InteractionLayer.SceneObjects);
            far.SetInteractionLayer(InteractionLayer.GizmoHandles);
            Physics.SyncTransforms();

            var resolver = new RayInteractionResolver();
            var winner = resolver.ResolvePrimary(new Ray(Vector3.zero, Vector3.forward), 50f);

            Assert.IsNotNull(winner);
            Assert.AreEqual(far.GetComponent<Collider>(), winner,
                "Gizmo layer must win over a nearer Selectable");
        }
        finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
    }

    [Test]
    public void Resolver_NoInteractionLayerHit_ReturnsNull()
    {
        var plain = GameObject.CreatePrimitive(PrimitiveType.Cube); // stays on Default layer
        try
        {
            plain.transform.position = new Vector3(0, 0, 3);
            Physics.SyncTransforms();
            var resolver = new RayInteractionResolver();
            var winner = resolver.ResolvePrimary(new Ray(Vector3.zero, Vector3.forward), 50f);
            Assert.IsNull(winner, "a hit outside the interaction mask is not a winner");
        }
        finally { Object.DestroyImmediate(plain); }
    }
```

> Note: these are EditMode tests relying on synchronous `Physics.Raycast` against freshly-added colliders (`Physics.SyncTransforms()` ensures registration). If this proves flaky in EditMode in this Unity version, demote these two assertions to manual VR verification in Task 11 and keep them as `[Explicit]` — the pure `PickWinnerIndex` tests already cover the selection logic.

- [ ] **Step 2: Run to verify it fails**

Run `InteractionPriorityTests` (EditMode).
Expected: FAIL — `RayInteractionResolver` does not exist (compile error).

- [ ] **Step 3: Implement the resolver**

`Assets/_App/Scripts/VrInteraction/RayInteractionResolver.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// Scene-scoped. Given an interactor ray, returns the prioritized winning collider:
/// among everything the ray passes through on the interaction layers, the highest-priority
/// layer wins (Gizmo > Bone > Selectable), with distance breaking ties within a layer.
/// Returns null when the ray hits nothing on those layers.
public class RayInteractionResolver
{
    private const int MaxHits = 32;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
    private readonly List<int>      _priorities = new List<int>(MaxHits);
    private readonly List<float>    _distances  = new List<float>(MaxHits);
    private readonly List<Collider> _colliders  = new List<Collider>(MaxHits);

    public Collider ResolvePrimary(Ray ray, float maxDistance)
    {
        int count = Physics.RaycastNonAlloc(
            ray, _hits, maxDistance, InteractionLayers.Mask, QueryTriggerInteraction.Ignore);

        _priorities.Clear();
        _distances.Clear();
        _colliders.Clear();

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i].collider;
            if (col == null) continue;
            if (!InteractionLayers.TryGetPriority(col.gameObject.layer, out var priority)) continue;
            _priorities.Add(priority);
            _distances.Add(_hits[i].distance);
            _colliders.Add(col);
        }

        int idx = InteractionLayers.PickWinnerIndex(_priorities, _distances);
        return idx < 0 ? null : _colliders[idx];
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run `InteractionPriorityTests` (EditMode).
Expected: PASS — both resolver tests green (or, if EditMode physics is unreliable, mark them `[Explicit]` per the Step 1 note and proceed).

- [ ] **Step 5: Register the resolver in `VrEditingSceneScope`**

In `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`, after the `GizmoController` registration (line 23), add:

```csharp
        builder.Register<RayInteractionResolver>(Lifetime.Scoped).AsSelf();
```

- [ ] **Step 6: Register the resolver in `SandboxSceneScope`**

In `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`, after the `GizmoController` registration (line 21), add the same line:

```csharp
        builder.Register<RayInteractionResolver>(Lifetime.Scoped).AsSelf();
```

- [ ] **Step 7: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors.

- [ ] **Step 8: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/RayInteractionResolver.cs Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs Assets/_App/Tests/VrInteraction/InteractionPriorityTests.cs
git commit -m "feat: RayInteractionResolver + scene-scope registration"
```

---

## Task 6: `XRPromeonInteractable` consumes the resolver

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs:56-61` (Construct) and `:166-182` (IsPrimaryFor)

- [ ] **Step 1: Add the resolver field and inject it**

In `XRPromeonInteractable.cs`, add a field next to the other injected services (after line 18, `_gizmoController`):

```csharp
    private RayInteractionResolver _rayResolver;
```

Replace the `Construct` method (lines 56–61) with:

```csharp
    [Inject]
    public void Construct(ISelectionManager selectionManager, GizmoController gizmoController, RayInteractionResolver rayResolver)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
        _rayResolver      = rayResolver;
    }
```

- [ ] **Step 2: Rewrite `IsPrimaryFor` to use the resolver**

Replace the `IsPrimaryFor` method (lines 166–182) with:

```csharp
    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        // Ray (Far) path: primary = owner of the prioritized hit (Gizmo > Bone > Selectable),
        // not merely the nearest collider — lets a bone behind the body mesh still win over it.
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (_rayResolver != null)
            {
                var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                var winner = _rayResolver.ResolvePrimary(
                    new Ray(origin.position, origin.forward), ray.maxRaycastDistance);
                return winner != null && colliders.Contains(winner);
            }

            // Fallback (resolver unavailable): pre-resolver nearest-hit behavior.
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false;
        }

        // True Near path (no ray interactor — physical hand interaction only).
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);

        return false;
    }
```

- [ ] **Step 3: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs
git commit -m "feat: XRPromeonInteractable resolves primary via layer priority"
```

---

## Task 7: `GizmoHandle` + `GizmoActivator` consume the resolver

`GizmoHandle` lives on the spawned gizmo prefab and is NOT DI-injected — it is bound explicitly by `GizmoActivator` (which IS injected). So the resolver reaches handles through the existing `Bind` path.

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoHandle.cs:14,21,132-144`
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs:34-52,144-145`

- [ ] **Step 1: Add resolver field + extend `Bind` in `GizmoHandle`**

In `GizmoHandle.cs`, add a field after `_activator` (line 14):

```csharp
    private RayInteractionResolver _rayResolver;
```

Replace the `Bind` method (line 21) with:

```csharp
    public void Bind(GizmoActivator activator, RayInteractionResolver rayResolver)
    {
        _activator   = activator;
        _rayResolver = rayResolver;
    }
```

- [ ] **Step 2: Rewrite `GizmoHandle.IsPrimaryFor`**

Replace `IsPrimaryFor` (lines 132–144) with:

```csharp
    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (_rayResolver != null)
            {
                var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                var winner = _rayResolver.ResolvePrimary(
                    new Ray(origin.position, origin.forward), ray.maxRaycastDistance);
                return winner != null && colliders.Contains(winner);
            }

            // Fallback (resolver unavailable): pre-resolver nearest-hit behavior.
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false;
        }
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);
        return false;
    }
```

- [ ] **Step 3: Inject the resolver into `GizmoActivator`**

In `GizmoActivator.cs`, add a field after `_outlineConfig` (line 12):

```csharp
    private RayInteractionResolver _rayResolver;
```

Replace the `Construct` signature and body assignment (lines 34–41) — add the parameter and store it:

```csharp
    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, GizmoController gizmoController, OutlineConfig outlineConfig, RayInteractionResolver rayResolver)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _gizmoController = gizmoController;
        _outlineConfig   = outlineConfig;
        _rayResolver     = rayResolver;
```

(Leave the rest of `Construct` — the `_bus.Subscribe(...)` calls and the Debug.Log — unchanged.)

- [ ] **Step 4: Pass the resolver when binding handles**

In `GizmoActivator.Spawn`, replace the handle-bind loop (lines 144–145):

```csharp
        foreach (var handle in _instance.GetComponentsInChildren<GizmoHandle>(includeInactive: true))
            handle.Bind(this, _rayResolver);
```

- [ ] **Step 5: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors. (No other caller of `GizmoHandle.Bind` exists — confirmed `Bind` is only called in `GizmoActivator.Spawn`.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/Gizmo/GizmoHandle.cs Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs
git commit -m "feat: GizmoHandle resolves primary via layer priority"
```

---

## Task 8: Assign the `Gizmo` layer to gizmo handles

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs:149-153` (Spawn outline loop)

- [ ] **Step 1: Set the layer where handles get their outline**

In `GizmoActivator.Spawn`, the loop over `MeshRenderer`s (lines 149–153) installs outlines per part. Set the `Gizmo` layer on the GameObject carrying each handle's collider in that same loop. Replace the loop with:

```csharp
        // Outline is installed at runtime (not authored on the prefab), one per mesh part.
        // Axis handles get their axis color; parts without a GizmoHandle (e.g. the move center) get white.
        foreach (var mr in _instance.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            var handle = mr.GetComponent<GizmoHandle>();
            InstallHandleOutline(mr.gameObject, handle != null ? AxisColor(handle.Axis) : Color.white);
        }

        // Gizmo layer: handle colliders sit on the GizmoHandle's own GameObject (GizmoHandle.Awake
        // keeps only the same-GO collider). Tag those so the resolver ranks the gizmo above everything.
        foreach (var handle in _instance.GetComponentsInChildren<GizmoHandle>(includeInactive: true))
            handle.gameObject.SetInteractionLayer(InteractionLayer.GizmoHandles);
```

- [ ] **Step 2: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs
git commit -m "feat: tag gizmo handles with the Gizmo interaction layer"
```

---

## Task 9: Assign the `Bone` layer to bone proxies

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs:295-298` (BuildProxyNode, after interaction components are added)

- [ ] **Step 1: Set the `Bone` layer on each proxy GameObject**

In `PromeonProxyRigBuilder.BuildProxyNode`, the proxy carries its collider on `proxyGo` (added in `AddCollider(proxyGo, mesh)`), and `XRPromeonInteractable.Awake` keeps only the same-GO collider. Tag the proxy GameObject right after the interaction components are added. Replace lines 295–298:

```csharp
        // Interaction components — DI wired by IObjectResolver.InjectGameObject at spawn time.
        // Colliders auto-discover in XRPromeonInteractable.Awake (own GO only by default).
        proxyGo.AddComponent<Selectable>();
        proxyGo.AddComponent<XRPromeonInteractable>();

        // Bone interaction layer: the resolver ranks bones above plain Selectables, so a bone
        // behind the body mesh is still reachable by the ray.
        proxyGo.SetInteractionLayer(InteractionLayer.BoneProxies);
```

- [ ] **Step 2: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs
git commit -m "feat: tag bone proxies with the Bone interaction layer"
```

---

## Task 10: Assign the `Selectable` layer to spawned assets

Spawned import assets do NOT always carry their collider on the spawned root. Verified during
implementation: `(Prb)Toilet` has `XRPromeonInteractable` (`_includeChildColliders = 1`) on the root
but its `BoxCollider`s on the child `toilet` mesh. Since the resolver only raycasts the interaction-
layer mask, a collider left on `Default` is invisible to it — tagging only the root would make such
assets unselectable. Fix: tag **every collider's GameObject** in the spawned hierarchy via
`go.SetInteractionLayerOnColliders(InteractionLayer.SceneObjects)` (added to
`GameObjectInteractionLayerExtensions`). Both spawn paths call `InjectGameObject(go)` — tag at the same
place. (Camera culling mask is Everything, so tagging a mesh child to `SceneObjects` does not hide it.)

**Files:**
- Modify: `Assets/_App/Scripts/SceneComposition/SceneGraph.cs:154-155`
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs:43`

- [ ] **Step 1: Tag load-spawned objects in `SceneGraph`**

In `SceneGraph.OnSceneOpenedAsync`, after `_resolver.InjectGameObject(go);` (line 155), add:

```csharp
                go.SetInteractionLayer(InteractionLayer.SceneObjects);
```

So the block reads:

```csharp
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _resolver.InjectGameObject(go);
                go.SetInteractionLayerOnColliders(InteractionLayer.SceneObjects);
```

- [ ] **Step 2: Tag user-spawned objects in `AssetSpawner`**

In `AssetSpawner.SpawnCoreAsync`, after `_resolver.InjectGameObject(go);` (line 43), add:

```csharp
            go.SetInteractionLayer(InteractionLayer.SceneObjects);
```

So the block reads:

```csharp
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            // Resolve DI on every MonoBehaviour in the spawned hierarchy (XRPromeonInteractable.Construct,
            // PromeonProxyRigBuilder.Construct, etc.).
            _resolver.InjectGameObject(go);
            go.SetInteractionLayerOnColliders(InteractionLayer.SceneObjects);
```

> Note: bone proxies are children created later by the rig builder and get the `Bone` layer in Task 9; tagging the spawned root `Selectable` here does not affect them (they live on separate child GameObjects with their own layer).

- [ ] **Step 3: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Scripts/SceneComposition/SceneGraph.cs Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs
git commit -m "feat: tag spawned assets with the Selectable interaction layer"
```

---

## Task 11: Remove the gizmo target-collider disable

With `Gizmo > Selectable`, the gizmo wins over its own target regardless, so disabling the target's collider during gizmo activation is redundant. Remove it and the `_originalTargetCollider` bookkeeping.

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs:21,133-134,183-184`

- [ ] **Step 1: Remove the field**

In `GizmoActivator.cs`, delete the field declaration (line 21):

```csharp
    private Collider       _originalTargetCollider;
```

- [ ] **Step 2: Remove the disable in `Spawn`**

In `Spawn`, delete these two lines (currently 133–134):

```csharp
        _originalTargetCollider = _target.GetComponent<Collider>();
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = false;
```

- [ ] **Step 3: Remove the re-enable in `Despawn`**

In `Despawn`, delete these two lines (currently 183–184):

```csharp
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = true;
        _originalTargetCollider = null;
```

- [ ] **Step 4: Verify compilation**

Run `mcp__unityMCP__read_console` (filter Error).
Expected: no compile errors; no remaining references to `_originalTargetCollider`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs
git commit -m "refactor: drop redundant gizmo target-collider disable (layer priority handles it)"
```

---

## Task 12: Tag editor-authored interactable prefabs + VR verification

Runtime-spawned objects, bone proxies, and gizmo handles are now tagged by code. Prefab-authored interactables placed directly in a scene (not spawned) would need an `InteractionLayerTag`.

**Finding (controller-verified):** all 11 prefabs carrying `XRPromeonInteractable` live in
`Content/Prefabs/Assets/` (`(Prb)Toilet`, `Crush Dummy`, `Street Tree 1-3`, `Potted Plant 1-3`,
`(Prb)Storage2`, `(Prb)Drawer1`, `(Prb)CoffeTable`). All reach a scene through
`AssetSpawner.SpawnCoreAsync` or `SceneGraph.OnSceneOpenedAsync` — both now tag every collider via
`SetInteractionLayerOnColliders` (Task 10). A grep of `Assets/_App/Scenes/**/*.unity` found **zero**
`XRPromeonInteractable` instances placed directly. **⇒ No `InteractionLayerTag` needs to be added
anywhere; the prefab-tagging step is a no-op.** `InteractionLayerTag` remains available for any future
directly-placed interactable.

- [x] **Step 1–2: Prefab tagging — N/A** (no directly-placed interactables; see finding above).

- [ ] **Step 3: Verify in VR (the real acceptance test)**

Hand off to the user. Per the spec's verification section, confirm in the headset:

1. Gizmo behind the floor / behind the target object → grabbable (Gizmo layer wins).
2. Bone behind the body mesh → selectable (Bone > Selectable).
3. Two selectable objects in line → nearest one selected (distance tie-break within Selectable).
4. Pointing at empty floor (nothing interactive behind) → deselect (no hit in mask).
5. Selecting an object then manipulating its gizmo still works after removing the target-collider disable (Task 11 regression check).

- [ ] **Step 4: Commit any prefab changes**

```bash
git add Assets/_App/Content/Prefabs
git commit -m "feat: tag editor-authored interactables with InteractionLayerTag (Selectable)"
```

---

## Self-Review

**1. Spec coverage:**
- Priority order Gizmo>Bone>Selectable, env = no layer → Task 2 (enum order), Task 5 (resolver masks only the three layers). ✓
- `InteractionLayer` enum + `SetInteractionLayer` extension + `InteractionLayerTag` → Tasks 2, 3, 4. ✓
- Three Unity layers created → Task 1. ✓
- `RayInteractionResolver` with `ResolvePrimary(Ray, maxDistance)`, RaycastAll on mask, priority-then-nearest → Task 5. ✓
- Consumers `XRPromeonInteractable.IsPrimaryFor` + `GizmoHandle.IsPrimaryFor` rewritten → Tasks 6, 7. ✓
- Layer assignment sites: gizmo handles (Task 8), bone proxies (Task 9), spawned assets (Task 10), prefab tags (Task 12). ✓
- Remove gizmo target-collider disable + `_originalTargetCollider` → Task 11. ✓
- maxDistance source = `XRRayInteractor.maxRaycastDistance` (confirmed public property); ray origin = `rayOriginTransform` fallback `transform` (confirmed public property). ✓
- UI out of scope, ray visual out of scope → not touched. ✓

**2. Placeholder scan:** No TBD/TODO; every code step shows full code; test steps contain real assertions. ✓

**3. Type consistency:** `InteractionLayer` enum, `InteractionLayers` (static: `Priority`, `UnityLayer`, `Mask`, `TryGetPriority`, `PickWinnerIndex`), `SetInteractionLayer` extension, `RayInteractionResolver.ResolvePrimary`, `GizmoHandle.Bind(GizmoActivator, RayInteractionResolver)` — names match across all tasks. ✓

**Naming-rule check (CLAUDE.md forbidden suffixes Manager/Handler/Utils/Helper/Controller/Processor/Service):** `RayInteractionResolver` (Resolver — allowed), `InteractionLayers` (plural noun), `InteractionLayerTag` (Tag), `GameObjectInteractionLayerExtensions` (Extensions) — none forbidden. One public type per file. ✓
