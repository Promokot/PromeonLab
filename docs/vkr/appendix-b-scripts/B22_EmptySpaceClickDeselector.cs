using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class EmptySpaceClickDeselector : MonoBehaviour
{
    [SerializeField] private NearFarInteractor _leftInteractor;
    [SerializeField] private NearFarInteractor _rightInteractor;

    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager) => _selectionManager = selectionManager;

    private void Update()
    {
        Check(_leftInteractor);
        Check(_rightInteractor);
    }

    private void Check(NearFarInteractor interactor)
    {
        if (interactor == null || _selectionManager == null) return;
        if (!interactor.activateInput.ReadWasPerformedThisFrame()) return;
        if (IsOverUI(interactor)) return;

        // TODO: restore Selectable-in-hovered check once UI guard is confirmed stable
        // foreach (var hovered in interactor.interactablesHovered)
        // {
        //     var go = (hovered as MonoBehaviour)?.gameObject;
        //     if (go != null && go.GetComponentInParent<Selectable>() != null) return;
        // }

        if (interactor.interactablesHovered.Count > 0) return;

        _selectionManager.Select(null);
    }

    // NearFarInteractor implements IUIInteractor – TryGetCurrentUIRaycastResult is available directly.
    // Old approach (GetComponentInChildren<XRRayInteractor>) was wrong – NearFarInteractor has no such child.
    // private static bool IsOverUI_Old(NearFarInteractor interactor)
    // {
    //     var ray = interactor.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
    //     if (ray != null && ray.TryGetCurrentUIRaycastResult(out var uiRaycast))
    //         return uiRaycast.gameObject != null;
    //     return false;
    // }
    private static bool IsOverUI(NearFarInteractor interactor) =>
        interactor.TryGetCurrentUIRaycastResult(out var r) && r.gameObject != null;
}
