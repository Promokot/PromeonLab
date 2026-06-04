using UnityEngine;
using UnityEngine.InputSystem;

public class UserPanelOpener : MonoBehaviour
{
    private InputAction _toggle;
    private UserPanel   _panel;

    private void Awake()
    {
        _panel = GetComponentInChildren<UserPanel>(true);

        // The panel may ship ENABLED in the prefab (easier to author and inspect), but at runtime it
        // must start hidden – the player opens it on demand with the toggle. Hide it here in Awake,
        // before any Start runs, so UserPanel.Start (detach-to-world + DontDestroyOnLoad, gated on the
        // first activation) still defers to the first real open exactly as it did when the prefab
        // shipped inactive. GetComponentInChildren(true) above already found it regardless of state.
        if (_panel != null && _panel.gameObject.activeSelf)
            _panel.gameObject.SetActive(false);

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
