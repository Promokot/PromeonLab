using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class WorldClickCatcher : MonoBehaviour
{
    [SerializeField] private XRRayInteractor _leftRay;
    [SerializeField] private XRRayInteractor _rightRay;
    [SerializeField] private InputActionReference _leftSelectAction;
    [SerializeField] private InputActionReference _rightSelectAction;

    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager) => _selectionManager = selectionManager;

    private void OnEnable()
    {
        if (_leftSelectAction != null)  _leftSelectAction.action.performed  += OnLeft;
        if (_rightSelectAction != null) _rightSelectAction.action.performed += OnRight;
    }

    private void OnDisable()
    {
        if (_leftSelectAction != null)  _leftSelectAction.action.performed  -= OnLeft;
        if (_rightSelectAction != null) _rightSelectAction.action.performed -= OnRight;
    }

    private void OnLeft(InputAction.CallbackContext _)  => CheckRay(_leftRay);
    private void OnRight(InputAction.CallbackContext _) => CheckRay(_rightRay);

    private void CheckRay(XRRayInteractor ray)
    {
        if (ray == null || _selectionManager == null) return;
        if (ray.TryGetCurrent3DRaycastHit(out var hit))
        {
            if (hit.collider.GetComponentInParent<Selectable>() == null
                && hit.collider.GetComponentInParent<UnityEngine.UI.Graphic>() == null)
                _selectionManager.Clear();
        }
        else
        {
            _selectionManager.Clear();
        }
    }
}
