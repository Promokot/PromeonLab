using System.Threading;
using System.Threading.Tasks;
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
    [SerializeField] private LabAsset_Item _cardPrefab;
    [SerializeField] private Button       _addButton;
    [SerializeField] private Button       _spawnButton;
    [SerializeField] private Button       _removeButton; // "RemoveBtn" – deletes the selected asset

    [Header("Properties")]
    [SerializeField] private TMP_Text _propertiesText;

    private ModeOrchestrator     _orchestrator;
    private BuiltinAssetLibrary  _builtinLibrary;
    private ImportedAssetLibrary _importedLibrary;
    private SavedAssetLibrary    _savedLibrary;
    private EventBus             _bus;
    private PanelRegionRouter   _router;
    private ImportedSourceProvider    _sources;

    private IAssetLibrary _activeLibrary;
    private ILabAsset     _selectedAsset;
    private LabAsset_Item  _selectedCard;
    private bool          _isEditableMode;
    private bool          _reopenAfterFileBrowser;
    private readonly System.Collections.Generic.Dictionary<string, Sprite> _thumbCache = new();

    [Inject]
    public void Construct(ModeOrchestrator orchestrator, BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved, EventBus bus, PanelRegionRouter router, ImportedSourceProvider sources)
    {
        _orchestrator    = orchestrator;
        _builtinLibrary  = builtin;
        _importedLibrary = imported;
        _savedLibrary    = saved;
        _bus             = bus;
        _router          = router;
        _sources         = sources;
    }

    private void Awake()
    {
        _builtinTabButton?.onClick.AddListener(() => SwitchLibrary(_builtinLibrary));
        _importedTabButton?.onClick.AddListener(() => SwitchLibrary(_importedLibrary));
        _savedTabButton?.onClick.AddListener(() => SwitchLibrary(_savedLibrary));
        _addButton?.onClick.AddListener(OnAddClicked);
        _spawnButton?.onClick.AddListener(OnSpawnClicked);
        _removeButton?.onClick.AddListener(OnRemoveClicked);
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
            card.Bind(asset, ResolveIcon(asset));
            card.Selected += OnCardSelected;
        }
    }

    private void OnCardSelected(LabAsset_Item card)
    {
        if (_selectedCard != null && _selectedCard != card)
            _selectedCard.SetSelected(false);

        _selectedCard = card;
        _selectedCard.SetSelected(true);

        _selectedAsset = card.Asset;
        RefreshSpawnButton();
        RefreshRemoveButton();
        ShowProperties(card.Asset);
    }

    private void ClearSelection()
    {
        if (_selectedCard != null)
            _selectedCard.SetSelected(false);

        _selectedCard  = null;
        _selectedAsset = null;
        RefreshSpawnButton();
        RefreshRemoveButton();
    }

    private void RefreshSpawnButton()
    {
        if (_spawnButton != null)
            _spawnButton.interactable = _isEditableMode && _selectedAsset != null;
    }

    private void RefreshRemoveButton()
    {
        // Deletable only for non-Builtin libraries (Imported/Saved) and only with a selection.
        // Mode-independent: you can prune libraries from the menu too.
        if (_removeButton != null)
            _removeButton.interactable =
                _selectedAsset != null && _activeLibrary != null && _activeLibrary != (IAssetLibrary)_builtinLibrary;
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
            // Aim the spawned object's +Z back toward the player so we face its front, not its
            // back. (-fwd points from the spawn point back to the camera; up stays world-up.)
            Rotation = Quaternion.LookRotation(-fwd, Vector3.up),
        });
    }

    private void OnRemoveClicked()
    {
        var asset   = _selectedAsset;
        var library = _activeLibrary;
        if (asset == null || library == null || library == (IAssetLibrary)_builtinLibrary) return;

        _ = DeleteAssetAsync(asset, library, CancellationToken.None);
    }

    // Removes the record from its library, persists the change, and deletes the raw source file.
    // Fire-and-forget from the click handler; exceptions are logged rather than swallowed.
    private async Task DeleteAssetAsync(ILabAsset asset, IAssetLibrary library, CancellationToken ct)
    {
        try
        {
            library.Remove(asset.Id);
            await library.SaveAsync(ct);
            _sources?.Delete(asset.SourceRef);
            RefreshGrid();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
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
        // React ONLY to OUR region (the one "assets"/"importWizard" live in). Without this guard an
        // unrelated region emptying – e.g. the VR keyboard (region "overlays") closing – also fires
        // with OpenModuleId == null and would wrongly reopen "assets", stomping an open import wizard.
        if (_router == null || !_router.TryGetModuleRegion("assets", out var myRegion) || e.RegionKey != myRegion)
            return;

        // Re-open the asset browser once the shared region empties after the file browser flow – but
        // ONLY when nothing else claimed the region. A successful pick hands the region to the import
        // wizard (OpenModuleId == "importWizard"); reopening here would stomp it. We wait until the
        // region actually empties (OpenModuleId == null), which is when the file browser is cancelled
        // or the wizard finishes – then the browser returns.
        if (_reopenAfterFileBrowser
            && string.IsNullOrEmpty(e.OpenModuleId) && !_router.IsOpen("fileBrowser"))
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

    // Builtin assets carry an inspector sprite; imported assets carry a relative ThumbnailRef
    // (a rendered model PNG, or the source image for References). Loaded sprites are cached by ref.
    private Sprite ResolveIcon(ILabAsset asset)
    {
        if (asset.Icon != null) return asset.Icon;

        var refPath = asset.ThumbnailRef;
        if (string.IsNullOrEmpty(refPath)) return null;

        if (_thumbCache.TryGetValue(refPath, out var cached)) return cached;

        Sprite sprite = null;
        try
        {
            var abs = _sources.AbsolutePath(refPath);
            if (System.IO.File.Exists(abs))
            {
                var bytes = System.IO.File.ReadAllBytes(abs);
                var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"AssetBrowserPanel: failed to load thumbnail '{refPath}'. {ex.Message}");
        }

        _thumbCache[refPath] = sprite;   // cache null too – don't retry a broken ref every rebuild
        return sprite;
    }
}
