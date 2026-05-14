using UnityEngine;

public class TransformCommand : ICommand
{
    private readonly Transform _target;
    private readonly Vector3 _newPosition;
    private readonly Quaternion _newRotation;
    private readonly Vector3 _newScale;
    private readonly Vector3 _oldPosition;
    private readonly Quaternion _oldRotation;
    private readonly Vector3 _oldScale;

    public TransformCommand(Transform target, Vector3 newPos, Quaternion newRot, Vector3 newScale)
    {
        _target      = target;
        _oldPosition = target.position;
        _oldRotation = target.rotation;
        _oldScale    = target.localScale;
        _newPosition = newPos;
        _newRotation = newRot;
        _newScale    = newScale;
    }

    public void Execute()
    {
        _target.position   = _newPosition;
        _target.rotation   = _newRotation;
        _target.localScale = _newScale;
    }

    public void Undo()
    {
        _target.position   = _oldPosition;
        _target.rotation   = _oldRotation;
        _target.localScale = _oldScale;
    }
}
