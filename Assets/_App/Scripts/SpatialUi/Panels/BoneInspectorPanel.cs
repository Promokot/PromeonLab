using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class BoneInspectorPanel : MonoBehaviour
{
    [SerializeField] private Button   _buildRigButton;
    [SerializeField] private Button   _openIkWizardButton;
    [SerializeField] private TMP_Text _boneCountText;

    private IRigRuntime       _rigRuntime;
    private ISelectionManager _selectionManager;
    private ISceneGraph       _sceneGraph;
    private IkWizardPanel     _ikWizard;

    [Inject]
    public void Construct(IRigRuntime rigRuntime, ISelectionManager selectionManager, ISceneGraph sceneGraph, IkWizardPanel ikWizard)
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

        var go = _sceneGraph.GetNode(nodeId);
        if (go == null) return;

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) { _boneCountText.text = "No SkinnedMeshRenderer"; return; }

        var def = _rigRuntime.BuildFromSkinnedMesh(smr);
        _rigRuntime.ApplyDefinition(def, smr);
        _boneCountText.text = $"{def.Bones.Count} bones";
    }

    private void OnOpenIkWizard() => _ikWizard?.OpenForSelection();
}
