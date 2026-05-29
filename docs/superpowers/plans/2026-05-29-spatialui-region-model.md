# SpatialUi Unified Region Model — Implementation Plan (B1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to
> implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the three ad-hoc panel-opening mechanisms (UserPanel nav `SetActive`,
`UserPanelKeyboardToggle` content swap, direct `FileBrowser.ShowLoadDialog`) with one registry-driven
region model: a `PanelRegionRouter` enforces "one open surface per region", and nav buttons, the
keyboard button, and the asset browser all open/close through it.

**Architecture:** Plain-C# `PanelRegionRouter` (SceneLifetimeScope) keyed on a logical region string
from `NavBarConfig` (via new `IRegionConfig`). Each opanable surface carries a `RegionMember`
(MonoBehaviour implementing `IRegionSurface`, default = `SetActive`, delegating to a sibling
`IRegionSurface` adapter when present). Modules are discovered + registered in a scope build callback
(covers inactive objects). Trigger behaviors (`RegionNavButton` — used by nav buttons *and* the keyboard
button — and `AssetBrowserPanel`) call the router; the file browser participates via a
`FileBrowserSurface` adapter over the third-party `SimpleFileBrowser` modal.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces, single `_App.Runtime` assembly), VContainer DI,
custom `EventBus`, OpenXR, NUnit (EditMode tests in `_App.Tests`), SimpleFileBrowser (ThirdParty).

**Spec:** `docs/superpowers/specs/2026-05-29-spatialui-region-model-design.md` (read its 2026-05-29
planning addendum — keyboard is button-toggled, `NavBarConfig` is reused, not renamed).

**Execution context:** Runs against the live Unity Editor via Unity MCP, one subagent at a time.
- Scripts: create/edit the `.cs` file, then `refresh_unity(compile=request, scope=scripts)`, then
  `read_console(types=[error], filter_text="CS")` — **only `CS####` entries are real errors**; ignore
  `MCP-FOR-UNITY: Client handler exited` / disposed-object churn.
- Prefab/scene edits: Unity MCP `manage_prefabs` / `manage_gameobject` / `manage_components`. MCP move/
  edit return strings are unreliable in this repo — **verify real state with `Glob`/`read_console`**.
- Verify-gate after each task: no `CS####`; `run_tests` (EditMode) green vs **baseline 143 passed /
  7 failed** (7 known unrelated: PathProviderTests×4, RingRotateStrategyTests×2,
  PromeonProxyRigBuilderTests×1); for surface tasks, open `VrEditing` + `Sandbox` scenes and confirm no
  "missing script" warnings.
- Branch: `feature/spatialui-region-model`. Commit per task. Never commit `main`/`dev`.

---

## File Structure

