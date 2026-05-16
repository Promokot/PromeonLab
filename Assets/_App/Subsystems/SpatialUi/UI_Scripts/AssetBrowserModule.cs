using System;
using System.Collections;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SimpleFileBrowser;

public class AssetBrowserModule : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float       _slideDist = 0.05f;
    [SerializeField] private float       _duration  = 0.25f;

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

    private BuiltinAssetLibrary  _builtinLibrary;
    private ImportedAssetLibrary _importedLibrary;
    private SavedAssetLibrary    _savedLibrary;
    private EventBus             _bus;

    private IAssetLibrary _activeLibrary;
    private ILabAsset     _selectedAsset;
    private bool          _isEditableMode;

    private Vector3   _shownLocalPos;
    private Vector3   _hiddenLocalPos;
    private bool      _visible;
    private Coroutine _anim;

    [Inject]
    public void Construct(BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved, EventBus bus)
    {
        _builtinLibrary  = builtin;
        _importedLibrary = imported;
        _savedLibrary    = saved;
        _bus             = bus;
    }

    private void Awake()
    {
        _shownLocalPos  = transform.localPosition;
        _hiddenLocalPos = _shownLocalPos - Vector3.up * _slideDist;

        transform.localPosition     = _hiddenLocalPos;
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _builtinTabButton?.onClick.AddListener(() => SwitchLibrary(_builtinLibrary));
        _importedTabButton?.onClick.AddListener(() => SwitchLibrary(_importedLibrary));
        _savedTabButton?.onClick.AddListener(() => SwitchLibrary(_savedLibrary));
        _addButton?.onClick.AddListener(OnAddClicked);
        _spawnButton?.onClick.AddListener(OnSpawnClicked);
    }

    private void Start()
    {
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
        if (_builtinLibrary != null)
            SwitchLibrary(_builtinLibrary);
    }

    private void OnDestroy() =>
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    private void OnModeChanged(ModeChangedEvent e)
    {
        _isEditableMode = e.CurrentMode is AppMode.VrEditing or AppMode.Sandbox;
        RefreshSpawnButton();
    }

    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        gameObject.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(true));
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(false));
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
        FileBrowser.ShowLoadDialog(
            onSuccess:      paths => _ = HandleImportAsync(paths[0]),
            onCancel:       () => { },
            pickMode:       FileBrowser.PickMode.Files,
            title:          "Import Asset",
            loadButtonText: "Import"
        );
    }

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

    private IEnumerator AnimRoutine(bool show)
    {
        var startAlpha = _canvasGroup.alpha;
        var endAlpha   = show ? 1f : 0f;
        var startPos   = transform.localPosition;
        var endPos     = show ? _shownLocalPos : _hiddenLocalPos;

        _canvasGroup.interactable   = show;
        _canvasGroup.blocksRaycasts = show;

        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / _duration);
            _canvasGroup.alpha      = Mathf.Lerp(startAlpha, endAlpha, p);
            transform.localPosition = Vector3.Lerp(startPos, endPos, p);
            yield return null;
        }

        _canvasGroup.alpha      = endAlpha;
        transform.localPosition = endPos;

        if (!show) gameObject.SetActive(false);
    }
}
