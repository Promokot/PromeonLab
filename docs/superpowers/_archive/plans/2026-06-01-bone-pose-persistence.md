# Bone-Pose Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** ✅ Implemented & verified (2026-06-01) — all 3 tasks complete, EditMode green (6 baseline only; +4 new tests), VR-verified. Plus a follow-up fix: reset persistent ShowBones state on scene exit (`InspectorPanel`).

**Goal:** Persist each rig's per-bone poses (full local TRS) across save/load so manual posing survives a scene reload.

**Architecture:** Add a `BonePose` record + a `BonePoses` list on `NodeData` (scene.json, schema v3). `ProxyRigRuntime` (which owns the proxy↔bone mapping) gains `CapturePoses`/`ApplyPoses` working on proxy-local transforms — the authoritative pose input that `BoneFollower` propagates to real bones. `SceneGraph` captures poses per rig node at save and re-applies them after spawn at load.

**Tech Stack:** Unity 6000.3.7f1, C#, `JsonUtility` serialization, VContainer, custom proxy-bone rig. Tests: Unity Test Runner (EditMode) via Unity MCP.

> **PROJECT RULE — NO GIT.** Never run `git add`/`git commit`/any git. Each task ends with a **Checkpoint** the orchestrator runs:
> 1. `refresh_unity` (mode:`force`, scope:`all`, compile:`request`)
> 2. `read_console` (types:`[error]`, filter_text:`CS`) — only `error CS####` matters; `MCP-FOR-UNITY: …`, `MissingReferenceException: m_Targets`, `SerializedObjectNotCreatableException` are harmless noise.
> 3. `run_tests` (mode:`EditMode`) + `get_test_job` (wait_timeout:60).
>
> **Baseline = 6 known pre-existing EditMode failures:** `PathProviderTests` ×4, `RingRotateStrategyTests` ×2. A task passes when compilation is clean and the failure set is exactly those 6 (plus any net-new tests this plan adds must PASS).

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Assets/_App/Scripts/StorageCore/BonePose.cs` | Create | Serializable per-bone local TRS record. |
| `Assets/_App/Scripts/StorageCore/NodeData.cs` | Modify | Add `List<BonePose> BonePoses`. |
| `Assets/_App/Scripts/StorageCore/SceneData.cs` | Modify | Default `SchemaVersion` 2 → 3. |
| `Assets/_App/Scripts/StorageCore/SceneSerializer.cs` | Modify | Add `< 3` migration branch. |
| `Assets/_App/Tests/StorageCore/BonePosePersistenceTests.cs` | Create | Serialization round-trip + legacy-migration tests. |
| `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs` | Modify | `_boneProxies` map; `Bind` +map param; `CapturePoses`/`ApplyPoses`. |
| `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs` | Modify | Build the boneName→proxy map and pass it to `Bind`. |
| `Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs` | Modify | Update 2 `Bind` calls to the new 4-arg signature. |
| `Assets/_App/Tests/RigBuilder/ProxyRigBonePoseTests.cs` | Create | `CapturePoses`/`ApplyPoses` round-trip + no-op tests. |
| `Assets/_App/Scripts/SceneComposition/SceneGraph.cs` | Modify | Capture poses in `CaptureSnapshot`; re-apply in `OnSceneOpenedAsync`; bump written `SchemaVersion`. |

**Task order:** Task 1 (data layer) → Task 2 (rig capture/apply) → Task 3 (SceneGraph wiring, depends on both).

---

## Task 1: Data contract + schema migration

**Files:**
- Create: `Assets/_App/Scripts/StorageCore/BonePose.cs`
- Modify: `Assets/_App/Scripts/StorageCore/NodeData.cs`
- Modify: `Assets/_App/Scripts/StorageCore/SceneData.cs`
- Modify: `Assets/_App/Scripts/StorageCore/SceneSerializer.cs`
- Test (create): `Assets/_App/Tests/StorageCore/BonePosePersistenceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/_App/Tests/StorageCore/BonePosePersistenceTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BonePosePersistenceTests
{
    [Test]
    public void SceneData_WithBonePoses_RoundTripsThroughSerializer()
    {
        var data = new SceneData { SceneId = "scene-01", DisplayName = "Test", CreatedAt = "2026-06-01" };
        data.Nodes.Add(new NodeData
        {
            NodeId    = "n1",
            BonePoses =
            {
                new BonePose
                {
                    BoneName      = "hips",
                    LocalPosition = new Vector3(1, 2, 3),
                    LocalRotation = Quaternion.Euler(0, 90, 0),
                    LocalScale    = new Vector3(2, 2, 2),
                },
            },
        });

        var json = SceneSerializer.Serialize(data);
        var back = SceneSerializer.Deserialize(json);

        Assert.AreEqual(3, back.SchemaVersion);
        Assert.AreEqual(1, back.Nodes[0].BonePoses.Count);
        var p = back.Nodes[0].BonePoses[0];
        Assert.AreEqual("hips", p.BoneName);
        Assert.AreEqual(new Vector3(1, 2, 3), p.LocalPosition);
        Assert.AreEqual(new Vector3(2, 2, 2), p.LocalScale);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0, 90, 0), p.LocalRotation), Is.LessThan(0.01f));
    }

    [Test]
    public void LegacyV2Json_WithoutBonePoses_MigratesToV3WithEmptyList()
    {
        var json = "{\"SchemaVersion\":2,\"SceneId\":\"old\",\"DisplayName\":\"Old\",\"CreatedAt\":\"x\"," +
                   "\"Nodes\":[{\"NodeId\":\"n1\",\"Position\":{\"x\":0,\"y\":0,\"z\":0}," +
                   "\"Rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"Scale\":{\"x\":1,\"y\":1,\"z\":1}}]}";
        var back = SceneSerializer.Deserialize(json);

        Assert.AreEqual(3, back.SchemaVersion);
        Assert.IsNotNull(back.Nodes[0].BonePoses);
        Assert.AreEqual(0, back.Nodes[0].BonePoses.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Orchestrator: `run_tests` (mode:`EditMode`, test_names:`["BonePosePersistenceTests"]`).
Expected: compile error / fail — `BonePose` type and `NodeData.BonePoses` don't exist yet.

- [ ] **Step 3: Create `BonePose.cs`**

```csharp
using System;
using UnityEngine;

[Serializable]
public class BonePose
{
    public string     BoneName;
    public Vector3     LocalPosition;
    public Quaternion LocalRotation;
    public Vector3     LocalScale;
}
```

- [ ] **Step 4: Add `BonePoses` to `NodeData.cs`**

Replace the full contents of `Assets/_App/Scripts/StorageCore/NodeData.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NodeData
{
    public string     NodeId;
    public AssetRef   AssetRef;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
    public string     DisplayName;
    public string     ParentNodeId;
    public List<BonePose> BonePoses = new(); // empty for non-rig nodes and pre-v3 scenes
}
```

- [ ] **Step 5: Bump `SceneData` default schema**

In `Assets/_App/Scripts/StorageCore/SceneData.cs`, change the field initializer:

```csharp
    public int            SchemaVersion = 3;
```

- [ ] **Step 6: Add the `< 3` migration branch**

Replace the full contents of `Assets/_App/Scripts/StorageCore/SceneSerializer.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

public static class SceneSerializer
{
    public static string Serialize(SceneData data) =>
        JsonUtility.ToJson(data, prettyPrint: true);

    public static SceneData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var data = JsonUtility.FromJson<SceneData>(json);
        if (data == null) return null;
        if (data.SchemaVersion < 2)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v2");
            data.SchemaVersion = 2;
            data.Nodes ??= new List<NodeData>();
        }
        if (data.SchemaVersion < 3)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v3");
            data.SchemaVersion = 3;
            foreach (var n in data.Nodes) n.BonePoses ??= new List<BonePose>();
        }
        return data;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Orchestrator: `run_tests` (mode:`EditMode`, test_names:`["BonePosePersistenceTests"]`).
Expected: both PASS.

- [ ] **Step 8: Checkpoint**

Full Checkpoint. Expected: clean compile; the 2 new tests PASS; failure set exactly the 6 baseline.

---

## Task 2: ProxyRigRuntime capture/apply + factory mapping

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`
- Modify: `Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs`
- Test (create): `Assets/_App/Tests/RigBuilder/ProxyRigBonePoseTests.cs`

**Background:** `ProxyRigRuntime.Bind` is called only by `RigEntityFactory.BuildProxyRig`. We add a `boneName → proxyTransform` map so capture/apply key on bone name without parsing GameObject names. `CapturePoses`/`ApplyPoses` read/write the proxy's LOCAL transform (the input `BoneFollower` propagates).

- [ ] **Step 1: Write the failing tests**

Create `Assets/_App/Tests/RigBuilder/ProxyRigBonePoseTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class ProxyRigBonePoseTests
{
    [Test]
    public void ApplyThenCapture_RoundTripsLocalTrsByBoneName()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var hips  = new GameObject("proxy_hips");  hips.transform.SetParent(proxyRoot.transform);
        var spine = new GameObject("proxy_spine"); spine.transform.SetParent(hips.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        var map = new Dictionary<string, Transform> { { "hips", hips.transform }, { "spine", spine.transform } };
        runtime.Bind(proxyRoot.transform, new List<GameObject> { hips, spine }, null, map);

        runtime.ApplyPoses(new List<BonePose>
        {
            new BonePose { BoneName = "hips",  LocalPosition = new Vector3(1, 2, 3),   LocalRotation = Quaternion.Euler(10, 20, 30), LocalScale = Vector3.one },
            new BonePose { BoneName = "spine", LocalPosition = new Vector3(0, 0.5f, 0), LocalRotation = Quaternion.Euler(0, 45, 0),   LocalScale = new Vector3(2, 2, 2) },
        });

        Assert.AreEqual(new Vector3(1, 2, 3), hips.transform.localPosition);
        Assert.AreEqual(new Vector3(2, 2, 2), spine.transform.localScale);

        var byName = runtime.CapturePoses().ToDictionary(p => p.BoneName);
        Assert.AreEqual(2, byName.Count);
        Assert.AreEqual(new Vector3(1, 2, 3), byName["hips"].LocalPosition);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0, 45, 0), byName["spine"].LocalRotation), Is.LessThan(0.01f));

        Object.DestroyImmediate(root);
    }

    [Test]
    public void ApplyPoses_NullAndUnknownBone_AreNoOps()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var hips = new GameObject("proxy_hips"); hips.transform.SetParent(proxyRoot.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { hips }, null,
            new Dictionary<string, Transform> { { "hips", hips.transform } });

        runtime.ApplyPoses(null); // must not throw
        runtime.ApplyPoses(new List<BonePose> { new BonePose { BoneName = "nonexistent", LocalPosition = Vector3.one } });

        Assert.AreEqual(Vector3.zero, hips.transform.localPosition, "unknown bone must not move hips");

        Object.DestroyImmediate(root);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Orchestrator: `run_tests` (mode:`EditMode`, test_names:`["ProxyRigBonePoseTests"]`).
Expected: compile error — `Bind` has no 4-arg overload; `CapturePoses`/`ApplyPoses` don't exist.

- [ ] **Step 3: Add the map field + extend `Bind` + add capture/apply in `ProxyRigRuntime.cs`**

In `Assets/_App/Scripts/RigBuilder/ProxyRigRuntime.cs`, add the field next to the other private collections (after the `_selectorColliders` line):

```csharp
    private readonly Dictionary<string, Transform> _boneProxies = new(); // boneName → proxy transform (pose I/O)
```

Replace the current `Bind` method:

```csharp
    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs, List<Collider> selectorColliders)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        _selectorColliders.Clear();
        if (selectorColliders != null) _selectorColliders.AddRange(selectorColliders);
        SetBonesInteractive(false); // start in whole-rig select mode
    }
