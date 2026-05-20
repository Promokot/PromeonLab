using System;
using System.Collections.Generic;
using UnityEngine;

public class TransformGizmoHierarchyController : MonoBehaviour
{
    public enum GizmoMode
    {
        Move,
        Rotate,
        Scale
    }

    [Header("Move")]
    [SerializeField] private Transform moveRoot;
    [SerializeField] private Transform moveCenter;
    [SerializeField] private Transform moveX;
    [SerializeField] private Transform moveY;
    [SerializeField] private Transform moveZ;

    [Header("Rotate")]
    [SerializeField] private Transform rotateRoot;
    [SerializeField] private Transform rotateX;
    [SerializeField] private Transform rotateY;
    [SerializeField] private Transform rotateZ;

    [Header("Scale")]
    [SerializeField] private Transform scaleRoot;
    [SerializeField] private Transform scaleCenter;
    [SerializeField] private Transform scaleX;
    [SerializeField] private Transform scaleY;
    [SerializeField] private Transform scaleZ;

    private readonly Dictionary<Transform, TransformState> _defaultStates = new();

    private void Awake()
    {
        CacheInitialState();
    }

    #region Public API

    public void SetMoveMode(Transform activeAxis)
    {
        ResetHierarchy();

        if (activeAxis == null)
            return;

        SetAsParent(activeAxis, new[]
        {
            moveCenter,
            moveX,
            moveY,
            moveZ
        });
    }

    public void SetRotateMode(Transform activeRing)
    {
        ResetHierarchy();

        if (activeRing == null)
            return;

        SetAsParent(activeRing, new[]
        {
            rotateX,
            rotateY,
            rotateZ
        });
    }

    public void SetScaleMode()
    {
        // Для scale иерархия не меняется,
        // но можно оставить сброс для консистентности.
        ResetHierarchy();
    }

    public void SetMode(GizmoMode mode, Transform activePart = null)
    {
        switch (mode)
        {
            case GizmoMode.Move:
                SetMoveMode(activePart);
                break;

            case GizmoMode.Rotate:
                SetRotateMode(activePart);
                break;

            case GizmoMode.Scale:
                SetScaleMode();
                break;
        }
    }

    public void ResetHierarchy()
    {
        foreach (var pair in _defaultStates)
        {
            Transform target = pair.Key;
            TransformState state = pair.Value;

            if (target == null)
                continue;

            target.SetParent(state.Parent, false);

            target.localPosition = state.LocalPosition;
            target.localRotation = state.LocalRotation;
            target.localScale = state.LocalScale;
        }
    }

    #endregion

    #region Internal Logic

    private void SetAsParent(Transform newParent, Transform[] group)
    {
        foreach (Transform element in group)
        {
            if (element == null)
                continue;

            if (element == newParent)
                continue;

            element.SetParent(newParent, true);
        }
    }

    private void CacheInitialState()
    {
        _defaultStates.Clear();

        Cache(moveRoot);
        Cache(moveCenter);
        Cache(moveX);
        Cache(moveY);
        Cache(moveZ);

        Cache(rotateRoot);
        Cache(rotateX);
        Cache(rotateY);
        Cache(rotateZ);

        Cache(scaleRoot);
        Cache(scaleCenter);
        Cache(scaleX);
        Cache(scaleY);
        Cache(scaleZ);
    }

    private void Cache(Transform target)
    {
        if (target == null)
            return;

        if (_defaultStates.ContainsKey(target))
            return;

        _defaultStates.Add(target, new TransformState
        {
            Parent = target.parent,
            LocalPosition = target.localPosition,
            LocalRotation = target.localRotation,
            LocalScale = target.localScale
        });
    }

    #endregion

    [Serializable]
    private class TransformState
    {
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }
}