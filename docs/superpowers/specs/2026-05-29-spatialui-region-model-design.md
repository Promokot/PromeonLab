# SpatialUi Unified Region Model — Design (Spec B1)

> Status: design (awaiting review). This is **sub-project B1** of the deferred spec-B reworks
> (see `2026-05-29-spatialui-animation-refactor-design.md` → "Out of scope — deferred to spec B").
> B1 unifies panel opening; it **changes runtime behavior** (unlike the pure rename of spec A).

> **Planning addendum (2026-05-29, post-review corrections):** Two adjustments made while writing
> the implementation plan, after re-reading the actual code:
> 1. **Keyboard is button-toggled, not focus-driven.** `UserPanelKeyboardToggle` swaps
>    `_defaultContent`↔`_keyboardContent` via a keyboard *button*; `KeyboardFocusEvent`
>    (`VrInputFieldProxy`) only routes keystrokes to `VrKeyboard`, it does not open anything. So the
>    keyboard migration is a **behavior-preserving 2-way region swap**: `_defaultContent` and
>    `_keyboardContent` become two `RegionMember`s in one region (`userPanelShell`); the keyboard
>    button calls `router.Open("keyboard")` / `router.Open("userPanelDefault")`. The
>    `KeyboardFocusEvent`→`VrKeyboard` typing path is untouched. (Replaces the spec's incorrect
>    "open on KeyboardFocusEvent / close on submit/blur".)
> 2. **No `NavBarConfig`→`PanelRegionConfig` rename in B1.** `NavBarConfig.Entry` already carries
>    exactly the needed fields (`Id`=moduleId, `ExclusiveGroup`=regionKey, `VisibleModes`). The
>    router consumes it via a new `IRegionConfig` interface that `NavBarConfig` implements (so the
>    router is unit-testable with a fake). The cosmetic rename touches runtime + the editor builder's
>    `t:NavBarConfig` filter for no functional gain — deferred to a trivial follow-up.

**Goal:** Replace the ~3 ad-hoc "open a panel" mechanisms with one registry-driven region model:
panels register themselves, panels that share a UI *region* are mutually exclusive (opening one
swaps out the other in the same place), and both nav buttons and code open/close through one API.

**Why now:** Today opening logic is fragmented across `UserPanel.OnNavButtonClicked` +
`HidePanelsInGroup` (nav modules), `UserPanelKeyboardToggle` (bespoke 2-way content swap), and a
third-party modal dialog (`FileBrowser.ShowLoadDialog` from `AssetBrowserPanel`). Each surface has
its own show/hide path, so adding a new surface (e.g. a future context menu) means inventing a
fourth path. Lifting the "region + mutual-exclusion + open/close" concern out of `UserPanel` into a
reusable coordinator makes the model uniform and extensible.

**Constraint:** Single runtime assembly (`_App.Runtime`); no namespaces for runtime code (type names
are global). VContainer DI (Root → Scene → Feature). Custom `EventBus`. Scene panels are wired by
`FindAnyObjectByType` + `c.Inject(...)` inside `LifetimeScope.Configure` (the sanctioned bootstrap
exception to the no-`Find` rule). Forbidden at runtime: `FindObjectOfType`/`GameObject.Find`,
singletons, `static` mutable state, generic type suffixes (`Manager`/`Handler`/`Controller`/…).

---

## Scope

**In scope (B1):**
1. Core mechanism: `PanelRegionRouter` + `IRegionSurface` + `RegionMember` + region registry SO.
2. Migrate the existing UserPanel **nav modules** onto the router (behavior-preserving).
3. Migrate the **keyboard** onto the router (replaces `UserPanelKeyboardToggle`).
4. Integrate the **file browser** as a region surface (adapter + `FilePickedEvent`) and fix
   `FileBrowserVrAnchor`'s forbidden `FindAnyObjectByType` (spec-B item 5, folded in here).

**Out of scope (named follow-ups):**
- **Detach → add-on** (spec-B item 3): split `SpatialPanelDetachable` so docked behavior is a normal
  region module and detach/float/chrome becomes an optional `PanelDetachAddon` stub. Own follow-up spec.
- **`VrKeyboard` rename** (spec-B item 2): `VrKeyboard` is the typing *brain* (root-scoped input
  service), referenced by type in third-party `KeyboardButtonController.cs` and its prefab. Renaming
  it touches ThirdParty for little gain. **Keep the name in B1**; revisit if the add-on follow-up
  needs it.
