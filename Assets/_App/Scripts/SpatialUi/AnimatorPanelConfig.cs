using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/Animator Panel Config")]
public class AnimatorPanelConfig : ScriptableObject
{
    [Header("Timeline metrics")]
    public float FramePx             = 30f;
    public float TrackNameWidth     = 100f;
    public int   MajorTickInterval   = 5;
    public int   DefaultTotalFrames  = 100;
    public int   DefaultFps          = 24;

    [Header("Key marker colors")]
    public Color KeyColor_Object   = new(0.18f, 0.50f, 0.95f, 1f);
    public Color KeyColor_Rig      = new(0.18f, 0.50f, 0.95f, 1f);
    public Color KeyColor_Bone     = new(0.33f, 0.29f, 0.72f, 1f);
    public Color KeyColor_Selected = new(0.95f, 0.69f, 0.13f, 1f);

    [Header("Track row")]
    public Color TrackRow_Active   = new(0.18f, 0.50f, 0.95f, 0.60f);
    public Color TrackRow_Inactive = Color.clear;

    [Header("Rig outliner row")]
    public Color RigRow_BonesOn    = new(0.31f, 0.51f, 0.94f, 1f);
    public Color RigRow_BonesOff   = Color.white;

    [Header("Key marker sizes")]
    public float KeySize         = 22f;
    public float KeySizeSelected = 26f;

    [Header("Ruler tick sizes")]
    public float MajorTickHeight = 24f;
    public float MinorTickHeight = 16f;

    [Header("Track row height")]
    public float RowHeight = 52f;
}
