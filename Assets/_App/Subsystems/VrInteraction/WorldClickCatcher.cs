using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class WorldClickCatcher : MonoBehaviour
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

        // TODO: re-enable UI guard when NearFarInteractor.TryGetCurrentUIRaycastResult is wired up properly.
        // The child-XRRayInteractor lookup below was unreliable — GetComponentInChildren returned null.
        // if (IsOverUI(interactor)) return;
        // foreach (var hovered in interactor.interactablesHovered)
        // {
        //     var go = (hovered as MonoBehaviour)?.gameObject;
        //     if (go != null && go.GetComponentInParent<Selectable>() != null) return;
        // }

        // Temporary: clear only when clicking truly empty 3D space (nothing hovered).
        if (interactor.interactablesHovered.Count > 0) return;

        _selectionManager.Clear();
    }

    // private static bool IsOverUI(NearFarInteractor interactor)
    // {
    //     var ray = interactor.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
    //     if (ray != null && ray.TryGetCurrentUIRaycastResult(out var uiRaycast))
    //         return uiRaycast.gameObject != null;
    //     return false;
    // }
}
