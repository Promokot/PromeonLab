using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class ScenePickerView : MonoBehaviour
{
    [SerializeField] private Transform _listRoot;
    [SerializeField] private GameObject _sceneItemPrefab;
    [SerializeField] private Button _createButton;
    [SerializeField] private TMP_InputField _nameInput;

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
        _createButton.onClick.AddListener(OnCreateClicked);
        Refresh();
    }

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
        var label = item.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = sceneId;

        var btn = item.GetComponentInChildren<Button>();
        if (btn != null)
            btn.onClick.AddListener(async () =>
            {
                var data = await _storage.LoadSceneAsync(sceneId, CancellationToken.None);
                if (data != null) OpenScene(data);
            });
    }

    private async void OnCreateClicked()
    {
        var name = _nameInput.text;
        if (string.IsNullOrWhiteSpace(name)) name = "New Scene";
        var data = await _storage.CreateSceneAsync(name, CancellationToken.None);
        OpenScene(data);
    }

    private void OpenScene(SceneData data)
    {
        _storage.SetActiveScene(data);
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.VrEditing);
    }
}
