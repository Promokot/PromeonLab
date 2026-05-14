using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class UiPanelManager : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private readonly PanelRegistry _registry;
    private readonly Transform _cameraTransform;
    private readonly IObjectResolver _resolver;

    private readonly Dictionary<PanelId, SpatialPanel> _panels = new();
    private AppMode _currentMode = AppMode.VrEditing;

    public UiPanelManager(EventBus bus, PanelRegistry registry, Camera mainCamera, IObjectResolver resolver)
    {
        _bus             = bus;
        _registry        = registry;
        _cameraTransform = mainCamera.transform;
        _resolver        = resolver;
    }

    public void Start()
    {
        _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
        SpawnPanels();
    }

    public void Dispose()
    {
        _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void SpawnPanels()
    {
        foreach (var entry in _registry.Panels)
        {
            var panel = _resolver.Instantiate(entry.Prefab);
            panel.Init(entry.Id, _cameraTransform);
            _panels[entry.Id] = panel;
        }
        RefreshVisibility();
    }

    private void OnModeChanged(ModeChangedEvent e)
    {
        _currentMode = e.CurrentMode;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        foreach (var (id, panel) in _panels)
            panel.SetVisible(_registry.IsVisibleIn(id, _currentMode));
    }

    public SpatialPanel GetPanel(PanelId id) =>
        _panels.TryGetValue(id, out var p) ? p : null;
}
