using System;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorSubEmptyState : MonoBehaviour
{
    [SerializeField] private GameObject _noSelectionPanel;
    [SerializeField] private GameObject _noContainerPanel;
    [SerializeField] private Button     _addAnimationButton;

    public Action OnAddAnimationClicked;

    public enum State { NoSelection, NoContainer }

    private void Awake()
    {
        if (_addAnimationButton != null)
            _addAnimationButton.onClick.AddListener(() => OnAddAnimationClicked?.Invoke());
    }

    public void Show(State state)
    {
        if (_noSelectionPanel != null) _noSelectionPanel.SetActive(state == State.NoSelection);
        if (_noContainerPanel != null) _noContainerPanel.SetActive(state == State.NoContainer);
    }

    public void HideAll()
    {
        if (_noSelectionPanel != null) _noSelectionPanel.SetActive(false);
        if (_noContainerPanel != null) _noContainerPanel.SetActive(false);
    }
}
