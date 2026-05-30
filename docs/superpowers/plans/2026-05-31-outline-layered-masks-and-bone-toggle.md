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
| `Assets/_App/Content/Resources/PromeonOutline/OutlineMask.mat` | Material using forked mask shader | Create (MCP/editor) |
| `Assets/_App/Content/Resources/PromeonOutline/OutlineFill.mat` | Material using forked fill shader | Create (MCP/editor) |
| `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs` | Per-instance stencil ref + RenderPriority + load forked materials | Modify (vendored — document) |
| `Assets/_App/Scripts/VrInteraction/Selectable.cs` | Selection outline | Modify: explicit RenderPriority = 0 |
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

## Phase 3 — Forked materials + per-instance stencil ref + RenderPriority (Outline.cs)

### Task 3.1: Create forked material assets in a Resources folder

QuickOutline builds outline materials via `Resources.Load<Material>("Materials/OutlineMask")`
(`Outline.cs:89-90`). To use the forked shaders without authoring per-object materials, create two
material assets that reference the forked shaders, in a Resources path we own. (This is the only
material asset work — no per-mesh materials are needed; `Outline` instantiates clones at runtime.)

**Files:**
- Create: `Assets/_App/Content/Resources/PromeonOutline/OutlineMask.mat` (shader `PromeonLab/OutlineMask`)
- Create: `Assets/_App/Content/Resources/PromeonOutline/OutlineFill.mat` (shader `PromeonLab/OutlineFill`)

- [ ] **Step 1 [MANUAL EDITOR / MCP]:** Create the folder `Assets/_App/Content/Resources/PromeonOutline/`.
- [ ] **Step 2 [MANUAL EDITOR / MCP]:** Create `OutlineMask.mat` using shader `PromeonLab/OutlineMask`
  and `OutlineFill.mat` using shader `PromeonLab/OutlineFill` (e.g. via `manage_material` create, or
  duplicate the originals at `QuickOutline/Resources/Materials/` and swap the shader). Leave all other
  values at defaults; `_StencilRef`/`_ZTest`/queue are driven at runtime by `Outline.cs`.
- [ ] **Step 3:** Confirm via Glob that both `.mat` files exist at the paths above.

### Task 3.2: Point Outline.cs at the forked materials + add stencil ref and RenderPriority

**Files:**
- Modify: `Assets/_App/ThirdParty/QuickOutline/Scripts/Outline.cs:35-49, 78-100, 302-338`

- [ ] **Step 1: Add the `RenderPriority` property next to the other public properties** (after
  `OutlineWidth`, `:49`):

```csharp
  public int RenderPriority {
    get { return renderPriority; }
    set {
      renderPriority = value;
      needsUpdate = true;
    }
  }
```

- [ ] **Step 2: Add the serialized backing field + per-instance stencil ref state** (in the fields
  block near `:77-81`):

```csharp
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

- [ ] **Step 3: Load the forked materials and allocate the stencil ref in `Awake`** (`:89-99`).
  Replace the two `Resources.Load` lines and add the ref allocation:

```csharp
    // Instantiate outline materials (forked shaders with a parameterised _StencilRef)
    outlineMaskMaterial = Instantiate(Resources.Load<Material>(@"PromeonOutline/OutlineMask"));
    outlineFillMaterial = Instantiate(Resources.Load<Material>(@"PromeonOutline/OutlineFill"));

    outlineMaskMaterial.name = "OutlineMask (Instance)";
    outlineFillMaterial.name = "OutlineFill (Instance)";

    // Unique stencil ref per instance (cycles within the free range)
    stencilRef = nextStencilRef;
    nextStencilRef++;
    if (nextStencilRef > STENCIL_MAX) nextStencilRef = STENCIL_MIN;
```

> Keep the material instance NAMES exactly ("OutlineMask (Instance)" / "OutlineFill (Instance)") —
> `PromeonProxyRigBuilder.SetBonesInteractive` strips leftover materials by the `OutlineMask`/
> `OutlineFill` name prefix (`:117-121`); renaming would break that cleanup.

- [ ] **Step 4: Apply the stencil ref + priority queue at the end of `UpdateMaterialProperties`**
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

- [ ] **Step 5 (controller): compile.** `read_console` → no `CS` errors. New `Outline.RenderPriority`
  member is now usable by callers.

- [ ] **Step 6: Checkpoint (user commits)** — `feat(outline): per-instance stencil ref + RenderPriority (anti-clip + layering)`

### Task 3.3: Verify anti-clip in VR (user)

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
  `project_quickoutline_patched` memory), add an entry: "Outline.cs — loads forked materials from
  `Resources/PromeonOutline/`, allocates a per-instance `_StencilRef`, adds serialized `RenderPriority`
  driving `renderQueue`. Re-apply on reimport. Forked shaders/materials live under `Content/` and are
  reimport-safe." Note that the forked shaders MUST exist or `Resources.Load` returns null.

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
- Manual-material concern (user's question) → only two material assets (Task 3.1); no per-object
  material authoring, because `Outline` instantiates clones in code.

**Type consistency:** `RenderPriority` (public int) ↔ `renderPriority` (serialized field) ↔
`STENCIL_MIN/MAX`, `QUEUE_STEP`, `nextStencilRef`, `stencilRef` — all defined in Task 3.2 and used
consistently in Tasks 3.2/4.1/4.2/4.3. Material instance names unchanged ("OutlineMask (Instance)")
so `PromeonProxyRigBuilder` strip logic (`:117-121`) still matches.

**Placeholder scan:** no TBD/TODO; every code step shows the actual code; the forked shaders are
reproduced in full (Tasks 2.1/2.2) since the engineer may run tasks out of order.

**Risk / open items:**
- The Phase-1 fix assumes the deferral is purely `needsUpdate`-driven. If VR shows it is also
  pipeline-deferral, Phase 5 is skipped and the nudge stays (Task 1.2 captures this).
- `Resources.Load("PromeonOutline/...")` returns null if Task 3.1 materials are missing or the
  forked shaders failed to compile — Task 2.2 Step 2 and Task 3.1 Step 3 gate against this.
- Stencil-ref wrap at 250 simultaneous outlines is not a realistic scene size; if it ever is,
  recycle by screen-space non-overlap (carried from the 2026-05-30 report's open questions).
```
