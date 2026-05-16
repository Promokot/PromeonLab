using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Transform   _gridRoot;
    [SerializeField] private LabAssetCard _cardPrefab;
    [SerializeField] private Button       _addButton;

    [Header("Properties")]
    [SerializeField] private Transform          _propertiesSlot;
    [SerializeField] private AssetPropertiesView _propertiesViewPrefab;

    private BuiltinAssetLibrary  _builtinLibrary;
    private ImportedAssetLibrary _importedLibrary;
    private SavedAssetLibrary    _savedLibrary;

    private IAssetLibrary        _activeLibrary;
    private AssetPropertiesView  _currentPropertiesView;

    private Vector3   _shownLocalPos;
    private Vector3   _hiddenLocalPos;
    private bool      _visible;
    private Coroutine _anim;

    public void Init(
        BuiltinAssetLibrary builtin,
        ImportedAssetLibrary imported,
        SavedAssetLibrary saved)
    {
        _builtinLibrary  = builtin;
        _importedLibrary = imported;
        _savedLibrary    = saved;
    }

    private void Awake()
    {
        _shownLocalPos  = transform.localPosition;
        _hiddenLocalPos = _shownLocalPos - Vector3.up * _slideDist;

        transform.localPosition     = _hiddenLocalPos;
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _builtinTabButton.onClick.AddListener(() => SwitchLibrary(_builtinLibrary));
        _importedTabButton.onClick.AddListener(() => SwitchLibrary(_importedLibrary));
        _savedTabButton.onClick.AddListener(() => SwitchLibrary(_savedLibrary));
        _addButton.onClick.AddListener(OnAddClicked);
    }

    private void Start()
    {
        if (_builtinLibrary != null)
            SwitchLibrary(_builtinLibrary);
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

        HideProperties();

        if (_activeLibrary == null || _cardPrefab == null) return;

        foreach (var asset in _activeLibrary.Assets)
        {
            var card = Instantiate(_cardPrefab, _gridRoot);
            card.Bind(asset);
            card.Selected += OnCardSelected;
        }
    }

    private void OnCardSelected(LabAssetCard card) => ShowProperties(card.Asset);

    private void ShowProperties(ILabAsset asset)
    {
        HideProperties();
        if (_propertiesViewPrefab == null || _propertiesSlot == null) return;
        _currentPropertiesView = Instantiate(_propertiesViewPrefab, _propertiesSlot);
        _currentPropertiesView.Bind(asset);
    }

    private void HideProperties()
    {
        if (_currentPropertiesView == null) return;
        Destroy(_currentPropertiesView.gameObject);
        _currentPropertiesView = null;
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
