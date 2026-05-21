using NUnit.Framework;

public class SelectionManagerTests
{
    private EventBus _bus;
    private SelectionManager _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _sut = new SelectionManager(_bus);
    }

    [Test]
    public void Select_SetsSelectedNodeId()
    {
        _sut.Select("a");
        Assert.AreEqual("a", _sut.SelectedNodeId);
    }

    [Test]
    public void Select_ReplacesPrevious()
    {
        _sut.Select("a");
        _sut.Select("b");
        Assert.AreEqual("b", _sut.SelectedNodeId);
    }

    [Test]
    public void Select_Null_Clears()
    {
        _sut.Select("a");
        _sut.Select(null);
        Assert.IsNull(_sut.SelectedNodeId);
    }

    [Test]
    public void Select_PublishesSelectionChangedEvent()
    {
        SelectionChangedEvent received = default;
        bool fired = false;
        _bus.Subscribe<SelectionChangedEvent>(e => { received = e; fired = true; });
        _sut.Select("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("a", received.SelectedNodeId);
    }

    [Test]
    public void Select_SameId_DoesNotRefire()
    {
        int count = 0;
        _bus.Subscribe<SelectionChangedEvent>(_ => count++);
        _sut.Select("a");
        _sut.Select("a");
        Assert.AreEqual(1, count);
    }
}
