using System;
using System.Collections.Generic;

public class EventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<object>();
        _handlers[type].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
            list.Remove(handler);
    }

    public void Publish<T>(T message) where T : struct
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;
        foreach (var handler in list.ToArray())
            ((Action<T>)handler).Invoke(message);
    }
}
