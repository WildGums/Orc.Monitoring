namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public class TestLoggerFactory<TFixture> : IMonitoringLoggerFactory
{
    private readonly TestLogger<TFixture> _fixtureLogger;
    private readonly HashSet<Type> _enabledTypes = new HashSet<Type>();

    public TestLoggerFactory(TestLogger<TFixture> fixtureLogger)
    {
        _fixtureLogger = fixtureLogger;
    }

    public ILogger<T> CreateLogger<T>()
    {
        if (_enabledTypes.Contains(typeof(T)))
        {
            return _fixtureLogger.CreateLogger<T>();
        }

        return new DummyLogger<T>();
    }

    public ILogger CreateLogger(Type type)
    {
        if (_enabledTypes.Contains(type))
        {
            return _fixtureLogger.CreateLogger(type);
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
