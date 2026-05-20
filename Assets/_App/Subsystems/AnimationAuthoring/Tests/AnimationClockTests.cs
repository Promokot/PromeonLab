using NUnit.Framework;

public class AnimationClockTests
{
    private EventBus       _bus;
    private AnimationClock _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _sut = new AnimationClock(_bus);
    }

    [Test]
    public void InitialState_IsNotPlaying_FrameZero()
    {
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(0, _sut.CurrentFrame);
    }

    [Test]
    public void Play_SetsIsPlayingTrue()
    {
        _sut.Play();
        Assert.IsTrue(_sut.IsPlaying);
    }

    [Test]
    public void Pause_SetsIsPlayingFalse_PreservesFrame()
    {
        _sut.Seek(10);
        _sut.Play();
        _sut.Pause();
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(10, _sut.CurrentFrame);
    }

    [Test]
    public void Stop_ResetsFrameToZero()
    {
        _sut.Seek(30);
        _sut.Play();
        _sut.Stop();
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(0, _sut.CurrentFrame);
    }

    [Test]
    public void Seek_ClampsToRange()
    {
        _sut.Seek(-5);
        Assert.AreEqual(0, _sut.CurrentFrame);

        _sut.Seek(9999);
        Assert.AreEqual(_sut.TotalFrames, _sut.CurrentFrame);
    }

    [Test]
    public void Seek_PublishesFrameChangedEvent()
    {
        int received = -1;
        _bus.Subscribe<FrameChangedEvent>(e => received = e.Frame);
        _sut.Seek(42);
        Assert.AreEqual(42, received);
    }

    [Test]
    public void Play_AtEndFrame_RewindsToZero()
    {
        _sut.Seek(_sut.TotalFrames);
        _sut.Play();
        Assert.AreEqual(0, _sut.CurrentFrame);
        Assert.IsTrue(_sut.IsPlaying);
    }
}
