using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class SceneInspectorView : MonoBehaviour
{
    [SerializeField] private GameObject     _emptyState;
    [SerializeField] private GameObject     _content;
    [SerializeField] private GameObject     _multiState;
    [SerializeField] private TMP_Text       _multiCountLabel;
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
    [SerializeField] private Button         _deleteButton;
    [SerializeField] private GameObject     _boneState;
    [SerializeField] private TMP_Text       _boneNameLabel;
    [SerializeField] private TMP_Text       _boneParentRigLabel;
    [SerializeField] private Toggle         _showBonesToggle;

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
        if (_nameField != null)
        {
            _nameField.onValueChanged.AddListener(OnNameLiveEdit);
            _nameField.onEndEdit.AddListener(OnNameCommit);
        }
        if (_deleteButton != null)
            _deleteButton.onClick.AddListener(OnDeleteClicked);
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.AddListener(OnShowBonesToggleChanged);
        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null)
        {
            _nameField.onValueChanged.RemoveListener(OnNameLiveEdit);
            _nameField.onEndEdit.RemoveListener(OnNameCommit);
        }
        if (_deleteButton != null)
            _deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.RemoveListener(OnShowBonesToggleChanged);
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private enum InspectorState { Empty, Single, Multi, Bone }

    private void Refresh()
    {
        if (_selection == null || _graph == null) return;

        var count    = _selection.SelectedIds?.Count ?? 0;
        var activeId = _selection.ActiveId;
        var state    = count == 0                                ? InspectorState.Empty
                     : count > 1                                 ? InspectorState.Multi
                     : (activeId != null && activeId.StartsWith("bone:")) ? InspectorState.Bone
                     :                                             InspectorState.Single;

        if (_emptyState != null) _emptyState.SetActive(state == InspectorState.Empty);
        if (_content    != null) _content   .SetActive(state == InspectorState.Single);
        if (_multiState != null) _multiState.SetActive(state == InspectorState.Multi);
        if (_boneState  != null) _boneState .SetActive(state == InspectorState.Bone);

        if (state == InspectorState.Multi && _multiCountLabel != null)
            _multiCountLabel.text = $"Multiple Objects Selected ({count})";

        if (state == InspectorState.Bone)
        {
            RefreshBoneState(activeId);
            _bound = null;
            return;
        }

        if (state != InspectorState.Single)
        {
            _bound = null;
            if (_showBonesToggle != null) _showBonesToggle.gameObject.SetActive(false);
            return;
        }

        _bound = _graph.GetNode(_selection.ActiveId);
        if (_bound == null) return;

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

        // Show Bones toggle is visible only when the selected node carries a rig builder.
        if (_showBonesToggle != null)
        {
            var rig = _bound.GetComponentInChildren<PromeonInteractableRigBuilder>(true);
            _showBonesToggle.gameObject.SetActive(rig != null);
            _showBonesToggle.SetIsOnWithoutNotify(rig != null && AreBonesInteractive(rig));
        }
    }

    private void RefreshBoneState(string boneNodeId)
    {
        // NodeId format: "bone:{rigNodeId}:{boneName}"
        var parts = boneNodeId.Split(':');
        var boneName  = parts.Length >= 3 ? parts[2] : boneNodeId;
        var rigNodeId = parts.Length >= 2 ? parts[1] : "";

        if (_boneNameLabel != null)
            _boneNameLabel.text = $"Bone: {boneName}";

        if (_boneParentRigLabel != null)
        {
            var rigNode = _graph.GetNode(rigNodeId);
            _boneParentRigLabel.text = rigNode != null ? $"Rig: {rigNode.DisplayName}" : $"Rig: {rigNodeId}";
        }

        if (_showBonesToggle != null) _showBonesToggle.gameObject.SetActive(false);
    }

    private static bool AreBonesInteractive(PromeonInteractableRigBuilder rig)
    {
        foreach (var go in rig.ProxyGOs)
        {
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled) return true;
        }
        return false;
    }

    private void OnNameLiveEdit(string newName)
    {
        if (_bound == null) return;
        if (string.IsNullOrWhiteSpace(newName)) return;
        var trimmed = newName.Trim();
        _bound.SetDisplayName(trimmed);
        _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = trimmed });
    }

    private void OnNameCommit(string newName)
    {
        if (_bound == null) return;
        string finalName;
        if (string.IsNullOrWhiteSpace(newName))
        {
            finalName = "Unnamed";
            _bound.SetDisplayName(finalName);
            if (_nameField != null) _nameField.SetTextWithoutNotify(finalName);
        }
        else
        {
            finalName = newName.Trim();
            _bound.SetDisplayName(finalName);
        }
        _bus?.Publish(new NodeRenamedEvent { NodeId = _bound.NodeId, NewName = finalName });
        _bus?.Publish(new SceneModifiedEvent());
    }

    private void OnDeleteClicked()
    {
        if (_bound == null) return;
        var nodeId = _bound.NodeId;
        _bound = null;
        _selection?.Clear();
        _graph.RemoveNode(nodeId); // destroys GO, publishes SceneModifiedEvent → outliner rebuilds
    }

    private void OnShowBonesToggleChanged(bool value)
    {
        if (_bound == null) return;
        var rig = _bound.GetComponentInChildren<PromeonInteractableRigBuilder>(true);
        if (rig == null) return;
        rig.SetBonesInteractive(value);
    }
}
