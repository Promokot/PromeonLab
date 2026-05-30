# Outline: Layered Masks + Bone-Toggle Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement
> this plan (shader/material + vendored-patch work with heavy in-VR visual verification — inline
> execution with checkpoints fits better than per-task subagents). Steps use checkbox (`- [ ]`)
> syntax for tracking.
>
> **Supersedes** the stencil portion of `docs/superpowers/plans/2026-05-30-outline-see-through.md`.
> That plan proposed a *per-category* / pooled stencil ref and left the URP stencil budget as an
> open SPIKE. This plan resolves the SPIKE (stencil is fully free — see Architecture), uses
> *unique-per-instance* stencil refs (fixes ALL clipping, including bone-vs-bone), and adds an
> explicit **layered priority** (render-queue) axis for the "послойность" the user asked for
> (selection < bones < gizmo).
>
> **Root-cause references:**
> - `docs/superpowers/investigations/2026-05-30-outline-see-through.md` (Bug 2 + Bug 2.1)
> - `docs/developer-notes/2026-05-21-bone-outline-needs-click.md` (original symptom log)
>
> **Git note:** user (Promokot) commits manually — no auto-commit. "Checkpoint" = stop, user commits.
> **Unity note:** the controller compiles/inspects via MCP. `[MANUAL EDITOR / MCP]` steps (creating
> material assets, editing the gizmo prefab, in-VR verification) are done by the user or via MCP.
> **Vendored-patch caveat:** the only vendored edit here is `Outline.cs`
> (`Assets/_App/ThirdParty/QuickOutline/Scripts/`). Reimport of QuickOutline overwrites it — every
> edit MUST be recorded in the QuickOutline patch note (Task 6) so it can be re-applied. The forked
> shaders and materials live under `Assets/_App/Content/` and survive reimport.

**Goal:** (A) Bone see-through outlines appear the same frame "Show Bones" is toggled — no click
needed. (B) Overlapping outlines stop clipping each other (per-instance stencil ref). (C) A defined
visual priority so bones render over the selection outline and the gizmo renders over everything.

**Architecture:** Three independent levers, each a separate phase:
1. **Bone toggle (app-side, no shaders):** re-assert `OutlineMode` after enabling the `Outline`
   component so `needsUpdate` is set and `UpdateMaterialProperties` runs immediately. `Outline.OnEnable`
   never sets `needsUpdate`; only `Awake` (runs once) and the property setters do — that is why a
   re-enabled outline waits for a click today.
2. **Anti-clip (forked shaders + Outline.cs):** QuickOutline hardcodes `Stencil Ref 1` in both
   shaders, so every outline shares one stencil slot and masks reject each other's fills where
   silhouettes overlap. Promote `Ref` to a `_StencilRef` shader property and assign a **unique value
   per `Outline` instance**. The active URP renderers (`Assets/Settings/Mobile_Renderer.asset`,
   `PC_Renderer.asset`) have `m_RendererFeatures: []` and `m_DefaultStencilState.overrideStencilState:
   0` — URP does not touch the stencil buffer, so the whole 1..250 range is free.
   **Material source:** instead of QuickOutline's `Resources.Load`, the forked materials are held by an
   `OutlineConfig` ScriptableObject (direct references — no Resources folder, no `Shader.Find`,
   no string lookups). `Outline` is fed the SO via a public `OutlineConfig` setter. Because `Outline`
   is added at runtime via `AddComponent` in two places (`Selectable.EnsureOutline:34`,
   `PromeonProxyRigBuilder:390`) and `AddComponent` runs `Awake`/`OnEnable` synchronously *before* the
   caller can inject the SO, material creation MUST move out of `Awake` into a **lazy build** that
   runs once the SO is present (on `OnEnable` for prefab-assigned SO, or when the setter assigns it
   on a runtime-added component). The gizmo's prefab `Outline` carries the SO reference serialized.
3. **Layered priority (render queue):** stencil stops mutual clipping but does not define who paints
   on top. A per-instance `RenderPriority` offsets the mask/fill `renderQueue` so a higher-priority
   outline draws later (on top). Selection = 0, bones = 1, gizmo = 2. Combined with each category's
   existing ZTest (gizmo/selection `OutlineAll` = ZTest Always; bones `SilhouetteOnly`), this yields
   "bones over selection, gizmo over everything."

