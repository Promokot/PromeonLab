# SpatialUi Region Model — Prefab Wiring Verification & Re-setup Checklist

> **Purpose:** After the B1 region-model refactor (`feature/spatialui-region-model`), confirm every
> serialized dependency the new model needs is correctly wired on the prefabs/config/scenes — and
> fix anything that did not survive. Executable manually (Inspector) or by an AI (read prefab YAML /
> Unity MCP).
>
> **Status (final static verification 2026-05-29 — DONE):** all serialized wiring confirmed intact, and
> one regression was found and fixed. `UserPanel.prefab` has 8 `RegionNavButton` (each `_moduleId` +
> non-null `_button`) and 8 `RegionMember` (correct `_moduleId`s); `SimpleFileBrowserCanvas.prefab` has
> `FileBrowserSurface` + `RegionMember(fileBrowser)`; `DefaultNavBarConfig.asset` has all 10 entries with
> regions; both scopes have `_navBarConfig` assigned.
>
> **Regression found & fixed (commit `f1af836`):** the `OutlinerBtn`/`InspectorBtn`/`AnimatorBtn`/`GizmoBtn`
> nav buttons were authored **inactive**, so their `RegionNavButton.Start` never ran and they would have
> been missing at runtime. They are now authored **active** (`ApplyMode` hides them per mode after `Start`).
> See A1 and Part E for the rule.
>
> **Only Part D (human Play-mode verification) remains.** Static gates (YAML re-read, `read_console`,
> `run_tests` 153/7) are complete — do **not** re-run Parts A–C unless a prefab is rebuilt (e.g. via
> `AnimatorPanelModuleBuilder`).

## Background — why the UserPanel Inspector looks "empty" (this is expected)

The old `UserPanel` held a `NavBarBinding[] _bindings` array (button↔panel↔entryId) and a
`_navBarConfig` reference that wired the whole nav system in one place. The refactor **removed** those
fields. Opening logic now lives in per-GameObject components driven by `PanelRegionRouter`:

- `RegionMember` on each openable surface (carries its `_moduleId`; default Show/Hide = SetActive).
- `RegionNavButton` on each button (carries `_moduleId` + `_button`; calls `router.Toggle`).
- `NavBarConfig` (now also `IRegionConfig`) supplies each module's **region** (`ExclusiveGroup`),
  per-mode visibility, and the per-region **default** (`IsRegionDefault`).

So an `UserPanel` Inspector that no longer shows a big bindings array is **correct**, not a regression.
The dependencies moved; they did not disappear.

---

## Part A — Required wiring manifest (the source of truth)

Verify each row. "GO" = GameObject (path under the prefab root). Initial-active matters because the
scene-scope build callback registers each `RegionMember` and treats currently-active ones as the
region's open module at startup.

### A1. `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab`

