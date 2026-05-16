using UnityEngine;
using UnityEngine.UI;

public class UserPanelKeyboardToggle : MonoBehaviour
{
    [SerializeField] private Button     _keyboardButton;
    [SerializeField] private GameObject _defaultContent;
    [SerializeField] private GameObject _keyboardContent;

    private void Start()
    {
        _keyboardButton.onClick.AddListener(OnToggle);
        _keyboardContent.SetActive(false);
        _defaultContent.SetActive(true);
    }

    private void OnToggle()
    {
        var showKeyboard = !_keyboardContent.activeSelf;
        _keyboardContent.SetActive(showKeyboard);
        _defaultContent.SetActive(!showKeyboard);
    }
}
