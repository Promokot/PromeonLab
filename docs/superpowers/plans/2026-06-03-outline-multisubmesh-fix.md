# Outline multi-submesh flat-fill fix (A') — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop selected multi-submesh meshes (Chair, Toilet) from flat-filling on select by ending runaway material/submesh accumulation in the QuickOutline patch.

**Architecture:** Two root fixes. (1) `AppendMaterials` becomes idempotent via a pure, unit-tested helper, so double-append / missed `OnDisable` can never accumulate outline material slots. (2) Multi-submesh meshes are cloned per-instance before `CombineSubmeshes` runs, so the shared imported asset mesh is never mutated and submesh count cannot inflate across selects. Single-submesh meshes are untouched. The now-redundant `realSubmeshCounts` cross-instance guard is removed.

**Tech Stack:** Unity 6000.3.7f1, C#, vendored QuickOutline (`Assets/_App/ThirdParty/QuickOutline`), Unity Test Runner (EditMode), URP. Compilation + tests run through the Unity Editor / Unity MCP (`refresh_unity`, `read_console`, `run_tests`) — there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-06-03-outline-multisubmesh-fix-design.md`

**Deviation from spec:** Spec change #3 ("single append path in `Selectable`") is intentionally **omitted**. The idempotent `AppendMaterials` (Task 1) fully neutralizes the double-append (every append yields exactly one mask+fill regardless of call count), so editing `Selectable` adds interaction-code risk for no behavioural gain. The user pre-approved this option. `Selectable.cs` is left unchanged.

---

## File Structure

