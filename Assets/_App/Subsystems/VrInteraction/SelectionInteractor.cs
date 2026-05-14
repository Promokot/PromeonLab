using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VContainer;

[RequireComponent(typeof(Collider))]
public class SelectionInteractor : XRSimpleInteractable
{
    private ISelectionManager _selectionManager;
    private SceneNode _node;

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    private void Awake() => _node = GetComponentInParent<SceneNode>();

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        if (_node != null)
            _selectionManager.Select(_node.NodeId);
    }
}
