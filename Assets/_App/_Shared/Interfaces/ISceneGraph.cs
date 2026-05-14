using UnityEngine;

public interface ISceneGraph
{
    GameObject GetNode(string nodeId);
    void AddNode(GameObject go);
    void RemoveNode(string nodeId);
}