**Tech Stack:** Unity 6000.3.7f1, URP 17.3.0 (Forward), ShaderLab stencil, QuickOutline (vendored),
OpenXR single-pass-instanced.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs` | Bone proxy lifecycle / outline enable | Modify: re-assert mode on enable; set bone RenderPriority; later remove hacks |
| `Assets/_App/Content/Shaders/PromeonOutlineMask.shader` | Forked mask shader with `_StencilRef` | Create |
| `Assets/_App/Content/Shaders/PromeonOutlineFill.shader` | Forked fill shader with `_StencilRef` | Create |
| `Assets/_App/Content/Materials/Outline/OutlineMask.mat` | Material using forked mask shader | Create (MCP) |
| `Assets/_App/Content/Materials/Outline/OutlineFill.mat` | Material using forked fill shader | Create (MCP) |
| `Assets/_App/Scripts/VrInteraction/Data/OutlineConfig.cs` | SO holding the two outline material refs | Create |
| `Assets/_App/Content/ScriptableObjects/DefaultOutlineConfig.asset` | SO asset wired to the two materials | Create (MCP) |
| `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` | Lazy material build from SO + per-instance stencil ref + RenderPriority | Modify (vendored — document) |
| `Assets/_App/Scripts/VrInteraction/Selectable.cs` | Selection outline | Modify: inject SO + RenderPriority = 0 |
| `Assets/_App/Content/Prefabs/...Vr3D_Gizmos.prefab` | Gizmo handle Outline components | Modify (editor): serialized RenderPriority = 2 |
| `docs/developer-notes/2026-05-21-bone-outline-needs-click.md` | Bug log | Update: mark resolved |
| QuickOutline patch note | Vendored-edit ledger | Update: record Outline.cs + shader fork |

---

## Phase 1 — Bones appear on toggle without a click (app-side, shippable alone)

This phase is independent of the shader work and fixes the user-visible "needs a click" bug on its
own. It does NOT yet delete the existing hacks — that happens in Phase 5 after VR verification.

### Task 1.1: Re-assert OutlineMode when enabling bone outlines

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs:124-125`

- [ ] **Step 1: Add the mode re-assert right after the outline is enabled.**

Current (`:124-127`):
```csharp
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
            var col     = go.GetComponent<Collider>();
            if (col     != null) col.enabled     = enabled;
```
Change to:
```csharp
            var outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = enabled;
                // Re-enabling an Outline does NOT re-run Awake, and OnEnable never sets needsUpdate,
                // so the SilhouetteOnly ZTest stays stale until something pokes a property setter
                // (today: the first click). Re-asserting the mode sets needsUpdate=true → Update runs
                // UpdateMaterialProperties this frame → the see-through rim appears immediately.
                if (enabled) outline.OutlineMode = Outline.Mode.SilhouetteOnly;
            }
            var col     = go.GetComponent<Collider>();
            if (col     != null) col.enabled     = enabled;
```

- [ ] **Step 2 (controller): compile.** Run `read_console`; expect no `CS` errors.

- [ ] **Step 3: Checkpoint (user commits)** — `fix(rig): bone see-through outline appears on Show Bones without a click`

### Task 1.2: Verify in VR (user)

- [ ] **Step 1 [MANUAL / VR]:** Enter bone-editing mode, press **Show Bones**. The see-through bone
  outline must appear the SAME frame the bone meshes appear — no click. Toggle off/on repeatedly; it
  must appear every time.
- [ ] **Step 2:** Note the result here:
  - If outlines now appear reliably → the `BumpOutlineNextFrame` + self-assign hacks are redundant
    (removed in Phase 5).
  - If outlines STILL need a nudge → the deferral is render-pipeline (not just `needsUpdate`); keep
    the hacks and flag for follow-up. (Phase 5 becomes conditional.)

---

## Phase 2 — Fork the two outline shaders with a `_StencilRef` property

### Task 2.1: Create the forked mask shader

**Files:**
- Create: `Assets/_App/Content/Shaders/PromeonOutlineMask.shader`

