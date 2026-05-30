using TMPro;
using UnityEngine;

// Карточка-секция в списке биндов: заголовок (название категории) + контейнер строк.
public class BindingSectionCard : MonoBehaviour
{
    [SerializeField] private TMP_Text  _titleText;
    [SerializeField] private Transform _rowList;

    public Transform RowList => _rowList;

    public void SetTitle(string title)
    {
        if (_titleText != null) _titleText.text = title;
    }
}
