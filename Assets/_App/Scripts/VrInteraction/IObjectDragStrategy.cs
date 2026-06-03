using UnityEngine;

public interface IObjectDragStrategy
{
    void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode);
}
