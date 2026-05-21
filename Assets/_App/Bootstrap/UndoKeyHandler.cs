using UnityEngine;
using VContainer;

public class UndoKeyHandler : MonoBehaviour
{
    private CommandStack _commandStack;
    private EventBus     _bus;
    private bool         _dragActive;

    [Inject]
    public void Construct(CommandStack commandStack, EventBus bus)
    {
        _commandStack = commandStack;
        _bus          = bus;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<GizmoDragStartedEvent>(OnDragStarted);
        _bus.Subscribe<GizmoDragEndedEvent>(OnDragEnded);
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GizmoDragStartedEvent>(OnDragStarted);
        _bus.Unsubscribe<GizmoDragEndedEvent>(OnDragEnded);
    }

    private void OnDragStarted(GizmoDragStartedEvent _) => _dragActive = true;
    private void OnDragEnded  (GizmoDragEndedEvent   _) => _dragActive = false;

    private void Update()
    {
        if (_dragActive) return;
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.Z))
            _commandStack.Undo();
    }
}
