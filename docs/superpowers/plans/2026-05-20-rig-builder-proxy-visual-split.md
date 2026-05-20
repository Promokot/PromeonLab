# RigBuilder Proxy/Visual Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `PromeonInteractableRigBuilder` so that in constraint mode each bone produces a collider-only manipulator proxy (independent, drives bone via `MultiParentConstraint`) and a separate visual GO (diamond mesh + Outline, child of the original bone), ensuring visuals always reflect true hierarchy position when parent bones are moved.

**Architecture:** Three changes land together in one file: (1) `EffectiveWidth` static formula (TDD first), (2) trivial renames/log-level fixes, (3) architectural split of `CreateBoneGO` → `CreateBoneGOs` with two GOs in constraint mode. Visual mode (no constraints) is unchanged — still one combined GO per bone pair.

**Tech Stack:** Unity 6, C#, Unity Animation Rigging (`MultiParentConstraint`), QuickOutline (`Outline`). Tests run via `Window > General > Test Runner` (EditMode).

---

## File Map

| File | Change |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Add `EffectiveWidth`, fix menu/log, refactor `CreateBoneGOs`, add `_visualGOs`, extract helpers |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | Add 3 tests for `EffectiveWidth` |

---

## Task 1: Add `EffectiveWidth` static method (TDD)

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs`
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

- [ ] **Step 1: Add 3 failing tests to the test file**

Open `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` and append these tests before the final closing brace:

```csharp
    [Test]
    public void EffectiveWidth_LongBone_ReturnsBoneWidth()
    {
        // 1.0 >> 5 * 0.06 — returns full boneWidth
        Assert.AreEqual(0.06f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 1.0f), 0.0001f);
    }

    [Test]
    public void EffectiveWidth_ShortBone_ReturnsCappedWidth()
    {
        // 0.1 * 0.2 = 0.02 < 0.06 — returns scaled width
        Assert.AreEqual(0.02f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 0.1f), 0.0001f);
    }

    [Test]
    public void EffectiveWidth_AtThreshold_ReturnsBoneWidth()
    {
        // 0.3 * 0.2 = 0.06 = boneWidth — exactly at threshold
        Assert.AreEqual(0.06f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 0.3f), 0.0001f);
    }
```

- [ ] **Step 2: Run tests — confirm they fail**

Open `Window > General > Test Runner` in Unity. Switch to **EditMode**. Run `EffectiveWidth_*`. Expected: 3 failures (`EffectiveWidth` method not found).

- [ ] **Step 3: Add `EffectiveWidth` to the main file**

In `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`, add this static method directly above `BuildDiamondMesh`:

```csharp
    public static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);
```

- [ ] **Step 4: Run tests — confirm they pass**

Test Runner → EditMode → run `EffectiveWidth_*`. Expected: 3 PASS. All 11 tests (8 existing + 3 new) should pass.

- [ ] **Step 5: Commit**

```
git add Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs
git add Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs
git commit -m "feat: add EffectiveWidth proportional diamond width formula"
```

---

## Task 2: Fix component menu name and constraint fallback log level

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

No new tests — these are 1-line changes with no testable logic.

- [ ] **Step 1: Fix the AddComponentMenu attribute**

Find this line at the top of the class (line ~5):
```csharp
[AddComponentMenu("PromeonLab/Interactable Rig Builder")]
```
Replace with:
```csharp
[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]
```

Now searching "Promeon Interactable" in the Add Component dialog will find the component.

- [ ] **Step 2: Downgrade the constraint fallback warning to a log**

Find `AddParentConstraint` and replace the `Debug.LogWarning` line:
```csharp
// BEFORE:
Debug.LogWarning("[PromeonInteractableRigBuilder] _buildConstraints=true but no rig parent set. Call SetConstraintRigParent() before Rebuild().", this);