```

with:

```csharp
    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs, List<Collider> selectorColliders,
                     IReadOnlyDictionary<string, Transform> boneProxies)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        _selectorColliders.Clear();
        if (selectorColliders != null) _selectorColliders.AddRange(selectorColliders);
        _boneProxies.Clear();
        if (boneProxies != null)
            foreach (var kv in boneProxies) _boneProxies[kv.Key] = kv.Value;
        SetBonesInteractive(false); // start in whole-rig select mode
    }

    // Per-bone pose I/O for scene persistence. The proxy's LOCAL transform is the authoritative pose
    // input (BoneFollower copies it onto the real bone each LateUpdate), so we capture/restore proxy
    // locals keyed by bone name. No-ops for null/empty input or unknown bone names.
    public List<BonePose> CapturePoses()
    {
        var poses = new List<BonePose>(_boneProxies.Count);
        foreach (var kv in _boneProxies)
        {
            var t = kv.Value;
            if (t == null) continue;
            poses.Add(new BonePose
            {
                BoneName      = kv.Key,
                LocalPosition = t.localPosition,
                LocalRotation = t.localRotation,
                LocalScale    = t.localScale,
            });
        }
        return poses;
    }

    public void ApplyPoses(IReadOnlyList<BonePose> poses)
    {
        if (poses == null) return;
        foreach (var p in poses)
        {
            if (p == null || string.IsNullOrEmpty(p.BoneName)) continue;
            if (!_boneProxies.TryGetValue(p.BoneName, out var t) || t == null) continue;
            t.localPosition = p.LocalPosition;
            t.localRotation = p.LocalRotation;
            t.localScale    = p.LocalScale;
        }
    }
