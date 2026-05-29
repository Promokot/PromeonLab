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
