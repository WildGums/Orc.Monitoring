namespace Orc.Monitoring;

using System;
using Microsoft.Extensions.Logging;

public sealed class MonitoringLoggerFactory : IMonitoringLoggerFactory, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    public MonitoringLoggerFactory()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    public ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}
