public class AnimationClipboard
{
    public FrameClipboard Current { get; private set; }
    public bool           IsEmpty => Current == null || Current.IsEmpty;

    public void Set(FrameClipboard clip)
    {
        if (clip == null || clip.IsEmpty) return;
        Current = clip;
    }

    public void Clear() => Current = null;
}
