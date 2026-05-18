using UnityEngine;

public enum DragMode { PositionOnly, RotationOnly }

public interface IDragStrategy
{
    void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode);
}

public class SingleDragStrategy : IDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        if (mode == DragMode.PositionOnly) self.position = targetPos;
        else                                self.rotation = targetRot;
    }
}