**Create (runtime, `Assets/_App/Scripts/SpatialUi/`):**
- `IRegionSurface.cs` — `{ void Show(); void Hide(); bool IsOpen { get; } }`.
- `IRegionConfig.cs` — `{ bool TryGetRegion(string,out string); bool IsVisibleInMode(string,AppMode); }`.
- `PanelRegionRouter.cs` — region open/close/exclusion authority (plain C#).
- `RegionMember.cs` — per-module registrar + default `IRegionSurface`.
- `Events/RegionChangedEvent.cs` — `{ string RegionKey; string OpenModuleId; }`.
- `Events/FilePickedEvent.cs` — `{ string Path; }`.
- `Behaviors/RegionNavButton.cs` — nav button → `router.Toggle`, per-mode visibility, brightness
  (the keyboard button reuses this — no keyboard-specific class).
- `Behaviors/FileBrowserSurface.cs` — `IRegionSurface` adapter over SimpleFileBrowser.

**Create (tests, `Assets/_App/Tests/SpatialUi/`):**
- `PanelRegionRouterTests.cs`.

**Modify:**
- `Scripts/SpatialUi/NavBarConfig.cs` — implement `IRegionConfig`.
- `Scripts/SpatialUi/Panels/UserPanel.cs` — remove nav/exclusion/brightness/mode logic.
- `Scripts/SpatialUi/Panels/AssetBrowserPanel.cs` — open via router, import via `FilePickedEvent`.
- `Scripts/SpatialUi/Behaviors/FileBrowserVrAnchor.cs` — inject target instead of `Find`.
- `Scripts/Bootstrap/VrEditingSceneScope.cs`, `SandboxSceneScope.cs` — register router/config/discovery.
- `Scripts/Bootstrap/RootLifetimeScope.cs` — drop root-level `AssetBrowserPanel` inject.

**Delete:**
- `Scripts/SpatialUi/Behaviors/UserPanelKeyboardToggle.cs`.

**Prefab/scene (Unity MCP):**
- `Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab` — add `RegionMember` to each nav module + to
  `_keyboardContent`; add `RegionNavButton` to each nav button and to the keyboard button.
- `Content/Prefabs/UI/Panels/UserPanel/SimpleFileBrowserCanvas.prefab` — add `RegionMember` +
  `FileBrowserSurface`.
- `DefaultNavBarConfig.asset` — add `keyboard`, `fileBrowser` entries.
- Both scene scope GameObjects — assign the `_navBarConfig` field.

---

## Task 1: Region interfaces + NavBarConfig adopts IRegionConfig

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/IRegionSurface.cs`
- Create: `Assets/_App/Scripts/SpatialUi/IRegionConfig.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/NavBarConfig.cs`

- [ ] **Step 1: Create `IRegionSurface.cs`**

```csharp
public interface IRegionSurface
{
    void Show();
    void Hide();
    bool IsOpen { get; }
}
```

- [ ] **Step 2: Create `IRegionConfig.cs`**

```csharp
public interface IRegionConfig
{
    bool TryGetRegion(string moduleId, out string regionKey);
    bool IsVisibleInMode(string moduleId, AppMode mode);
}
```

- [ ] **Step 3: Make `NavBarConfig` implement `IRegionConfig`**

Edit the class declaration and add `TryGetRegion`. `IsVisibleInMode(string, AppMode)` already exists
and matches the interface. Final file:

```csharp
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/NavBarConfig")]
public class NavBarConfig : ScriptableObject, IRegionConfig
{
    [Serializable]
    public struct Entry
    {
        public string    Id;
        public AppMode[] VisibleModes;
        public string    ExclusiveGroup;
    }

    [SerializeField] private Entry[] _entries;

    public bool TryGetEntry(string id, out Entry entry)
    {
        if (_entries != null)
            foreach (var e in _entries)
                if (e.Id == id) { entry = e; return true; }
        entry = default;
        return false;
    }

    public bool TryGetRegion(string moduleId, out string regionKey)
    {
        if (TryGetEntry(moduleId, out var e)) { regionKey = e.ExclusiveGroup; return true; }
        regionKey = null;
        return false;
    }

    public bool IsVisibleInMode(string id, AppMode mode)
    {
        if (!TryGetEntry(id, out var e)) return false;
        if (e.VisibleModes == null) return false;
        foreach (var m in e.VisibleModes)
            if (m == mode) return true;
        return false;
    }
}
```

- [ ] **Step 4: Compile and check console**

`refresh_unity(compile=request, scope=scripts)` then `read_console(types=[error], filter_text="CS")`.
Expected: no `CS####` entries.

- [ ] **Step 5: Commit**

```bash
git add Assets/_App/Scripts/SpatialUi/IRegionSurface.cs Assets/_App/Scripts/SpatialUi/IRegionConfig.cs Assets/_App/Scripts/SpatialUi/NavBarConfig.cs Assets/_App/Scripts/SpatialUi/IRegionSurface.cs.meta Assets/_App/Scripts/SpatialUi/IRegionConfig.cs.meta
git commit -m "feat(ui): add IRegionSurface + IRegionConfig; NavBarConfig implements IRegionConfig"
```

---

## Task 2: PanelRegionRouter (TDD)

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/PanelRegionRouter.cs`
- Test: `Assets/_App/Tests/SpatialUi/PanelRegionRouterTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/_App/Tests/SpatialUi/PanelRegionRouterTests.cs`. Uses plain NUnit (project style: no
namespace, constructor injection, `new EventBus()`). Fakes for `IRegionConfig` and `IRegionSurface`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;

public class PanelRegionRouterTests
{
    private class FakeConfig : IRegionConfig
    {
        public readonly Dictionary<string, string> Regions = new();
        public readonly Dictionary<string, AppMode[]> Visible = new();
        public bool TryGetRegion(string id, out string region) => Regions.TryGetValue(id, out region);
        public bool IsVisibleInMode(string id, AppMode mode)
        {
            if (!Visible.TryGetValue(id, out var modes) || modes == null) return false;
            foreach (var m in modes) if (m == mode) return true;
            return false;
        }
    }

    private class FakeSurface : IRegionSurface
    {
        public int ShowCalls, HideCalls;
        public bool IsOpen { get; private set; }
        public void Show() { ShowCalls++; IsOpen = true; }
        public void Hide() { HideCalls++; IsOpen = false; }
    }

    private EventBus _bus;
    private FakeConfig _config;
    private PanelRegionRouter _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _config = new FakeConfig();
        _sut = new PanelRegionRouter(_config, _bus);
    }

    [Test]
    public void Open_ShowsModule()
    {
        _config.Regions["a"] = "body";
        var a = new FakeSurface();
        _sut.Register("a", a);
        _sut.Open("a");
        Assert.IsTrue(a.IsOpen);
        Assert.AreEqual(1, a.ShowCalls);
    }

    [Test]
    public void Open_SecondInSameRegion_HidesFirst()
    {
        _config.Regions["a"] = "body";
        _config.Regions["b"] = "body";
        var a = new FakeSurface(); var b = new FakeSurface();
        _sut.Register("a", a); _sut.Register("b", b);
        _sut.Open("a");
        _sut.Open("b");
        Assert.IsFalse(a.IsOpen);
        Assert.IsTrue(b.IsOpen);
    }

    [Test]
    public void Open_DifferentRegion_LeavesFirstOpen()
    {
        _config.Regions["a"] = "body";
        _config.Regions["dialog"] = "dialog";
        var a = new FakeSurface(); var d = new FakeSurface();
        _sut.Register("a", a); _sut.Register("dialog", d);
        _sut.Open("a");
        _sut.Open("dialog");
        Assert.IsTrue(a.IsOpen);
        Assert.IsTrue(d.IsOpen);
    }

    [Test]
    public void Toggle_OpensThenCloses()
    {
        _config.Regions["a"] = "body";
        var a = new FakeSurface();
        _sut.Register("a", a);
        _sut.Toggle("a");
        Assert.IsTrue(a.IsOpen);
        _sut.Toggle("a");
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void Open_AfterClose_ReopensInSameRegion()
    {
        _config.Regions["a"] = "body";
        _config.Regions["b"] = "body";
        var a = new FakeSurface(); var b = new FakeSurface();
        _sut.Register("a", a); _sut.Register("b", b);
        _sut.Open("a");
        _sut.Close("a");
        _sut.Open("b");
        Assert.IsTrue(b.IsOpen);
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void Open_PublishesRegionChangedEvent()
    {
        _config.Regions["a"] = "body";
        RegionChangedEvent received = default; bool fired = false;
        _bus.Subscribe<RegionChangedEvent>(e => { received = e; fired = true; });
        var a = new FakeSurface();
        _sut.Register("a", a);
        _sut.Open("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("body", received.RegionKey);
        Assert.AreEqual("a", received.OpenModuleId);
    }

    [Test]
    public void ModeChanged_ClosesModuleNotVisibleInNewMode()
    {
        _config.Regions["a"] = "body";
        _config.Visible["a"] = new[] { AppMode.VrEditing };
        var a = new FakeSurface();
        _sut.Register("a", a);
        _sut.Open("a");
        _bus.Publish(new ModeChangedEvent { CurrentMode = AppMode.MainMenu });
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void ModeChanged_KeepsModuleVisibleInNewMode()
    {
        _config.Regions["a"] = "body";
        _config.Visible["a"] = new[] { AppMode.VrEditing, AppMode.MainMenu };
        var a = new FakeSurface();
        _sut.Register("a", a);
        _sut.Open("a");
        _bus.Publish(new ModeChangedEvent { CurrentMode = AppMode.MainMenu });
        Assert.IsTrue(a.IsOpen);
    }
}
```

> Before writing, confirm `ModeChangedEvent` has a `CurrentMode` field of type `AppMode` (it does —
> `UserPanel.OnModeChanged` reads `e.CurrentMode`). Create `Events/RegionChangedEvent.cs` (next step)
> before running tests so the test compiles.

- [ ] **Step 2: Create `Events/RegionChangedEvent.cs`**

```csharp
public struct RegionChangedEvent
{
    public string RegionKey;
    public string OpenModuleId;
}
```

- [ ] **Step 3: Run tests to verify they fail**

`run_tests(mode=EditMode)` (or via Test Runner). Expected: `PanelRegionRouterTests` fail to compile /
fail because `PanelRegionRouter` does not exist yet.

- [ ] **Step 4: Implement `PanelRegionRouter.cs`**

```csharp
using System;
using System.Collections.Generic;

public class PanelRegionRouter : IDisposable
{
    private readonly IRegionConfig _config;
    private readonly EventBus _bus;
    private readonly Dictionary<string, IRegionSurface> _modules = new();
    private readonly Dictionary<string, string> _openByRegion = new();

    public PanelRegionRouter(IRegionConfig config, EventBus bus)
    {
        _config = config;
        _bus    = bus;
        _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    }

    public void Dispose() => _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    public void Register(string moduleId, IRegionSurface surface)
    {
        if (string.IsNullOrEmpty(moduleId) || surface == null) return;
        _modules[moduleId] = surface;
        if (surface.IsOpen && _config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
            _openByRegion[region] = moduleId;
    }

    public bool IsOpen(string moduleId) =>
        _modules.TryGetValue(moduleId, out var s) && s.IsOpen;

    public void Open(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var surface)) return;

        if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
        {
            if (_openByRegion.TryGetValue(region, out var current) && current != moduleId
                && _modules.TryGetValue(current, out var currentSurface))
                currentSurface.Hide();
            _openByRegion[region] = moduleId;
            surface.Show();
            _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = moduleId });
        }
        else
        {
            surface.Show();
        }
    }

    public void Close(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var surface)) return;
        surface.Hide();
        if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region)
            && _openByRegion.TryGetValue(region, out var current) && current == moduleId)
        {
            _openByRegion.Remove(region);
            _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = null });
        }
    }

    public void Toggle(string moduleId)
    {
        if (IsOpen(moduleId)) Close(moduleId);
        else Open(moduleId);
    }

    public void ApplyMode(AppMode mode)
    {
        var toClose = new List<string>();
        foreach (var kv in _modules)
            if (kv.Value.IsOpen && !_config.IsVisibleInMode(kv.Key, mode))
                toClose.Add(kv.Key);
        foreach (var id in toClose) Close(id);
    }

    private void OnModeChanged(ModeChangedEvent e) => ApplyMode(e.CurrentMode);
}
```

- [ ] **Step 5: Run tests to verify they pass**

`run_tests(mode=EditMode)`. Expected: 8 `PanelRegionRouterTests` pass; total still matches baseline +8
new passes (151 passed / 7 failed), no new failures.

- [ ] **Step 6: Commit**

```bash
git add Assets/_App/Scripts/SpatialUi/PanelRegionRouter.cs Assets/_App/Scripts/SpatialUi/PanelRegionRouter.cs.meta Assets/_App/Scripts/SpatialUi/Events/RegionChangedEvent.cs Assets/_App/Scripts/SpatialUi/Events/RegionChangedEvent.cs.meta Assets/_App/Tests/SpatialUi/PanelRegionRouterTests.cs Assets/_App/Tests/SpatialUi/PanelRegionRouterTests.cs.meta
git commit -m "feat(ui): add PanelRegionRouter with per-region mutual exclusion + tests"
```

---

## Task 3: RegionMember component

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/RegionMember.cs`

- [ ] **Step 1: Create `RegionMember.cs`**

```csharp
using UnityEngine;

public class RegionMember : MonoBehaviour, IRegionSurface
{
    [SerializeField] private string _moduleId;

    private IRegionSurface _custom;
    private bool _resolved;

    public string ModuleId => _moduleId;

    private IRegionSurface Custom
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                foreach (var s in GetComponents<IRegionSurface>())
                    if (!ReferenceEquals(s, this)) { _custom = s; break; }
            }
            return _custom;
        }
    }

    public bool IsOpen => Custom != null ? Custom.IsOpen : gameObject.activeSelf;

    public void Show()
    {
        if (Custom != null) Custom.Show();
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (Custom != null) Custom.Hide();
        else gameObject.SetActive(false);
    }
}
```

- [ ] **Step 2: Compile and check console**

`refresh_unity(compile=request, scope=scripts)` then `read_console(types=[error], filter_text="CS")`.
Expected: no `CS####`.