```

(`using System.Collections.Generic;` is already imported in this file — `Dictionary`/`IReadOnlyList`/`IReadOnlyDictionary` resolve. `BonePose` is in the global namespace.)

- [ ] **Step 4: Build the map and pass it in `RigEntityFactory.cs`**

In `Assets/_App/Scripts/AssetBrowser/RigEntityFactory.cs`, in `BuildProxyRig`, add the map declaration right after `var proxyGOs = new List<GameObject>();`:

```csharp
        var boneProxies = new Dictionary<string, Transform>();
```

In `BuildProxyRig`, the loop calls `BuildProxyNode(bone, proxyRoot, set, proxyGOs, terminalAxis, invertAxis);` — add `boneProxies` as the last argument:

```csharp
            BuildProxyNode(bone, proxyRoot, set, proxyGOs, boneProxies, terminalAxis, invertAxis);
```

Replace the `runtime.Bind(...)` call:

```csharp
        runtime.Bind(proxyRoot, proxyGOs, selectorColliders);
```

with:

```csharp
        runtime.Bind(proxyRoot, proxyGOs, selectorColliders, boneProxies);
```

Change the `BuildProxyNode` signature to accept the map:

```csharp
    private void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set, List<GameObject> proxyGOs, TerminalBoneAxis terminalAxis, bool invertAxis)
