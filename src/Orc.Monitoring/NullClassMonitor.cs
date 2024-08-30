namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public class NullClassMonitor : IClassMonitor
{
    private readonly ILogger<NullClassMonitor> _logger;

    public NullClassMonitor()
    : this(MonitoringController.CreateLogger<NullClassMonitor>())
    {
        
    }

    public NullClassMonitor(ILogger<NullClassMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartAsyncMethod called for {callerMethod}");
        return AsyncMethodCallContext.GetDummyCallContext(() => new AsyncMethodCallContext(MonitoringController.CreateLogger<AsyncMethodCallContext>()));
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartMethod called for {callerMethod}");
        return MethodCallContext.GetDummyCallContext(() => new MethodCallContext(MonitoringController.CreateLogger<MethodCallContext>()));
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"NullClassMonitor.LogStatus called with {status.GetType().Name}");
    }
}
