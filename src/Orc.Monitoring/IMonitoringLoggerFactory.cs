namespace Orc.Monitoring;

using System;
using Microsoft.Extensions.Logging;

public interface IMonitoringLoggerFactory
{
    ILogger<T> CreateLogger<T>();
    ILogger CreateLogger(Type type);
}