**Root `UserPanel` component (the gutted script):**
| Field | Expected |
|---|---|
| `_mainMenuButton` | set (the Main Menu button) |
| `_exitButton` | set (the Exit button) |
| `_lockButton` | set (the Lock button) |
| `_lockButtonImage` | set (the Lock button's Image) |
| smart-follow floats | non-zero defaults (recenterAngle 45, smoothTime 0.5, …) |
| ~~`_bindings`, `_navBarConfig`, brightness floats~~ | **must NOT exist** (removed) — and **no "missing script"** on the root (the old `UserPanelKeyboardToggle` must be gone) |

**`RegionMember` on each content module (under `UserPanel/ModulesSlot/…`), `_moduleId`:**
| GO | `_moduleId` | Initial active |
|---|---|---|
| `SettingsModule` | `settings` | inactive |
| `AssetBrowserModule` | `assets` | inactive |
| `SceneOutlinerModule` | `outliner` | inactive |
| `SceneInspectorModule` | `inspector` | inactive |
| `GizmoToolsModule` | `gizmo` | inactive |
| `AnimatorPanelModule` | `animator` | inactive |

**`RegionMember` on the two overlay surfaces (under `UserPanel/OverlaysSlot/…`):**
| GO | `_moduleId` | Initial active |
|---|---|---|
| `Default` | `userPanelDefault` | **active** (it is the `overlays` region default) |
| `Keyboard` | `keyboard` | inactive |

**`RegionNavButton` on each nav button, `_moduleId` + `_button` (= the `Button` on the same GO):**
| GO (path) | `_moduleId` |
|---|---|
| `OverlaysSlot/Default/ButtonsBar_1/SettingsButton` *(or RightPart — confirm)* | `settings` |
| `OverlaysSlot/Default/ButtonsBar_1/AssetsBtn` | `assets` |
| `OverlaysSlot/Default/ButtonsBar_1/OutlinerBtn` | `outliner` |
| `OverlaysSlot/Default/ButtonsBar_1/InspectorBtn` | `inspector` |
| `OverlaysSlot/Default/ButtonsBar_1/AnimatorBtn` | `animator` |
| `OverlaysSlot/Default/ButtonsBar_2/RiggingBtn` | `rigging` (dead entry — button will hide in all modes; harmless) |
| `OverlaysSlot/Default/ButtonsBar_2/GizmoBtn` | `gizmo` |
| `FuncButtons/RightPart/KeyboardButton` | `keyboard` |

> Each `RegionNavButton._button` must point to the `Button` component on its **own** GameObject (not
> null, not another button). Brightness floats may stay at defaults (1.2 / 0.6 / 0.8).
>
> **CRITICAL — nav-button GameObjects must be authored ACTIVE.** `RegionNavButton.Start` (wires
> `onClick`, subscribes to `RegionChangedEvent`/`ModeChangedEvent`, runs `ApplyMode`) only fires on an
> **active** GameObject. The old always-active `UserPanel.ApplyMode` activated buttons centrally; the
> new per-button model cannot self-activate from an inactive GO. So `SettingsButton`, `AssetsBtn`,
> `OutlinerBtn`, `InspectorBtn`, `AnimatorBtn`, `GizmoBtn`, and `KeyboardButton` must all be **active**
> at author time — `ApplyMode` then hides them per mode after `Start` runs. `RiggingBtn` may stay
> **inactive** (dead `rigging` entry; it should never appear). This was the one regression the refactor
> introduced; fixed in `f1af836`.

### A2. `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/SimpleFileBrowserCanvas.prefab`

| Component on canvas root | Expected |
|---|---|
| `RegionMember` | `_moduleId = fileBrowser` |
| `FileBrowserSurface` | present (no serialized fields; gets `EventBus`+router via `[Inject]`) |
| `FileBrowserVrAnchor` | present; **no serialized target** (it now injects `AssetBrowserPanel`) |

### A3. `Assets/_App/Content/ScriptableObjects/DefaultNavBarConfig.asset`

| `Id` | `ExclusiveGroup` (region) | `IsRegionDefault` | `VisibleModes` |
|---|---|---|---|
| `settings` | `center` | false | all modes |
| `assets` | `center` | false | MainMenu/VrEditing/Sandbox |
| `outliner` | `left` | false | VrEditing/Sandbox |
| `inspector` | `right` | false | VrEditing/Sandbox |
| `gizmo` | `center` | false | VrEditing/Sandbox |
| `animator` | `center` | false | VrEditing + (its mode) |
| `keyboard` | `overlays` | false | VrEditing/Sandbox |
| `userPanelDefault` | `overlays` | **true** | VrEditing/Sandbox |
| `fileBrowser` | `dialog` | false | VrEditing/Sandbox |
| `rigging` | (empty) | false | (empty) — dead placeholder |

> **Region semantics to sanity-check (UX decision):** `center`, `left`, `right` are **independent**
> regions, so one `center` module + `outliner`(left) + `inspector`(right) can all be open at once;
> within `center` only one of settings/assets/gizmo/animator shows at a time. `overlays` holds
> {Default, Keyboard} with Default as the auto-restored default. `dialog` holds only the file browser.
> If you actually want, e.g., the keyboard to also hide left/right panels, change its region — but the
> current mapping preserves the pre-refactor exclusivity.

### A4. Scenes `Assets/_App/Scenes/VrEditing.unity` and `Assets/_App/Scenes/Sandbox.unity`

| Item | Expected |
|---|---|
| Scope component `_navBarConfig` | → `DefaultNavBarConfig.asset` (on `VrEditingSceneScope` GO in VrEditing; on `SandBox_VrEditingSceneScope` GO in Sandbox) |
| `UserPanel` scene instance | inherits all the prefab components above; no orphaned/"missing" overrides on the removed `_bindings`/`_navBarConfig` fields |
| `SimpleFileBrowserCanvas` scene instance | present and is the **scene** instance SimpleFileBrowser uses (not the Resources `_legacy` copy) |

---

## Part B — Verification procedure

### B1. Manual (Unity Inspector)
1. Open `UserPanel.prefab` in Prefab Mode. Click the root → confirm the `UserPanel` component shows only the kept fields and **no yellow "missing script"** warning anywhere in the hierarchy.
2. Walk the A1 table: select each listed GO, confirm the named component is present and its `_moduleId` (and `_button`, where applicable) matches. Confirm initial active state via the GameObject's enabled checkbox (Default = on; all modules + Keyboard = off).
3. Open `SimpleFileBrowserCanvas.prefab`, confirm A2.
4. Select `DefaultNavBarConfig.asset`, confirm A3 (regions + the single `IsRegionDefault` on `userPanelDefault`).
5. Open each scene; select the scope GO and confirm `_navBarConfig` is assigned; select the `UserPanel` instance and check the Overrides dropdown for stale/missing overrides — "Revert All" any that reference removed fields.

### B2. AI-assisted (read-only, no Play)
- Grep each prefab for `_moduleId:` and `_button:` and cross-check against the A1/A2 tables (this is how the current state was confirmed).
- Read `DefaultNavBarConfig.asset` and diff against A3.
- For scene checks, use Unity MCP `manage_gameobject`/`manage_scene get_hierarchy` to read the scope's `_navBarConfig` and the `UserPanel` instance components.
- Do NOT attempt Play-mode automation via MCP — it is unreliable here; behavior (Part D) is human-verified.

---

## Part C — Remediation (only for rows that fail verification)

- **Missing `RegionMember` on a module GO:** Add Component → `RegionMember`; set `_moduleId` to the value from A1. (Add it on the GO inside `UserPanel.prefab`; see the nested-prefab note in Part E.)
- **`RegionNavButton._button` null:** drag the `Button` component from the same GameObject into `_button`. `_moduleId` must equal the panel's id it controls.
- **Wrong/empty `_moduleId`:** set it to the exact string from A1/A3 (case-sensitive; the router matches `RegionMember.ModuleId` ↔ `NavBarConfig.Entry.Id`).
- **`_navBarConfig` unassigned on a scope:** assign `DefaultNavBarConfig.asset` on the scope GO; save the scene.
- **Config entry missing/wrong region:** edit `DefaultNavBarConfig.asset` entries to match A3. A module with no matching config entry has **no region** → it never participates in exclusion (router just Show/Hides it).
- **A nav button never appears at runtime (its module can't be opened):** the button GameObject was authored **inactive**, so `RegionNavButton.Start` never ran. Set the button GO **active** in `UserPanel.prefab` (this is the `f1af836` fix). `ApplyMode` still hides it in modes where its module isn't visible — authoring it active only lets `Start` run once. Do **not** do this for `RiggingBtn` (dead entry; leave inactive).
- **"Missing script" on a GO:** it's the deleted `UserPanelKeyboardToggle` — remove the missing component from the GO.

---

## Part D — Play-mode behavior verification (human, in VR)

After A–C pass, confirm runtime behavior in `VrEditing` (and `Sandbox`):
1. **Load:** no exceptions / NullReference; UserPanel appears; the `Default` nav-bar overlay is visible.
2. **Nav:** each nav button opens/closes its module; opening a second `center` module closes the first; `outliner`(left)/`inspector`(right) open independently of `center`; active-button brightness toggles.
3. **Keyboard:** the keyboard button shows the keyboard and **hides the nav-bar overlay**; pressing it again (or it closing) **restores the nav-bar overlay** (region-default auto-reopen); typing into fields still works.
4. **File browser:** "+" in the asset browser opens the dialog positioned in front of the asset browser; picking a file imports it; cancel closes cleanly; the asset browser stays visible behind it.
5. **Mode change** `MainMenu`↔`VrEditing`: modules not visible in the new mode close; their nav buttons hide.

---

## Part E — Gotchas worth a deliberate check

- **Nested-prefab override fragility.** The `ModulesSlot` modules (e.g. `AssetBrowserModule`,
  `AnimatorPanelModule`) are **nested prefab instances** inside `UserPanel.prefab`. Their `RegionMember`
  was added on the instance within `UserPanel` (an override), not necessarily on the standalone module
  prefab asset. This works in-game, but: if `AnimatorPanelModuleBuilder` (the editor tool) **rebuilds**
  `AnimatorPanelModule.prefab`, or someone re-imports a module prefab, the override could be lost.
  **Recommendation:** consider adding the `RegionMember` to each module's **own prefab asset** (so it
  travels with the prefab) and removing the per-instance override — or at minimum, re-run this checklist
  after using `AnimatorPanelModuleBuilder`.
- **Initial active state == initial open module per region.** Because the router registers active
  `RegionMember`s as their region's open module at startup, the authored on/off state matters:
  `Default` ON (overlays region starts on Default), every `ModulesSlot` module OFF, `Keyboard` OFF.
  If a module is authored ON by mistake, it will be the region's startup module.
- **`RegionNavButton.Start` needs an ACTIVE GameObject.** Unlike the old centralized `UserPanel.ApplyMode`
  (which could activate a button from the outside), each button now wires itself in its own `Start` —
  which Unity does not call on an inactive GO. A nav button authored inactive is therefore dead: no
  `onClick`, no event subscription, never shown. Author all live nav buttons active
  (`Settings`/`Assets`/`Outliner`/`Inspector`/`Animator`/`Gizmo`/`Keyboard`); `ApplyMode` hides them per
  mode afterward. Only the dead `RiggingBtn` stays inactive. (This was the regression fixed in `f1af836` —
  the four `center`/`left`/`right` buttons were authored inactive.)
- **Scene instance vs prefab.** `UserPanel` is a **scene** object (found via `FindAnyObjectByType` in
  `RootLifetimeScope`), so the scene's instance must carry the prefab's new components. If the scene
  instance predates the prefab edits and has overrides, reapply/ revert so it matches the prefab.
- **File browser singleton source.** Confirm SimpleFileBrowser uses the **scene** `SimpleFileBrowserCanvas`
  instance (with our `FileBrowserVrAnchor`/`FileBrowserSurface`), not the `ThirdParty/.../Resources/
  SimpleFileBrowserCanvas_legacy.prefab`. If the legacy Resources copy is picked up, the dialog won't be
  VR-positioned — remove/neutralize the legacy copy or ensure the scene instance initializes first.
- **`rigging` is a dead entry** (null panel, empty region/modes). Its `RegionNavButton` will hide in
  every mode. Leave as-is, or remove the `RiggingBtn`'s `RegionNavButton` + the `rigging` config entry
  if you want it gone.
