# VR Launch Settings — Audit & Fix Checklist (2026-05-30)

Scope: Workstream A. Why VR is broken on Quest ("старые билды — ничего не происходит";
"последний билд — не открывается, жёстко тормозит и плывёт"). Verified against the actual
files on disk **after** the user's "I enabled OpenXR for PC and Android" change, plus live
state via Unity MCP (`PromeonLab@7b9a6da1`, Unity 6000.3.7f1).

Apply mode: **checklist only** — nothing in files was modified by this audit.

> Reading caveat: during this session the file-read tools intermittently served **stale/cached**
> content. The findings below are taken only from reads that were corroborated by a fresh
> process (`cat`/`sed`) or a clean `Read`. Where a claim still rests on a possibly-cached grep it
> is marked **(confirm in editor)**.

---

## TL;DR — corrected picture

The core XR plumbing is now **correct** (this is a change from the first draft of this doc):

- ✅ OpenXR Loader is assigned for **both** Android and Standalone
  (`bb2068d93afd206449373aa9e22588c9`). The user's change applied.
- ✅ **Meta Quest Support** feature is enabled for Android.
- ✅ Render mode = Single Pass Instanced; build scenes correct; Mobile URP pipeline; IL2CPP/ARM64;
  Linear; New Input System — all fine.

So "ничего не происходит / плывёт / тормозит" is most likely **not** "XR didn't start" — it's
**what OpenXR was told to start with**. Two strong suspects remain:

1. **A large set of AR Foundation / Mixed-Reality features are enabled** for a pure-VR app
   (passthrough camera, occlusion, meshing, planes, anchors, bounding boxes, raycast, colocation,
   boundary visibility). These request heavy runtime subsystems + extra OpenXR extensions at
   session start — on Quest that means real perf cost and possible compositor/blend-mode weirdness
   ("плывёт"). None are needed to author animations in VR.
