using UnityEngine;

// Pure baking of a keyframe track into a legacy AnimationClip + interpolation tangents.
// Extracted from AnimationAuthoring (A1) so sampling/persistence no longer share a file with baking.
public static class AnimationClipBaker
{
    public static AnimationClip BuildClip(AnimTrackData track, int fps, InterpolationMode mode)
    {
        var clip = new AnimationClip { legacy = true };
        var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
        var rx = new AnimationCurve(); var ry = new AnimationCurve();
        var rz = new AnimationCurve(); var rw = new AnimationCurve();
        var sx = new AnimationCurve(); var sy = new AnimationCurve(); var sz = new AnimationCurve();

        foreach (var k in track.Keys)
        {
            float t = (float)k.Frame / fps;
            px.AddKey(t, k.Position.x); py.AddKey(t, k.Position.y); pz.AddKey(t, k.Position.z);
            rx.AddKey(t, k.Rotation.x); ry.AddKey(t, k.Rotation.y);
            rz.AddKey(t, k.Rotation.z); rw.AddKey(t, k.Rotation.w);
            sx.AddKey(t, k.Scale.x);    sy.AddKey(t, k.Scale.y);    sz.AddKey(t, k.Scale.z);
        }

        foreach (var curve in new[] { px, py, pz, rx, ry, rz, rw, sx, sy, sz })
            ApplyInterpolation(curve, mode);

        clip.SetCurve("", typeof(Transform), "localPosition.x",   px);
        clip.SetCurve("", typeof(Transform), "localPosition.y",   py);
        clip.SetCurve("", typeof(Transform), "localPosition.z",   pz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.x", rx);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.y", ry);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.z", rz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.w", rw);
        clip.SetCurve("", typeof(Transform), "localScale.x",      sx);
        clip.SetCurve("", typeof(Transform), "localScale.y",      sy);
        clip.SetCurve("", typeof(Transform), "localScale.z",      sz);
        return clip;
    }

    public static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)
    {
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            if (mode == InterpolationMode.Stepped)
            {
                keys[i].inTangent  = float.PositiveInfinity;
                keys[i].outTangent = float.PositiveInfinity;
            }
            else
            {
                if (i < keys.Length - 1)
                {
                    float dt = keys[i + 1].time - keys[i].time;
                    keys[i].outTangent = dt > 0f ? (keys[i + 1].value - keys[i].value) / dt : 0f;
                }
                if (i > 0)
                {
                    float dt = keys[i].time - keys[i - 1].time;
                    keys[i].inTangent = dt > 0f ? (keys[i].value - keys[i - 1].value) / dt : 0f;
                }
            }
        }
        curve.keys = keys;
    }
}