- **Registry merge:** `PanelRegistry` (top-level scene panels) stays separate from the region
  registry. Merging the two levels is the rejected "variant A" — not needed for unification.
- **Context menu:** does not exist in code. The model leaves room for it as a future
  `router.Open(...)` consumer; nothing is built for it now.
- **IkWizardPanel import rework** (spec-B item 4): independent sub-project B2.

---

## The model

A script's **region** is a logical string key declared in a ScriptableObject (generalizes today's
`NavBarConfig.ExclusiveGroup`). Within one region, **at most one surface is open**; opening surface
B in region R closes whatever was open in R. Layout/positioning stays authored per surface (manual,
as today) — the region is a logical grouping, not a physical slot.

### New types

```
SpatialUi/                          (framework primitives at root)
├── PanelRegionRouter.cs            plain C#, SceneLifetimeScope service
├── IRegionSurface.cs               { void Show(); void Hide(); bool IsOpen { get; } }
├── RegionMember.cs                 MonoBehaviour: per-module registrar + default surface
└── PanelRegionConfig.cs            SO (evolved from NavBarConfig) — moduleId → {regionKey, VisibleInModes}

SpatialUi/Behaviors/
├── RegionNavButton.cs              nav button → router.Toggle(moduleId)
├── KeyboardFocusOpener.cs          KeyboardFocusEvent → router.Open/Close("keyboard")
└── FileBrowserSurface.cs           IRegionSurface adapter over SimpleFileBrowser

SpatialUi/Events/
└── FilePickedEvent.cs              struct { string Path; }   (published by FileBrowserSurface)
```

