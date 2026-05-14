using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class IkSetupWizard : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown _rootBoneDropdown;
    [SerializeField] private TMP_Dropdown _endBoneDropdown;
    [SerializeField] private Button       _confirmButton;
    [SerializeField] private Button       _cancelButton;

    private RigRuntime       _rigRuntime;
    private SelectionManager _selectionManager;
    private SceneGraph       _sceneGraph;

    private SkinnedMeshRenderer _currentSmr;
    private RigDefinition       _currentDef;

    [Inject]
    public void Construct(RigRuntime rigRuntime, SelectionManager selectionManager, SceneGraph sceneGraph)
    {
        _rigRuntime       = rigRuntime;
        _selectionManager = selectionManager;
        _sceneGraph       = sceneGraph;
    }

    private void Awake()
    {
        _confirmButton.onClick.AddListener(OnConfirm);
        _cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void OpenForSelection()
    {
        var node = _sceneGraph.GetNode(_selectionManager.SelectedNodeId);
        if (node == null) return;

        _currentSmr = node.GetComponentInChildren<SkinnedMeshRenderer>();
        if (_currentSmr == null) return;

        _currentDef = _rigRuntime.BuildFromSkinnedMesh(_currentSmr);
        PopulateDropdowns(_currentDef);
        gameObject.SetActive(true);
    }

    private void PopulateDropdowns(RigDefinition def)
    {
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var b in def.Bones)
            options.Add(new TMP_Dropdown.OptionData(b.BoneName));

        _rootBoneDropdown.ClearOptions();
        _endBoneDropdown.ClearOptions();
        _rootBoneDropdown.AddOptions(options);
        _endBoneDropdown.AddOptions(options);

        if (options.Count > 1)
            _endBoneDropdown.value = options.Count - 1;
    }

    private void OnConfirm()
    {
        if (_currentDef == null || _currentSmr == null) return;

        var chain = new IkChainRecord
        {
            RootBone = _rootBoneDropdown.options[_rootBoneDropdown.value].text,
            EndBone  = _endBoneDropdown.options[_endBoneDropdown.value].text,
            Weight   = 1f
        };
        _currentDef.IkChains.Add(chain);
        _rigRuntime.ApplyDefinition(_currentDef, _currentSmr);
        gameObject.SetActive(false);
    }
}
