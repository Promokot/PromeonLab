using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ToolbarPanel : SpatialPanel
{
    [SerializeField] private Button _openAssetBrowserButton;
    [SerializeField] private Button _openSceneOutlinerButton;

    private UiPanelManager _panelManager;

    [Inject]
    public void Construct(UiPanelManager panelManager)
    {
        _panelManager = panelManager;
    }

    private void Awake()
    {
        _openAssetBrowserButton.onClick.AddListener(OnAssetBrowserClicked);
        _openSceneOutlinerButton.onClick.AddListener(OnSceneOutlinerClicked);
    }

    private void OnAssetBrowserClicked() =>
        _panelManager.GetPanel(PanelId.AssetBrowser)?.SetVisible(true);

    private void OnSceneOutlinerClicked() =>
        _panelManager.GetPanel(PanelId.SceneOutliner)?.SetVisible(true);
}
