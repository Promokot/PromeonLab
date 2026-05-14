using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VContainer;

public class GizmoHandle : XRGrabInteractable
{
    private GizmoController _controller;
    private Transform _target;

    [Inject]
    public void Construct(GizmoController controller) => _controller = controller;

    public void SetTarget(Transform target) => _target = target;

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (_target != null)
            _controller.CommitMove(_target, _target.position);
    }
}
