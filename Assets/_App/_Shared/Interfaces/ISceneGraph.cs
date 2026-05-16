using UnityEngine;

public interface ISceneGraph
{
    GameObject GetNode(string nodeId);
    void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null);
    void RemoveNode(string nodeId);
}
