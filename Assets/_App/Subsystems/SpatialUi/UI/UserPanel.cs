using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    [Serializable]
    public struct ContextMenuEntry
    {
        public AppMode   Mode;
        public GameObject Prefab;
    }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _exitButton;

    [Header("Modules")]
    [SerializeField] private SettingsModule   _settingsModule;
    [SerializeField] private Transform        _contextSlot;

    [Header("Context Menus")]
    [SerializeField] private ContextMenuEntry[] _contextMenus;

    private ModeOrchestrator _orchestrator;
    private EventBus         _bus;
    private GameObject       _currentContext;

    [Inject]
    public void Construct(ModeOrchestrator orchestrator, EventBus bus)
    {
        _orchestrator = orchestrator;
        _bus          = bus;
    }

    private void Start()
    {
        _mainMenuButton.onClick.AddListener(OnMainMenu);
        _settingsButton.onClick.AddListener(OnSettings);
        _exitButton.onClick.AddListener(OnExit);
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void OnDestroy() =>
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    private void OnMainMenu() => _orchestrator?.TransitionTo(AppMode.MainMenu);

    private void OnSettings() => _settingsModule?.Toggle();

    private void OnExit() => Application.Quit();

    private void OnModeChanged(ModeChangedEvent e) => SwapContext(e.CurrentMode);

    private void SwapContext(AppMode mode)
    {
        if (_currentContext != null)
        {
            Destroy(_currentContext);
            _currentContext = null;
        }

        if (_contextSlot == null) return;

        foreach (var entry in _contextMenus)
        {
            if (entry.Mode == mode && entry.Prefab != null)
            {
                _currentContext = Instantiate(entry.Prefab, _contextSlot);
                _currentContext.transform.localPosition = Vector3.zero;
                _currentContext.transform.localRotation = Quaternion.identity;
                break;
            }
        }
    }
}
