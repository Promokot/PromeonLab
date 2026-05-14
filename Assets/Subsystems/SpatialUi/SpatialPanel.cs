using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class SpatialPanel : MonoBehaviour
{
    [SerializeField] private PanelType _panelType = PanelType.BodyLocked;
    [SerializeField] private bool _billboard = true;
    [SerializeField] private Vector3 _defaultOffset = new Vector3(0, 0, 1.2f);

    public PanelId PanelId { get; private set; }

    private Transform _cameraTransform;

    public void Init(PanelId id, Transform cameraTransform)
    {
        PanelId = id;
        _cameraTransform = cameraTransform;
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        if (_panelType == PanelType.BodyLocked)
            FollowCamera();

        if (_billboard)
            FaceCamera();
    }

    private void FollowCamera()
    {
        var cam = _cameraTransform;
        transform.position = cam.position + cam.rotation * _defaultOffset;
    }

    private void FaceCamera()
    {
        var dir = transform.position - _cameraTransform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);
}
