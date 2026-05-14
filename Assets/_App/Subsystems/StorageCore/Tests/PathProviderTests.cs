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
    public void AssetPath_ReturnsExpectedPath()
    {
        Assert.AreEqual("/data/scenes/scene-01/assets/Models/mesh.fbx",
            _sut.AssetPath("scene-01", "Models/mesh.fbx"));
    }
}
