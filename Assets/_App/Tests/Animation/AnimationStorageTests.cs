using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class AnimationStorageTests
{
    private string _root;
    private PathProvider _paths;

    [SetUp]
    public void SetUp()
    {
        _root  = Path.Combine(Path.GetTempPath(), "animstore-" + System.Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "scenes", "s1"));
        _paths = new PathProvider(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public async Task LoadAsync_OldVersionFile_IsLeftOnDisk_AndReturnsEmpty()
    {
        var path = _paths.AnimationJson("s1");
        File.WriteAllText(path, "{\"schemaVersion\":1}");
        var sut = new AnimationStorage(_paths);

        var data = await sut.LoadAsync("s1", CancellationToken.None);

        Assert.IsNotNull(data, "returns fresh data");
        Assert.AreEqual(0, data.Containers.Count, "empty");
        Assert.IsTrue(File.Exists(path), "B4: old file must NOT be deleted");
    }

    [Test]
    public async Task LoadAsync_MissingFile_ReturnsEmpty()
    {
        var sut  = new AnimationStorage(_paths);
        var data = await sut.LoadAsync("s1", CancellationToken.None);
        Assert.IsNotNull(data);
        Assert.AreEqual(0, data.Containers.Count);
    }
}
