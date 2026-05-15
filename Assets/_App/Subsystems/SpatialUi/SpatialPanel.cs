using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class SpatialPanel : MonoBehaviour
{
    [SerializeField] private PanelType _panelType   = PanelType.BodyLocked;
    [SerializeField] private bool      _billboard   = true;
    [SerializeField] private Vector3   _defaultOffset = new Vector3(0, 0, 1.2f);

    [Header("Lazy Follow")]
    [SerializeField] private bool  _lazyFollow      = false;
    [SerializeField] private float _lazyAngle       = 45f;
    [SerializeField] private float _lazySpeed       = 2f;

    public PanelId PanelId { get; private set; }

    private Transform _cameraTransform;
    private Vector3   _lazyTarget;
    private bool      _lazyInit;

    public void Init(PanelId id, Transform cameraTransform)
    {
        PanelId          = id;
        _cameraTransform = cameraTransform;
    }

    protected virtual void Awake()
    {
        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;
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
        var cam      = _cameraTransform;
        var idealPos = cam.position + cam.rotation * _defaultOffset;

        if (!_lazyFollow)
        {
            transform.position = idealPos;
            return;
        }

        if (!_lazyInit)
        {
            _lazyTarget      = idealPos;
            transform.position = idealPos;
            _lazyInit        = true;
            return;
        }

        var dir = transform.position - cam.position;
        if (dir.sqrMagnitude > 0.001f && Vector3.Angle(cam.forward, dir.normalized) > _lazyAngle)
            _lazyTarget = idealPos;

        transform.position = Vector3.Lerp(transform.position, _lazyTarget, Time.deltaTime * _lazySpeed);
    }

    private void FaceCamera()
    {
        var dir = transform.position - _cameraTransform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);
}