- [ ] **Step 3: Commit**

```bash
git add Assets/_App/Scripts/SpatialUi/RegionMember.cs Assets/_App/Scripts/SpatialUi/RegionMember.cs.meta
git commit -m "feat(ui): add RegionMember (per-module registrar + default surface)"
```

---

## Task 4: DI wiring + discovery in scene scopes

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Add region registrations to `VrEditingSceneScope`**

Add the serialized field at the top of the class (next to `_panelRegistry`):

```csharp
    [SerializeField] private NavBarConfig _navBarConfig;
```

At the **end** of `Configure(...)` (after the existing `gizmoToolsPanel` block), add:

```csharp
        // --- B1 region model ---
        if (_navBarConfig != null)
            builder.RegisterInstance(_navBarConfig).As<IRegionConfig>().AsSelf();
        builder.Register<PanelRegionRouter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        builder.RegisterBuildCallback(c =>
        {
            var router = c.Resolve<PanelRegionRouter>();

            foreach (var nav in Object.FindObjectsByType<RegionNavButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.Inject(nav);
            foreach (var fbs in Object.FindObjectsByType<FileBrowserSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.Inject(fbs);
            foreach (var anchor in Object.FindObjectsByType<FileBrowserVrAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.Inject(anchor);

            foreach (var rm in Object.FindObjectsByType<RegionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.Inject(rm);
                router.Register(rm.ModuleId, rm);
            }

            var modeOrch = c.Resolve<ModeOrchestrator>();
            router.ApplyMode(modeOrch.CurrentMode);
        });
```

