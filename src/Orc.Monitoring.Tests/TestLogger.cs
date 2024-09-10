namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public class TestLogger<T>(List<string> logMessages) : ILogger<T>
{
    public TestLogger() : this([])
    {
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);

        logMessages.Add(message);

        Console.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");
    }

    public IEnumerable<string> LogMessages => logMessages;

    public ILogger<T2> CreateLogger<T2>() => new TestLogger<T2>(logMessages);

    public ILogger CreateLogger(Type type)
    {
        var loggerType = typeof(TestLogger<>).MakeGenericType(type);
        return (ILogger)Activator.CreateInstance(loggerType, logMessages);
    }
}
