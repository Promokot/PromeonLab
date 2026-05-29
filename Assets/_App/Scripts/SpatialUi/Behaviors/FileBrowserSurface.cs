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
        Debug.Log($"[FBDBG] FileBrowserSurface.Construct busNull={bus == null} routerNull={router == null}");
    }

    public bool IsOpen => FileBrowser.IsOpen;

    public void Show()
    {
        Debug.Log($"[FBDBG] FileBrowserSurface.Show ENTER activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} IsOpenBefore={FileBrowser.IsOpen}");
        if (FileBrowser.IsOpen) return;
        // Activate our in-hierarchy canvas BEFORE touching the static FileBrowser API.
        // SetActive(true) runs FileBrowser.Awake synchronously → registers m_instance, so the
        // Instance getter never hits its dead Resources fallback (the canvas prefab moved to
        // Content/ and the Resources copy is renamed *_legacy → Resources.Load returns null).
        if (!gameObject.activeSelf) gameObject.SetActive(true);
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
        Debug.Log($"[FBDBG] FileBrowserSurface.Show AFTER ShowLoadDialog IsOpen={FileBrowser.IsOpen}");
    }

    public void Hide()
    {
        if (FileBrowser.IsOpen) FileBrowser.HideDialog();
    }
}
