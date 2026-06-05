using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// UserPanel module for exporting the current scene.
/// Mirrors the AnimatorPanel injection pattern: root-scope deps injected via Construct(),
/// subscriptions opened/closed in OnEnable/OnDisable.
///
/// The prefab must be wired in the editor (see handoff doc):
///   - _fileNameInput  → TMP_InputField for the desired file name
///   - _pathLabel      → TMP_Text showing the computed output path (read-only)
///   - _sceneNameLabel → TMP_Text showing the active scene display name (read-only)
///   - _exportButton   → Button that triggers the export
///   - _statusLabel    → TMP_Text for last export status / error (read-only)
/// </summary>
public class ExportPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField _fileNameInput;
    [SerializeField] private TMP_Text       _pathLabel;
    [SerializeField] private TMP_Text       _sceneNameLabel;
    [SerializeField] private Button         _exportButton;
    [SerializeField] private TMP_Text       _statusLabel;

    private EventBus      _bus;
    private SceneContext  _ctx;
    private SceneExporter _exporter;
    private AppStorage    _storage;

    [Inject]
    public void Construct(EventBus bus, SceneContext ctx, SceneExporter exporter, AppStorage storage)
    {
        _bus      = bus;
        _ctx      = ctx;
        _exporter = exporter;
        _storage  = storage;
    }

    private void OnEnable()
    {
        if (_bus == null) return;

        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Subscribe<SceneExportedEvent>(OnExported);

        if (_fileNameInput != null)
            _fileNameInput.onValueChanged.AddListener(OnFileNameChanged);

        if (_exportButton != null)
            _exportButton.onClick.AddListener(OnExportClicked);

        RefreshSceneInfo();
        RefreshPathLabel();
    }

    private void OnDisable()
    {
        if (_bus == null) return;

        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Unsubscribe<SceneExportedEvent>(OnExported);

        if (_fileNameInput != null)
            _fileNameInput.onValueChanged.RemoveListener(OnFileNameChanged);

        if (_exportButton != null)
            _exportButton.onClick.RemoveListener(OnExportClicked);
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void OnSceneContextChanged(SceneContextChangedEvent _) => RefreshSceneInfo();

    private void OnFileNameChanged(string _) => RefreshPathLabel();

    private void OnExportClicked()
    {
        if (_bus == null) return;
        var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
        _bus.Publish(new SceneExportRequestedEvent { FileName = name });

        if (_statusLabel != null)
            _statusLabel.text = "Exporting…";
        if (_exportButton != null)
            _exportButton.interactable = false;
    }

    private void OnExported(SceneExportedEvent e)
    {
        if (_statusLabel != null)
            _statusLabel.text = e.Message;
        if (_exportButton != null)
            _exportButton.interactable = true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void RefreshSceneInfo()
    {
        var sceneId = _storage?.ActiveSceneId;
        var scene   = !string.IsNullOrEmpty(sceneId) ? _storage.GetCachedScene(sceneId) : null;
        var name    = scene?.DisplayName ?? "–";

        if (_sceneNameLabel != null)
            _sceneNameLabel.text = name;

        // Default file-name suggestion to the scene's display name (once, only when blank).
        if (_fileNameInput != null && string.IsNullOrEmpty(_fileNameInput.text) && scene != null)
            _fileNameInput.text = scene.DisplayName;

        RefreshPathLabel();
    }

    private void RefreshPathLabel()
    {
        if (_pathLabel == null || _exporter == null) return;
        var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
        _pathLabel.text = _exporter.BuildTargetPath(name);
    }
}
