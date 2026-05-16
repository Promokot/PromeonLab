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
    public void Toggle_FirstCall_AddsAndSetsActive()
    {
        _sut.Toggle("a");
        Assert.AreEqual("a", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_SecondDifferent_AddsAndSetsActiveToSecond()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        Assert.AreEqual("b", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a", "b" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_ExistingActive_RemovesAndActivatesLastRemaining()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Toggle("b");
        Assert.AreEqual("a", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "a" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_ExistingNonActive_RemovesKeepsActive()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Toggle("a");
        Assert.AreEqual("b", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "b" }, _sut.SelectedIds);
    }

    [Test]
    public void Clear_EmptiesSelectionAndActive()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Clear();
        Assert.IsNull(_sut.ActiveId);
        Assert.AreEqual(0, _sut.SelectedIds.Count);
    }

    [Test]
    public void Select_ReplacesWholeSelectionWithSingle()
    {
        _sut.Toggle("a");
        _sut.Toggle("b");
        _sut.Select("c");
        Assert.AreEqual("c", _sut.ActiveId);
        CollectionAssert.AreEqual(new[] { "c" }, _sut.SelectedIds);
    }

    [Test]
    public void Toggle_PublishesSelectionChangedEvent()
    {
        SelectionChangedEvent received = default;
        bool fired = false;
        _bus.Subscribe<SelectionChangedEvent>(e => { received = e; fired = true; });
        _sut.Toggle("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("a", received.SelectedNodeId);
        CollectionAssert.AreEqual(new[] { "a" }, received.SelectedNodeIds);
    }
}
