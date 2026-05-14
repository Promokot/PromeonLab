using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class MainMenuPanel : MonoBehaviour
{
    [SerializeField] private Button _openEditorButton;

    private ModeOrchestrator _orchestrator;

    [Inject]
    public void Construct(ModeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    private void Awake() =>
        _openEditorButton.onClick.AddListener(() => _orchestrator.TransitionTo(AppMode.VrEditing));
}
