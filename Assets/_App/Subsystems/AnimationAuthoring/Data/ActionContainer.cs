using System;
using System.Collections.Generic;

[Serializable]
public class ActionContainer
{
    public string             OwnerNodeId;
    public int                Fps         = 24;
    public int                TotalFrames = 60;
    public List<AnimTrackData> Tracks     = new();

    public AnimTrackData FindTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        return null;
    }

    public AnimTrackData GetOrCreateTrack(string nodeId)
    {
        var existing = FindTrack(nodeId);
        if (existing != null) return existing;
        var track = new AnimTrackData { NodeId = nodeId };
        Tracks.Add(track);
        return track;
    }

    public bool HasAnyKeyAtFrame(int frame)
    {
        foreach (var t in Tracks)
            if (t.HasKey(frame)) return true;
        return false;
    }

    public IReadOnlyList<string> ExistingTrackNodeIds()
    {
        var ids = new string[Tracks.Count];
        for (int i = 0; i < Tracks.Count; i++) ids[i] = Tracks[i].NodeId;
        return ids;
    }

    public void TruncateToTotalFrames()
    {
        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            Tracks[i].TrimKeysAfter(TotalFrames);
            if (Tracks[i].Keys.Count == 0) Tracks.RemoveAt(i);
        }
    }
}
