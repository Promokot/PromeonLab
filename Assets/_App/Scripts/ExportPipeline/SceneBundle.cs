using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// External-facing scene description written as `scene.json` inside the export bundle.
/// One-way snapshot for an outside tool — NOT a re-importable PromeonLab format.
/// Reuses BonePose and AnimKeyData so the bundle does not duplicate those shapes.
/// </summary>
[Serializable]
public class SceneBundle
{
    public int       schemaVersion = 1;
    public string    exportedAtUtc;
    public SceneRef  scene = new();
    public int       fps = 24;
    public List<Node> nodes = new();

    [Serializable]
    public class SceneRef
    {
        public string id;
        public string name;
    }

    [Serializable]
    public class Node
    {
        public string     nodeId;
        public string     displayName;
        public string     parentNodeId;
        public string     assetSource;     // "Imported" | "Builtin"
        public string     assetId;
        public string     assetType;       // "Object" | "Rig" | "Reference"
        public string     geometryFile;    // "models/{id}.glb" / "textures/{id}.png", or "" when missing
        public bool       geometryMissing; // true when no source file was bundled (e.g. Builtin)
        public Vector3    position;
        public Quaternion rotation;
        public Vector3    scale;
        public List<BonePose> bonePoses = new();
        public Animation  animation;       // null when the node has no ActionContainer
    }

    [Serializable]
    public class Animation
    {
        public int    totalFrames;
        public string interpolation;       // "Linear" | "Stepped"
        public bool   loop;
        public List<Track> tracks = new();
    }

    [Serializable]
    public class Track
    {
        public string targetNodeId;        // object track = node id; bone track = "bone:{node}:{bone}"
        public List<AnimKeyData> keys = new();
    }
}
