using System;
using System.IO;
using System.Threading;
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
        _bus?.Subscribe<FilePickedEvent>(OnFilePicked);
        _isEditableMode = _orchestrator?.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
        RefreshSpawnButton();
        if (_builtinLibrary != null)
            SwitchLibrary(_builtinLibrary);
    }

    private void OnDestroy()
    {
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);
        _bus?.Unsubscribe<FilePickedEvent>(OnFilePicked);
    }

    private void OnModeChanged(ModeChangedEvent e)
    {
        _isEditableMode = e.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
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

    private void OnAddClicked() => _router?.Open("fileBrowser");

    private void OnFilePicked(FilePickedEvent e) => _ = HandleImportAsync(e.Path);

    private async System.Threading.Tasks.Task HandleImportAsync(string filePath)
    {
        var asset = new ImportedLabAsset(
            id:          Guid.NewGuid().ToString("N")[..8],
            displayName: Path.GetFileNameWithoutExtension(filePath),
            type:        AssetType.Model,
            filePath:    filePath
        );

        _importedLibrary.Add(asset);
        await _importedLibrary.SaveAsync(CancellationToken.None);

        if (_activeLibrary == _importedLibrary)
            RefreshGrid();
    }
}