- [ ] **Step 1: Write the shader** (identical to `QuickOutline/Resources/Shaders/OutlineMask.shader`
  except the new name and the parameterised stencil ref):

```shaderlab
Shader "PromeonLab/OutlineMask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _StencilRef("Stencil Ref", Float) = 1
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+100"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "Mask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref [_StencilRef]
        Pass Replace
      }
    }
  }
}
```

### Task 2.2: Create the forked fill shader

**Files:**
- Create: `Assets/_App/Content/Shaders/PromeonOutlineFill.shader`

- [ ] **Step 1: Write the shader** (identical to `OutlineFill.shader` except name + `_StencilRef`):

```shaderlab
Shader "PromeonLab/OutlineFill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0

    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
    _StencilRef("Stencil Ref", Float) = 1
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref [_StencilRef]
        Comp NotEqual
      }

      CGPROGRAM
      #include "UnityCG.cginc"

      #pragma vertex vert
      #pragma fragment frag

      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 position : SV_POSITION;
        fixed4 color : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform fixed4 _OutlineColor;
      uniform float _OutlineWidth;

      v2f vert(appdata input) {
        v2f output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normal;
        float3 viewPosition = UnityObjectToViewPos(input.vertex);
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, normal));

        output.position = UnityViewToClipPos(viewPosition + viewNormal * -viewPosition.z * _OutlineWidth / 1000.0);
        output.color = _OutlineColor;

        return output;
      }

      fixed4 frag(v2f input) : SV_Target {
        return input.color;
      }
      ENDCG
    }
  }
}
```

- [ ] **Step 2 (controller): refresh assets** (`refresh_unity`); confirm both forked shaders compile
  with no shader errors in `read_console`.

- [ ] **Step 3: Checkpoint (user commits)** — `feat(outline): fork QuickOutline shaders with _StencilRef property`

---

## Phase 3 — OutlineConfig SO + forked materials + Outline.cs lazy build (stencil ref + priority)

No Resources folder, no `Shader.Find`. The two forked materials are referenced directly by an
`OutlineConfig` ScriptableObject; `Outline` is fed that SO and builds its material instances lazily.

### Task 3.1: Create the OutlineConfig ScriptableObject class

**Files:**
- Create: `Assets/_App/Scripts/VrInteraction/Data/OutlineConfig.cs`

- [ ] **Step 1: Write the SO class** (one public type per file; SO suffix `Config` per conventions):

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "OutlineConfig", menuName = "PromeonLab/Outline Config")]
public class OutlineConfig : ScriptableObject
{
    [SerializeField] private Material maskMaterial;
    [SerializeField] private Material fillMaterial;

    public Material MaskMaterial => maskMaterial;
    public Material FillMaterial => fillMaterial;
}
```

- [ ] **Step 2 (controller): compile.** `read_console` → no `CS` errors (the `[CreateAssetMenu]` and
  the type must exist before the SO asset can be created in Task 3.2).

### Task 3.2: Create forked materials + the SO asset (controller, via MCP)

**Files:**
- Create: `Assets/_App/Content/Materials/Outline/OutlineMask.mat` (shader `PromeonLab/OutlineMask`)
- Create: `Assets/_App/Content/Materials/Outline/OutlineFill.mat` (shader `PromeonLab/OutlineFill`)
- Create: `Assets/_App/Content/ScriptableObjects/DefaultOutlineConfig.asset`

- [ ] **Step 1 [MCP]:** Create both materials via `manage_material`, assigning shader
  `PromeonLab/OutlineMask` and `PromeonLab/OutlineFill` respectively. Leave property values at
  defaults — `_StencilRef`/`_ZTest`/`renderQueue` are driven at runtime by `Outline.cs`.
- [ ] **Step 2 [MCP]:** Create `DefaultOutlineConfig.asset` (type `OutlineConfig`) and wire
  `maskMaterial` → `OutlineMask.mat`, `fillMaterial` → `OutlineFill.mat`.
- [ ] **Step 3:** Confirm via Glob that all three assets exist at the paths above.

### Task 3.3: Refactor Outline.cs — lazy material build from SO + stencil ref + RenderPriority

**Files:**
- Modify: `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs`

- [ ] **Step 1: Add the SO field + setter and the `RenderPriority` property** (next to the other
  public properties, after `OutlineWidth`, `:49`):

```csharp
  public OutlineConfig OutlineConfig {
    get { return outlineConfig; }
    set {
      outlineConfig = value;
      // Runtime-added components miss the OnEnable build (SO was null then); build now if possible.
      if (isActiveAndEnabled && outlineMaskMaterial == null && TryBuildMaterials())
        AppendMaterials();
    }
  }

  public int RenderPriority {
    get { return renderPriority; }
    set {
      renderPriority = value;
      needsUpdate = true;
    }
  }
