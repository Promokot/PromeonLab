using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ImportWizardPanel : MonoBehaviour, IRegionSurface
{
    [Header("Wizard UI")]
    [SerializeField] private TMP_Text       _fileNameLabel;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Toggle         _objectToggle;
    [SerializeField] private Toggle         _rigToggle;
    [SerializeField] private Toggle         _referenceToggle;
    [Header("Leaf-Bone Axis (Rig)")]
    [SerializeField] private Toggle _axisXToggle;
    [SerializeField] private Toggle _axisYToggle;
    [SerializeField] private Toggle _axisZToggle;
    [SerializeField] private Toggle _axisInvertToggle;
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
        // Subscribe at DI time, NOT in OnEnable: this panel stays hidden (GameObject
        // inactive) until an import is requested, so OnEnable never runs to wire the
        // subscription. EventBus invokes the delegate regardless of active state, so the
        // request still reaches us and we can self-activate via Open → Show.
        _bus?.Subscribe<ImportRequestedEvent>(OnImportRequested);
    }

    private void Awake()
    {
        _importButton?.onClick.AddListener(OnImport);
        _cancelButton?.onClick.AddListener(OnCancel);
    }

    private void OnDestroy() => _bus?.Unsubscribe<ImportRequestedEvent>(OnImportRequested);

    private void OnImportRequested(ImportRequestedEvent e)
    {
        _filePath = e.FilePath;
        if (_fileNameLabel != null) _fileNameLabel.text = System.IO.Path.GetFileName(e.FilePath);
        if (_nameInput != null)     _nameInput.text     = e.SuggestedName;
        SetTypeSelection(e.SuggestedType);
        ClearAxisSelection();
        _router?.Open("importWizard");
    }

    // Region router calls these when the region opens/closes. RegionMember delegates here
    // for custom surfaces and never touches SetActive itself, so we must toggle our own
    // GameObject — otherwise the wizard would stay invisible even when "open".
    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        _open = true;
    }

    public void Hide()
    {
        _open = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnImport()
    {
        _bus?.Publish(new ImportConfirmedEvent
        {
            Confirmed   = true,
            FilePath    = _filePath,
            DisplayName = string.IsNullOrWhiteSpace(_nameInput?.text) ? System.IO.Path.GetFileNameWithoutExtension(_filePath) : _nameInput.text,
            ChosenType  = SelectedType(),
            TerminalBonesAxis       = SelectedTerminalBonesAxis(),
            InvertTerminalBonesAxis = _axisInvertToggle != null && _axisInvertToggle.isOn,
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

    // Leaves every axis toggle off so the wizard opens in Auto (no explicit leaf-bone axis).
    // The Axis Toggle Group must have AllowSwitchOff enabled, otherwise Unity's EnsureValidState
    // force-selects the first toggle on enable and Auto can never be the default.
    private void ClearAxisSelection()
    {
        _axisXToggle?.SetIsOnWithoutNotify(false);
        _axisYToggle?.SetIsOnWithoutNotify(false);
        _axisZToggle?.SetIsOnWithoutNotify(false);
        _axisInvertToggle?.SetIsOnWithoutNotify(false);
    }

    private TerminalBoneAxis SelectedTerminalBonesAxis()
    {
        if (_axisXToggle != null && _axisXToggle.isOn) return TerminalBoneAxis.X;
        if (_axisYToggle != null && _axisYToggle.isOn) return TerminalBoneAxis.Y;
        if (_axisZToggle != null && _axisZToggle.isOn) return TerminalBoneAxis.Z;
        return TerminalBoneAxis.Auto; // no toggle selected = Auto (orient along parent direction)
    }
}
