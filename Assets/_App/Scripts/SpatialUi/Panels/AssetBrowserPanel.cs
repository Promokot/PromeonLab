using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class AssetBrowserPanel : MonoBehaviour
{
    [Header("Library Tabs")]
    [SerializeField] private Button _builtinTabButton;
    [SerializeField] private Button _importedTabButton;
    [SerializeField] private Button _savedTabButton;

    [Header("Grid")]
    [SerializeField] private Transform    _gridRoot;
    [SerializeField] private LabAssetCard _cardPrefab;
    [SerializeField] private Button       _addButton;
    [SerializeField] private Button       _spawnButton;

    [Header("Properties")]
    [SerializeField] private TMP_Text _propertiesText;

    private ModeOrchestrator     _orchestrator;
    private BuiltinAssetLibrary  _builtinLibrary;
    private ImportedAssetLibrary _importedLibrary;
    private SavedAssetLibrary    _savedLibrary;
    private EventBus             _bus;
    private PanelRegionRouter   _router;

    private IAssetLibrary _activeLibrary;
    private ILabAsset     _selectedAsset;
    private bool          _isEditableMode;
    private bool          _reopenAfterFileBrowser;

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

    private void Awake()
    {
        _builtinTabButton?.onClick.AddListener(() => SwitchLibrary(_builtinLibrary));
        _importedTabButton?.onClick.AddListener(() => SwitchLibrary(_importedLibrary));
        _savedTabButton?.onClick.AddListener(() => SwitchLibrary(_savedLibrary));
        _addButton?.onClick.AddListener(OnAddClicked);
        _spawnButton?.onClick.AddListener(OnSpawnClicked);
    }

    private void Start()
    {
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
        _bus?.Subscribe<AssetImportedEvent>(OnAssetImported);
        _bus?.Subscribe<RegionChangedEvent>(OnRegionChanged);
        _isEditableMode = _orchestrator?.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
        RefreshSpawnButton();
        if (_builtinLibrary != null)
            SwitchLibrary(_builtinLibrary);
    }

    private void OnDestroy()
    {
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);
        _bus?.Unsubscribe<AssetImportedEvent>(OnAssetImported);
        _bus?.Unsubscribe<RegionChangedEvent>(OnRegionChanged);
    }

    private void OnModeChanged(ModeChangedEvent e)
    {
        _isEditableMode = e.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
        _reopenAfterFileBrowser = false; // don't carry a pending return across a mode switch
        RefreshSpawnButton();
    }

    private void SwitchLibrary(IAssetLibrary library)
    {
        _activeLibrary = library;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        foreach (Transform child in _gridRoot)
            Destroy(child.gameObject);

        ClearSelection();
        ClearProperties();

        if (_activeLibrary == null || _cardPrefab == null) return;

        foreach (var asset in _activeLibrary.Assets)
        {
            var card = Instantiate(_cardPrefab, _gridRoot);
            card.Bind(asset);
            card.Selected += OnCardSelected;
        }
    }

    private void OnCardSelected(LabAssetCard card)
    {
        _selectedAsset = card.Asset;
        RefreshSpawnButton();
        ShowProperties(card.Asset);
    }

    private void ClearSelection()
    {
        _selectedAsset = null;
        RefreshSpawnButton();
    }

    private void RefreshSpawnButton()
    {
        if (_spawnButton != null)
            _spawnButton.interactable = _isEditableMode && _selectedAsset != null;
    }

    private void ShowProperties(ILabAsset asset)
    {
        if (_propertiesText == null) return;
        _propertiesText.text =
            $"Name: {asset.DisplayName}\n" +
            $"Type: {asset.Type}";
    }

    private void ClearProperties()
    {
        if (_propertiesText != null)
            _propertiesText.text = string.Empty;
    }

    private void OnSpawnClicked()
    {
        if (_selectedAsset == null || _bus == null) return;

        var cam = Camera.main?.transform;
        if (cam == null) return;

        var fwd = cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        else fwd.Normalize();

        var pos = new Vector3(
            cam.position.x + fwd.x * 1.2f,
            0f,
            cam.position.z + fwd.z * 1.2f);

        _bus.Publish(new AssetSpawnRequestedEvent
        {
            Asset    = _selectedAsset,
            Position = pos,
            Rotation = Quaternion.identity,
        });
    }

    private void OnAddClicked()
    {
        // We launch the file browser, which shares our `center_top` region and so hides us.
        // Flag a return so we re-open ourselves once it closes (success or cancel).
        _reopenAfterFileBrowser = true;
        _router?.Open("fileBrowser");
    }

    private void OnRegionChanged(RegionChangedEvent e)
    {
        // Re-open the asset browser after the file browser we launched closes. Guard on the
        // router's live state so unrelated region changes (or our own re-open) don't retrigger.
        if (_reopenAfterFileBrowser && _router != null && !_router.IsOpen("fileBrowser"))
        {
            _reopenAfterFileBrowser = false;
            _router.Open("assets");
        }
    }

    private void OnAssetImported(AssetImportedEvent e)
    {
        if (_activeLibrary == _importedLibrary)
            RefreshGrid();
    }
}
