namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public class TestLogger<T> : ILogger<T>
{
    private readonly List<string> _logMessages;

    public TestLogger()
    {
        _logMessages = new List<string>();
    }

    public TestLogger(List<string> logMessages)
    {
        _logMessages = logMessages;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);

        _logMessages.Add(message);

        Console.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");
    }

    public IEnumerable<string> LogMessages => _logMessages;

    public ILogger<T2> CreateLogger<T2>() => new TestLogger<T2>(_logMessages);

    public ILogger CreateLogger(Type type)
    {
        var loggerType = typeof(TestLogger<>).MakeGenericType(type);
        return (ILogger)Activator.CreateInstance(loggerType, _logMessages);
    }
}