// AFTER:
Debug.Log("[PromeonInteractableRigBuilder] No rig parent set — constraints skipped, falling back to visual mode. Call SetConstraintRigParent() before Rebuild().", this);
```

This fires when pressing the Inspector "Rebuild" button without RigRuntime setup — expected behavior, not an error.

- [ ] **Step 3: Verify Unity compiles without errors**

Switch to Unity Editor. Wait for domain reload. Check the Console — no compile errors.

- [ ] **Step 4: Commit**

```
git add Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs
git commit -m "fix: rename component menu entry, downgrade constraint fallback to log"
```

---

## Task 3: Refactor `CreateBoneGOs` — split proxy and visual in constraint mode

**Files:**
- Modify: `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs`

This is the main architectural change. The result: in constraint mode each bone pair creates a `Proxy_*` GO (collider only, under `_BoneProxies`) and a `Visual_*` GO (diamond mesh + Outline, child of the original bone). Visual mode is unchanged.

- [ ] **Step 1: Add the `_visualGOs` field**

Find the field declarations block:
```csharp
    private readonly List<GameObject> _boneGOs       = new();
    private readonly List<GameObject> _constraintGOs = new();
```
Replace with:
```csharp
    private readonly List<GameObject> _boneGOs       = new();
    private readonly List<GameObject> _visualGOs     = new();
    private readonly List<GameObject> _constraintGOs = new();
```

- [ ] **Step 2: Extract `AddCollider` helper**

The current `CreateBoneGO` method has an inline collider block. We need it as a standalone helper because both the proxy (constraint mode) and the combined GO (visual mode) need a collider, but the visual GO does not.

Add this private method after `AddParentConstraint`:

```csharp
    void AddCollider(GameObject go)
    {
        if (_useConvexCollider)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = _boneMesh;
            mc.convex = true;
        }
        else
        {
            var col       = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height    = 1f;
            col.radius    = 0.5f;
        }
    }
```

- [ ] **Step 3: Extract `AddMeshAndOutline` helper**

Add this private method after `AddCollider`:

```csharp
    void AddMeshAndOutline(GameObject go)
    {
        go.AddComponent<MeshFilter>().sharedMesh = _boneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _boneMaterial;
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonInteractableRigBuilder] _boneMaterial not assigned.", this);

        var outline         = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 3f;
    }
```

- [ ] **Step 4: Replace `CreateBoneGO` with `CreateBoneGOs`**

Delete the entire `GameObject CreateBoneGO(Transform start, Transform end)` method and replace it with:

```csharp
    void CreateBoneGOs(Transform start, Transform end)
    {
        if (_proxyRoot != null)
        {
            // Constraint mode: two GOs per bone pair
            var worldVec = end.position - start.position;
            float length = Mathf.Max(worldVec.magnitude, 0.0001f);
            float width  = EffectiveWidth(_boneWidth, length);

            // Proxy — manipulator, collider only, independent under _BoneProxies
            var proxyGo = new GameObject($"Proxy_{start.name}");
            proxyGo.transform.SetParent(_proxyRoot, worldPositionStays: false);
            proxyGo.transform.SetPositionAndRotation(
                start.position,
                Quaternion.FromToRotation(Vector3.up, worldVec / length));
            proxyGo.transform.localScale = new Vector3(width, length, width);
            AddCollider(proxyGo);
            _boneGOs.Add(proxyGo);

            // Visual — diamond + Outline, child of original bone, follows hierarchy
            var localEnd    = start.InverseTransformPoint(end.position);
            float localLen  = Mathf.Max(localEnd.magnitude, 0.0001f);
            float localW    = EffectiveWidth(_boneWidth, localLen);

            var visualGo = new GameObject($"Visual_{start.name}");
            visualGo.transform.SetParent(start, worldPositionStays: false);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localEnd.normalized);
            visualGo.transform.localScale    = new Vector3(localW, localLen, localW);
            AddMeshAndOutline(visualGo);
            _visualGOs.Add(visualGo);

            AddParentConstraint(start, proxyGo.transform);
        }
        else
        {
            // Visual mode: single combined GO, child of bone
            var localEnd = start.InverseTransformPoint(end.position);
            float length = Mathf.Max(localEnd.magnitude, 0.0001f);
            float width  = EffectiveWidth(_boneWidth, length);

            var go = new GameObject($"Bone_{start.name}");
            go.transform.SetParent(start, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localEnd.normalized);
            go.transform.localScale    = new Vector3(width, length, width);
            AddMeshAndOutline(go);
            AddCollider(go);
            _boneGOs.Add(go);
        }
    }
