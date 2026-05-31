using System.Collections.Generic;
using UnityEngine;

public class GizmoHierarchy : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private Transform _moveRoot;
    [SerializeField] private Transform _moveCenter;
    [SerializeField] private Transform _moveX;
    [SerializeField] private Transform _moveY;
    [SerializeField] private Transform _moveZ;

    [Header("Rotate")]
    [SerializeField] private Transform _rotateRoot;
    [SerializeField] private Transform _rotateX;
    [SerializeField] private Transform _rotateY;
    [SerializeField] private Transform _rotateZ;

    [Header("Scale")]
    [SerializeField] private Transform _scaleRoot;
    [SerializeField] private Transform _scaleCenter;
    [SerializeField] private Transform _scaleX;
    [SerializeField] private Transform _scaleY;
    [SerializeField] private Transform _scaleZ;

    private readonly Dictionary<Transform, TransformState> _defaultStates = new();

    private void Awake() => CacheInitialState();

    public void ShowMode(GizmoMode mode)
    {
        if (_moveRoot   != null) _moveRoot.gameObject  .SetActive(mode == GizmoMode.Move);
        if (_rotateRoot != null) _rotateRoot.gameObject.SetActive(mode == GizmoMode.Rotate);
        if (_scaleRoot  != null) _scaleRoot.gameObject .SetActive(mode == GizmoMode.Scale);
    }

    public void OnHandleGrabbed(GizmoHandle handle)
    {
        if (handle == null) return;
        // Grab highlight is the Outline yellow set by GizmoActivator; the hierarchy only re-parents
        // the sibling axes under the grabbed handle so they follow it during the drag.
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:
                SetAsParent(handle.transform, new[] { _moveCenter, _moveX, _moveY, _moveZ });
                break;
            case HandleKind.RotateRing:
                SetAsParent(handle.transform, new[] { _rotateX, _rotateY, _rotateZ });
                break;
            // ScaleAxis / ScaleUniform: no re-parent
        }
    }

    public void OnHandleReleased(GizmoMode currentMode)
    {
        ResetHierarchy();
        ShowMode(currentMode);
    }

    public void ResetHierarchy()
    {
        foreach (var pair in _defaultStates)
        {
            var t = pair.Key;
            var s = pair.Value;
            if (t == null) continue;
            t.SetParent(s.Parent, false);
            t.localPosition = s.LocalPosition;
            t.localRotation = s.LocalRotation;
            t.localScale    = s.LocalScale;
        }
    }

    private static void SetAsParent(Transform newParent, Transform[] group)
    {
        foreach (var element in group)
        {
            if (element == null || element == newParent) continue;
            element.SetParent(newParent, worldPositionStays: true);
        }
    }

    private void CacheInitialState()
    {
        _defaultStates.Clear();
        Cache(_moveRoot); Cache(_moveCenter); Cache(_moveX); Cache(_moveY); Cache(_moveZ);
        Cache(_rotateRoot); Cache(_rotateX); Cache(_rotateY); Cache(_rotateZ);
        Cache(_scaleRoot); Cache(_scaleCenter); Cache(_scaleX); Cache(_scaleY); Cache(_scaleZ);
    }

    private void Cache(Transform t)
    {
        if (t == null || _defaultStates.ContainsKey(t)) return;
        _defaultStates.Add(t, new TransformState
        {
            Parent         = t.parent,
            LocalPosition  = t.localPosition,
            LocalRotation  = t.localRotation,
            LocalScale     = t.localScale,
        });
    }

    private class TransformState
    {
        public Transform  Parent;
        public Vector3    LocalPosition;
        public Quaternion LocalRotation;
        public Vector3    LocalScale;
    }
}
