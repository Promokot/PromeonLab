using TMPro;
using UnityEngine;
using VContainer;

public class SceneInspectorView : MonoBehaviour
{
    [SerializeField] private GameObject     _emptyState;
    [SerializeField] private GameObject     _content;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private TMP_Text       _typeLabel;
    [SerializeField] private TMP_Text       _positionLabel;
    [SerializeField] private TMP_Text       _rotationLabel;
    [SerializeField] private TMP_Text       _scaleLabel;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private SceneNode         _bound;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
    {
        _bus       = bus;
        _graph     = graph;
        _selection = selection;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null) _nameField.onEndEdit.AddListener(OnNameChanged);
        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null) _nameField.onEndEdit.RemoveListener(OnNameChanged);
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void Refresh()
    {
        if (_selection == null || _graph == null) return;
        var activeId = _selection.ActiveId;
        _bound = string.IsNullOrEmpty(activeId) ? null : _graph.GetNode(activeId);
        var has = _bound != null;
        if (_emptyState != null) _emptyState.SetActive(!has);
        if (_content    != null) _content.SetActive(has);
        if (!has) return;
        if (_nameField     != null) _nameField.SetTextWithoutNotify(_bound.DisplayName);
        if (_typeLabel     != null) _typeLabel.text     = $"Type: {_bound.AssetRef}";
        if (_positionLabel != null) _positionLabel.text = $"Pos: {_bound.transform.position:F2}";
        if (_rotationLabel != null) _rotationLabel.text = $"Rot: {_bound.transform.rotation.eulerAngles:F1}";
        if (_scaleLabel    != null) _scaleLabel.text    = $"Scale: {_bound.transform.localScale:F2}";
    }

    private void OnNameChanged(string newName)
    {
        if (_bound == null || string.IsNullOrWhiteSpace(newName)) return;
        _bound.SetDisplayName(newName.Trim());
        _bus?.Publish(new SceneModifiedEvent());
    }
}
