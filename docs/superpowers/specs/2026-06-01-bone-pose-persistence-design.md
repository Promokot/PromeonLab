# Bone-Pose Persistence — Design

**Date:** 2026-06-01
**Status:** Approved (pending spec review)
**Scope:** Persist per-bone poses of a rig across save/load. One cohesive change across `StorageCore`, `RigBuilder`, `SceneComposition`, `AssetBrowser` (factory wiring).

## Problem

A rig's root-node transform (`Position`/`Rotation`/`Scale`) is saved and restored, but individual
**bone poses are not**. If the user rotates/moves/scales bones, then leaves the scene and returns, the
rig is rebuilt in its bind pose and the edits are lost. `SceneGraph.CaptureSnapshot` writes only the
root transform per node; `NodeData` has no per-bone data.

## How bones work today (verified)

- The user manipulates a **proxy bone** (`proxyGo`): the gizmo writes `proxy.localPosition`,
  `proxy.localRotation`, `proxy.localScale`.
- `BoneFollower` (on each real skeleton bone) copies the proxy's local TRS to the bone every
  `LateUpdate`: `localPosition`/`localRotation` directly, and `localScale = Scale(baseScale,
  proxy.localScale)` (proxy rests at scale 1, so its scale acts as a multiplier on the bone's rest
  scale).
- So the **proxy bone's local TRS is the authoritative pose input.** Capturing and restoring proxy
  local TRS reproduces the exact bone result, including the scale-multiplier semantics.
- Bones are **transient nodes** (`_transientNodes`); `CaptureSnapshot` iterates `_nodes` only, so
  bones are never saved as separate nodes. Bone poses therefore belong **inside the rig node's
  `NodeData`**, not as separate nodes.
- The proxy hierarchy is rebuilt deterministically from the skeleton by
  `RigEntityFactory.BuildProxyRig` at every restore, so proxy-local values are stable session to
  session.

## Design

### 1. Data contract

New serializable type (`Assets/_App/Scripts/StorageCore/BonePose.cs`):

```csharp
[Serializable]
public class BonePose
{
    public string     BoneName;
    public Vector3     LocalPosition;
    public Quaternion LocalRotation;
    public Vector3     LocalScale;
}
```

`NodeData` gains a list (default empty so non-rig nodes and pre-existing scenes carry an empty list):

```csharp
public List<BonePose> BonePoses = new();
```

Per the chosen contract, the **full local TRS** is stored per bone (matches exactly what
`BoneFollower` consumes). Poses are stored **densely** (every proxy bone), which is simple and robust
for the rig sizes in this app.

### 2. Schema migration

`SceneData.SchemaVersion`: `2 → 3`. In `SceneSerializer.Deserialize`, add a `data.SchemaVersion < 3`
branch that bumps the version and logs (consistent with the existing `< 2` branch). No data transform
is required: a v2 JSON has no `BonePoses` field, and `JsonUtility` leaves the field at its initializer
value (empty list), which `ApplyPoses` treats as "no poses → bind pose." Backward-compatible.

### 3. Capture (ProxyRigRuntime owns the proxy↔bone mapping)

`ProxyRigRuntime` gains:

```csharp
public List<BonePose> CapturePoses();
```

It returns one `BonePose` per proxy bone, reading the proxy transform's **local** `position`,
`rotation`, `scale`, keyed by bone name. (This is the authoritative input that `BoneFollower`
propagates — not the real bone's final TRS.)

`SceneGraph.CaptureSnapshot`: for each node, if it has a `ProxyRigRuntime`
(`node.GetComponentInChildren<ProxyRigRuntime>(true)`), set `nd.BonePoses = rig.CapturePoses()`.
Non-rig nodes leave `BonePoses` empty.

### 4. Restore

`ProxyRigRuntime` gains:

```csharp
public void ApplyPoses(IReadOnlyList<BonePose> poses);
```

For each pose, it finds the proxy by bone name and sets the proxy transform's local TRS. `BoneFollower`
then propagates to the real bone on the next `LateUpdate`. This works regardless of bone-mode vs
whole-rig mode (the follower always reads the proxy) and regardless of whether proxies are currently
active (transform values persist).

`SceneGraph.OnSceneOpenedAsync`: after the node is spawned and `InjectGameObject`'d, if
`nd.BonePoses` is non-empty and the spawned `go` has a `ProxyRigRuntime`, call
`rig.ApplyPoses(nd.BonePoses)`.

### 5. Bone-name ↔ proxy mapping

`RigEntityFactory.BuildProxyRig` already creates each proxy named `proxy_{bone.name}` and wires
`BoneFollower(bone → proxy)`. Rather than parse GameObject names at capture/apply time, the factory
passes a `boneName → proxyTransform` map into `ProxyRigRuntime.Bind`, which stores it for
`CapturePoses`/`ApplyPoses`. `Bind`'s signature grows by one parameter; the factory is its only
caller.

## Data flow

```
Save (ModeExiting → SceneAutoSaver → CaptureSnapshot):
  per rig node → ProxyRigRuntime.CapturePoses() → nd.BonePoses → scene.json (v3)

Load (SceneOpened → OnSceneOpenedAsync):
  spawn rig (BuildProxyRig, bind pose) → ApplyPoses(nd.BonePoses) → proxies posed
  → BoneFollower.LateUpdate → real bones posed
```

## Testing

- **Serialization round-trip (EditMode):** build a `SceneData` v3 with a node carrying `BonePoses`
  (non-trivial TRS), `SceneSerializer.Serialize` then `Deserialize`, assert the TRS values survive and
  `SchemaVersion == 3`. Plus a legacy case: a v2 JSON string (no `BonePoses` field) deserializes to a
  node with an empty `BonePoses` list and is migrated to v3.
- **ProxyRigRuntime capture/apply (EditMode):** build a minimal proxy hierarchy in code (a couple of
  child GameObjects registered through `Bind` with a bone-name map), `ApplyPoses` a known set, then
  `CapturePoses` and assert the round-trip matches by bone name. Null/empty input and unknown bone
  names are no-ops.

## Out of scope

- Animation keyframes / NLA (this is static rig posing only — the current scene state, not timeline
  data).
- Storing poses in the asset recipe (poses are per-scene runtime state and live in `scene.json`, not
  in the global asset library).
- Saving the bone-mode/selection UI state.

## Risks

- **Bone-name uniqueness:** capture/apply key on bone name. If a skeleton has duplicate bone names,
  the map collapses them. Skeletons in this app use unique bone names (glTF/import convention); the
  map build should log a warning if it sees a duplicate so the case is visible rather than silent.
- **Asset bind-pose change between sessions:** poses are stored as absolute proxy-local TRS. If the
  underlying asset's bind pose changes (re-import of the same `AssetId`), restored poses are applied
  on top of the new rest. Acceptable — within a scene's lifetime the asset is fixed, and this is no
  worse than the root-transform behaviour.
