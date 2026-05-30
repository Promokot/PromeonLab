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

## Still open (product / runtime calls)
- Graphics API (Vulkan vs GLES3) actual state for Android — verify in Player Settings.
- Whether passthrough (`ARCameraFeature`) is intentional, or leftover from package defaults.
- XR Origin camera / tracking-origin correctness in a real build (see prediction #4).
