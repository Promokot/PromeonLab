using UnityEngine;

public interface IGizmoDragStrategy
{
    void BeginDrag(Transform target, AxisKind axis, Vector3 handPos, Quaternion handRot);
    void UpdateDrag(Vector3 handPos, Quaternion handRot);
    void EndDrag();
}
