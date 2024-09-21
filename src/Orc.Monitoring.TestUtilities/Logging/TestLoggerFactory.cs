namespace Orc.Monitoring.TestUtilities.Logging;

using System;
using System.Collections.Generic;
using Core.Abstractions;
using Microsoft.Extensions.Logging;
using Utilities.Logging;

public class TestLoggerFactory<TFixture>(TestLogger<TFixture> fixtureLogger) : IMonitoringLoggerFactory
{
    private readonly HashSet<Type> _enabledTypes = [];

    public ILogger<T> CreateLogger<T>()
    {
        if (_enabledTypes.Contains(typeof(T)))
        {
            return fixtureLogger.CreateLogger<T>();
        }

        return new DummyLogger<T>();
    }

    public ILogger CreateLogger(Type type)
    {
        if (_enabledTypes.Contains(type))
        {
            return fixtureLogger.CreateLogger(type);
        }

        return new DummyLogger();
    }

    public void EnableLoggingFor<T>()
    {
        _enabledTypes.Add(typeof(T));
    }

    private class DummyLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }

    private class DummyLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }
}
