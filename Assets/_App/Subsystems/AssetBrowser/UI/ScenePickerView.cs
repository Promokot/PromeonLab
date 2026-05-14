using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ScenePickerView : MonoBehaviour
{
    [SerializeField] private Transform _listRoot;
    [SerializeField] private GameObject _sceneItemPrefab;
    [SerializeField] private MainMenuPanel _menuPanel;

    private AppStorage _storage;
    private EventBus _bus;
    private ModeOrchestrator _orchestrator;

    [Inject]
    public void Construct(AppStorage storage, EventBus bus, ModeOrchestrator orchestrator)
    {
        _storage      = storage;
        _bus          = bus;
        _orchestrator = orchestrator;
    }

    private void Start()
    {
        _menuPanel.CreateRequested += OnCreateRequested;
        Refresh();
    }

    private void OnDestroy() => _menuPanel.CreateRequested -= OnCreateRequested;

    private void Refresh()
    {
        foreach (Transform child in _listRoot)
            Destroy(child.gameObject);

        foreach (var sceneId in _storage.GetAllSceneIds())
            SpawnSceneItem(sceneId);
    }

    private void SpawnSceneItem(string sceneId)
    {
        var item  = Instantiate(_sceneItemPrefab, _listRoot);
        var label = item.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null) label.text = sceneId;

        var btn = item.GetComponentInChildren<Button>();
        if (btn != null)
            btn.onClick.AddListener(async () =>
            {
                var data = await _storage.LoadSceneAsync(sceneId, CancellationToken.None);
                if (data != null) OpenScene(data);
            });
    }

    private async void OnCreateRequested(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) sceneName = "New Scene";
        var data = await _storage.CreateSceneAsync(sceneName, CancellationToken.None);
        OpenScene(data);
    }

    private void OpenScene(SceneData data)
    {
        _storage.SetActiveScene(data);
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.VrEditing);
    }
}
