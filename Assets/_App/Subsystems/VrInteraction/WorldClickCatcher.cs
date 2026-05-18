using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class WorldClickCatcher : MonoBehaviour
{
    [SerializeField] private NearFarInteractor _leftInteractor;
    [SerializeField] private NearFarInteractor _rightInteractor;

    private ISelectionManager _selectionManager;
    private bool _leftWasActive;
    private bool _rightWasActive;

    [Inject]
    public void Construct(ISelectionManager selectionManager) => _selectionManager = selectionManager;

    private void OnEnable()
    {
        _leftWasActive  = _leftInteractor  != null && _leftInteractor.isSelectActive;
        _rightWasActive = _rightInteractor != null && _rightInteractor.isSelectActive;
    }

    private void Update()
    {
        Check(_leftInteractor,  ref _leftWasActive);
        Check(_rightInteractor, ref _rightWasActive);
    }

    private void Check(NearFarInteractor interactor, ref bool wasActive)
    {
        if (interactor == null || _selectionManager == null) return;

        var isActive    = interactor.isSelectActive;
        var justPressed = isActive && !wasActive;
        wasActive = isActive;

        if (!justPressed) return;

        // Guard 1: UI hit — selection survives UI navigation (button taps, outliner rows, etc.)
        if (IsOverUI(interactor)) return;

        // Guard 2: hovering a Selectable in 3D — not "empty space"
        foreach (var hovered in interactor.interactablesHovered)
        {
            var go = (hovered as MonoBehaviour)?.gameObject;
            if (go != null && go.GetComponentInParent<Selectable>() != null) return;
        }

        _selectionManager.Clear();
    }

    private static bool IsOverUI(NearFarInteractor interactor)
    {
        var ray = interactor.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null && ray.TryGetCurrentUIRaycastResult(out var uiRaycast))
            return uiRaycast.gameObject != null;
        return false;
    }
}
