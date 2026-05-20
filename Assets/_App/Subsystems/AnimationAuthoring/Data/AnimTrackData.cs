using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimTrackData
{
    public string           NodeId;
    public List<AnimKeyData> Keys = new();

    public void UpsertKey(int frame, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        for (int i = 0; i < Keys.Count; i++)
        {
            if (Keys[i].Frame != frame) continue;
            Keys[i] = new AnimKeyData { Frame = frame, Position = pos, Rotation = rot, Scale = scale };
            return;
        }
        Keys.Add(new AnimKeyData { Frame = frame, Position = pos, Rotation = rot, Scale = scale });
        Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }

    public void RemoveKey(int frame)
    {
        for (int i = Keys.Count - 1; i >= 0; i--)
            if (Keys[i].Frame == frame) Keys.RemoveAt(i);
    }

    public bool HasKey(int frame)
    {
        foreach (var k in Keys)
            if (k.Frame == frame) return true;
        return false;
    }
}