> `RegionNavButton` and `FileBrowserSurface` are created in later tasks. This task may compile-fail that
> the build callback references types not yet present — so create the two behavior files as **empty
> stubs first** (Step 0 below). To keep the build green, do Step 0.

- [ ] **Step 0 (do first): Create empty behavior stubs so Task 4 compiles**

Create minimal stubs (filled in Tasks 5–7):

`Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs`:
```csharp
using UnityEngine;
public class RegionNavButton : MonoBehaviour { }
```
`Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserSurface.cs`:
```csharp
using UnityEngine;
public class FileBrowserSurface : MonoBehaviour, IRegionSurface
{
    public bool IsOpen => false;
    public void Show() { }
    public void Hide() { }
}
```

- [ ] **Step 2: Mirror the same additions in `SandboxSceneScope`**

Add the identical `[SerializeField] private NavBarConfig _navBarConfig;` field and the identical
`// --- B1 region model ---` block at the end of its `Configure(...)`.

- [ ] **Step 3: Drop the root-level `AssetBrowserPanel` inject**

In `RootLifetimeScope.Configure`, **remove** these lines (the scene scope will inject AssetBrowserPanel
with the scene-scoped router in Task 7; root cannot resolve a scene service):

```csharp
        var assetBrowser = Object.FindAnyObjectByType<AssetBrowserPanel>(FindObjectsInactive.Include);
        if (assetBrowser != null)
            builder.RegisterBuildCallback(c => c.Inject(assetBrowser));
```

- [ ] **Step 4: Assign `_navBarConfig` on both scene scope GameObjects**

Open `VrEditing.unity`, select the `VrEditingSceneScope` GameObject, set its `_navBarConfig` field to
`Assets/_App/Content/ScriptableObjects/DefaultNavBarConfig.asset`. Repeat in `Sandbox` scene for
`SandboxSceneScope`. (Unity MCP `manage_gameobject` set component field, or do it in the Inspector.)
Save both scenes.

- [ ] **Step 5: Compile + smoke test**

`refresh_unity(compile=request, scope=scripts)`; `read_console(types=[error], filter_text="CS")` → no
`CS####`. Enter Play in `VrEditing`; confirm no exceptions on load and existing UI still appears
(modules not yet migrated, so behavior unchanged — the router simply has nothing registered yet except
any pre-existing RegionMembers, which is none). `run_tests(mode=EditMode)` → 151/7.

- [ ] **Step 6: Commit**

```bash
git add Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserSurface.cs Assets/_App/Scenes/VrEditing.unity Assets/_App/Scenes/Sandbox.unity
git add Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs.meta Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserSurface.cs.meta
git commit -m "feat(ui): register PanelRegionRouter + module discovery in scene scopes"
```

---

