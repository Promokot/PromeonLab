using System.Collections.Generic;

public class CommandStack
{
    private readonly int _maxHistory;
    private readonly LinkedList<ICommand> _history = new();

    public CommandStack(int maxHistory = 30) => _maxHistory = maxHistory;

    public void Execute(ICommand command)
    {
        command.Execute();
        _history.AddLast(command);
        if (_history.Count > _maxHistory)
            _history.RemoveFirst();
    }

    public void Undo()
    {
        if (_history.Count == 0) return;
        var cmd = _history.Last.Value;
        _history.RemoveLast();
        cmd.Undo();
    }
}
