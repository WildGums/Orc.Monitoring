namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public class NullClassMonitor : IClassMonitor
{
    private readonly IMonitoringLoggerFactory _loggerFactory;
    private readonly ILogger<NullClassMonitor> _logger;

    public NullClassMonitor(IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<NullClassMonitor>();
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartAsyncMethod called for {callerMethod}");
        return AsyncMethodCallContext.GetDummyCallContext(() => new AsyncMethodCallContext(_loggerFactory));
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartMethod called for {callerMethod}");
        return MethodCallContext.GetDummyCallContext(() => new MethodCallContext(_loggerFactory));
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"NullClassMonitor.LogStatus called with {status.GetType().Name}");
    }
}
