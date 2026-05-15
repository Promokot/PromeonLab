using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class MainMenuPanel : MonoBehaviour
{
    [SerializeField] private Button   _openSandboxButton;
    [SerializeField] private Button   _openSceneButton;
    [SerializeField] private TMP_Text _openSceneLabel;
    /*[SerializeField] private Button   _exitButton;*/

    private AppStorage       _storage;
    private EventBus         _bus;
    private ModeOrchestrator _orchestrator;
    private string           _selectedSceneId;

    [Inject]
    public void Construct(AppStorage storage, EventBus bus, ModeOrchestrator orchestrator)
    {
        _storage      = storage;
        _bus          = bus;
        _orchestrator = orchestrator;
    }

    private void Start()
    {
        _openSandboxButton.onClick.AddListener(OnOpenSandbox);
        _openSceneButton.onClick.AddListener(() => { _ = OpenSceneAsync(); });
        _openSceneButton.interactable = false;
        /*_exitButton.onClick.AddListener(OnExit);*/
        _bus.Subscribe<SceneSelectedEvent>(OnSceneSelected);
    }

    private void OnDestroy() =>
        _bus.Unsubscribe<SceneSelectedEvent>(OnSceneSelected);

    private void OnSceneSelected(SceneSelectedEvent e)
    {
        _selectedSceneId = e.SceneId;
        var hasScene = !string.IsNullOrEmpty(e.SceneId);
        _openSceneButton.interactable = hasScene;
        _openSceneLabel.text = hasScene ? $"Open  {e.DisplayName}" : "Open Scene";
    }

    /*private void OnExit() => Application.Quit();*/

    private void OnOpenSandbox()
    {
        var data = _storage.BeginSandboxSession();
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.Sandbox);
    }

    private async Task OpenSceneAsync()
    {
        if (string.IsNullOrEmpty(_selectedSceneId)) return;
        var data = await _storage.LoadSceneAsync(_selectedSceneId, CancellationToken.None);
        if (data == null) return;
        _storage.SetActiveScene(data);
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.VrEditing);
    }
}
