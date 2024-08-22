namespace Orc.Monitoring;

using System.Runtime.CompilerServices;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public class NullClassMonitor : IClassMonitor
{
    private readonly ILogger<NullClassMonitor> _logger = MonitoringController.CreateLogger<NullClassMonitor>();
    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartAsyncMethod called for {callerMethod}");
        return AsyncMethodCallContext.Dummy;
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartMethod called for {callerMethod}");
        return MethodCallContext.Dummy;
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"NullClassMonitor.LogStatus called with {status.GetType().Name}");
    }
}