```

- [ ] **Step 2: Add the serialized fields + per-instance stencil state** (in the fields block near
  `:77-81`, alongside `outlineMaskMaterial`/`outlineFillMaterial`):

```csharp
  [SerializeField]
  private OutlineConfig outlineConfig;

  [SerializeField]
  private int renderPriority;

  // Per-instance stencil ref so overlapping outlines never clip each other's fill.
  // The active URP renderers (Assets/Settings/Mobile_Renderer.asset, PC_Renderer.asset) declare
  // no renderer features and overrideStencilState=0, so the whole 1..250 range is free.
  private const int STENCIL_MIN = 1;
  private const int STENCIL_MAX = 250;
  private const int QUEUE_STEP  = 20; // renderQueue gap between priority levels (mask+fill fit in one step)
  private static int nextStencilRef = STENCIL_MIN;
  private int stencilRef;
```

- [ ] **Step 3: Remove material instantiation from `Awake`.** `Awake` keeps only renderer caching,
  smooth-normal load, and `needsUpdate = true`. Delete the two `Resources.Load`/`Instantiate` lines
  and the two `.name =` lines (`:88-93`) — material creation moves to the lazy builder.

- [ ] **Step 4: Add the lazy builder + append helper** (new private methods):

```csharp
  private bool TryBuildMaterials() {
    if (outlineMaskMaterial != null) return true;            // already built
    if (outlineConfig == null ||
        outlineConfig.MaskMaterial == null ||
        outlineConfig.FillMaterial == null) return false;    // no SO yet

    outlineMaskMaterial = Instantiate(outlineConfig.MaskMaterial);
    outlineFillMaterial = Instantiate(outlineConfig.FillMaterial);

    outlineMaskMaterial.name = "OutlineMask (Instance)";
    outlineFillMaterial.name = "OutlineFill (Instance)";

    // Unique stencil ref per instance (cycles within the free range)
    stencilRef = nextStencilRef;
    nextStencilRef++;
    if (nextStencilRef > STENCIL_MAX) nextStencilRef = STENCIL_MIN;
    return true;
  }

  private void AppendMaterials() {
    foreach (var renderer in renderers) {
      var materials = renderer.sharedMaterials.ToList();
      materials.Add(outlineMaskMaterial);
      materials.Add(outlineFillMaterial);
      renderer.materials = materials.ToArray();
    }
    needsUpdate = true;
  }
```

> Keep the instance NAMES exactly ("OutlineMask (Instance)" / "OutlineFill (Instance)") —
> `PromeonProxyRigBuilder.SetBonesInteractive` strips leftover materials by that name prefix
> (`:117-121`); renaming breaks that cleanup.

- [ ] **Step 5: Rewrite `OnEnable` to build-then-append** (`:102-113`):

```csharp
  void OnEnable() {
    if (!TryBuildMaterials()) return; // SO not assigned yet (runtime-added); setter will append later
    AppendMaterials();
  }