```

- [ ] **Step 5: Update `Rebuild` to call `CreateBoneGOs`**

Find this line in `Rebuild()`:
```csharp
        foreach (var (start, end) in ExtractPairs(transforms))
            _boneGOs.Add(CreateBoneGO(start, end));
```
Replace with:
```csharp
        foreach (var (start, end) in ExtractPairs(transforms))
            CreateBoneGOs(start, end);
```

- [ ] **Step 6: Update `SetVisualsEnabled` to cover both lists**

Replace the current `SetVisualsEnabled` method with:

```csharp
    public void SetVisualsEnabled(bool enabled)
    {
        ToggleVisuals(_boneGOs, enabled);
        ToggleVisuals(_visualGOs, enabled);
    }

    void ToggleVisuals(List<GameObject> list, bool enabled)
    {
        foreach (var go in list)
        {
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
        }
    }
```

In constraint mode `_boneGOs` are proxies (no MeshRenderer), so the `GetComponent` returns null — safe for both modes without branching.

- [ ] **Step 7: Update `DestroyBoneGOs` to also destroy `_visualGOs`**

Replace the current `DestroyBoneGOs` method with:

```csharp
    void DestroyBoneGOs()
    {
        foreach (var go in _constraintGOs)
            if (go != null) DestroyObj(go);
        _constraintGOs.Clear();

        if (_proxyRoot != null)
        {
            DestroyObj(_proxyRoot.gameObject);   // cascades to all Proxy_* GOs
            _proxyRoot = null;
        }
        else
        {
            foreach (var go in _boneGOs)
                if (go != null) DestroyObj(go);
        }
        _boneGOs.Clear();

        // Visual GOs are children of original bones, not of _proxyRoot — destroy explicitly
        foreach (var go in _visualGOs)
            if (go != null) DestroyObj(go);
        _visualGOs.Clear();
    }
```

- [ ] **Step 8: Verify compilation in Unity**

Switch to Unity Editor. Wait for domain reload. Console must show zero compile errors before continuing.

- [ ] **Step 9: Manual smoke test — constraint mode**

1. Select a GameObject that has a `SkinnedMeshRenderer` in its children and a `PromeonInteractableRigBuilder` component.
2. Ensure `_buildConstraints` is checked and `_boneMaterial` is assigned in the Inspector.
3. Press the **"Rebuild"** button in the Inspector.
4. In the **Scene Hierarchy**, verify:
   - `_BoneProxies` container exists with children named `Proxy_*` (no MeshRenderer component on them).
   - Under each original bone transform there is a `Visual_*` child with a MeshRenderer and Outline component.
   - Under `_Rig` there are `PC_*` constraint GOs.
5. Select a `Proxy_*` GO and move it in the Scene view — the corresponding bone and its `Visual_*` child should follow.

- [ ] **Step 10: Manual smoke test — visual mode**

1. Uncheck `_buildConstraints` on the component.
2. Press **"Rebuild"**.
3. Hierarchy should show `Bone_*` GOs as direct children of each bone (combined mesh + collider).
4. No `_BoneProxies` container, no `PC_*` GOs.

- [ ] **Step 11: Commit**

```
git add Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs
git commit -m "refactor: split constraint-mode bone into Proxy (collider) + Visual (bone child)"
```