- **Modify** `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` — add pure `WithOutlineMaterials` helper, rewrite `AppendMaterials` (idempotent + drop temp diagnostic), add `CloneMultiSubmeshMeshes` + `ownedMeshes` field + `OnDestroy` cleanup, revert `EnsureMaterialPerSubmesh`/`CombineSubmeshes` to their shared `subMeshCount` forms, delete `realSubmeshCounts`/`RealSubmeshCount`.
- **Create** `Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs` — EditMode test for the pure helper.
- **Modify** `Assets/_App/ThirdParty/QuickOutline/PROMEON_PATCHES.md` — replace the now-superseded patch entries (#5 padding, #6 real-submesh tracking) with the clone-based approach.

> **Why only the pure helper is unit-tested:** `AppendMaterials`, the clone swap, `CombineSubmeshes`, stencil rendering all depend on Unity's `Awake`/`OnEnable` lifecycle and the GPU, which EditMode tests do not drive. The material-list dedup logic is the one piece of pure, bug-prone array logic and is the part that caused the accumulation, so it gets a real test. Everything else is verified manually in play mode (Task 3).

---

## Task 1: Idempotent outline-material append (pure helper + test)

**Files:**
- Modify: `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` (add helper; rewrite `AppendMaterials` at lines 247-263)
- Create: `Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class OutlineMaterialsTests
{
    private static Material NewMat() => new Material(Shader.Find("Universal Render Pipeline/Unlit"));

    [Test]
    public void WithOutlineMaterials_AppendsMaskAndFillOnce()
    {
        var baseMat = NewMat();
        var mask    = NewMat();
        var fill    = NewMat();

        var result = Outline.WithOutlineMaterials(new[] { baseMat }, mask, fill);

        CollectionAssert.AreEqual(new[] { baseMat, mask, fill }, result);

        Object.DestroyImmediate(baseMat);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(fill);
    }

    [Test]
    public void WithOutlineMaterials_IsIdempotent_NoAccumulation()
    {
        var baseMat = NewMat();
        var mask    = NewMat();
        var fill    = NewMat();

        var once  = Outline.WithOutlineMaterials(new[] { baseMat }, mask, fill);
        var twice = Outline.WithOutlineMaterials(once, mask, fill);

        // Re-applying must NOT add a second mask/fill pair (the flat-fill accumulation bug).
        CollectionAssert.AreEqual(new[] { baseMat, mask, fill }, twice);

        Object.DestroyImmediate(baseMat);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(fill);
    }
}
```

- [ ] **Step 2: Force-import the new test file, then run it to verify it fails**

The file is new, so Unity must import it before it compiles. Run (Unity MCP):
- `refresh_unity` with `scope=all`, `mode=force`, `compile=request`, `wait_for_ready=true`
- `read_console` `types=["error"]` `filter_text="CS"` — expect a compile error `'Outline' does not contain a definition for 'WithOutlineMaterials'` (the method does not exist yet). This is the "failing test" state.

- [ ] **Step 3: Add the pure helper to `Outline.cs`**

In `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs`, add this method directly above `private void AppendMaterials()` (currently line 247):

```csharp
  // Idempotent: strips any existing copies of the two outline materials, then appends exactly one of
  // each. Safe to call any number of times — a double-append or a missed OnDisable can never grow the
  // material array (the flat-fill-on-select accumulation bug). Pure + static so it is unit-testable.
  public static Material[] WithOutlineMaterials(Material[] current, Material mask, Material fill) {
    var list = current.ToList();
    list.RemoveAll(m => m == mask || m == fill);
    list.Add(mask);
    list.Add(fill);
    return list.ToArray();
  }
```

(`System.Linq` is already imported at the top of the file, so `ToList`/`RemoveAll` resolve.)

- [ ] **Step 4: Rewrite `AppendMaterials` to use the helper and drop the temp diagnostic**

Replace the entire current `AppendMaterials` body (lines 247-263, the version containing the `[OutlineDiag]` `Debug.Log`) with:

```csharp
  private void AppendMaterials() {
    foreach (var renderer in renderers) {
      renderer.materials = WithOutlineMaterials(renderer.sharedMaterials, outlineMaskMaterial, outlineFillMaterial);
    }
    needsUpdate = true;
  }
```

This removes the `[OutlineDiag]` logging entirely.

- [ ] **Step 5: Compile and run the test to verify it passes**

- `refresh_unity` `scope=scripts` `compile=request` `wait_for_ready=true`
- `read_console` `types=["error"]` `filter_text="CS"` — expect 0 real `CS####` errors.
- `run_tests` with `test_mode=EditMode`, `test_filter=OutlineMaterialsTests` → poll `get_test_job` until done. Expect both tests PASS.

- [ ] **Step 6: Commit**

```bash
git add "Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs" "Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs"
git commit -m "Make outline material append idempotent + drop diagnostic"
```

---

## Task 2: Per-instance mesh clone for multi-submesh meshes

**Files:**
- Modify: `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` (delete `realSubmeshCounts`/`RealSubmeshCount`; add `ownedMeshes` field + `CloneMultiSubmeshMeshes`; edit `Awake`, `OnDestroy`, `EnsureMaterialPerSubmesh`, `CombineSubmeshes`)

> No automated test (Unity asset/lifecycle). Verified manually in Task 3.

- [ ] **Step 1: Delete the `realSubmeshCounts` dictionary and `RealSubmeshCount` helper**

Remove this block (currently lines 19-32, just under `private static HashSet<Mesh> registeredMeshes = ...`):

```csharp
  // Original (pre-combine) submesh count per shared mesh. CombineSubmeshes mutates the SHARED mesh
  // once per mesh (guarded by registeredMeshes), so a SECOND Outline instance on the same mesh would
  // otherwise read the inflated subMeshCount and EnsureMaterialPerSubmesh would pad a real material
  // onto the appended combined submesh — overdrawing the whole object with the last material.
  // Recording the real count the first time we touch a mesh keeps padding bounded to actual submeshes.
  private static readonly Dictionary<Mesh, int> realSubmeshCounts = new Dictionary<Mesh, int>();

  private static int RealSubmeshCount(Mesh mesh) {
    if (!realSubmeshCounts.TryGetValue(mesh, out int count)) {
      count = mesh.subMeshCount; // first sighting precedes this component's CombineSubmeshes
      realSubmeshCounts[mesh] = count;
    }
    return count;
  }
```

So that the line `private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();` is immediately followed by the `public enum Mode {` block.

- [ ] **Step 2: Add the `ownedMeshes` field**

In `Outline.cs`, directly after the line `private Material outlineFillMaterial;` (currently line 130), add:

```csharp

  // Per-instance mesh clones for multi-submesh renderers. CombineSubmeshes grows subMeshCount, so it
  // must run on a clone, never on the shared imported asset (else repeated selects inflate the shared
  // mesh for the whole editor session). Freed in OnDestroy.
  private readonly List<Mesh> ownedMeshes = new List<Mesh>();
```

(`System.Collections.Generic` is already imported.)

- [ ] **Step 3: Call the clone step first in `Awake`**

Replace the current `Awake` body (lines 134-149) with:

```csharp
  void Awake() {

    // Cache renderers
    renderers = GetComponentsInChildren<Renderer>();

    // Multi-submesh meshes get a per-instance clone FIRST, so the submesh combine below never mutates
    // the shared imported asset (repeated selects would otherwise inflate it for the whole session).
    CloneMultiSubmeshMeshes();

    // Ensure each renderer has at least one material per submesh, so QuickOutline's CombineSubmeshes
    // runs and the appended mask/fill cover the WHOLE mesh (not just the last submesh) on assets
    // whose mesh has more submeshes than materials.
    EnsureMaterialPerSubmesh();

    // Retrieve or generate smooth normals
    LoadSmoothNormals();

    // Apply material properties immediately
    needsUpdate = true;
  }

  // Swap each multi-submesh renderer's mesh for a private clone. Single-submesh meshes are left shared
  // (they are never combined — the appended mask/fill overflow already covers their one submesh).
  void CloneMultiSubmeshMeshes() {
    foreach (var renderer in renderers) {
      if (renderer == null) continue;

      if (renderer is SkinnedMeshRenderer smr) {
        var shared = smr.sharedMesh;
        if (shared == null || shared.subMeshCount <= 1) continue;
        var clone = Instantiate(shared);
        clone.name = shared.name + " (OutlineClone)";
        smr.sharedMesh = clone;
        ownedMeshes.Add(clone);
      } else {
        var mf = renderer.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount <= 1) continue;
        var clone = Instantiate(mf.sharedMesh);
        clone.name = mf.sharedMesh.name + " (OutlineClone)";
        mf.sharedMesh = clone;
        ownedMeshes.Add(clone);
      }
    }
  }
```

- [ ] **Step 4: Revert `EnsureMaterialPerSubmesh` to the shared `subMeshCount` form**

Replace the current `EnsureMaterialPerSubmesh` (lines 151-176) with:

```csharp
  void EnsureMaterialPerSubmesh() {
    foreach (var renderer in renderers) {
      if (renderer == null) continue;

      Mesh mesh = null;
      if (renderer is SkinnedMeshRenderer smr) {
        mesh = smr.sharedMesh;
      } else {
        var meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null) mesh = meshFilter.sharedMesh;
      }
      if (mesh == null) continue;

      var materials = renderer.sharedMaterials;
      if (materials.Length >= mesh.subMeshCount) continue; // already enough — no-op

      var padded = new Material[mesh.subMeshCount];
      for (int i = 0; i < padded.Length; i++) {
        padded[i] = materials[Mathf.Min(i, materials.Length - 1)];
      }
      renderer.sharedMaterials = padded;
    }
  }
```

This is safe with `mesh.subMeshCount` again because, for multi-submesh renderers, `mesh` is now the **pristine clone** (cloned in Step 3, not yet combined — `LoadSmoothNormals` combines it afterward).

- [ ] **Step 5: Revert `CombineSubmeshes` to the original shared form**

Replace the current `CombineSubmeshes` (lines 390-410, the `RealSubmeshCount`-based version) with:

```csharp
  void CombineSubmeshes(Mesh mesh, Material[] materials) {

    // Skip meshes with a single submesh
    if (mesh.subMeshCount == 1) {
      return;
    }

    // Skip if submesh count exceeds material count
    if (mesh.subMeshCount > materials.Length) {
      return;
    }

    // Append combined submesh
    mesh.subMeshCount++;
    mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
  }
```

This is safe to use `mesh.subMeshCount` directly: `LoadSmoothNormals` calls it once per Awake, and for a multi-submesh renderer `mesh` is the per-instance clone (combined exactly once, never re-seen). Single-submesh meshes return at the first guard.

- [ ] **Step 6: Free mesh clones in `OnDestroy`**

Replace the current `OnDestroy` (lines 222-227) with:

```csharp
  void OnDestroy() {

    // Destroy material instances
    if (outlineMaskMaterial != null) Destroy(outlineMaskMaterial);
    if (outlineFillMaterial != null) Destroy(outlineFillMaterial);

    // Free per-instance mesh clones.
    for (int i = 0; i < ownedMeshes.Count; i++) {
      if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
    }
    ownedMeshes.Clear();
  }
```

- [ ] **Step 7: Compile and confirm no errors**

- `refresh_unity` `scope=scripts` `compile=request` `wait_for_ready=true`
- `read_console` `types=["error"]` `filter_text="CS"` — expect 0 real `CS####` errors (the `realSubmeshCounts`/`RealSubmeshCount` references are all gone; confirm no "does not exist in the current context").
- `run_tests` `test_mode=EditMode` `test_filter=OutlineMaterialsTests` → still PASS.

- [ ] **Step 8: Commit**

```bash
git add "Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs"
git commit -m "Clone multi-submesh meshes per outline instance; never mutate shared mesh"
```

---

## Task 3: Docs, one-time cleanup, manual verification

**Files:**
- Modify: `Assets/_App/ThirdParty/QuickOutline/PROMEON_PATCHES.md`

- [ ] **Step 1: Update the vendored-patch log**

In `Assets/_App/ThirdParty/QuickOutline/PROMEON_PATCHES.md`, append a new section documenting the final approach (so a package reimport can restore it). Add at the end of the file:

```markdown

## 7. Per-instance clone + idempotent append — fixes flat-fill on multi-submesh (2026-06-03)
Supersedes the accumulation-prone parts of #5/#6. Two changes:
- `AppendMaterials` is now idempotent via the pure static `Outline.WithOutlineMaterials(current, mask, fill)`
  (strips any existing mask/fill before re-appending). A double-append or missed `OnDisable` can no
  longer grow the material array. Unit-tested in `Assets/_App/Tests/VrInteraction/OutlineMaterialsTests.cs`.
- `CloneMultiSubmeshMeshes()` (called first in `Awake`) swaps each multi-submesh renderer's mesh for a
  per-instance `Instantiate` clone, so `CombineSubmeshes` never mutates the shared imported asset. Clones
  are tracked in `ownedMeshes` and destroyed in `OnDestroy`. Single-submesh meshes are left shared.
  The static `realSubmeshCounts` guard from #6 is removed (clones make each instance self-contained).
Net: multi-submesh meshes (Chair, Toilet) no longer flat-fill on select, and material/submesh counts
stay constant across repeated selects.
```

- [ ] **Step 2: One-time shared-mesh cleanup**

The shared Chair/Toilet meshes may still be inflated in the running editor from the old bug. Restore them to pristine: in Unity, reimport the affected FBX files (Project window → select `Assets/_App/ThirdParty/HouseInteriorPack/Models/(Msh)Chair2.fbx` and `(Msh)Toilet.fbx` → right-click → Reimport), **or** restart the Unity Editor. Either resets `subMeshCount` to the asset's authored value, so the clone is taken from a clean mesh.

Run `refresh_unity` `scope=assets` `mode=force` `wait_for_ready=true` afterward.

- [ ] **Step 3: Manual verification in play mode**

Enter Play Mode and verify:
1. Spawn **Chair** and **Toilet**. Select → deselect → reselect each several times. Expect: a clean rim outline every time, **no flat fill / blackening**, and no growth of artifacts across cycles.
2. Spawn a **cube** and a **rig (Dummy Rig)**, select them. Expect: outline unchanged from before (single-submesh path untouched).
3. Open the console: expect **no** `[OutlineDiag]` lines (diagnostic removed) and no `"more materials than submeshes"` spam on the Chair/Toilet.

If any check fails, STOP and return to the spec/root-cause — do not patch further blindly.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_App/ThirdParty/QuickOutline/PROMEON_PATCHES.md"
git commit -m "Document outline clone + idempotent-append patch"
```

---

## Self-Review (completed during authoring)

- **Spec coverage:** Spec fix #1 → Task 1. Spec fix #2 (clone, single-submesh untouched, remove `realSubmeshCounts`, `OnDestroy` cleanup) → Task 2. Spec fix #3 → intentionally omitted (documented above, user pre-approved). One-time reimport → Task 3 Step 2. Diagnostic removal → Task 1 Step 4. PROMEON_PATCHES update → Task 3 Step 1. Manual testing → Task 3 Step 3.
- **Placeholder scan:** none — every code step shows full code.
- **Type consistency:** `WithOutlineMaterials(Material[], Material, Material) → Material[]` used identically in test, helper, and `AppendMaterials`. `ownedMeshes` (`List<Mesh>`) defined in Task 2 Step 2, populated in Step 3, freed in Step 6. `CloneMultiSubmeshMeshes()` defined and called in Step 3.
