using SimpleFileBrowser;
using UnityEngine;
using VContainer;

[RequireComponent(typeof(Canvas))]
public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private AssetBrowserPanel _target;

    [Inject]
    public void Construct(AssetBrowserPanel target)
    {
        _target = target;
        Debug.Log($"[FBDBG] FileBrowserVrAnchor.Construct targetNull={target == null}");
    }

    private void Start()
    {
        transform.localScale = Vector3.one * _scale;

        var canvas  = GetComponent<Canvas>();
        var mainCam = Camera.main;
        if (canvas != null && mainCam != null)
            canvas.worldCamera = mainCam;

        RepositionToTarget();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        if (!_target.gameObject.activeInHierarchy)
        {
            if (FileBrowser.IsOpen)
            {
                Debug.Log("[FBDBG] FileBrowserVrAnchor AUTO-HIDE: target inactive while dialog open → HideDialog()");
                FileBrowser.HideDialog();
            }
            return;
        }

        RepositionToTarget();
    }

    private void RepositionToTarget()
    {
        if (_target == null) return;
        var t = _target.transform;
        transform.position = t.position - t.forward * _forwardOffset;
        transform.rotation = t.rotation;
    }
}