**`PanelRegionRouter`** (plain C#, registered `Lifetime.Scoped` in the scene scopes):
- State: `Dictionary<string, IRegionSurface> _modules` (moduleId → surface) and
  `Dictionary<string, string> _openByRegion` (regionKey → currently-open moduleId).
- `void Register(string moduleId, IRegionSurface surface)` — called once per module at scope build.
- `void Open(string moduleId)` — look up regionKey from the config; if another module is open in
  that region and it's not this one, `Hide()` it and clear the slot; `Show()` this module; record it
  as the region's open module.
- `void Close(string moduleId)` — `Hide()` it; clear the region slot if it held this module.
- `void Toggle(string moduleId)` — `Open` if not currently open, else `Close`.
- `void CloseRegion(string regionKey)` — close whatever is open there.
- Subscribes to `ModeChangedEvent`: for each registered module not `VisibleInMode(currentMode)`
  that is open, `Close` it (replaces `UserPanel.ApplyMode`'s panel-hiding half).
- **Does not** spawn panels (that stays `UiPanelOrchestrator`), own input bindings (those are the
  trigger Behaviors), or manage top-level panel visibility.

**`IRegionSurface`** — the router speaks only `Show()`/`Hide()`/`IsOpen`, never raw `SetActive`. This
is what lets the file browser (a modal that must be raised via library API) participate uniformly.

**`RegionMember`** (MonoBehaviour on each module root):
- `[SerializeField] private string _moduleId;`
- Implements the **default** `IRegionSurface`: `Show()` → `gameObject.SetActive(true)`,
  `Hide()` → `gameObject.SetActive(false)`, `IsOpen` → `gameObject.activeSelf` — **unless** a sibling
  component implements `IRegionSurface` (resolved once via `GetComponent`), in which case it delegates
  to that sibling (this is how `FileBrowserSurface` overrides show/hide).
- Exposes `ModuleId` so the scope can register it.
- Does **not** self-register in `OnEnable`/`Awake` — those never fire for modules that start inactive.
  Registration is discovery-based (see DI wiring).

**`PanelRegionConfig`** (SO; rename of `NavBarConfig`, GUID-safe asset rename):
- Per entry: `moduleId` (string), `regionKey` (string; was `ExclusiveGroup`), `VisibleInModes`
  (`AppMode[]`). Keeps existing `IsVisibleInMode`/`TryGetEntry` API, keyed by `moduleId`.
- Rationale for keeping it separate from `PanelRegistry`: the two describe different *levels* —
  `PanelRegistry` = which top-level panels to spawn per mode (read by `UiPanelOrchestrator`);
  `PanelRegionConfig` = how sub-modules within hosts share regions and exclude each other.

### Trigger Behaviors

| Trigger | Today | B1 |
|---|---|---|
| Nav button click | `UserPanel.OnNavButtonClicked` + `HidePanelsInGroup` | `RegionNavButton` → `router.Toggle(moduleId)` |
| Input field focus → keyboard | `UserPanelKeyboardToggle` 2-way swap | `KeyboardFocusOpener` on `KeyboardFocusEvent` → `router.Open("keyboard")`; on submit/blur → `router.Close("keyboard")` |
| Mode change | `UserPanel.ApplyMode` | `PanelRegionRouter` closes now-invisible modules; `RegionNavButton` self-hides its button per `VisibleInModes` |
| "+" in asset browser → file dialog | `AssetBrowserPanel.OnAddClicked` → `FileBrowser.ShowLoadDialog(lambda)` | `AssetBrowserPanel` → `router.Open("fileBrowser")`; result via `FilePickedEvent` |

`UserPanel` slims down: it keeps smart-follow, lock, main-menu/exit buttons, and nav-button
*brightness* styling, but hands the open/close/exclusion/mode-hiding logic to the router and
`RegionNavButton`. Its `NavBarBinding[]` is removed (modules are discovered, not host-listed).

---

## DI wiring

`PanelRegionRouter` and `PanelRegionConfig` are scene-level (same level as `UiPanelOrchestrator`).
In **both** `VrEditingSceneScope` and `SandboxSceneScope`:

```csharp
[SerializeField] private PanelRegionConfig _regionConfig;   // assign asset in inspector
...
builder.RegisterInstance(_regionConfig);
builder.Register<PanelRegionRouter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

// discovery-based registration of all modules (including inactive ones):
builder.RegisterBuildCallback(c =>
{
    var router = c.Resolve<PanelRegionRouter>();
    foreach (var rm in Object.FindObjectsByType<RegionMember>(
                 FindObjectsInactive.Include, FindObjectsSortMode.None))
    {
        c.Inject(rm);                       // satisfies [Inject] on the member / its sibling surface
        router.Register(rm.ModuleId, rm);
    }
});
```

Trigger behaviors that need the router (`RegionNavButton`, `KeyboardFocusOpener`,
`AssetBrowserPanel`, `FileBrowserSurface`/`FileBrowserVrAnchor`) are injected the same way the scope
already injects panels (`FindAnyObjectByType` + `c.Inject(...)`, `FindObjectsInactive.Include`).

**VContainer ordering caveat:** `c.Inject(component)` runs *after* Unity's `Awake`/`OnEnable` for
instantiated/scene objects. So injected fields are guaranteed only by `Start`. Trigger behaviors must
read the injected router in `Start` or later, never in `Awake`/`OnEnable`. (Registration above runs
in a build callback, after the container is built, so it is safe.)

`PanelRegionRouter` is registered `AsImplementedInterfaces` so it can be an `IStartable`/`IDisposable`
if it needs to subscribe/unsubscribe `ModeChangedEvent` on the scope lifecycle.

---

## Per-surface integration

### Nav modules (behavior-preserving)
Each existing nav-target GameObject (Outliner, Settings, AssetBrowser, …) gets a `RegionMember` with
its `moduleId`; its `regionKey` (from `PanelRegionConfig`, migrated from the old `ExclusiveGroup`)
keeps the current exclusivity. `RegionNavButton` on each nav button calls `router.Toggle(moduleId)`.
Observable behavior is unchanged.

### Keyboard
The keyboard layout panel becomes a `RegionMember` (`moduleId = "keyboard"`). `KeyboardFocusOpener`
subscribes to `KeyboardFocusEvent` → `router.Open("keyboard")`, and closes on submit/blur. **Region
choice:** the keyboard gets its **own** region (`regionKey = "keyboard"`), so it does *not* hide the
body module that owns the focused field (you keep seeing the field you're typing into). This is a
deliberate change from the old full-content swap (`_defaultContent`↔`_keyboardContent`); to restore
the old "keyboard replaces body" behavior, set `regionKey = "body"` in the config — no code change.
`UserPanelKeyboardToggle` is deleted. `VrKeyboard` (the typing brain) is untouched.

### File browser
`SimpleFileBrowserCanvas.prefab` (the scene-resident dialog, already nested under UserPanel) gets:
- `RegionMember` (`moduleId = "fileBrowser"`, `regionKey = "dialog"` — its own region, so opening it
  does **not** hide the asset browser behind it);
- `FileBrowserSurface : MonoBehaviour, IRegionSurface` — `Show()` calls
  `FileBrowser.ShowLoadDialog(onSuccess, onCancel, PickMode.Files, "Import Asset", "Import")`;
  `Hide()` calls `FileBrowser.HideDialog()`; `IsOpen` returns `FileBrowser.IsOpen`. On `onSuccess` it
  publishes `FilePickedEvent { Path = paths[0] }`; `onCancel` calls `router.Close("fileBrowser")`.
- `FileBrowserVrAnchor` keeps positioning the dialog, but its target comes via DI, not `Find`.

`AssetBrowserPanel`: `OnAddClicked` becomes `_router.Open("fileBrowser")`; the import logic moves
behind a `FilePickedEvent` subscription (`HandleImportAsync(e.Path)`). The library import config
(pick mode, title) lives in `FileBrowserSurface` (the sole caller).

### `FileBrowserVrAnchor` DI fix (spec-B item 5)
- Scene scope: `RegisterInstance(assetBrowser)` (today it is only injected-into, not resolvable).
- `FileBrowserVrAnchor` gets `[Inject] public void Construct(AssetBrowserPanel target)`; delete the
  `Object.FindAnyObjectByType<AssetBrowserPanel>(...)` line; cache `target` and use its transform.
- Add `FileBrowserVrAnchor` to the scope's find-and-inject build callback.

---

## Migration approach & verification

1. Add core types (`IRegionSurface`, `RegionMember`, `PanelRegionRouter`) and rename
   `NavBarConfig`→`PanelRegionConfig` (GUID-safe; update `UserPanel`, `AnimatorPanelModuleBuilder`
   const/type, and the `.asset`). Compile clean.
2. Register router + config + discovery callback in both scene scopes.
3. Migrate nav modules: add `RegionMember`/`RegionNavButton`, move `ExclusiveGroup`→`regionKey`;
   strip the moved logic out of `UserPanel`. Verify nav behavior unchanged in `VrEditing`/`Sandbox`.
4. Migrate keyboard: add `RegionMember` + `KeyboardFocusOpener`; delete `UserPanelKeyboardToggle`.
   Verify focusing a field opens the keyboard and submit closes it.
5. Integrate file browser: add `RegionMember` + `FileBrowserSurface`; route `AssetBrowserPanel` "+"
   through the router; add `FilePickedEvent`; DI-fix `FileBrowserVrAnchor`. Verify import flow and
   VR positioning; **confirm the scene `SimpleFileBrowserCanvas` instance is used, not the Resources
   `_legacy` copy** (otherwise the adapter targets the wrong canvas).
6. After each batch: `read_console` shows no `CS####`; Test Runner green vs the 143/150 baseline
   (7 known unrelated failures); open `MainMenu`/`VrEditing`/`Sandbox` and confirm no "missing
   script" warnings and that region swapping works.

MCP moves/renames: verify real state with `Glob`/console — return strings are unreliable in this
repo (see project memory).

---

## Testing

- **`PanelRegionRouter` unit tests** (EditMode, no scene): drive with a fake `PanelRegionConfig` (or
  an injected map) and fake `IRegionSurface` doubles that record `Show`/`Hide`. Cover:
  - opening a module in an empty region shows it and records it open;
  - opening B in a region holding A hides A and shows B (mutual exclusion);
  - opening a module in a *different* region does not touch the first;
  - `Toggle` opens-then-closes;
  - `Close` clears the region slot;
  - `ModeChangedEvent` closes modules not visible in the new mode, leaves visible ones.
- **Manual scene verification** for the actual surfaces (nav swap, keyboard on focus, file browser
  open/import), since DI + prefab wiring + VR positioning are not unit-testable.

---

## Risks

- **VContainer inject-after-OnEnable** ordering (handled: register in build callback; behaviors read
  router in `Start`).
- **Inactive-module registration** (handled: discovery via `FindObjectsByType(..., Include)` in the
  scope, not `OnEnable`).
- **File browser uses Resources copy, not the scene instance** — would make the adapter/anchor target
  the wrong canvas. Mitigation: explicit verification step (#5) that the scene instance is the active
  singleton; remove/neutralize the `_legacy` Resources copy if it shadows it.
- **Keyboard region behavior change** (own region vs old full swap) is intentional and config-revertable;
  call it out during scene verification so the UX is confirmed, not assumed.
- **`PanelRegionConfig` rename fan-out** — `UserPanel._navBarConfig`, the `.asset`, and
  `AnimatorPanelModuleBuilder` (`NavBarConfigPath`/`NavBarConfig` type) must all move together;
  GUID-safe for the asset, type-name edits for the `.cs` (no namespaces → global names).
