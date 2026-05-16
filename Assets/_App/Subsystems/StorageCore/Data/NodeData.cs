using System;
using UnityEngine;

[Serializable]
public class NodeData
{
    public string     NodeId;
    public AssetRef   AssetRef;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
    public string     DisplayName;
    public string     ParentNodeId;
}
