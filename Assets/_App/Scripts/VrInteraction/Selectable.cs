using UnityEngine;
using VContainer;

public class Selectable : MonoBehaviour
{
    private SceneNode     _node;
    private Outline       _outline;
    private OutlineConfig _outlineConfig;

    public string    NodeId => _node?.NodeId;
    public SceneNode Node   => _node;

    private void Awake()
    {
        _node = GetComponent<SceneNode>();
    }

    [Inject]
    public void Construct(OutlineConfig outlineConfig)
    {
        _outlineConfig = outlineConfig;
    }

    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.Selected:
                _outline.enabled        = true;
                _outline.OutlineColor   = _outlineConfig != null ? _outlineConfig.SelectColor : new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth   = 6f;
                _outline.RenderPriority = 0; // base layer; bones (1) and gizmo (2) draw on top
                break;
        }
    }

    private void EnsureOutline()
    {
        if (_outline == null)
        {
            // A bone proxy may already carry an Outline (Outline is [DisallowMultipleComponent]),
            // so reuse an existing one before adding.
            _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
        }
        if (_outlineConfig != null)
            _outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
    }
}
