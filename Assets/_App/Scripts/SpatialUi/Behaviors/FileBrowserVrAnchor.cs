using SimpleFileBrowser;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private AssetBrowserPanel _target;

    private void Start()
    {
        _target = Object.FindAnyObjectByType<AssetBrowserPanel>(FindObjectsInactive.Include);

        transform.localScale = Vector3.one * _scale;

        var canvas  = GetComponent<Canvas>();
        var mainCam = Camera.main;
        if (canvas != null && mainCam != null)
            canvas.worldCamera = mainCam;

        RepositionToTarget();
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        if (!_target.gameObject.activeInHierarchy)
        {
            // HideDialog → SetActive(false) on this same GameObject is safe;
            // Unity stops LateUpdate for inactive objects after this call returns.
            if (FileBrowser.IsOpen)
                FileBrowser.HideDialog();
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