```

- [ ] **Step 6: Guard `Update`, `OnDisable`, `OnDestroy` against un-built materials.**
  - `Update` (`:132-138`): wrap with `if (needsUpdate && outlineMaskMaterial != null)`.
  - `OnDisable` (`:140-151`): early-return `if (outlineMaskMaterial == null) return;` before the
    remove loop.
  - `OnDestroy` (`:153-158`): `if (outlineMaskMaterial != null) Destroy(outlineMaskMaterial);` and the
    same null-guard for the fill material.

- [ ] **Step 7: Apply the stencil ref + priority queue at the end of `UpdateMaterialProperties`**
  (after the `switch`, before the closing brace, `:337`):

```csharp
    // Per-instance stencil ref: same value on mask (Replace) and fill (NotEqual) so the fill rim
    // tests against THIS instance's silhouette only — overlapping outlines no longer clip each other.
    outlineMaskMaterial.SetFloat("_StencilRef", stencilRef);
    outlineFillMaterial.SetFloat("_StencilRef", stencilRef);

    // Layered priority: higher RenderPriority paints later (on top). Selection=0, bones=1, gizmo=2.
    outlineMaskMaterial.renderQueue = 3100 + renderPriority * QUEUE_STEP;
    outlineFillMaterial.renderQueue = 3110 + renderPriority * QUEUE_STEP;
```

- [ ] **Step 8 (controller): compile.** `read_console` → no `CS` errors. `Outline.OutlineConfig` and
  `Outline.RenderPriority` are now usable by callers.

- [ ] **Step 9: Checkpoint (user commits)** — `feat(outline): SO-fed lazy materials + per-instance stencil ref + RenderPriority`

### Task 3.4: Inject the SO at every Outline creation site

`Outline` is added at runtime in two `_App` places; each must hand it the SO. The gizmo prefab gets
the SO serialized (Task 4.3).

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Selectable.cs`
- Modify: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs:390` (bone Outline creation)

- [ ] **Step 1 [INVESTIGATE]:** Confirm how each creator can obtain the SO. `PromeonProxyRigBuilder`
  already carries serialized bone-outline color fields (`_boneOutlineColorSelected` etc.) → add a
  serialized `[SerializeField] private OutlineConfig _outlineConfig;` and assign it on the rig
  prefab/bake. `Selectable` is itself `AddComponent`'d at runtime (`PromeonProxyRigBuilder:305` and the
  asset-spawn path) → it cannot be inspector-wired, so its creator must pass the SO. Identify the
  asset-spawn factory that adds `Selectable` and give `Selectable` a `public OutlineConfig OutlineConfig`
  field the creator sets (or inject via the existing DI used in that factory). Record the chosen
  wiring here before editing.
- [ ] **Step 2: `PromeonProxyRigBuilder` — assign SO to the bone Outline** (at `:390`, right after
  `var outline = go.AddComponent<Outline>();`):

```csharp
        var outline          = go.AddComponent<Outline>();
        outline.OutlineConfig = _outlineConfig;
```

- [ ] **Step 3: `Selectable` — assign SO before the Outline renders** (in `EnsureOutline`, `:32-35`):

```csharp
    private void EnsureOutline()
    {
        if (_outline == null)
        {
            _outline = gameObject.AddComponent<Outline>();
            _outline.OutlineConfig = _outlineConfig; // see Step 1 for how _outlineConfig is supplied
        }
    }
```

- [ ] **Step 4 (controller): compile.** `read_console` → no `CS` errors.
- [ ] **Step 5 [MANUAL EDITOR / MCP]:** Assign `DefaultOutlineConfig.asset` to the new
  `_outlineConfig` serialized slot on the rig builder (prefab) and anywhere `Selectable`'s SO is
  inspector-wired.
- [ ] **Step 6: Checkpoint (user commits)** — `feat(outline): wire OutlineConfig SO into all Outline creation sites`

### Task 3.5: Verify anti-clip in VR (user)

- [ ] **Step 1 [MANUAL / VR]:** Select object A (gets outline), then select/show a second outlined
  object B that overlaps A on screen. B's outline must render fully through/over A's silhouette — no
  clipped rim in the overlap region. Move the head so the overlap changes; the rim stays complete.
- [ ] **Step 2 [MANUAL / VR]:** Single-object selection still renders a normal outline (no regression).

---

## Phase 4 — Layered priority wiring (selection / bones / gizmo)

`RenderPriority` defaults to 0, so selection already sits at the base. This phase raises bones and the
gizmo above it.

### Task 4.1: Bones render above the selection outline

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs` (the enable block edited in Phase 1)

- [ ] **Step 1: Set bone priority where the mode is re-asserted** (the `if (enabled)` block from
  Task 1.1):

```csharp
                if (enabled)
                {
                    outline.OutlineMode   = Outline.Mode.SilhouetteOnly;
                    outline.RenderPriority = 1; // above the selected-mesh outline (priority 0)
                }
```

