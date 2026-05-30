using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Master-detail Settings панель: левый rail (general / bindings), справа контент.
// general — пустой. bindings рендерится из ControlsProfile карточками-секциями в рантайме.
public class SettingsPanel : MonoBehaviour
{
    [Header("Rail")]
    [SerializeField] private Button _generalTabButton;
    [SerializeField] private Button _bindingsTabButton;

    [Header("Content")]
    [SerializeField] private GameObject _generalContent;
    [SerializeField] private GameObject _bindingsContent;
    [SerializeField] private TMP_Text   _headerTitle;

    [Header("Bindings")]
    [SerializeField] private ControlsProfile    _profile;
    [SerializeField] private Transform          _bindingsRoot;   // контейнер секций (VerticalLayoutGroup)
    [SerializeField] private BindingSectionCard _sectionPrefab;
    [SerializeField] private BindingRow         _rowPrefab;

    [Header("Tab Colors")]
    [SerializeField] private Color _activeTabColor   = new Color(0.165f, 0.227f, 0.4f, 1f); // ~#2A3A66
    [SerializeField] private Color _inactiveTabColor = new Color(0f, 0f, 0f, 0f);           // transparent

    private void Awake()
    {
        _generalTabButton?.onClick.AddListener(ShowGeneral);
        _bindingsTabButton?.onClick.AddListener(ShowBindings);
    }

    private void Start()
    {
        BuildBindings();
        ShowGeneral(); // по умолчанию открыта вкладка General
    }

    private void OnDestroy()
    {
        _generalTabButton?.onClick.RemoveListener(ShowGeneral);
        _bindingsTabButton?.onClick.RemoveListener(ShowBindings);
    }

    private void ShowGeneral()  => SetActiveTab(general: true);
    private void ShowBindings() => SetActiveTab(general: false);

    private void SetActiveTab(bool general)
    {
        if (_generalContent != null)  _generalContent.SetActive(general);
        if (_bindingsContent != null) _bindingsContent.SetActive(!general);
        if (_headerTitle != null)     _headerTitle.text = general ? "General" : "Bindings";
        HighlightTab(_generalTabButton, general);
        HighlightTab(_bindingsTabButton, !general);
    }

    private void HighlightTab(Button button, bool active)
    {
        if (button == null) return;
        var graphic = button.targetGraphic;
        if (graphic != null) graphic.color = active ? _activeTabColor : _inactiveTabColor;
    }

    private void BuildBindings()
    {
        if (_bindingsRoot == null || _rowPrefab == null || _sectionPrefab == null) return;

        foreach (Transform child in _bindingsRoot)
            Destroy(child.gameObject);

        if (_profile == null)
        {
            Debug.LogWarning("[SettingsPanel] ControlsProfile not assigned — bindings list empty.", this);
            return;
        }

        ControlBindingCategory? currentGroup = null;
        Transform rowParent = null;
        foreach (var binding in _profile.Bindings)
        {
            if (currentGroup != binding.Category)
            {
                currentGroup = binding.Category;
                var section = Instantiate(_sectionPrefab, _bindingsRoot);
                section.SetTitle(binding.Category.ToString());
                rowParent = section.RowList != null ? section.RowList : _bindingsRoot;
            }

            var row = Instantiate(_rowPrefab, rowParent);
            row.Bind(binding);
        }
    }
}