2. **No controller interaction profile is enabled — CONFIRMED.** Every profile is off
   (`OculusTouchControllerProfile`, `MetaQuestTouchPlusControllerProfile`,
   `MetaQuestTouchProControllerProfile`, `KHRSimpleControllerProfile` … all `m_enabled: 0`).
   Without one, OpenXR binds no controllers → input dead (exactly why "дальше проверить не
   удалось"), and OpenXR Project Validation will error.

---

## Verified evidence

### A1 — OpenXR loaders ARE assigned (was wrong in first draft) ✅
`Assets/XR/XRGeneralSettingsPerBuildTarget.asset` (clean `cat` read):
- `Android Providers.m_Loaders` → `- {fileID: 11400000, guid: bb2068d93afd206449373aa9e22588c9, type: 2}` (OpenXR Loader)
- `Standalone Providers.m_Loaders` → same loader
- `Keys: 0100000007000000` → Standalone(1) + Android(7); `Values` → `Standalone Settings`, `Android Settings`
- Both `*_Settings.m_InitManagerOnStart: 1` (initialize XR on startup) ✅

### A2 — OpenXR features for Android: Meta Quest Support ON, AR/MR suite ON, ALL controller profiles OFF
`Assets/XR/Settings/OpenXR Package Settings.asset` — full Android map (clean read):

**Enabled (`m_enabled: 1`):** `MetaQuestFeature` (Meta Quest Support) ✅, `OpenXRLifeCycleFeature`,
`OpenXRCompositionLayersFeature`, `DisplayUtilitiesFeature`, **+ full MR set**: `ARSessionFeature`,
`ARCameraFeature` (passthrough), `AROcclusionFeature`, `ARMeshFeature`, `ARPlaneFeature`,
`ARAnchorFeature`, `ARBoundingBoxFeature`, `ARRaycastFeature`, `ColocationDiscoveryFeature`,
`BoundaryVisibilityFeature`.

**Disabled (`m_enabled: 0`) — the problem:** every interaction profile —
`OculusTouchControllerProfile` (line 420/422), `MetaQuestTouchPlusControllerProfile` (1426/1428),
`MetaQuestTouchProControllerProfile` (1035/1037), `KHRSimpleControllerProfile`, hand/eye/palm —
plus `FoveatedRenderingFeature` (280/282), `OculusQuestFeature`, `SpaceWarpFeature`,
`XrPerformanceSettingsFeature`, `AutomaticDynamicResolutionFeature`.

### A3 — Already CORRECT (do not touch)
- OpenXR Android `m_renderMode: 1` = Single Pass Instanced ✅ (ignore legacy
  `PlayerSettings.m_StereoRenderingPath: 0`).
- Build Settings (MCP): `Bootstrap`@0, `MainMenu`@1, `VrEditing`@2, `Sandbox`@3, all enabled ✅.
- Android uses Mobile quality (`QualitySettings.m_PerPlatformDefaultQuality.Android: 1`) →
  `Mobile_RPAsset` (MSAA 4, RenderScale 1, HDR off, no opaque/depth copy) ✅.
- `activeInputHandler: 1` (Input System Package / New) ✅; IL2CPP + ARM64 + Linear + gpuSkinning +
  MTRendering ✅.

---

## FIX CHECKLIST (Unity Editor → rebuild)

### Step 1 — Turn OFF the AR / Mixed-Reality features you don't use  (primary perf suspect)
`Project Settings > XR Plug-in Management > OpenXR` → **Android** tab → **disable**, unless you
intentionally want passthrough/MR:
- Meta Quest: Camera (Passthrough), Occlusion, Meshing, Planes, Anchors, Bounding Boxes, Raycasts,
  Session, Colocation Discovery, Boundary Visibility.
- Keep: **Meta Quest Support**, OpenXR Composition Layers (only if you use composition layers),
  Display Utilities, OpenXR Life Cycle.
- Do the same on the **Desktop** tab to keep them in sync.

### Step 2 — Enable a controller interaction profile  (fixes input)
Same OpenXR screen → **Interaction Profiles**, add (Android + Desktop):
- ✅ **Oculus Touch Controller Profile** (Quest 2)
- ✅ **Meta Quest Touch Plus Controller Profile** (Quest 3)

### Step 3 — Run OpenXR Project Validation (Android)
`Project Settings > XR Plug-in Management > Project Validation` → Android → fix every error.
This is the authoritative check and will confirm/deny Step 2's "no profile" suspicion immediately.

### Step 4 — Perf pass
- Enable **Foveated Rendering** (Android) and set foveation level at runtime (needs Vulkan).
- Confirm **Graphics API = Vulkan** for Android (Player Settings > Other Settings).
- Set display refresh / `Application.targetFrameRate` to 72 Hz (90 if stable).

### Step 5 — Store-readiness (not a launch blocker)
- `AndroidMinSdkVersion` is **25** — fine for sideload; raise to 29 (run) / 32 (Meta Store) later.

---

## "What else might go wrong" — predictions

1. **Passthrough still active** — if `ARCameraFeature` stays enabled, the session may run in an
   MR blend mode with an opaque scene → visual artifacts / the "плывёт" feel. Disabling the MR set
   (Step 1) is the highest-value change after the loader fix.
2. **Validation red on Android** — near-certain it flags "add an interaction profile". Apply it.
3. **Quest 3 controllers dead** if only the legacy Oculus Touch profile is on — add Touch Plus.
   Ties into the `InputBindings` subsystem / XRI action maps.
4. **XR Origin / tracking origin** — rig is the custom `User XR Origin (XR Rig)` variant (project
   memory), not present in `Bootstrap` at edit time (DontDestroyOnLoad / mode scenes). Confirm in a
   build that the active camera is the XR Origin's tracked camera and Tracking Origin Mode = Floor;
   a wrong camera = locked/drifting view regardless of XR settings.
5. **Foveation/SpaceWarp without Vulkan** → runtime errors. Keep off until Vulkan confirmed.
6. **Stale merged AndroidManifest** — after changing OpenXR features, do a clean rebuild.

---

## Quick verify after the build
- Launches directly into stereo VR (not a 2D panel, not passthrough unless intended).
- Head tracking 1:1, no swimming.
- Controllers tracked; rays/grip work.
- `adb logcat` shows OpenXR session created; no "interaction profile"/"no graphics binding" errors.

---

## RUNTIME CONFIRMATION (2026-05-30, Editor play via Quest Link)

User ran from Bootstrap in-editor. Result: **head tracking works, image shows, but controllers
are fully gone — invisible, no movement, no interaction.** Log shows OpenXR loader initialized,
plus two harmless feature-disable warnings and one benign audio warning. Crucially, **no
controller-related error appears.**

→ This CONFIRMS the A2 diagnosis. With every interaction profile disabled (Standalone profiles
all `m_enabled: 0`, verified), OpenXR starts a session (HMD view pose always available → head
look + image), but creates **no controller devices** → pose bindings resolve to nothing →
controller models never instantiate and all input is dead. No error is logged because the runtime
never even attempts controller init. Step 2 (enable Oculus Touch + Meta Quest Touch Plus profiles)
is the fix; it must be applied on the **Desktop** tab too (Quest Link uses the Standalone profile).

Supporting fact: the project's own `InputBindings` subsystem is a 2-line placeholder
(`InputBindings.cs:2` → `InputBindingsPlaceholder {}`); all input rides on the XRI sample
(`ControllerInputActionManager` + `XRI Default Input Actions`). No `_App` code references
`PoseControl`/`OpenXR.Input`/`TrackedPoseDriver` (grep = 0), so OpenXR Project Validation fixes
(incl. "use InputSystem.XR.PoseControl") cannot break project code — safe to apply.

### Log warnings triage
- `failed to enable XR_META_boundary_visibility` / `XR_META_environment_depth` → Boundary
  Visibility + Occlusion MR features; the PC OpenXR runtime (Link) lacks these Meta extensions, so
  they self-disable. Harmless, and exactly why Step 1 (disable the MR suite) is recommended.
- `Error setting active audio output driver. Falling back to default` → benign Quest Link noise.

### Project Validation — safe to apply?
Yes. "Switch to InputSystem.XR.PoseControl instead of OpenXR.Input.PoseControl" only rebinds XRI
pose actions; no project code touches PoseControl. Recommended order: (1) add controller profiles,
(2) disable unused MR features, (3) then apply remaining validation fixes — the list shrinks after
1–2.

## ✅ RESOLVED (2026-05-30): Oculus XR Plugin conflicted with OpenXR

**Root cause:** the project had BOTH `com.unity.xr.openxr` (OpenXR Plugin) AND
`com.unity.xr.oculus` (Oculus XR Plugin) installed. Oculus XR Plugin is not an OpenXR module — it's
a separate, mutually-exclusive backend. It hijacked controller device registration (device showed
`Type: OculusHMD` in Input Debugger), so controller `devicePosition`/`deviceRotation` stayed at
zero (buttons/stick/velocity still worked, head still tracked). Controllers sat at scene origin
with no pose.

**Fix that worked:** removed the `com.unity.xr.oculus` package (Package Manager → Remove) + ensured
only OpenXR is ticked in XR Plug-in Management (Android + Desktop), rebuilt `Library`. Controllers
immediately tracked correctly. The correct OpenXR companion for Quest is `com.unity.xr.meta-openxr`,
which stays.

**Note:** the rounds below (profiles, rig, pose-bindings, usages) were investigative dead-ends —
all of those were actually fine. The single fact that pointed here was "position/rotation zero at
the DEVICE level in Input Debugger" + two XR providers in the manifest. Kept for the record.

---

## CONTROLLERS STILL DEAD — review round 2 (2026-05-30) [superseded by RESOLVED above]

After the user assigned profiles + applied validation fixes, controllers are still invisible /
untracked in-app (head works). Reviewed prefabs, the rig, and re-read the OpenXR settings.

### Interaction profiles are NOW correct — so profiles are NOT the cause
Re-read `Assets/XR/Settings/OpenXR Package Settings.asset`, confirmed via three independent parses
(grep -A, a Python block parser, and a line-paired scan — all agree):

| Profile | Standalone | Android |
|---|---|---|
| OculusTouchControllerProfile | **1** (line 490) | **1** (line 422) |
| MetaQuestTouchPlusControllerProfile | **1** | **1** |
| MetaQuestTouchProControllerProfile | **1** | **1** |
| all hand/other profiles | 0 | 0 |

So the controller profiles the user enabled DID apply, on both platforms. The earlier draft of this
section (claiming Oculus Touch was off) was based on a STALE cached file read and is **retracted**.
With Oculus Touch + Touch Plus + Touch Pro all enabled, every Quest controller has a matching
binding profile → the dead controllers are NOT an OpenXR-profile problem.

### Rig / prefab review
- `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` is a **variant** of the XRI
  Starter `XR Origin (XR Rig)` (base guid `f6336ac4ac8b4d34bc5072418cdc62a0`, confirmed via the
  base prefab `.meta`). The base rig has the standard Left/Right Controller GameObjects with
  Position/Rotation/Tracking-State input actions, Camera Offset, Main Camera, and Locomotion.
- The base rig has an `InputActionManager` with an `m_ActionAssets:` list (line ~885) — this is the
  component that must hold the `XRI Default Input Actions` asset AND have its action maps enabled at
  runtime. **This is the prime remaining suspect and must be verified live** (see below).
- Variant override (prefab lines 168-179) zeros a `m_RayInteractorChanged` persistent-call array —
  UI ray callback wiring, unrelated to pose.
- Per project memory (`interaction_input_model`), XRI's select flow was deliberately replaced by
  `XRPromeonInteractable` (tap=select / hold-trigger=rotate / hold-grip=move). That changes *what*
  interaction does, but still depends on controller devices + enabled input actions.

### Remaining hypotheses (ranked) — needs live verification
1. **InputActionManager has no action asset / actions never enabled.** If the rig's
   `InputActionManager.m_ActionAssets` is empty, or some bootstrap disables it, the controller
   pose/visual actions never fire → controllers invisible & inert while the HMD (driven separately)
   still tracks. **Most likely.**
2. **TrackedPoseDriver input refs on Left/Right Controller unassigned** (Position/Rotation actions
   not bound) → no pose.
3. **Controller model/visual disabled** but tracking works (would still allow rays) — less likely
   given "no interaction either."
4. **A bootstrap/DI step disables interactors** (the variant wires `WorldClickCatcher` to left/right
   interactors; if those are toggled off in code, rays die).

### Decisive live checks (do these — they pinpoint it)
1. **Window > Analysis > Input Debugger** in Play mode (Quest Link):
   - No XR Controller devices → OpenXR binding problem (but profiles look fine, so unlikely).
   - Devices present → it's a rig/input-actions wiring problem (hypotheses 1-2).
2. In the **Hierarchy at runtime**, select the rig's `Input Action Manager` and confirm its Action
   Assets list contains `XRI Default Input Actions` and is enabled.
3. Select **Left/Right Controller** at runtime → check the `Tracked Pose Driver` Position/Rotation
   action references are assigned and the GameObject is active.
4. Watch the console for any input-system / action-asset errors on entering Play.

> Tooling caveat: file-read tools in this session repeatedly returned STALE-cached and at least one
> CORRUPTED result, so deeper static prefab parsing was abandoned as untrustworthy. The profile
> table above is trusted (3 agreeing parses). The rig-level hypotheses must be closed with the live
> checks above rather than by re-reading the prefab YAML.

## Still open (product / runtime calls)
- Graphics API (Vulkan vs GLES3) actual state for Android — verify in Player Settings.
- Whether passthrough (`ARCameraFeature`) is intentional, or leftover from package defaults.
- XR Origin camera / tracking-origin correctness in a real build (see prediction #4).
