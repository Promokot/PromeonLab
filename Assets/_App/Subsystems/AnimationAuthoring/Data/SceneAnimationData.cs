using System;
using System.Collections.Generic;

[Serializable]
public class SceneAnimationData
{
    public int                 schemaVersion = 1;
    public int                 Fps           = 30;
    public int                 TotalFrames   = 120;
    public List<AnimTrackData> Tracks        = new();

    public AnimTrackData GetOrCreateTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        var track = new AnimTrackData { NodeId = nodeId };
        Tracks.Add(track);
        return track;
    }

    public AnimTrackData FindTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        return null;
    }
}
