using UnityEngine;
using UnityEngine.InputSystem;

public class UserPanelOpener : MonoBehaviour
{
    private InputAction _toggle;
    private UserPanel   _panel;

    private void Awake()
    {
        _panel = GetComponentInChildren<UserPanel>(true);

        _toggle = new InputAction("ToggleUserPanel", InputActionType.Button);
        _toggle.AddBinding("<XRController>{LeftHand}/primaryButton");
        _toggle.AddBinding("<XRController>{RightHand}/primaryButton");
    }

    private void OnEnable()
    {
        _toggle.Enable();
        _toggle.performed += OnToggle;
    }

    private void OnDisable()
    {
        _toggle.performed -= OnToggle;
        _toggle.Disable();
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        if (_panel == null) return;

        var go = _panel.gameObject;
        if (go.activeSelf)
        {
            go.SetActive(false);
        }
        else
        {
            _panel.ResetPosition();
            go.SetActive(true);
        }
    }
}
