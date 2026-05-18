using UnityEngine;

public enum DragMode { PositionOnly, RotationOnly, SixDof }

public interface IDragStrategy
{
    void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode);
}

public class SingleDragStrategy : IDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        switch (mode)
        {
            case DragMode.PositionOnly:
                self.position = targetPos;
                break;
            case DragMode.RotationOnly:
                self.rotation = targetRot;
                break;
            case DragMode.SixDof:
                self.position = targetPos;
                self.rotation = targetRot;
                break;
        }
    }
}
