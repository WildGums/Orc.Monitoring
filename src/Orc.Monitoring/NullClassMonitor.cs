namespace Orc.Monitoring;

using System;
using System.Runtime.CompilerServices;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;

public class NullClassMonitor : IClassMonitor
{
    private readonly MethodCallContextFactory _methodCallContextFactory;
    private readonly ILogger<NullClassMonitor> _logger;

    public NullClassMonitor(IMonitoringLoggerFactory loggerFactory, MethodCallContextFactory methodCallContextFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(methodCallContextFactory);

        _methodCallContextFactory = methodCallContextFactory;
        _logger = loggerFactory.CreateLogger<NullClassMonitor>();
    }

    public IMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartAsyncMethod called for {callerMethod}");
        return _methodCallContextFactory.GetDummyAsyncMethodCallContext();
    }

    public IMethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        _logger.LogDebug($"NullClassMonitor.StartMethod called for {callerMethod}");
        return _methodCallContextFactory.GetDummyMethodCallContext();
    }

    public IMethodCallContext StartExternalMethod(MethodConfiguration config, Type externalType,
        string externalMethodName,
        bool async = false)
    {
        _logger.LogDebug($"NullClassMonitor.StartExternalMethod called for {externalType.Name}.{externalMethodName}");
        return async
            ? _methodCallContextFactory.GetDummyAsyncMethodCallContext() 
            : _methodCallContextFactory.GetDummyMethodCallContext();

    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"NullClassMonitor.LogStatus called with {status.GetType().Name}");
    }
}