## Task 5: Migrate nav modules to the router

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs`
- Prefab: `Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab`

- [ ] **Step 1: Enumerate the current nav bindings**

Inspect the `UserPanel` component on `UserPanel.prefab` (Unity MCP `manage_prefabs` open + read the
`UserPanel` serialized `_bindings`). Record, for each binding: `EntryId`, the `NavButton` GameObject,
and the `Panel` GameObject. Cross-check `EntryId`s against entries in `DefaultNavBarConfig.asset`
(each `EntryId` must have an entry with the right `ExclusiveGroup` = its region). Write the list into
the task notes — Steps 4–5 add one component per item.

- [ ] **Step 2: Implement `RegionNavButton.cs`**

Replaces UserPanel's per-button open/exclusion/brightness/visibility. Brightness logic
(`Brighten` + ColorBlocks) moves here, driven by `RegionChangedEvent`.

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class RegionNavButton : MonoBehaviour
{
    [SerializeField] private string _moduleId;
    [SerializeField] private Button _button;
    [SerializeField] [Range(0f, 2f)] private float _inactiveHoverBrightness = 1.2f;
    [SerializeField] [Range(0f, 2f)] private float _activeBrightness        = 0.6f;
    [SerializeField] [Range(0f, 2f)] private float _activeHoverBrightness   = 0.8f;

    private PanelRegionRouter _router;
    private IRegionConfig     _config;
    private ModeOrchestrator  _orchestrator;
    private EventBus          _bus;

    private ColorBlock _inactiveColors;
    private ColorBlock _activeColors;
    private string     _region;

    [Inject]
    public void Construct(PanelRegionRouter router, IRegionConfig config, ModeOrchestrator orchestrator, EventBus bus)
    {
        _router       = router;
        _config       = config;
        _orchestrator = orchestrator;
        _bus          = bus;
    }

    private void Start()
    {
        if (_button != null)
        {
            var baseColor = _button.colors.normalColor;
            var block     = _button.colors;

            _inactiveColors                 = block;
            _inactiveColors.normalColor      = baseColor;
            _inactiveColors.highlightedColor = Brighten(baseColor, _inactiveHoverBrightness);
            _inactiveColors.selectedColor    = baseColor;

            _activeColors                 = block;
            _activeColors.normalColor      = Brighten(baseColor, _activeBrightness);
            _activeColors.highlightedColor = Brighten(baseColor, _activeHoverBrightness);
            _activeColors.selectedColor    = Brighten(baseColor, _activeBrightness);

            _button.colors = _inactiveColors;
            _button.onClick.AddListener(OnClick);
        }

        if (_config != null) _config.TryGetRegion(_moduleId, out _region);
        _bus?.Subscribe<RegionChangedEvent>(OnRegionChanged);
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
        if (_orchestrator != null) ApplyMode(_orchestrator.CurrentMode);
        SetActiveColors(_router != null && _router.IsOpen(_moduleId));
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
        _bus?.Unsubscribe<RegionChangedEvent>(OnRegionChanged);
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void OnClick() => _router?.Toggle(_moduleId);

    private void OnRegionChanged(RegionChangedEvent e)
    {
        if (e.RegionKey == _region)
            SetActiveColors(e.OpenModuleId == _moduleId);
    }

    private void OnModeChanged(ModeChangedEvent e) => ApplyMode(e.CurrentMode);

    private void ApplyMode(AppMode mode)
    {
        var visible = _config != null && _config.IsVisibleInMode(_moduleId, mode);
        gameObject.SetActive(visible);
    }

    private void SetActiveColors(bool active)
    {
        if (_button != null) _button.colors = active ? _activeColors : _inactiveColors;
    }

    private static Color Brighten(Color c, float mult)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var vNew = mult >= 1f ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f)) : v * mult;
        var result = Color.HSVToRGB(h, s, vNew);
        result.a = c.a;
        return result;
    }
}
```

- [ ] **Step 3: Gut `UserPanel.cs`**

