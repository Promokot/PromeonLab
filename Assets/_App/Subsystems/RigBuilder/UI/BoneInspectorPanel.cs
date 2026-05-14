using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class BoneInspectorPanel : MonoBehaviour
{
    [SerializeField] private Button   _buildRigButton;
    [SerializeField] private Button   _openIkWizardButton;
    [SerializeField] private TMP_Text _boneCountText;

    private RigRuntime       _rigRuntime;
    private SelectionManager _selectionManager;
    private SceneGraph       _sceneGraph;
    private IkSetupWizard    _ikWizard;

    [Inject]
    public void Construct(RigRuntime rigRuntime, SelectionManager selectionManager, SceneGraph sceneGraph, IkSetupWizard ikWizard)
    {
        _rigRuntime       = rigRuntime;
        _selectionManager = selectionManager;
        _sceneGraph       = sceneGraph;
        _ikWizard         = ikWizard;
    }

    private void Awake()
    {
        _buildRigButton.onClick.AddListener(OnBuildRig);
        _openIkWizardButton.onClick.AddListener(OnOpenIkWizard);
    }

    private void OnBuildRig()
    {
        var nodeId = _selectionManager.SelectedNodeId;
        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _sceneGraph.GetNode(nodeId);
        if (node == null) return;

        var smr = node.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) { _boneCountText.text = "No SkinnedMeshRenderer"; return; }

        var def = _rigRuntime.BuildFromSkinnedMesh(smr);
        _rigRuntime.ApplyDefinition(def, smr);
        _boneCountText.text = $"{def.Bones.Count} bones";
    }

    private void OnOpenIkWizard() => _ikWizard?.OpenForSelection();
}
