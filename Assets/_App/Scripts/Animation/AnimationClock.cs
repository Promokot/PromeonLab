using UnityEngine;
using VContainer.Unity;

public class AnimationClock : ITickable
{
    public int  CurrentFrame { get; private set; }
    public int  TotalFrames  { get; private set; } = 60;
    public int  Fps          { get; private set; } = 24;
    public bool IsPlaying    { get; private set; }

    private float            _accumulated;
    private readonly EventBus _bus;

    public AnimationClock(EventBus bus) => _bus = bus;

    public void Tick()
    {
        if (!IsPlaying) return;
        _accumulated += Time.deltaTime * Fps;
        var next = Mathf.FloorToInt(_accumulated);
        if (next == CurrentFrame) return;
        AdvanceFrame(next);
    }

    internal void AdvanceFrame(int next)
    {
        if (next >= TotalFrames)
        {
            IsPlaying    = false;
            CurrentFrame = 0;
            _accumulated = 0f;
            _bus.Publish(new FrameChangedEvent         { Frame = 0 });
            _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });
            return;
        }

        CurrentFrame = next;
        _bus.Publish(new FrameChangedEvent         { Frame     = CurrentFrame });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = IsPlaying, Frame = CurrentFrame });
    }

    public void Play()
    {
        if (CurrentFrame >= TotalFrames) Seek(0);
        IsPlaying = true;
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = true, Frame = CurrentFrame });
    }

    public void Pause()
    {
        IsPlaying = false;
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = CurrentFrame });
    }

    public void Stop()
    {
        IsPlaying    = false;
        _accumulated = 0f;
        CurrentFrame = 0;
        _bus.Publish(new FrameChangedEvent         { Frame     = 0 });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0 });
    }

    public void Seek(int frame)
    {
        CurrentFrame = Mathf.Clamp(frame, 0, TotalFrames);
        _accumulated = CurrentFrame;
        _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
    }

    public void Configure(int totalFrames, int fps)
    {
        TotalFrames = Mathf.Max(1, totalFrames);
        Fps         = Mathf.Max(1, fps);

        if (CurrentFrame > TotalFrames)
        {
            CurrentFrame = TotalFrames;
            _accumulated = TotalFrames;
            _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
        }
    }
}
