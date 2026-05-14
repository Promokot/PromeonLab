using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TMPro;

public class PropertyPanel : MonoBehaviour, IStartable, IDisposable
{
    [SerializeField] private TMP_Text _positionText;
    [SerializeField] private TMP_Text _rotationText;
    [SerializeField] private TMP_Text _scaleText;

    private EventBus _bus;
    private SceneGraph _sceneGraph;

    [Inject]
    public void Construct(EventBus bus, SceneGraph sceneGraph)
    {
        _bus        = bus;
        _sceneGraph = sceneGraph;
    }

    public void Start() => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (e.SelectedNodeId == null) { ClearDisplay(); return; }
        var node = _sceneGraph.GetNode(e.SelectedNodeId);
        if (node == null) return;
        var t = node.transform;
        _positionText.text = $"Pos: {t.position:F2}";
        _rotationText.text = $"Rot: {t.eulerAngles:F1}";
        _scaleText.text    = $"Scl: {t.localScale:F2}";
    }

    private void ClearDisplay()
    {
        _positionText.text = "Pos: —";
        _rotationText.text = "Rot: —";
        _scaleText.text    = "Scl: —";
    }
}
