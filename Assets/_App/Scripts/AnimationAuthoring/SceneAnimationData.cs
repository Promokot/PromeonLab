using System;
using System.Collections.Generic;

[Serializable]
public class SceneAnimationData
{
    public int                   schemaVersion = 2;
    public List<ActionContainer> Containers    = new();

    public ActionContainer FindByOwner(string ownerNodeId)
    {
        foreach (var c in Containers)
            if (c.OwnerNodeId == ownerNodeId) return c;
        return null;
    }

    public ActionContainer CreateContainer(string ownerNodeId, int totalFrames = 60, int fps = 24)
    {
        var existing = FindByOwner(ownerNodeId);
        if (existing != null) return existing;
        var c = new ActionContainer
        {
            OwnerNodeId = ownerNodeId,
            TotalFrames = totalFrames,
            Fps         = fps
        };
        Containers.Add(c);
        return c;
    }

    public void RemoveContainer(string ownerNodeId)
    {
        for (int i = Containers.Count - 1; i >= 0; i--)
            if (Containers[i].OwnerNodeId == ownerNodeId) Containers.RemoveAt(i);
    }
}
