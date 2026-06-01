# Rig Leaf-Bone Orientation Axis — Design

**Date:** 2026-06-01
**Status:** Approved (design); spec under review.

## Problem

Terminal (leaf) bones of a proxy rig — those with no child bone in the selected set —
currently orient their diamond mesh along *direction-from-parent*
(`bone.position − bone.parent.position`, in the bone's local space). For many skeletons
this looks bad (a finger tip or head bone points off at an arbitrary angle inherited from
the parent). The user wants to choose, **per rig**, which local axis a terminal bone's
visual points along, via a toggle group (X / Y / Z) already added to the import wizard prefab.

Non-leaf bones (those with children) are unaffected — they already build toward the next bone.

## Decisions (locked during brainstorming)

- **Granularity:** one axis **per rig** (applies to ALL of that rig's terminal bones). Not per-bone.
- **Direction/sign:** **always positive** local axis (`+X` / `+Y` / `+Z`). No sign toggle.
- **Axis space:** the bone's **local** space (the diamond rotates with the bone, DCC-style).
- **Source of the axis (per-rig everywhere):**
  - Imported rigs → stored in the recipe (`RigDefinition.TerminalAxis`), chosen in the import wizard.
  - Builtin rigs → stored where builtin assets are configured (`BuiltinLabAsset` in the `BuiltinAssetLibrary` SO).
- **`Auto` value** preserves today's direction-from-parent behavior and doubles as the
  "unset" default for backward compatibility (old recipes, manual rigs, builtin entries not set).

## Data model

New enum (own file, `Assets/_App/Scripts/RigBuilder/TerminalBoneAxis.cs`):

```csharp
public enum TerminalBoneAxis { Auto = 0, X = 1, Y = 2, Z = 3 }
```

`Auto = 0` is deliberate: JsonUtility deserializes a missing field to 0, and a default
struct/inspector value is 0 — so anything without an explicit choice keeps the current look.

- `RigDefinition` += `public TerminalBoneAxis TerminalAxis;` — per-import value, travels in the recipe.
  Additive field, sane default (`Auto`) → **no `StorageMigrator` migration needed**;
  `SchemaVersion` unchanged.
- `BuiltinLabAsset` += `[SerializeField] private TerminalBoneAxis _terminalAxis;` with
  `public TerminalBoneAxis TerminalAxis => _terminalAxis;` — configured per entry in the
  `BuiltinAssetLibrary` SO. Meaningful only for Rig-type entries; harmless (ignored) on others.
  Crush Dummy's entry left at `Auto` keeps its current look until deliberately changed.

## Data flow

**Import (wizard → recipe):**
1. `ImportWizardSurface` reads the selected toggle of the "Axis Toggle Group" (`Toggle X/Y/Z`)
   and publishes it on `ImportConfirmedEvent` (new field `TerminalBoneAxis TerminalAxis`).
   The axis group **stays always visible** (it sits under the rig-selection section in the
   prefab; kept visible for simplicity). It only affects Rig imports anyway — for Object/Reference
   the published axis is ignored downstream (no `recipe.rig` to stamp). Default selection when no
   prior choice: `X` (first toggle).
2. `ImportPipeline.RunImportAsync`, after `_builders.BuildAsync(...)` returns the recipe,
   stamps the choice: `if (recipe.rig != null) recipe.rig.TerminalAxis = e.TerminalAxis;`
   The shared `IAssetEntityBuilder.BuildAsync` signature is **unchanged** — the pipeline owns
   the wizard→recipe mapping.

**Construction (restore → proxies):**
3. `RigEntityFactory.BuildProxyRig` gains a third parameter:
   `BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis)`,
   threaded into `BuildProxyNode`.
4. The leaf branch (`children.Count == 0`) resolves the long axis:
   - `Auto` → current behavior (`localChildDir` from parent).
   - `X` → `Vector3.right`, `Y` → `Vector3.up`, `Z` → `Vector3.forward`.
   Length stays `parentLen * 0.5`; width unchanged. Non-leaf branch untouched.
5. Callers resolve and pass the axis:
   - `RigEntityBuilder.RestoreAsync`: imported → `recipe.rig.TerminalAxis`;
     builtin → `((BuiltinLabAsset)asset).TerminalAxis`.
   - `RigRuntime.ApplyDefinition`: `definition.TerminalAxis` (manual in-VR rigging →
     `Auto` by default, since the extractor leaves it 0).

## Components touched

| File | Change |
|---|---|
| `RigBuilder/TerminalBoneAxis.cs` | **new** enum |
| `RigBuilder/RigDefinition.cs` | += `TerminalAxis` field |
| `AssetBrowser/BuiltinLabAsset.cs` | += serialized `_terminalAxis` + getter |
| `AssetBrowser/Events/ImportConfirmedEvent.cs` | += `TerminalAxis` field |
| `SpatialUi/Behaviors/ImportWizardSurface.cs` | serialize axis toggles; read selected → event (group stays always visible) |
| `AssetBrowser/ImportPipeline.cs` | stamp `recipe.rig.TerminalAxis` from event |
| `AssetBrowser/RigEntityFactory.cs` | `BuildProxyRig`/`BuildProxyNode` += axis param; leaf-branch switch |
| `AssetBrowser/RigEntityBuilder.cs` | `RestoreAsync` resolves axis (recipe vs builtin) → passes it |
| `RigBuilder/RigRuntime.cs` | `ApplyDefinition` passes `definition.TerminalAxis` |
| Crush Dummy entry in `BuiltinAssetLibrary` SO | set axis (optional; `Auto` keeps current) |

## Error handling / edge cases

- No skeleton → `BuildProxyRig` already no-ops (axis irrelevant).
- Old imported recipes (no field) → `Auto` → current look. No migration.
- Non-Rig builtin entries carry an ignored `_terminalAxis`.
- Wizard with no axis toggle selected (shouldn't happen with a ToggleGroup) → fall back to `X`.

## Testing

- `RigEntityFactoryBuildProxyTests`: update existing calls to the new 3-arg `BuildProxyRig`
  (pass `TerminalBoneAxis.Auto` to preserve current assertions). Add a test: a leaf bone built
  with `TerminalBoneAxis.X` orients its diamond long-axis along the bone's local +X (and `Auto`
  reproduces the from-parent direction).
- Optional: a small `ImportPipeline`/wizard test that the chosen axis lands on `recipe.rig`.

## Out of scope (future)

- The user's note: builtin assets should eventually be **processed by type from the SO** (feed a
  bare model / skinned model / image and let the type-keyed builder process it, like imports), and
  **rudimentary `AssetType` values should be cleaned up**. This is a separate slice — not this spec.
- Per-bone axis overrides; sign/negative-axis selection; editor bake tool (Slice C).
