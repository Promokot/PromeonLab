using System;
using System.Collections.Generic;

// App-lifetime router for the persistent UserPanel nav model.
// Owns BOTH the module surfaces (panels) and the nav buttons, and is the single
// subscriber to ModeChangedEvent: it drives panel open/close AND button visibility
// + active-highlight purely from the current AppMode. Lives at root (Singleton) so it
// matches the persistent panel's lifetime — see RootLifetimeScope.
public class PanelRegionRouter : IDisposable
{
    private readonly IRegionConfig _config;
    private readonly EventBus _bus;
    private readonly Dictionary<string, IRegionSurface>  _modules = new();
    private readonly Dictionary<string, RegionNavButton> _buttons = new();
    private readonly Dictionary<string, string> _openByRegion = new();

    private AppMode _mode = AppMode.MainMenu;

    public PanelRegionRouter(IRegionConfig config, EventBus bus)
    {
        _config = config;
        _bus    = bus;
        _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    }

    public void Dispose() => _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    // --- registration -----------------------------------------------------

    public void RegisterModule(string moduleId, IRegionSurface surface)
    {
        if (string.IsNullOrEmpty(moduleId) || surface == null) return;
        _modules[moduleId] = surface;
        if (surface.IsOpen && _config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
            _openByRegion[region] = moduleId;
        ApplyButtonState(moduleId);
    }

    public void RegisterButton(RegionNavButton button)
    {
        if (button == null || string.IsNullOrEmpty(button.ModuleId)) return;
        _buttons[button.ModuleId] = button;
        ApplyButtonState(button.ModuleId);
    }

    // --- queries ----------------------------------------------------------

    public bool IsOpen(string moduleId) =>
        TryGetAlive(moduleId, out var s) && s.IsOpen;

    // --- open / close / toggle -------------------------------------------

    public void Open(string moduleId)
    {
        if (!TryGetAlive(moduleId, out var surface)) return;

        if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
        {
            if (_openByRegion.TryGetValue(region, out var current) && current != moduleId
                && TryGetAlive(current, out var currentSurface))
            {
                currentSurface.Hide();
                ApplyButtonState(current);
            }
            _openByRegion[region] = moduleId;
            surface.Show();
            ApplyButtonState(moduleId);
            _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = moduleId });
        }
        else
        {
            surface.Show();
            ApplyButtonState(moduleId);
        }
    }

    public void Close(string moduleId)
    {
        if (!TryGetAlive(moduleId, out var surface)) return;
        surface.Hide();
        ApplyButtonState(moduleId);

        if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region)
            && _openByRegion.TryGetValue(region, out var current) && current == moduleId)
        {
            _openByRegion.Remove(region);
            _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = null });

            if (_config.TryGetRegionDefault(region, out var def) && def != moduleId)
                Open(def);
        }
    }

    public void Toggle(string moduleId)
    {
        if (IsOpen(moduleId)) Close(moduleId);
        else Open(moduleId);
    }

    // --- mode-driven visibility ------------------------------------------

    public void ApplyMode(AppMode mode)
    {
        _mode = mode;

        // close any open module not visible in the new mode
        List<string> toClose = null;
        foreach (var kv in _modules)
            if (Alive(kv.Value) && kv.Value.IsOpen && !_config.IsVisibleInMode(kv.Key, mode))
                (toClose ??= new List<string>()).Add(kv.Key);
        if (toClose != null)
            foreach (var id in toClose) Close(id);

        // open each region's default surface when the region is otherwise empty and the
        // default is visible in this mode — the resting-state half of Close()'s reopen,
        // applied on mode entry/startup (otherwise a region with an inactive default member
        // stays blank until something else in it opens and closes).
        EnsureRegionDefaults(mode);

        // refresh every button's visibility + highlight for the new mode
        foreach (var kv in _buttons)
            ApplyButtonState(kv.Key);
    }

    private void EnsureRegionDefaults(AppMode mode)
    {
        HashSet<string> regions = null;
        foreach (var kv in _modules)
            if (_config.TryGetRegion(kv.Key, out var region) && !string.IsNullOrEmpty(region))
                (regions ??= new HashSet<string>()).Add(region);
        if (regions == null) return;

        foreach (var region in regions)
        {
            // region already holds a live, open module? leave it alone.
            if (_openByRegion.TryGetValue(region, out var open)
                && TryGetAlive(open, out var openSurface) && openSurface.IsOpen)
                continue;

            if (_config.TryGetRegionDefault(region, out var def)
                && _modules.ContainsKey(def)
                && _config.IsVisibleInMode(def, mode))
                Open(def);
        }
    }

    private void OnModeChanged(ModeChangedEvent e) => ApplyMode(e.CurrentMode);

    // --- helpers ----------------------------------------------------------

    private void ApplyButtonState(string moduleId)
    {
        if (!_buttons.TryGetValue(moduleId, out var button) || button == null) return;
        button.SetVisible(_config.IsVisibleInMode(moduleId, _mode));
        button.SetActiveHighlight(IsOpen(moduleId));
    }

    // Resolve a live surface; drops entries whose backing Unity object was destroyed
    // (e.g. a scene-bound member like the file browser after its scene unloaded).
    private bool TryGetAlive(string moduleId, out IRegionSurface surface)
    {
        if (_modules.TryGetValue(moduleId, out surface))
        {
            if (Alive(surface)) return true;
            _modules.Remove(moduleId);
        }
        surface = null;
        return false;
    }

    private static bool Alive(IRegionSurface s) =>
        s != null && !(s is UnityEngine.Object uo && uo == null);
}
