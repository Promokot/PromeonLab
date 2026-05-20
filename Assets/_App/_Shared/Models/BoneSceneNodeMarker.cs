using UnityEngine;

/// Marker for a proxy bone's SceneNode. SceneGraph uses this to locate
/// baked bone proxies in a spawned rig and rewrite their NodeId into the
/// runtime "bone:{rigNodeId}:{boneName}" form, then register them as
/// transient nodes (findable by GetNode, invisible to the outliner).
[DisallowMultipleComponent]
public class BoneSceneNodeMarker : MonoBehaviour
{
}
