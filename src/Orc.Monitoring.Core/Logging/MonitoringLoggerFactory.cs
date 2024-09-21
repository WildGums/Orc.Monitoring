namespace Orc.Monitoring.Core.Logging;

using System;
using System.Collections.Generic;
using Abstractions;
using Microsoft.Extensions.Logging;

public sealed class MonitoringLoggerFactory : IMonitoringLoggerFactory, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    private readonly Dictionary<Type, ILogger> _loggers = new();

    public static MonitoringLoggerFactory Instance { get; } = new();

    public MonitoringLoggerFactory()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public ILogger<T> CreateLogger<T>()
    {
        if (!_loggers.TryGetValue(typeof(T), out var logger))
        {
            logger = _loggerFactory.CreateLogger<T>();
            _loggers[typeof(T)] = logger;

        }

        return (ILogger<T>)logger;
    }

    public ILogger CreateLogger(Type type)
    {
        if (!_loggers.TryGetValue(type, out var logger))
        {
            logger = _loggerFactory.CreateLogger(type);
            _loggers[type] = logger;
        }

        return logger;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}
