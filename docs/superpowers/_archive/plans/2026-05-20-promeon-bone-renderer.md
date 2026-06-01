# PromeonBoneRenderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Runtime bone visualization for VR (Quest 3) — diamond-shaped GameObjects parented to joint transforms, inheriting from `BoneRenderer`, with `CapsuleCollider` per bone for future grabbable support.

**Architecture:** `PromeonBoneRenderer : BoneRenderer` reuses `transforms[]` and color/size inspector fields from the parent. `Rebuild()` (called from `Awake`) creates N child GameObjects — one per bone pair — each parented to the parent joint so the Unity hierarchy drives movement with zero per-frame script cost. A shared static `Mesh` (6-vertex diamond octahedron) is built once.

**Tech Stack:** Unity 6, URP 17, `Unity.Animation.Rigging` package (already in project).

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs` | **Create** | Main MonoBehaviour — mesh build, pair extraction, `Rebuild()` |
| `Assets/_App/Subsystems/RigBuilder/Tests/Subsystems.RigBuilder.Tests.asmdef` | **Create** | Test assembly definition |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs` | **Create** | EditMode unit tests for mesh and pair extraction |
| `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` | **Modify** (lines 42–57) | Replace `BoneRenderer` with `PromeonBoneRenderer`, call `Rebuild()` |
| `Assets/_App/Editor/PromeonBoneRendererEditor.cs` | **Create** | "Rebuild" button in Inspector |
| `Assets/_App/Editor/PromeonLab.Editor.asmdef` | **Modify** | Add `Subsystems.RigBuilder` reference |

---

### Task 1: Test Assembly

**Files:**
- Create: `Assets/_App/Subsystems/RigBuilder/Tests/Subsystems.RigBuilder.Tests.asmdef`
- Create: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs`

- [ ] **Step 1: Create test asmdef**

Create `Assets/_App/Subsystems/RigBuilder/Tests/Subsystems.RigBuilder.Tests.asmdef`:

```json
{
    "name": "Subsystems.RigBuilder.Tests",
    "references": ["Subsystems.RigBuilder"],
    "includePlatforms": ["Editor"],
    "optionalUnityReferences": ["TestAssemblies"],
    "autoReferenced": false
}
```

- [ ] **Step 2: Create placeholder test file**

Create `Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class PromeonBoneRendererTests
{
}
```

- [ ] **Step 3: Verify assembly appears in Test Runner**

In Unity: **Window → General → Test Runner → EditMode** — `Subsystems.RigBuilder.Tests` appears in the list. No red console errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Subsystems/RigBuilder/Tests/
git commit -m "test: add RigBuilder test assembly"
```

---

### Task 2: Diamond Mesh + Tests

