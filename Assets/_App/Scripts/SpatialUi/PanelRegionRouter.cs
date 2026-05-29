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
