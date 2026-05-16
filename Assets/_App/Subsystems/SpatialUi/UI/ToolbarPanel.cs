using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ToolbarPanel : SpatialPanel
{
    [SerializeField] private Button _openAssetBrowserButton;
    [SerializeField] private Button _openSceneOutlinerButton;

    private UiPanelManager _panelManager;
    private UserPanel      _userPanel;

    [Inject]
    public void Construct(UiPanelManager panelManager, UserPanel userPanel)
    {
        _panelManager = panelManager;
        _userPanel    = userPanel;
    }

    private void Awake()
    {
        _openAssetBrowserButton.onClick.AddListener(OnAssetBrowserClicked);
        _openSceneOutlinerButton.onClick.AddListener(OnSceneOutlinerClicked);
    }

    private void OnAssetBrowserClicked() => _userPanel?.ToggleAssetsModule();

    private void OnSceneOutlinerClicked() =>
        _panelManager.GetPanel(PanelId.SceneOutliner)?.SetVisible(true);
}