- [ ] **Step 2 (controller): compile.** `read_console` → no `CS` errors.

### Task 4.2: Selection outline pinned to base priority (explicit)

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/Selectable.cs:24-28`

- [ ] **Step 1: Set priority 0 explicitly on the Selected branch** (documents intent; value is the
  default but being explicit guards against a future default change):

```csharp
            case SelectionVisual.Selected:
                _outline.enabled       = true;
                _outline.OutlineColor  = new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth  = 6f;
                _outline.RenderPriority = 0; // base layer; bones (1) and gizmo (2) draw on top
                break;
```

- [ ] **Step 2 (controller): compile.** `read_console` → no `CS` errors.

### Task 4.3: Gizmo renders above everything

**Files:**
- Modify (editor): the gizmo prefab `Vr3D_Gizmos.prefab` Outline components (locate via
  `find_gameobjects`/Glob — referenced in `docs/superpowers/investigations/2026-05-30-gizmo.md` as
  carrying `QuickOutline::Outline` on each handle).

- [ ] **Step 1 [MANUAL EDITOR / MCP]:** On each gizmo handle's `Outline` component, set the serialized
  `Render Priority` field to `2`. (The field is serialized as of Task 3.2, so it persists in the
  prefab — no gizmo code change needed.)
- [ ] **Step 2 [MANUAL EDITOR]:** Confirm the gizmo handle `Outline` mode is `OutlineAll` (ZTest Always)
  so it shows through occluders. If it is `SilhouetteOnly`/another mode and the gizmo must be visible
  on top of solid geometry, set it to `OutlineAll`.
- [ ] **Step 3: Checkpoint (user commits)** — `feat(outline): layered render priority — bones over selection, gizmo over all`

### Task 4.4: Verify layering in VR (user)

- [ ] **Step 1 [MANUAL / VR]:** With the skinned mesh selected (outlined) AND Show Bones on — every
  bone silhouette is visible over/through the mesh outline, including overlap regions.
- [ ] **Step 2 [MANUAL / VR]:** With an object selected and the gizmo occluded by that object — the
  gizmo outline renders on top of the object's outline (gizmo wins).

---

## Phase 5 — Remove redundant bone-outline hacks (conditional on Phase 1 verification)

Only do this if Task 1.2 confirmed bones appear reliably without a click. If they still needed a
nudge, SKIP this phase and log a follow-up instead.

### Task 5.1: Delete BumpOutlineNextFrame and the self-assign nudge

**Files:**
- Modify: `Assets/_App/Scripts/RigBuilder/PromeonProxyRigBuilder.cs:129-133, 139-157`

- [ ] **Step 1: Remove the self-assign render nudge** (`:129-133`):
```csharp
            // DELETE these lines:
            if (enabled && mr != null && mr.sharedMaterial != null)
                mr.sharedMaterial = mr.sharedMaterial;
```
- [ ] **Step 2: Remove the `BumpOutlineNextFrame` start call** (`:142`):
```csharp
            // DELETE:
            if (enabled && isActiveAndEnabled) StartCoroutine(BumpOutlineNextFrame());
