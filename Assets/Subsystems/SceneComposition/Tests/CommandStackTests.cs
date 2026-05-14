using NUnit.Framework;

public class CommandStackTests
{
    private CommandStack _sut;

    [SetUp]
    public void SetUp() => _sut = new CommandStack(maxHistory: 5);

    [Test]
    public void Undo_AfterExecute_CallsUndo()
    {
        int undoCalls = 0;
        var cmd = new TestCommand(onUndo: () => undoCalls++);
        _sut.Execute(cmd);
        _sut.Undo();
        Assert.AreEqual(1, undoCalls);
    }

    [Test]
    public void Undo_EmptyStack_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.Undo());
    }

    [Test]
    public void Execute_ExceedsMaxHistory_DropsOldest()
    {
        int undoCalls = 0;
        var oldest = new TestCommand(onUndo: () => undoCalls++);
        _sut.Execute(oldest);
        for (int i = 0; i < 5; i++)
            _sut.Execute(new TestCommand(onUndo: () => { }));
        for (int i = 0; i < 5; i++) _sut.Undo();
        Assert.AreEqual(0, undoCalls);
    }

    private class TestCommand : ICommand
    {
        private readonly System.Action _onUndo;
        public TestCommand(System.Action onUndo) => _onUndo = onUndo;
        public void Execute() { }
        public void Undo() => _onUndo();
    }
}
