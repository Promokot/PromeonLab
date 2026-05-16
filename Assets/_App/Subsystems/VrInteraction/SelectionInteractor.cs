using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VContainer;

[RequireComponent(typeof(Collider))]
public class SelectionInteractor : XRSimpleInteractable
{
    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        var selectable = GetComponentInParent<Selectable>();
        if (selectable != null && _selectionManager != null)
            _selectionManager.Toggle(selectable.NodeId);
    }
}