**Files:**
- Create: `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs` (mesh only)
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs`

- [ ] **Step 1: Write failing tests**

Replace `PromeonBoneRendererTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class PromeonBoneRendererTests
{
    [Test]
    public void BuildDiamondMesh_HasSixVertices()
    {
        var mesh = PromeonBoneRenderer.BuildDiamondMesh();
        Assert.AreEqual(6, mesh.vertexCount);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HasTwentyFourTriangleIndices()
    {
        var mesh = PromeonBoneRenderer.BuildDiamondMesh();
        Assert.AreEqual(24, mesh.triangles.Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HeadVertexAtOrigin()
    {
        var mesh = PromeonBoneRenderer.BuildDiamondMesh();
        Assert.AreEqual(Vector3.zero, mesh.vertices[0]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_TailVertexAtUnitY()
    {
        var mesh = PromeonBoneRenderer.BuildDiamondMesh();
        Assert.AreEqual(Vector3.up, mesh.vertices[5]);
        Object.DestroyImmediate(mesh);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

**Window → General → Test Runner → EditMode → Run All**
Expected: compile error — `PromeonBoneRenderer` does not exist yet.

- [ ] **Step 3: Create PromeonBoneRenderer with BuildDiamondMesh**

Create `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("PromeonLab/Bone Renderer (Promeon)")]
public class PromeonBoneRenderer : BoneRenderer
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float _boneWidth = 0.12f;

    private readonly List<GameObject> _boneGOs = new();
    private static Mesh s_BoneMesh;

    // Awake, Rebuild, ExtractPairs added in later tasks.

    public static Mesh BuildDiamondMesh()
    {
        var mesh = new Mesh { name = "PromeonBoneDiamond" };

        mesh.vertices = new[]
        {
            new Vector3( 0f,    0f,    0f),    // 0 head
            new Vector3( 0.5f,  0.15f, 0f),    // 1 shoulder +X
            new Vector3(-0.5f,  0.15f, 0f),    // 2 shoulder -X
            new Vector3( 0f,    0.15f, 0.5f),  // 3 shoulder +Z
            new Vector3( 0f,    0.15f,-0.5f),  // 4 shoulder -Z
            new Vector3( 0f,    1f,    0f),    // 5 tail
        };

        // Winding order: clockwise when viewed from outside (Unity left-hand coords).
        mesh.triangles = new[]
        {
            // Head faces (4 tris from v0 to shoulder ring)
            0, 1, 3,
            0, 3, 2,
            0, 2, 4,
            0, 4, 1,
            // Tail faces (4 tris from shoulder ring to v5)
            1, 5, 3,
            3, 5, 2,
            2, 5, 4,
            4, 5, 1,
        };

        mesh.RecalculateNormals();
        return mesh;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

**Window → General → Test Runner → EditMode → Run All**
Expected: 4 mesh tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs
git add Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs
git commit -m "feat: add PromeonBoneRenderer with diamond mesh"
```

---

### Task 3: Bone Pair Extraction + Tests

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs` (add `ExtractPairs`)
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs`

- [ ] **Step 1: Write failing tests for ExtractPairs**

Append inside the `PromeonBoneRendererTests` class (after the mesh tests):

```csharp
private readonly List<GameObject> _created = new();

[TearDown]
public void TearDown()
{
    foreach (var go in _created)
        if (go != null) Object.DestroyImmediate(go);
    _created.Clear();
}

private GameObject MakeGO(string name, Transform parent = null)
{
    var go = new GameObject(name);
    if (parent != null) go.transform.SetParent(parent);
    _created.Add(go);
    return go;
}

[Test]
public void ExtractPairs_ParentChildBothInSet_ReturnsPair()
{
    var parent = MakeGO("Hip");
    var child  = MakeGO("Thigh", parent.transform);

    var pairs = PromeonBoneRenderer.ExtractPairs(
        new[] { parent.transform, child.transform });

    Assert.AreEqual(1, pairs.Length);
    Assert.AreEqual(parent.transform, pairs[0].start);
    Assert.AreEqual(child.transform,  pairs[0].end);
}

[Test]
public void ExtractPairs_ChildNotInSet_NoPairReturned()
{
    var parent = MakeGO("Hip");
    MakeGO("Thigh", parent.transform); // child exists in hierarchy but not passed in

    var pairs = PromeonBoneRenderer.ExtractPairs(new[] { parent.transform });

    Assert.AreEqual(0, pairs.Length);
}

[Test]
public void ExtractPairs_LeafBone_NotReturnedAsPair()
{
    var leaf = MakeGO("Foot"); // no children in set

    var pairs = PromeonBoneRenderer.ExtractPairs(new[] { leaf.transform });

    Assert.AreEqual(0, pairs.Length);
}

[Test]
public void ExtractPairs_NullTransformInArray_SkippedSafely()
{
    var parent = MakeGO("Hip");
    var child  = MakeGO("Thigh", parent.transform);

    var pairs = PromeonBoneRenderer.ExtractPairs(
        new Transform[] { null, parent.transform, child.transform, null });

    Assert.AreEqual(1, pairs.Length);
}
```

- [ ] **Step 2: Run tests — verify new tests fail**

**Window → General → Test Runner → EditMode → Run All**
Expected: 4 pair tests FAIL with "method not found".

- [ ] **Step 3: Add ExtractPairs to PromeonBoneRenderer**

Add inside the `PromeonBoneRenderer` class (after `BuildDiamondMesh`):

```csharp
public static (Transform start, Transform end)[] ExtractPairs(Transform[] transforms)
{
    var set    = new HashSet<Transform>(transforms);
    set.Remove(null);
    var result = new List<(Transform, Transform)>();

    foreach (var t in transforms)
    {
        if (t == null) continue;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            if (set.Contains(child))
                result.Add((t, child));
        }
    }
    return result.ToArray();
}
```

- [ ] **Step 4: Run tests — verify all 8 pass**

**Window → General → Test Runner → EditMode → Run All**
Expected: all 8 tests (4 mesh + 4 pairs) PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs
git add Assets/_App/Subsystems/RigBuilder/Tests/PromeonBoneRendererTests.cs
git commit -m "feat: add ExtractPairs with tests"
```

---

### Task 4: Full PromeonBoneRenderer

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs` (add `Rebuild`, `CreateBoneGO`, lifecycle)

- [ ] **Step 1: Replace PromeonBoneRenderer.cs with full implementation**

Replace the entire file content:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("PromeonLab/Bone Renderer (Promeon)")]
public class PromeonBoneRenderer : BoneRenderer
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float _boneWidth = 0.12f;

    private readonly List<GameObject> _boneGOs = new();
    private static Mesh s_BoneMesh;

    void Awake() => Rebuild();

    void OnValidate() => Rebuild();

    void OnDestroy() => DestroyBoneGOs();

    public void Rebuild()
    {
        DestroyBoneGOs();

        if (!drawBones || transforms == null || transforms.Length == 0) return;

        if (s_BoneMesh == null) s_BoneMesh = BuildDiamondMesh();

        foreach (var (start, end) in ExtractPairs(transforms))
            _boneGOs.Add(CreateBoneGO(start, end));
    }

    GameObject CreateBoneGO(Transform start, Transform end)
    {
        var go = new GameObject($"Bone_{start.name}");
        go.transform.SetParent(start, worldPositionStays: false);

        go.AddComponent<MeshFilter>().sharedMesh = s_BoneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _boneMaterial;
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonBoneRenderer] _boneMaterial not assigned.", this);

        var col    = go.AddComponent<CapsuleCollider>();
        col.direction = 1;      // Y-axis, matches bone local Y
        col.height    = 1f;     // local space; world height = 1 * localScale.y = bone length
        col.radius    = 0.5f * _boneWidth;

        var localEnd = start.InverseTransformPoint(end.position);
        float length = localEnd.magnitude;
        if (length < 0.0001f) length = 0.0001f;

        var dir = localEnd.normalized;
        if (dir == Vector3.zero) dir = Vector3.up;

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        go.transform.localScale    = new Vector3(_boneWidth * length, length, _boneWidth * length);

        return go;
    }

    void DestroyBoneGOs()
    {
        foreach (var go in _boneGOs)
            if (go != null) DestroyObj(go);
        _boneGOs.Clear();
    }

    static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                        DestroyImmediate(obj);
    }

    public static Mesh BuildDiamondMesh()
    {
        var mesh = new Mesh { name = "PromeonBoneDiamond" };

        mesh.vertices = new[]
        {
            new Vector3( 0f,    0f,    0f),
            new Vector3( 0.5f,  0.15f, 0f),
            new Vector3(-0.5f,  0.15f, 0f),
            new Vector3( 0f,    0.15f, 0.5f),
            new Vector3( 0f,    0.15f,-0.5f),
            new Vector3( 0f,    1f,    0f),
        };

        mesh.triangles = new[]
        {
            0, 1, 3,  0, 3, 2,  0, 2, 4,  0, 4, 1,
            1, 5, 3,  3, 5, 2,  2, 5, 4,  4, 5, 1,
        };

        mesh.RecalculateNormals();
        return mesh;
    }

    public static (Transform start, Transform end)[] ExtractPairs(Transform[] transforms)
    {
        var set    = new HashSet<Transform>(transforms);
        set.Remove(null);
        var result = new List<(Transform, Transform)>();

        foreach (var t in transforms)
        {
            if (t == null) continue;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (set.Contains(child))
                    result.Add((t, child));
            }
        }
        return result.ToArray();
    }
}
```

- [ ] **Step 2: Verify compilation**

**Window → General → Console** — no errors from `PromeonBoneRenderer.cs`.

- [ ] **Step 3: Run all tests — verify nothing broke**

**Window → General → Test Runner → EditMode → Run All**
Expected: all 8 tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Subsystems/RigBuilder/PromeonBoneRenderer.cs
git commit -m "feat: complete PromeonBoneRenderer with Rebuild and bone GOs"
```

---

### Task 5: RigRuntime Integration

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/RigRuntime.cs` (lines 42–57)

Current code (lines 42–57 of `RigRuntime.cs`):

```csharp
var boneRenderer = animator.gameObject.GetComponent<BoneRenderer>();
if (boneRenderer == null) boneRenderer = animator.gameObject.AddComponent<BoneRenderer>();
var transforms = new List<Transform>();
foreach (var bone in definition.Bones)
{
    var t = FindBone(smr, bone.BoneName);
    if (t != null) transforms.Add(t);
}
var prop = typeof(BoneRenderer).GetProperty("transforms");
if (prop?.CanWrite == true)
    prop.SetValue(boneRenderer, transforms.ToArray());
else
{
    var field = typeof(BoneRenderer).GetField("m_Transforms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (field != null) field.SetValue(boneRenderer, transforms.ToArray());
}
```

- [ ] **Step 1: Replace BoneRenderer setup with PromeonBoneRenderer**

Replace those lines with:

```csharp
var boneRenderer = animator.gameObject.GetComponent<PromeonBoneRenderer>();
if (boneRenderer == null) boneRenderer = animator.gameObject.AddComponent<PromeonBoneRenderer>();
var transforms = new List<Transform>();
foreach (var bone in definition.Bones)
{
    var t = FindBone(smr, bone.BoneName);
    if (t != null) transforms.Add(t);
}
var field = typeof(BoneRenderer).GetField("m_Transforms",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
if (field != null) field.SetValue(boneRenderer, transforms.ToArray());
boneRenderer.Rebuild();
```

- [ ] **Step 2: Verify compilation**

**Window → General → Console** — no errors from `RigRuntime.cs`.

- [ ] **Step 3: Run all tests**

**Window → General → Test Runner → EditMode → Run All**
Expected: all 8 tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Subsystems/RigBuilder/RigRuntime.cs
git commit -m "feat: wire PromeonBoneRenderer in RigRuntime.ApplyDefinition"
```

---

### Task 6: Editor Rebuild Button

**Files:**
- Create: `Assets/_App/Editor/PromeonBoneRendererEditor.cs`
- Modify: `Assets/_App/Editor/PromeonLab.Editor.asmdef`

- [ ] **Step 1: Add Subsystems.RigBuilder to editor asmdef**

In `Assets/_App/Editor/PromeonLab.Editor.asmdef`, add `"Subsystems.RigBuilder"` to `references`:

```json
{
    "name": "PromeonLab.Editor",
    "references": ["_Shared", "Subsystems.SpatialUi", "Subsystems.AnimationAuthoring", "Subsystems.RigBuilder", "Unity.TextMeshPro"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create editor script**

Create `Assets/_App/Editor/PromeonBoneRendererEditor.cs`:

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PromeonBoneRenderer))]
public class PromeonBoneRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild"))
        {
            var renderer = (PromeonBoneRenderer)target;
            renderer.Rebuild();
            EditorUtility.SetDirty(renderer);
        }
    }
}
```

- [ ] **Step 3: Verify compilation and Inspector button**

**Window → General → Console** — no errors.
Select a GameObject with `PromeonBoneRenderer` — Inspector should show default fields plus a "Rebuild" button below them.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Editor/PromeonBoneRendererEditor.cs
git add Assets/_App/Editor/PromeonLab.Editor.asmdef
git commit -m "feat: add PromeonBoneRendererEditor with Rebuild button"
```

---

## Smoke Test (Manual in Unity Editor)

1. Open a scene that has a character with `SkinnedMeshRenderer` and call `rigRuntime.ApplyDefinition(definition, smr)`.
2. Verify child GOs named `Bone_*` appear under each joint Transform in the Hierarchy.
3. Each `Bone_*` GO must have: `MeshFilter`, `MeshRenderer`, `CapsuleCollider`.
4. Enter Play Mode — bones follow the rig pose automatically (no script per-frame update).
5. Add `XRGrabInteractable` to one `Bone_*` GO — no compile errors or crashes.
6. Build for Android (Quest) — no build errors related to `PromeonBoneRenderer`.
