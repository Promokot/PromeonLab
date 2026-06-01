using System;
using System.Collections.Generic;
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
    public List<BonePose> BonePoses = new(); // empty for non-rig nodes and pre-v3 scenes
}
