using UnityEngine;
using UnityEngine.UI;

public class GizmoToolsPanelOpener : MonoBehaviour
{
    [SerializeField] private Button     _toggleButton;
    [SerializeField] private GameObject _subPanel;

    private void Awake()
    {
        if (_toggleButton != null) _toggleButton.onClick.AddListener(Toggle);
    }

    private void Toggle()
    {
        if (_subPanel == null) return;
        _subPanel.SetActive(!_subPanel.activeSelf);
    }
}