```
- [ ] **Step 3: Remove the `BumpOutlineNextFrame` coroutine method entirely** (`:145-157`).
- [ ] **Step 4:** Keep the material-strip block (`:115-122`) — it is still useful defensive cleanup
  against accumulated outline materials across toggles.
- [ ] **Step 5 (controller): compile.** `read_console` → no `CS` errors. Confirm no remaining
  references to `BumpOutlineNextFrame`.

### Task 5.2: Re-verify after removal (user)

- [ ] **Step 1 [MANUAL / VR]:** Repeat Task 1.2 — bones still appear on toggle without a click after
  the hacks are gone. If regressed, revert this phase only (Phases 1–4 stand on their own).
- [ ] **Step 2: Checkpoint (user commits)** — `refactor(rig): drop bone-outline nudge hacks (mode re-assert makes them unnecessary)`

---

## Phase 6 — Documentation

### Task 6.1: Record the vendored Outline.cs edit in the QuickOutline patch note

- [ ] **Step 1:** In the QuickOutline patch note (the file tracking the `isReadable` guard — see the
  `project_quickoutline_patched` memory), add an entry: "Outline.cs — material creation moved out of
  Awake into a lazy `TryBuildMaterials()` fed by a serialized `OutlineConfig` SO (replaces
  `Resources.Load`); allocates a per-instance `_StencilRef`; adds serialized `RenderPriority` driving
  `renderQueue`; lifecycle guards for un-built materials. Re-apply on reimport. The `OutlineConfig`
  class, forked shaders, materials, and SO asset live under `_App/Scripts`/`_App/Content` and are
  reimport-safe." Note the SO must be assigned at every `Outline` creation site or no outline renders.

### Task 6.2: Mark the bone-outline bug resolved

- [ ] **Step 1:** In `docs/developer-notes/2026-05-21-bone-outline-needs-click.md`, change Status to
  "Resolved (2026-05-31)" and add a one-line pointer to this plan + the root cause (OnEnable never set
  `needsUpdate`; fixed by re-asserting `OutlineMode` on enable).
- [ ] **Step 2: Checkpoint (user commits)** — `docs(outline): record stencil/priority patch + close bone-outline bug`

---

## Self-Review

**Spec coverage:**
- Bones appear without click → Phase 1 (Task 1.1) + Phase 5 cleanup.
- Overlapping outlines stop clipping → Phases 2–3 (forked shaders + per-instance `_StencilRef`).
- "Послойность" (bones over selection, gizmo over all) → Phase 4 (`RenderPriority` 0/1/2).
- URP stencil-budget SPIKE → resolved in Architecture (renderers don't use stencil; verified in
  `Mobile_Renderer.asset`/`PC_Renderer.asset`).
- Material source / "no searching" (user's choice) → `OutlineConfig` SO holds direct material refs
  (Task 3.1/3.2); no Resources folder, no `Shader.Find`. Only two material assets + one SO asset;
  no per-object material authoring, because `Outline` instantiates clones in code.

**Type consistency:** `OutlineConfig` SO (`MaskMaterial`/`FillMaterial`, Task 3.1) ↔ `Outline.OutlineConfig`
setter + `outlineConfig` field (Task 3.3) ↔ creator assignments (Task 3.4). `RenderPriority` (public int) ↔
`renderPriority` (serialized field) ↔ `STENCIL_MIN/MAX`, `QUEUE_STEP`, `nextStencilRef`, `stencilRef` —
all defined in Task 3.3 and used consistently in Tasks 3.3/4.1/4.2/4.3. `TryBuildMaterials`/`AppendMaterials`
names consistent across `OnEnable`, the setter, and the lifecycle guards. Material instance names
unchanged ("OutlineMask (Instance)") so `PromeonProxyRigBuilder` strip logic (`:117-121`) still matches.

**Placeholder scan:** no TBD/TODO; every code step shows the actual code; the forked shaders are
reproduced in full (Tasks 2.1/2.2). The one `[INVESTIGATE]` step (3.4 Step 1) is a deliberate wiring
discovery — it must resolve how `Selectable` (runtime-added) receives the SO before its edits; not a
content placeholder.

**Risk / open items:**
- The Phase-1 fix assumes the deferral is purely `needsUpdate`-driven. If VR shows it is also
  pipeline-deferral, Phase 5 is skipped and the nudge stays (Task 1.2 captures this).
- Lazy-build timing: a runtime-added `Outline` whose SO is set AFTER `OnEnable` relies on the setter's
  build+append path (Task 3.3 Step 1). If a creator enables the component without ever setting the SO,
  no outline appears — Task 3.4 ensures every creator assigns it; the gizmo gets it serialized.
- `Outline` is `[DisallowMultipleComponent]`; bones add both `Selectable` (`:305`) and a bone `Outline`
  (`:390`). If `Selectable.EnsureOutline` later runs on a bone it reuses the existing `Outline`
  (GetComponent-then-Add), so the SO must already be set — confirm in Task 3.4 Step 1.
- Stencil-ref wrap at 250 simultaneous outlines is not a realistic scene size; if it ever is,
  recycle by screen-space non-overlap (carried from the 2026-05-30 report's open questions).
```
