using System;
using VContainer.Unity;

public class UnsavedChangesGuard : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private bool _isDirty;

    public bool IsDirty => _isDirty;

    public UnsavedChangesGuard(EventBus bus) => _bus = bus;

    public void Start()
    {
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SceneOpenedEvent>(OnOpened);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SceneOpenedEvent>(OnOpened);
    }

    public bool CanNavigate() => !_isDirty;

    public void ClearDirty() => _isDirty = false;

    private void OnModified(SceneModifiedEvent _) => _isDirty = true;
    private void OnOpened(SceneOpenedEvent _)    => _isDirty = false;
}
