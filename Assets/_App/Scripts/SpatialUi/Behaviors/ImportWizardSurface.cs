using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ImportWizardSurface : MonoBehaviour, IRegionSurface
{
    [Header("Wizard UI")]
    [SerializeField] private TMP_Text       _fileNameLabel;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Toggle         _objectToggle;
    [SerializeField] private Toggle         _rigToggle;
    [SerializeField] private Toggle         _referenceToggle;
    [SerializeField] private Button         _importButton;
    [SerializeField] private Button         _cancelButton;

    private EventBus          _bus;
    private PanelRegionRouter _router;
    private string            _filePath;
    private bool              _open;

    public bool IsOpen => _open;

    [Inject]
    public void Construct(EventBus bus, PanelRegionRouter router)
    {
        _bus    = bus;
        _router = router;
    }

    private void Awake()
    {
        _importButton?.onClick.AddListener(OnImport);
        _cancelButton?.onClick.AddListener(OnCancel);
    }

    private void OnEnable()  => _bus?.Subscribe<ImportRequestedEvent>(OnImportRequested);
    private void OnDisable() => _bus?.Unsubscribe<ImportRequestedEvent>(OnImportRequested);

    private void OnImportRequested(ImportRequestedEvent e)
    {
        _filePath = e.FilePath;
        if (_fileNameLabel != null) _fileNameLabel.text = System.IO.Path.GetFileName(e.FilePath);
        if (_nameInput != null)     _nameInput.text     = e.SuggestedName;
        SetTypeSelection(e.SuggestedType);
        _router?.Open("importWizard");
    }

    public void Show() => _open = true;   // region router calls this when the region opens
    public void Hide() => _open = false;

    private void OnImport()
    {
        _bus?.Publish(new ImportConfirmedEvent
        {
            Confirmed   = true,
            FilePath    = _filePath,
            DisplayName = string.IsNullOrWhiteSpace(_nameInput?.text) ? System.IO.Path.GetFileNameWithoutExtension(_filePath) : _nameInput.text,
            ChosenType  = SelectedType(),
        });
        _router?.Close("importWizard");
    }

    private void OnCancel()
    {
        _bus?.Publish(new ImportConfirmedEvent { Confirmed = false, FilePath = _filePath });
        _router?.Close("importWizard");
    }

    private void SetTypeSelection(AssetType t)
    {
        if (_objectToggle    != null) _objectToggle.isOn    = t == AssetType.Object;
        if (_rigToggle       != null) _rigToggle.isOn       = t == AssetType.Rig;
        if (_referenceToggle != null) _referenceToggle.isOn = t == AssetType.Reference;
    }

    private AssetType SelectedType()
    {
        if (_rigToggle       != null && _rigToggle.isOn)       return AssetType.Rig;
        if (_referenceToggle != null && _referenceToggle.isOn) return AssetType.Reference;
        return AssetType.Object;
    }
}
