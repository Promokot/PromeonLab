# Outline multi-submesh flat-fill fix (A') — Design

**Goal:** Stop selected multi-submesh meshes (Chair, Toilet) from being flat-filled by the outline fill pass, by eliminating runaway material/submesh accumulation in the QuickOutline patch.

**Scope:** `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` + `Assets/_App/Scripts/VrInteraction/Selectable.cs`. No shader changes. No change to single-submesh behavior (it already works).

---

## Root cause (proven by runtime diagnostic)

A temporary log in `Outline.AppendMaterials` printed, per renderer, `matsBefore` / `subMeshes` / `mode` / `stencilRef` on select:

- Single-submesh objects (cube, every rig bone proxy, Tree): **`mats=1 subMeshes=1`** — stable, outline correct.
- `Chair/chair2` (authored 3 materials / 3 submeshes): **`mats=6 subMeshes=7`** — both inflated and growing.

Two compounding defects, both confined to the multi-submesh path:

1. **Double append.** `Selectable.SetVisualState(Selected)` triggers `AppendMaterials` twice per select — once via `EnsureOutline → SetOutlineMaterials` (line 35→60) and once via `_outline.enabled = true → OnEnable` (line 42). `OnDisable` does a single `List.Remove` per material, so each select/deselect cycle leaves an extra mask+fill pair → material count climbs.

2. **Shared-mesh combine pollution.** `CombineSubmeshes` mutates the **shared imported mesh** (`subMeshCount++` + an all-triangles combined submesh). It re-runs across selects/domain-reloads and permanently inflates the shared asset mesh for the editor session. The `realSubmeshCounts` guard added earlier this session is poisoned because the shared mesh was already inflated before it existed, so it records the inflated count as "real".

Net: after append the renderer has more materials (8) than submeshes (7); the mask/fill no longer map to a single whole-mesh submesh. In the default `OutlineMode.OutlineAll` the fill pass uses `ZTest Always` and its stencil `NotEqual` test passes across the whole surface (mask coverage is broken), so the fill flat-fills the entire mesh in the outline colour.

## Fix (A')

### 1. Idempotent `AppendMaterials`
Strip any already-present outline materials before adding, so append is safe to call any number of times and a missed `OnDisable` can never accumulate:

```csharp
private void AppendMaterials() {
  foreach (var renderer in renderers) {
    var materials = renderer.sharedMaterials.ToList();
    materials.RemoveAll(m => m == outlineMaskMaterial || m == outlineFillMaterial);
    materials.Add(outlineMaskMaterial);
    materials.Add(outlineFillMaterial);
    renderer.materials = materials.ToArray();
  }
  needsUpdate = true;
}
```

### 2. Combine on a per-instance mesh clone, never the shared asset
`CombineSubmeshes` is the only mutation that accumulates (`subMeshCount` grows). Isolate it:

- **Single-submesh meshes are left untouched** — no clone, no combine. The appended mask/fill overflow onto the single submesh, which IS the whole mesh, so coverage is already correct (confirmed: cube/bones/Tree render fine).
- **Multi-submesh meshes are cloned onto the renderer** before any mutation: `meshFilter.mesh = Instantiate(sharedMesh)` (or `skinned.sharedMesh = Instantiate(...)`). Smooth-normal UV3 write and the submesh combine then run on the **clone**, so the shared asset mesh is never modified.
- Track each created clone and `Destroy` it in `OnDestroy` to avoid leaks.
- Remove `realSubmeshCounts` (only existed to guard re-combine of the shared mesh; clones make each instance self-contained). The combine no longer sits under the `registeredMeshes` skip — `registeredMeshes` remains only as the smooth-normal UV3 dedup for *single-submesh shared* meshes.

Because each multi-submesh instance owns its mesh, it combines exactly once in its own `Awake`; there is no cross-instance state and nothing to accumulate.

### 3. Single append path in `Selectable`
Remove the duplicate append at the source: `SetVisualState(Selected)` should set the colour/width/priority and enable the outline through **one** path, not both `EnsureOutline`'s build-and-append and a separate `enabled = true`. With fix #1 this is already non-accumulating, but collapsing to one path keeps the flow legible.

### Diagnostic removal
Delete the temporary `[OutlineDiag]` `Debug.Log` block from `AppendMaterials`.

## One-time cleanup (manual)

The shared Chair/Toilet meshes are already inflated in the current editor session (`subMeshCount` 7 vs 3). Runtime mutation does not persist to the FBX, so a **reimport of the affected FBX files (or an editor restart)** once after the fix restores the shared meshes to pristine — required because the clone is taken from the shared mesh and must start clean.

## Testing

Automated tests can't cover stencil/material render behaviour (Unity lifecycle + GPU). Verification is manual, in play mode / headset:

- Select Chair and Toilet repeatedly (select → deselect → reselect ×several). Expect: a clean rim outline every time, **no flat fill**, and no growth in artifacts across cycles.
- Confirm single-submesh assets (cube, Tree, rig bones) are unchanged.
- Confirm no `"more materials than submeshes"` spam and no `[OutlineDiag]` lines remain.

## Risks

- `Outline.cs` is vendored — record the change in `Assets/_App/ThirdParty/QuickOutline/PROMEON_PATCHES.md` (patch entry) so a package reimport can restore it.
- Mesh clones cost one mesh copy per outlined multi-submesh instance; acceptable (few such instances) and freed on `OnDestroy`.