```

becomes:

```csharp
    private void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set, List<GameObject> proxyGOs, Dictionary<string, Transform> boneProxies, TerminalBoneAxis terminalAxis, bool invertAxis)
```

Inside `BuildProxyNode`, immediately after `proxyGOs.Add(proxyGo);`, record the mapping:

```csharp
        boneProxies[bone.name] = proxyGo.transform;
```

And the recursive call at the end of `BuildProxyNode` — `BuildProxyNode(child, proxyGo.transform, set, proxyGOs, terminalAxis, invertAxis);` — gets the map too:

```csharp
            BuildProxyNode(child, proxyGo.transform, set, proxyGOs, boneProxies, terminalAxis, invertAxis);
```

(`using System.Collections.Generic;` is already imported in this file.)

- [ ] **Step 5: Update the existing `ProxyRigRuntimeTests` Bind calls**

In `Assets/_App/Tests/RigBuilder/ProxyRigRuntimeTests.cs`, the two `Bind` calls now need a 4th argument.

Replace:
```csharp
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1, p2 },
            new List<Collider> { selectorCol });
```
with:
```csharp
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1, p2 },
            new List<Collider> { selectorCol }, new Dictionary<string, Transform>());
```

Replace:
```csharp
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1 }, null);
```
with:
```csharp
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1 }, null, null);
```

(`using System.Collections.Generic;` is already imported at the top of this test file.)

- [ ] **Step 6: Run tests to verify they pass**

Orchestrator: `run_tests` (mode:`EditMode`, test_names:`["ProxyRigBonePoseTests","ProxyRigRuntimeTests"]`).
Expected: all PASS.

- [ ] **Step 7: Checkpoint**

Full Checkpoint. Expected: clean compile; new + updated rig tests PASS; failure set exactly the 6 baseline.

---

## Task 3: SceneGraph capture + restore wiring

**Files:**
- Modify: `Assets/_App/Scripts/SceneComposition/SceneGraph.cs`

**Background:** This wires the two ends. No new automated test — this is an integration path needing storage, spawners, EventBus, and the DI resolver, which EditMode can't exercise cleanly. Coverage comes from Task 1 (serialization) + Task 2 (capture/apply) units, plus the manual VR round-trip at the end. `ProxyRigRuntime` is attached to the rig's root GameObject by `BuildProxyRig`, so `GetComponentInChildren<ProxyRigRuntime>(true)` on the node finds it.

- [ ] **Step 1: Capture poses + write schema v3 in `CaptureSnapshot`**

In `Assets/_App/Scripts/SceneComposition/SceneGraph.cs`, in `CaptureSnapshot`, change the `SceneData` initializer's `SchemaVersion = 2` to `3`:

```csharp
        var data = new SceneData
        {
            SchemaVersion = 3,
            SceneId       = sceneId,
            DisplayName   = displayName,
            CreatedAt     = createdAt,
        };
