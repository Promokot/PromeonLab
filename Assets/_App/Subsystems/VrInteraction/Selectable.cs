using UnityEngine;

public class Selectable : MonoBehaviour
{
    private SceneNode _node;
    private Outline   _outline;

    public string    NodeId => _node?.NodeId;
    public SceneNode Node   => _node;

    private void Awake()
    {
        _node = GetComponent<SceneNode>();
    }

    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.InSet:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.55f, 0f);
                _outline.OutlineWidth = 4f;
                break;
            case SelectionVisual.Active:
                _outline.enabled      = true;
                _outline.OutlineColor = new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth = 6f;
                break;
        }
    }

    private void EnsureOutline()
    {
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
    }
}
