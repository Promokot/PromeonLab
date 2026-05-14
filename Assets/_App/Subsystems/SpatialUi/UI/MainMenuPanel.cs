using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class MainMenuPanel : MonoBehaviour
{
    [SerializeField] private Button _openEditorButton;
    [SerializeField] private Button _createButton;
    [SerializeField] private TMP_InputField _nameInput;

    public event Action<string> CreateRequested;

    private ModeOrchestrator _orchestrator;

    [Inject]
    public void Construct(ModeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    private void Awake()
    {
        _openEditorButton.onClick.AddListener(() => _orchestrator.TransitionTo(AppMode.VrEditing));
        _createButton.onClick.AddListener(() => CreateRequested?.Invoke(_nameInput.text));
    }
}
