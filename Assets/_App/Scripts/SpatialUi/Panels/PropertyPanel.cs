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

    private EventBus     _bus;
    private SceneContext _ctx;

    [Inject]
    public void Construct(EventBus bus, SceneContext ctx)
    {
        _bus = bus;
        _ctx = ctx;
    }

    public void Start()
    {
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        if (!e.HasScene) ClearDisplay();
    }

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (e.SelectedNodeId == null || _ctx.Graph == null) { ClearDisplay(); return; }
        var node = _ctx.Graph.GetNode(e.SelectedNodeId);
        if (node == null) return;
        var t = node.transform;
        _positionText.text = $"Pos: {t.position:F2}";
        _rotationText.text = $"Rot: {t.eulerAngles:F1}";
        _scaleText.text    = $"Scl: {t.localScale:F2}";
    }

    private void ClearDisplay()
    {
        _positionText.text = "Pos: –";
        _rotationText.text = "Rot: –";
        _scaleText.text    = "Scl: –";
    }
}
