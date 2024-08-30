namespace Orc.Monitoring.Tests;

using System;
using Microsoft.Extensions.Logging;

public class TestLoggerFactory<TFixture> : IMonitoringLoggerFactory
{
    private readonly TestLogger<TFixture> _fixtureLogger;

    public TestLoggerFactory(TestLogger<TFixture> fixtureLogger)
    {
        _fixtureLogger = fixtureLogger;
    }

    public ILogger<T> CreateLogger<T>()
    {
        return _fixtureLogger.CreateLogger<T>();
    }

    public ILogger CreateLogger(Type type)
    {
        return _fixtureLogger.CreateLogger(type);
    }
}
