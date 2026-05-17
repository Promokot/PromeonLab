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

        foreach (var hovered in interactor.interactablesHovered)
        {
            var go = (hovered as MonoBehaviour)?.gameObject;
            if (go == null) continue;
            if (go.GetComponentInParent<Selectable>() != null) return;
            if (go.GetComponentInParent<UnityEngine.UI.Graphic>() != null) return;
        }
        _selectionManager.Clear();
    }
}
