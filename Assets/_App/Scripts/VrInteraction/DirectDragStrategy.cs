using UnityEngine;

public class DirectDragStrategy : IObjectDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        if (mode == DragMode.PositionOnly) self.position = targetPos;
        else                                self.rotation = targetRot;
    }
}
