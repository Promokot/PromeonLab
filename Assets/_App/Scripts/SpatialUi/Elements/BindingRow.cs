using TMPro;
using UnityEngine;

// Одна строка в списке биндов: название + описание + бейдж (рука + input-label).
// Иконка опциональна и в v1 не используется (поле IconId хранится в данных для md/будущего).
public class BindingRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _handText;    // L / R / L+R / ""
    [SerializeField] private TMP_Text _inputText;   // напр. "tap trigger"

    public void Bind(ControlBinding binding)
    {
        if (_nameText != null)        _nameText.text        = binding.Action;
        if (_descriptionText != null) _descriptionText.text = binding.Description;
        if (_handText != null)        _handText.text        = HandLabel(binding.Hand);
        if (_inputText != null)       _inputText.text       = binding.InputLabel;
    }

    private static string HandLabel(ControlHand hand) => hand switch
    {
        ControlHand.Left  => "L",
        ControlHand.Right => "R",
        ControlHand.Both  => "L+R",
        ControlHand.Any   => "•",
        _                 => string.Empty, // None
    };
}
