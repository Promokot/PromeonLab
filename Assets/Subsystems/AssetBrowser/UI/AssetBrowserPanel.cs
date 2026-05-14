using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SimpleFileBrowser;

public class AssetBrowserPanel : SpatialPanel
{
    [SerializeField] private Transform _listRoot;
    [SerializeField] private GameObject _assetItemPrefab;
    [SerializeField] private Button _importButton;

    private AssetImporter _importer;
    private EventBus _bus;

    [Inject]
    public void Construct(AssetImporter importer, EventBus bus)
    {
        _importer = importer;
        _bus      = bus;
    }

    private void Start()
    {
        _importButton.onClick.AddListener(OnImportClicked);
        _bus.Subscribe<AssetImportedEvent>(OnAssetImported);
    }

    private void OnDestroy() =>
        _bus.Unsubscribe<AssetImportedEvent>(OnAssetImported);

    private void OnImportClicked()
    {
        FileBrowser.ShowLoadDialog(
            onSuccess: paths => _ = HandleImportAsync(paths[0]),
            onCancel:  () => { },
            pickMode:  FileBrowser.PickMode.Files,
            title:     "Select a model",
            loadButtonText: "Import"
        );
    }

    private async System.Threading.Tasks.Task HandleImportAsync(string path)
    {
        var (go, entry) = await _importer.ImportAsync(path, CancellationToken.None);
        if (go != null)
        {
            _bus.Publish(new AssetImportedEvent { AssetId = entry.AssetId });
        }
        else
        {
            _bus.Publish(new ErrorOccurredEvent
            {
                Level   = ErrorLevel.Warning,
                Message = $"'{System.IO.Path.GetFileName(path)}' is not available in the demo catalog."
            });
        }
    }

    private void OnAssetImported(AssetImportedEvent e) => RefreshList();

    private void RefreshList()
    {
        foreach (Transform child in _listRoot)
            Destroy(child.gameObject);
        // Phase 5 will populate from SceneGraph
    }
}
