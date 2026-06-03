using NUnit.Framework;

public class PathProviderTests
{
    private PathProvider _sut;

    [SetUp]
    public void SetUp() => _sut = new PathProvider("/data");

    [Test]
    public void SceneRoot_ReturnsExpectedPath()
    {
        Assert.AreEqual("/data/scenes/scene-01", _sut.SceneRoot("scene-01"));
    }

    [Test]
    public void SceneJson_ReturnsExpectedPath()
    {
        Assert.AreEqual("/data/scenes/scene-01/scene.json", _sut.SceneJson("scene-01"));
    }

    [Test]
    public void AnimationJson_ReturnsExpectedPath()
    {
        Assert.AreEqual("/data/scenes/scene-01/animation.json",
            _sut.AnimationJson("scene-01"));
    }
}