```

Then, inside the `foreach (var pair in _nodes)` loop, resolve the rig before adding the node and include `BonePoses` in the `NodeData` initializer. The loop body becomes:

```csharp
        foreach (var pair in _nodes)
        {
            var id   = pair.Key;
            var node = pair.Value;
            string parentId = null;
            if (node.transform.parent != null && node.transform.parent != _spawnedRoot)
            {
                var pn = node.transform.parent.GetComponent<SceneNode>();
                if (pn != null) parentId = pn.NodeId;
            }
            var rig = node.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true);
            data.Nodes.Add(new NodeData
            {
                NodeId       = id,
                AssetRef     = node.AssetRef,
                Position     = node.transform.position,
                Rotation     = node.transform.rotation,
                Scale        = node.transform.localScale,
                DisplayName  = node.DisplayName,
                ParentNodeId = parentId,
                BonePoses    = rig != null ? rig.CapturePoses() : new List<BonePose>(),
            });
        }
```

(`using System.Collections.Generic;` is already imported in `SceneGraph.cs`.)

- [ ] **Step 2: Re-apply poses after spawn in `OnSceneOpenedAsync`**

In the first `foreach (var nd in data.Nodes)` loop of `OnSceneOpenedAsync`, the current tail is:

```csharp
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _resolver.InjectGameObject(go);
```

Add the pose re-application immediately after `_resolver.InjectGameObject(go);`:

```csharp
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _resolver.InjectGameObject(go);
                if (nd.BonePoses != null && nd.BonePoses.Count > 0)
                    go.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true)?.ApplyPoses(nd.BonePoses);
```

- [ ] **Step 3: Checkpoint**

Full Checkpoint. Expected: clean compile; failure set exactly the 6 baseline (no new automated test in this task).

---

## Final Verification (after all tasks)

- [ ] Full `run_tests` (mode:`EditMode`): the 4 net-new tests (2 in `BonePosePersistenceTests`, 2 in `ProxyRigBonePoseTests`) PASS; failures are exactly the 6 baseline, zero new.
- [ ] `read_console` (types:`[error]`, filter_text:`CS`): no `error CS####`.
- [ ] Hand to user for VR round-trip: spawn a rig, enter bone mode, rotate/move/scale a few bones, return to `MainMenu`, re-enter `VrEditing` (same scene), confirm the bones come back posed (not bind pose). Also confirm a previously-saved (v2) scene still loads (rig in bind pose, no errors).

---

## Self-Review

**Spec coverage:**
- Spec §1 data contract (`BonePose`, `NodeData.BonePoses`) → Task 1 Steps 3–4. ✓
- Spec §2 schema migration (v3, `< 3` branch) → Task 1 Steps 5–6. ✓
- Spec §3 capture (`ProxyRigRuntime.CapturePoses`, `SceneGraph` wiring) → Task 2 Step 3 + Task 3 Step 1. ✓
- Spec §4 restore (`ApplyPoses`, `OnSceneOpenedAsync`) → Task 2 Step 3 + Task 3 Step 2. ✓
- Spec §5 bone-name↔proxy map (factory passes map to `Bind`) → Task 2 Steps 3–4. ✓
- Spec §Testing (serialization round-trip + legacy; capture/apply round-trip + no-ops) → Task 1 Step 1 + Task 2 Step 1. ✓
- Spec out-of-scope (keyframes, recipe storage, UI state) → not present. ✓

**Placeholder scan:** No TBD/TODO/vague steps; every code step shows full code. ✓

**Type consistency:** `Bind(Transform, List<GameObject>, List<Collider>, IReadOnlyDictionary<string,Transform>)` — same 4-arg shape in ProxyRigRuntime def (Task 2 Step 3), factory call (Step 4), and both test call sites (Steps 1, 5). `CapturePoses()`/`ApplyPoses(IReadOnlyList<BonePose>)` consistent across definition, tests, and SceneGraph callers. `BonePose` fields (`BoneName`/`LocalPosition`/`LocalRotation`/`LocalScale`) identical in Task 1 type def and all usages. `SchemaVersion == 3` consistent (SceneData default, migration target, CaptureSnapshot write, test assertions). ✓

**Migration ordering:** `< 2` branch bumps to 2, then `< 3` branch (2 < 3) bumps to 3 and ensures `BonePoses` non-null — old v1/v2 scenes both land at v3 cleanly. ✓
