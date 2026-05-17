using TMPro;
using UnityEngine;
using VContainer;

public class SceneInspectorView : MonoBehaviour
{
    [SerializeField] private GameObject     _emptyState;
    [SerializeField] private GameObject     _content;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private TMP_Text       _typeLabel;
    [SerializeField] private TMP_Text       _posX;
    [SerializeField] private TMP_Text       _posY;
    [SerializeField] private TMP_Text       _posZ;
    [SerializeField] private TMP_Text       _rotX;
    [SerializeField] private TMP_Text       _rotY;
    [SerializeField] private TMP_Text       _rotZ;
    [SerializeField] private TMP_Text       _scaleX;
    [SerializeField] private TMP_Text       _scaleY;
    [SerializeField] private TMP_Text       _scaleZ;

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
        if (_nameField != null) _nameField.SetTextWithoutNotify(_bound.DisplayName);
        if (_typeLabel != null) _typeLabel.text = $"Type: {_bound.AssetRef}";

        var pos   = _bound.transform.position;
        var rot   = _bound.transform.rotation.eulerAngles;
        var scale = _bound.transform.localScale;

        if (_posX != null) _posX.text = pos.x.ToString("F2");
        if (_posY != null) _posY.text = pos.y.ToString("F2");
        if (_posZ != null) _posZ.text = pos.z.ToString("F2");

        if (_rotX != null) _rotX.text = rot.x.ToString("F1");
        if (_rotY != null) _rotY.text = rot.y.ToString("F1");
        if (_rotZ != null) _rotZ.text = rot.z.ToString("F1");

        if (_scaleX != null) _scaleX.text = scale.x.ToString("F2");
        if (_scaleY != null) _scaleY.text = scale.y.ToString("F2");
        if (_scaleZ != null) _scaleZ.text = scale.z.ToString("F2");
    }

    private void OnNameChanged(string newName)
    {
        if (_bound == null || string.IsNullOrWhiteSpace(newName)) return;
        _bound.SetDisplayName(newName.Trim());
        _bus?.Publish(new SceneModifiedEvent());
    }
}