Remove everything related to nav bindings, exclusivity, brightness, and mode handling; keep
smart-follow, lock, main-menu/exit, drag. The `EventBus` dependency is no longer needed. Final file:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _exitButton;

    [Header("Lock")]
    [SerializeField] private Button _lockButton;
    [SerializeField] private Image  _lockButtonImage;

    [Header("Smart Follow")]
    [SerializeField] private float _recenterAngle     = 45f;
    [SerializeField] private float _smoothTime        = 0.5f;
    [SerializeField] private float _minDistance       = 0.25f;
    [SerializeField] private float _preferredDistance = 0.7f;
    [SerializeField] private float _maxDistance       = 1.25f;
    [SerializeField] private float _yOffset           = -0.15f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _faceBelowOffset   = 0.15f;

    private ModeOrchestrator _orchestrator;

    private bool     _locked;
    private bool     _initialized;
    private bool     _isDragging;
    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    private static readonly Color ColorUnlocked = new Color(0.62f, 1.00f, 0.77f, 0.90f);
    private static readonly Color ColorLocked   = new Color(1.00f, 0.42f, 0.42f, 0.90f);

    [Inject]
    public void Construct(ModeOrchestrator orchestrator) => _orchestrator = orchestrator;

    private void Start()
    {
        _mainMenuButton?.onClick.AddListener(OnMainMenu);
        _exitButton?.onClick.AddListener(OnExit);
        _lockButton?.onClick.AddListener(OnLockToggle);
    }

    protected override void LateUpdate()
    {
        if (_cameraTransform == null) return;
        if (!_isDragging && !_locked)
            UpdateSmartFollow();
        FaceCameraBelow();
    }

    private void UpdateSmartFollow()
    {
        if (!_initialized)
        {
            var fwd = GetCameraYawForward();
            transform.position = new Vector3(
                _cameraTransform.position.x + fwd.x * _preferredDistance,
                _cameraTransform.position.y + _yOffset,
                _cameraTransform.position.z + fwd.z * _preferredDistance);
            _initialized    = true;
            _followVelocity = Vector3.zero;
            return;
        }

        var camXZ   = new Vector3(_cameraTransform.position.x, 0f, _cameraTransform.position.z);
        var panelXZ = new Vector3(transform.position.x,        0f, transform.position.z);
        var delta   = panelXZ - camXZ;
        var xzDist  = delta.magnitude;

        if (xzDist > 0.001f)
        {
            var yaw   = GetCameraYawForward();
            var angle = Vector3.Angle(yaw, delta.normalized);

            if (angle > _recenterAngle)
            {
                var targetXZ = camXZ + yaw * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
            }
            else if (xzDist < _minDistance || xzDist > _maxDistance)
            {
                var targetXZ = camXZ + delta.normalized * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
            }
        }

        if (_activeTarget.HasValue)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _activeTarget.Value,
                ref _followVelocity, _smoothTime);

            if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
            {
                transform.position = _activeTarget.Value;
                _activeTarget      = null;
                _followVelocity    = Vector3.zero;
            }
        }
    }

    private Vector3 GetCameraYawForward()
    {
        var f = _cameraTransform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    private void FaceCameraBelow()
    {
        var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
        var dir    = transform.position - target;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void ResetPosition()
    {
        _initialized = false;
        _locked      = false;
        if (_lockButtonImage != null)
            _lockButtonImage.color = ColorUnlocked;
    }

    public void SetDragging(bool active)
    {
        _isDragging = active;
        if (!active)
        {
            _activeTarget   = null;
            _followVelocity = Vector3.zero;
        }
    }

    public void MoveDelta(Vector3 delta)
    {
        if (_isDragging)
            transform.position += delta;
    }

    private void OnLockToggle()
    {
        _locked = !_locked;
        if (_lockButtonImage != null)
            _lockButtonImage.color = _locked ? ColorLocked : ColorUnlocked;
    }

    private void OnMainMenu() => _orchestrator?.TransitionTo(AppMode.MainMenu);
    private void OnExit()     => Application.Quit();
}
```

> `RootLifetimeScope` injects `UserPanel` with `Construct(ModeOrchestrator)` — `ModeOrchestrator` is a
> root Singleton, so the narrowed signature still resolves. No scope edit needed for UserPanel.

- [ ] **Step 4: Compile**

`refresh_unity(compile=request, scope=scripts)`; `read_console(types=[error], filter_text="CS")` → no
`CS####`. (UserPanel's removed `_bindings`/`_navBarConfig` serialized data is dropped harmlessly.)

- [ ] **Step 5: Prefab — add `RegionMember` to each nav module, `RegionNavButton` to each nav button**

For each binding recorded in Step 1, on `UserPanel.prefab`:
- Add `RegionMember` to the module `Panel` GameObject; set `_moduleId` = the binding `EntryId`.
- Add `RegionNavButton` to the `NavButton` GameObject; set `_moduleId` = the binding `EntryId` and
  `_button` = that GameObject's `Button` component.
Verify via `manage_prefabs` read that each got the components with correct field values.

- [ ] **Step 6: Verify in scenes**

Open `VrEditing` and `Sandbox`, enter Play. Confirm: nav buttons open/close their panels; opening one
in an exclusive group closes its siblings (same as before); active/inactive button brightness matches
prior behavior; buttons hidden in modes where they were hidden before. `read_console` → no exceptions,
no "missing script". `run_tests(mode=EditMode)` → 151/7.

- [ ] **Step 7: Commit**

```bash
git add Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab"
git commit -m "feat(ui): migrate UserPanel nav modules to PanelRegionRouter via RegionNavButton"
```

---

## Task 6: Migrate the keyboard to a region module (no keyboard-specific logic)

The keyboard becomes a normal region module in the **same region as the swappable nav content
modules**, opened by a plain `RegionNavButton` on its existing button. No dedicated toggle class. Its
control is already a UnityUI `Button` (`UserPanelKeyboardToggle._keyboardButton` is `Button`), so there
is **no component swap** — only the behavior moves from `UserPanelKeyboardToggle` to `RegionNavButton`.

**Files:**
- Delete: `Assets/_App/Scripts/SpatialUi/Behaviors/UserPanelKeyboardToggle.cs`
- Asset: `DefaultNavBarConfig.asset`
- Prefab: `UserPanel.prefab`

- [ ] **Step 1: Determine the nav content region**

From Task 5's enumeration, identify the `ExclusiveGroup` shared by the swappable **content** modules
(the ones that occupy the main content area — e.g. Outliner / Settings / AssetBrowser). Call it
`<contentRegion>`. (If nav modules span more than one group, pick the group for the primary content
area where the keyboard should appear.) Record it.

- [ ] **Step 2: Add region entries to `DefaultNavBarConfig.asset`**

Add two entries (Unity MCP `manage_scriptable_object` or edit in Inspector), each
`VisibleModes = [VrEditing, Sandbox]`:
- `Id = "keyboard"`,    `ExclusiveGroup = "<contentRegion>"`  (the value from Step 1)
- `Id = "fileBrowser"`, `ExclusiveGroup = "dialog"`           (used in Task 7)

- [ ] **Step 3: Inspect, then delete `UserPanelKeyboardToggle.cs`**

First inspect the `UserPanelKeyboardToggle` component on `UserPanel.prefab` and record: the GameObject
it sits on, and its `_keyboardButton`, `_defaultContent`, `_keyboardContent` references. Then remove the
file (and its `.meta`).

- [ ] **Step 4: Prefab wiring on `UserPanel.prefab`**

- Add `RegionMember` to the `_keyboardContent` GameObject; `_moduleId = "keyboard"`. Authored initial
  state: **inactive**. (Manual positioning of the keyboard within the content area is expected — see
  the spec's region note; author it to sit where the content modules appear.)
- Add `RegionNavButton` to the GameObject that held `UserPanelKeyboardToggle`'s `_keyboardButton`
  (i.e. the keyboard button object); set `_moduleId = "keyboard"`, `_button` = that `Button`, and the
  **same brightness params** as the other nav buttons (`_inactiveHoverBrightness`, `_activeBrightness`,
  `_activeHoverBrightness`).
- Remove the now-orphaned `UserPanelKeyboardToggle` component from its GameObject.
- `_defaultContent` stays **always active** — it is the base/nav chrome; its child nav modules are
  individually `RegionMember`s (from Task 5). **Overlap check:** if `_defaultContent` carries a visible
  content-area background that the keyboard should cover, ensure the keyboard's authored panel covers it
  (the keyboard is the active module; default chrome stays behind). Verify visually in Step 5.

- [ ] **Step 5: Compile + verify**

`refresh_unity`; `read_console(filter_text="CS")` → none. Open `VrEditing`, Play:
- Tap the keyboard button → keyboard opens; any open nav content module closes (shared region).
- Tap the keyboard button again → keyboard closes; content region empty, nav bar still visible.
- Tap another nav module while keyboard is open → that module opens, keyboard closes (region swap).
- Typing still works (`VrInputFieldProxy`→`VrKeyboard` path untouched).
- No "missing script" (the deleted `UserPanelKeyboardToggle` was re-homed to `RegionNavButton`).
`run_tests(mode=EditMode)` → 151/7.

- [ ] **Step 6: Commit**

```bash
git add "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab" Assets/_App/Content/ScriptableObjects/DefaultNavBarConfig.asset
git rm Assets/_App/Scripts/SpatialUi/Behaviors/UserPanelKeyboardToggle.cs
git commit -m "feat(ui): make keyboard a region module via RegionNavButton; remove UserPanelKeyboardToggle"
```

---

## Task 7: Integrate the file browser + fix FileBrowserVrAnchor DI

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserSurface.cs`
- Create: `Assets/_App/Scripts/SpatialUi/Events/FilePickedEvent.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserVrAnchor.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`, `SandboxSceneScope.cs`
- Prefab: `SimpleFileBrowserCanvas.prefab`

- [ ] **Step 1: Create `Events/FilePickedEvent.cs`**

```csharp
public struct FilePickedEvent
{
    public string Path;
}
```

- [ ] **Step 2: Implement `FileBrowserSurface.cs`**

```csharp
using SimpleFileBrowser;
using UnityEngine;
using VContainer;

public class FileBrowserSurface : MonoBehaviour, IRegionSurface
{
    private EventBus          _bus;
    private PanelRegionRouter _router;

    [Inject]
    public void Construct(EventBus bus, PanelRegionRouter router)
    {
        _bus    = bus;
        _router = router;
    }

    public bool IsOpen => FileBrowser.IsOpen;

    public void Show()
    {
        if (FileBrowser.IsOpen) return;
        FileBrowser.ShowLoadDialog(
            onSuccess:      paths =>
            {
                if (paths != null && paths.Length > 0)
                    _bus?.Publish(new FilePickedEvent { Path = paths[0] });
                _router?.Close("fileBrowser");
            },
            onCancel:       () => _router?.Close("fileBrowser"),
            pickMode:       FileBrowser.PickMode.Files,
            title:          "Import Asset",
            loadButtonText: "Import");
    }

    public void Hide()
    {
        if (FileBrowser.IsOpen) FileBrowser.HideDialog();
    }
}
```

- [ ] **Step 3: Rewire `AssetBrowserPanel.cs`**

Add `PanelRegionRouter` to `Construct`; replace `OnAddClicked`'s direct dialog call with
`_router.Open("fileBrowser")`; subscribe to `FilePickedEvent` and import on it. Specific edits:

Add field:
```csharp
    private PanelRegionRouter _router;
```
Change `Construct` signature + body to also take and assign the router:
```csharp
    [Inject]
    public void Construct(ModeOrchestrator orchestrator, BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved, EventBus bus, PanelRegionRouter router)
    {
        _orchestrator    = orchestrator;
        _builtinLibrary  = builtin;
        _importedLibrary = imported;
        _savedLibrary    = saved;
        _bus             = bus;
        _router          = router;
    }
```
In `Start()`, add the subscription (next to the existing `ModeChangedEvent` subscribe):
```csharp
        _bus?.Subscribe<FilePickedEvent>(OnFilePicked);
```
In `OnDestroy()`, add the matching unsubscribe:
```csharp
        _bus?.Unsubscribe<FilePickedEvent>(OnFilePicked);
```
Replace `OnAddClicked` body:
```csharp
    private void OnAddClicked() => _router?.Open("fileBrowser");

    private void OnFilePicked(FilePickedEvent e) => _ = HandleImportAsync(e.Path);
```
(`HandleImportAsync(string)` already exists; the old `FileBrowser.ShowLoadDialog(...)` block is removed,
so the `using SimpleFileBrowser;` in this file becomes unused — remove it.)

- [ ] **Step 4: Fix `FileBrowserVrAnchor.cs` DI**

Replace the `Find` with constructor injection:
```csharp
using SimpleFileBrowser;
using UnityEngine;
using VContainer;

[RequireComponent(typeof(Canvas))]
public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private AssetBrowserPanel _target;

    [Inject]
    public void Construct(AssetBrowserPanel target) => _target = target;

    private void Start()
    {
        transform.localScale = Vector3.one * _scale;

        var canvas  = GetComponent<Canvas>();
        var mainCam = Camera.main;
        if (canvas != null && mainCam != null)
            canvas.worldCamera = mainCam;

        RepositionToTarget();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        if (!_target.gameObject.activeInHierarchy)
        {
            if (FileBrowser.IsOpen) FileBrowser.HideDialog();
            return;
        }

        RepositionToTarget();
    }

    private void RepositionToTarget()
    {
        if (_target == null) return;
        var t = _target.transform;
        transform.position = t.position - t.forward * _forwardOffset;
        transform.rotation = t.rotation;
    }
}
```

- [ ] **Step 5: Register `AssetBrowserPanel` as an instance in both scene scopes**

In `VrEditingSceneScope`, change the existing AssetBrowserPanel block (currently inject-only) to also
register the instance so `FileBrowserVrAnchor` can resolve it:
```csharp
        var assetBrowser = Object.FindAnyObjectByType<AssetBrowserPanel>(FindObjectsInactive.Include);
        if (assetBrowser != null)
        {
            builder.RegisterInstance(assetBrowser);
            builder.RegisterBuildCallback(c => c.Inject(assetBrowser));
        }
```
Apply the identical change in `SandboxSceneScope`. (The Task-4 build callback already injects
`FileBrowserSurface` and `FileBrowserVrAnchor`; AssetBrowserPanel's `Construct` now resolves the
scene-scoped `PanelRegionRouter`, which is why Task 4 removed the root-level inject.)

- [ ] **Step 6: Prefab wiring on `SimpleFileBrowserCanvas.prefab`**

- Add `FileBrowserSurface` component to the canvas root.
- Add `RegionMember` to the canvas root; `_moduleId = "fileBrowser"` (its `Custom` will resolve the
  sibling `FileBrowserSurface`, so Show/Hide route through the dialog API).
- Confirm `FileBrowserVrAnchor` is present (it already is).

- [ ] **Step 7: Compile + verify (incl. scene-instance check)**

`refresh_unity`; `read_console(filter_text="CS")` → none. Open `VrEditing`, Play:
- Tap "+" in the asset browser → the file dialog opens, positioned in front of the asset browser
  (anchor working via DI), asset browser still visible behind it (region `dialog`, no swap).
- Pick a file → it imports (the `FilePickedEvent` path), dialog closes.
- Cancel → dialog closes cleanly.
- **Verify the scene `SimpleFileBrowserCanvas` instance is the one used**, not the Resources
  `_legacy` copy: confirm the dialog that appears is the nested-prefab instance (check it carries our
  `FileBrowserVrAnchor`/`FileBrowserSurface` and is positioned in VR). If the Resources copy is used
  instead, the dialog will not be VR-positioned — in that case ensure the scene `FileBrowser` singleton
  instance exists/initializes before first `ShowLoadDialog` (it should, being in the scene).
`run_tests` → 151/7.

- [ ] **Step 8: Commit**

```bash
git add Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserSurface.cs Assets/_App/Scripts/SpatialUi/Events/FilePickedEvent.cs Assets/_App/Scripts/SpatialUi/Events/FilePickedEvent.cs.meta Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs Assets/_App/Scripts/SpatialUi/Behaviors/FileBrowserVrAnchor.cs Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/SimpleFileBrowserCanvas.prefab"
git commit -m "feat(ui): integrate file browser as region surface; DI-fix FileBrowserVrAnchor"
```

---

## Task 8: Final verification + docs

**Files:**
- Modify: `Assets/_App/Documentation/STRUCTURE.md`
- Modify: `Assets/_App/Documentation/conventions.md`

- [ ] **Step 1: Full regression pass**

Open `MainMenu`, `VrEditing`, `Sandbox`. For each: enter Play, confirm no exceptions / no "missing
script" warnings. Exercise: nav module open/close + exclusivity + per-mode visibility; keyboard 2-way
swap + typing; file browser open/import/cancel + VR positioning; mode transition
(`MainMenu`↔`VrEditing`) closes mode-hidden modules. `run_tests(mode=EditMode)` → 151 passed / 7 failed
(the 7 known unrelated).

- [ ] **Step 2: Update `STRUCTURE.md`**

Update the `SpatialUi/` subtree: add `PanelRegionRouter.cs`, `IRegionSurface.cs`, `IRegionConfig.cs`,
`RegionMember.cs` at root; `Behaviors/RegionNavButton.cs`, `Behaviors/FileBrowserSurface.cs`;
`Events/RegionChangedEvent.cs`, `Events/FilePickedEvent.cs`; remove
`Behaviors/UserPanelKeyboardToggle.cs`. Refresh counts.

- [ ] **Step 3: Update `conventions.md`**

In "SpatialUi Script Roles", note the region model: `PanelRegionRouter` (Framework), `IRegionSurface` /
`RegionMember` (Framework), region triggers in `Behaviors/`. Add one line: panels that share a region
key (`NavBarConfig.ExclusiveGroup`) are mutually exclusive, opened/closed via `PanelRegionRouter`.

- [ ] **Step 4: Commit**

```bash
git add Assets/_App/Documentation/STRUCTURE.md Assets/_App/Documentation/conventions.md
git commit -m "docs: document SpatialUi region model (router/surface/member)"
```

---

## Notes for the executor

- **Deferred (NOT in this plan):** detach add-on (`SpatialPanelDetachable` stays untouched; its
  detach is effectively dormant — UserPanel no longer reacts to `PanelDetachedEvent`/`PanelClosedEvent`,
  which is acceptable because detach is parked for a follow-up); `VrKeyboard` rename;
  `NavBarConfig`→`PanelRegionConfig` rename; context menu; IkWizard (sub-project B2).
- **`SpatialPanelDetachable` interaction:** UserPanel previously called `dp.EntryId = ...` and reacted
  to detach/close events. Those code paths are removed in Task 5. If any prefab module still carries a
  `SpatialPanelDetachable`, leave the component in place (dormant); the detach follow-up will adapt it.
- **MCP reliability:** never trust `manage_*` return strings — re-read with `manage_prefabs`/`Glob` and
  `read_console`. Only `CS####` are real compile errors.
