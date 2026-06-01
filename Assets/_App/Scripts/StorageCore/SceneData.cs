using System;
using System.Collections.Generic;

[Serializable]
public class SceneData
{
    public int            SchemaVersion = 3;
    public string         SceneId;
    public string         DisplayName;
    public string         CreatedAt;
    public List<NodeData> Nodes = new();
}
