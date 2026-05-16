using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private void Start()
    {
        var target = Object.FindAnyObjectByType<AssetBrowserModule>(FindObjectsInactive.Include);
        if (target == null)
        {
            Debug.LogWarning("FileBrowserVrAnchor: AssetBrowserModule not found — file browser will appear at world origin.");
            return;
        }

        var t = target.transform;
        // Panel forward points away from user; subtract to move toward camera.
        transform.position   = t.position - t.forward * _forwardOffset;
        transform.rotation   = t.rotation;
        transform.localScale = Vector3.one * _scale;

        var mainCam = Camera.main;
        var canvas  = GetComponent<Canvas>();
        if (canvas != null && mainCam != null)
            canvas.worldCamera = mainCam;
    }
}
