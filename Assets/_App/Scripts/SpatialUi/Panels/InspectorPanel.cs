using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class InspectorPanel : MonoBehaviour
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

    [SerializeField] private GameObject     _boneState;
    [SerializeField] private TMP_Text       _boneNameLabel;
    [SerializeField] private TMP_Text       _boneParentRigLabel;
    [SerializeField] private TMP_Text       _bonePosX;
    [SerializeField] private TMP_Text       _bonePosY;
    [SerializeField] private TMP_Text       _bonePosZ;
    [SerializeField] private TMP_Text       _boneRotX;
    [SerializeField] private TMP_Text       _boneRotY;
    [SerializeField] private TMP_Text       _boneRotZ;
    [SerializeField] private TMP_Text       _boneScaleX;
    [SerializeField] private TMP_Text       _boneScaleY;
    [SerializeField] private TMP_Text       _boneScaleZ;

    [SerializeField] private Toggle         _showBonesToggle;
    [SerializeField] private Button         _deleteButton;

    private EventBus          _bus;
    private SceneContext      _ctx;
    private IAssetRegistry    _registry;

    private SceneNode _bound;          // currently selected rig/object
    private Transform _boneTransform;  // currently selected bone proxy transform
    private string    _boneRigId;      // parent rig node id (when bone selected)
    private string    _activeBoneRigId; // rig whose bone mode is ON; persists across selection so the
                                        // Show Bones toggle stays reachable even with no/bone selection

    [Inject]
    public void Construct(EventBus bus, SceneContext ctx, IAssetRegistry registry)
    {
        _bus      = bus;
        _ctx      = ctx;
        _registry = registry;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null)
        {
            _nameField.onValueChanged.AddListener(OnNameLiveEdit);
            _nameField.onEndEdit.AddListener(OnNameCommit);
        }
        if (_deleteButton != null)    _deleteButton.onClick.AddListener(OnDeleteClicked);
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.AddListener(OnShowBonesToggleChanged);
        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_nameField != null)
        {
            _nameField.onValueChanged.RemoveListener(OnNameLiveEdit);
            _nameField.onEndEdit.RemoveListener(OnNameCommit);
        }
        if (_deleteButton != null)    _deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (_showBonesToggle != null) _showBonesToggle.onValueChanged.RemoveListener(OnShowBonesToggleChanged);
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        if (e.HasScene) Refresh();
        else if (_emptyState != null)
        {
            _emptyState.SetActive(true);
            if (_content   != null) _content.SetActive(false);
            if (_boneState != null) _boneState.SetActive(false);
        }
    }

    private enum InspectorState { Empty, Single, Bone }

    private void Refresh()
    {
        if (!_ctx.HasScene) return;

        var activeId = _ctx.Selection.SelectedNodeId;
        var state    = string.IsNullOrEmpty(activeId)            ? InspectorState.Empty
                     : activeId.StartsWith("bone:")              ? InspectorState.Bone
                     :                                             InspectorState.Single;

        if (_emptyState != null) _emptyState.SetActive(state == InspectorState.Empty);
        if (_content    != null) _content   .SetActive(state == InspectorState.Single);
        if (_boneState  != null) _boneState .SetActive(state == InspectorState.Bone);

        _bound         = null;
        _boneTransform = null;
        _boneRigId     = null;

        ProxyRigRuntime rig = null;

        if (state == InspectorState.Single)
        {
            _bound = _ctx.Graph.GetNode(activeId);
            if (_bound != null)
            {
                BindSingle(_bound);
                rig = _bound.GetComponentInChildren<ProxyRigRuntime>(true);
            }
        }
        else if (state == InspectorState.Bone)
        {
            BindBone(activeId);
            if (!string.IsNullOrEmpty(_boneRigId))
            {
                var rigNode = _ctx.Graph.GetNode(_boneRigId);
                if (rigNode != null) rig = rigNode.GetComponentInChildren<ProxyRigRuntime>(true);
            }
        }

        // Delete: only when a normal (non-bone) node is selected.
        if (_deleteButton != null) _deleteButton.gameObject.SetActive(state == InspectorState.Single && _bound != null);

        // ShowBones toggle: stays reachable while a rig's bone mode is active, regardless of the current
        // selection (we deselect the rig on entering bone mode, and a bone/empty selection must still be
        // able to turn it off). Prefer the active bone-mode rig; otherwise the rig from selection.
        var toggleRig = rig;
        if (_activeBoneRigId != null)
        {
            var activeNode = _ctx.Graph.GetNode(_activeBoneRigId);
            var activeRig  = activeNode != null ? activeNode.GetComponentInChildren<ProxyRigRuntime>(true) : null;
            if (activeRig != null) toggleRig = activeRig;
            else _activeBoneRigId = null; // active rig vanished (scene change) — drop bone mode
        }
        if (_showBonesToggle != null)
        {
            _showBonesToggle.gameObject.SetActive(toggleRig != null);
            if (toggleRig != null) _showBonesToggle.SetIsOnWithoutNotify(_activeBoneRigId != null);
        }
    }

    private void BindSingle(SceneNode node)
    {
        if (_nameField != null) _nameField.SetTextWithoutNotify(node.DisplayName);
        if (_typeLabel != null)
        {
            var asset = _registry?.Find(node.AssetRef);
            _typeLabel.text = asset != null
                ? $"Type: {asset.Type}"
                : $"Type: {node.AssetRef.Source}";
        }

        var pos   = node.transform.position;
        var rot   = node.transform.rotation.eulerAngles;
        var scale = node.transform.localScale;

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

    private void BindBone(string boneNodeId)
    {
        // NodeId format: "bone:{rigNodeId}:{boneName}"
        var parts     = boneNodeId.Split(':');
        var boneName  = parts.Length >= 3 ? parts[2] : boneNodeId;
        _boneRigId    = parts.Length >= 2 ? parts[1] : "";

        if (_boneNameLabel != null) _boneNameLabel.text = $"Bone: {boneName}";

        if (_boneParentRigLabel != null)
        {
            var rigNode = _ctx.Graph.GetNode(_boneRigId);
            _boneParentRigLabel.text = rigNode != null ? $"Rig: {rigNode.DisplayName}" : $"Rig: {_boneRigId}";
        }

        _boneTransform = _ctx.Graph.GetNode(boneNodeId)?.transform;
        if (_boneTransform == null) return;

        var pos   = _boneTransform.position;
        var rot   = _boneTransform.rotation.eulerAngles;
        var scale = _boneTransform.localScale;

        if (_bonePosX != null) _bonePosX.text = pos.x.ToString("F2");
        if (_bonePosY != null) _bonePosY.text = pos.y.ToString("F2");
        if (_bonePosZ != null) _bonePosZ.text = pos.z.ToString("F2");

        if (_boneRotX != null) _boneRotX.text = rot.x.ToString("F1");
        if (_boneRotY != null) _boneRotY.text = rot.y.ToString("F1");
        if (_boneRotZ != null) _boneRotZ.text = rot.z.ToString("F1");

        if (_boneScaleX != null) _boneScaleX.text = scale.x.ToString("F2");
        if (_boneScaleY != null) _boneScaleY.text = scale.y.ToString("F2");
        if (_boneScaleZ != null) _boneScaleZ.text = scale.z.ToString("F2");
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
        _ctx.Selection?.Select(null);
        _ctx.Graph.RemoveNode(nodeId); // destroys GO, publishes SceneModifiedEvent → outliner rebuilds
    }

    private void OnShowBonesToggleChanged(bool value)
    {
        // Resolve the target rig. When turning OFF we may have no selection (entering bone mode
        // deselected the rig), so fall back to the remembered active bone-mode rig.
        ProxyRigRuntime rig = null;
        string                 rigNodeId = null;

        if (_bound != null)
        {
            rig       = _bound.GetComponentInChildren<ProxyRigRuntime>(true);
            rigNodeId = _bound.NodeId;
        }
        else if (!string.IsNullOrEmpty(_boneRigId))
        {
            var rigNode = _ctx.Graph.GetNode(_boneRigId);
            if (rigNode != null) { rig = rigNode.GetComponentInChildren<ProxyRigRuntime>(true); rigNodeId = rigNode.NodeId; }
        }
        else if (!string.IsNullOrEmpty(_activeBoneRigId))
        {
            var rigNode = _ctx.Graph.GetNode(_activeBoneRigId);
            if (rigNode != null) { rig = rigNode.GetComponentInChildren<ProxyRigRuntime>(true); rigNodeId = rigNode.NodeId; }
        }

        if (rig == null) return;

        rig.SetBonesInteractive(value);
        _bus?.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = value });

        if (value)
        {
            // Enter bone mode: remember the rig and drop the rig-object selection immediately, so we
            // start clean inside the rig. InteractionMaskBinder flips the cast mask to BoneProxies.
            _activeBoneRigId = rigNodeId;
            _ctx.Selection?.Select(null);
        }
        else
        {
            // Exit bone mode: forget it and re-select the rig object (mask returns to SceneObjects).
            _activeBoneRigId = null;
            _ctx.Selection?.Select(rigNodeId);
        }
    }
}
