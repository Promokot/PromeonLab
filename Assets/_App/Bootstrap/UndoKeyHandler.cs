using UnityEngine;
using VContainer;

public class UndoKeyHandler : MonoBehaviour
{
    private CommandStack _commandStack;

    [Inject]
    public void Construct(CommandStack commandStack) => _commandStack = commandStack;

    private void Update()
    {
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.Z))
            _commandStack.Undo();
    }
}
