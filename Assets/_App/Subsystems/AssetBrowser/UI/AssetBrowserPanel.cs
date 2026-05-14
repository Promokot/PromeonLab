using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SimpleFileBrowser;

public class AssetBrowserPanel : SpatialPanel
{
    [SerializeField] private Transform  _listRoot;
    [SerializeField] private GameObject _assetItemPrefab;
    [SerializeField] private Button     _importButton;

    private AssetImporter     _importer;
    private EventBus          _bus;
    private SceneGraph        _sceneGraph;
    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(AssetImporter importer, EventBus bus, SceneGraph sceneGraph, ISelectionManager selectionManager)
    {
        _importer         = importer;
        _bus              = bus;
        _sceneGraph       = sceneGraph;
        _selectionManager = selectionManager;
    }

    private void Start()
    {
        _importButton.onClick.AddListener(OnImportClicked);
        _bus.Subscribe<SceneModifiedEvent>(OnSceneModified);
    }

    private void OnDestroy() =>
        _bus.Unsubscribe<SceneModifiedEvent>(OnSceneModified);

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
        var (go, _) = await _importer.ImportAsync(path, CancellationToken.None);
        if (go == null)
        {
            _bus.Publish(new ErrorOccurredEvent
            {
                Level   = ErrorLevel.Warning,
                Message = $"'{System.IO.Path.GetFileName(path)}' is not available in the demo catalog."
            });
        }
    }

    private void OnSceneModified(SceneModifiedEvent _) => RefreshList();

    private void RefreshList()
    {
        foreach (Transform child in _listRoot)
            Destroy(child.gameObject);

        foreach (var (nodeId, node) in _sceneGraph.Nodes)
        {
            var go   = Instantiate(_assetItemPrefab, _listRoot);
            var item = go.GetComponent<AssetBrowserItem>();
            item.Init(nodeId, node.gameObject.name);
            item.Clicked += OnItemClicked;
        }
    }

    private void OnItemClicked(AssetBrowserItem item) =>
        _selectionManager.Select(item.NodeId);
}
