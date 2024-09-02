namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters;

public class MethodCallInfoPool
{
    private static readonly MethodCallInfo Null = new() { IsNull = true };

    private readonly IMonitoringController _monitoringController;
    private readonly ILogger<MethodCallInfoPool> _logger;
    private readonly ConcurrentBag<MethodCallInfo> _pool = [];

    public MethodCallInfoPool(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _monitoringController = monitoringController;
        _logger = loggerFactory.CreateLogger<MethodCallInfoPool>();
    }

    internal static MethodCallInfoPool Instance { get; } = new MethodCallInfoPool(MonitoringController.Instance, MonitoringLoggerFactory.Instance);

    public MethodCallInfo Rent(IClassMonitor? classMonitor, Type callerType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, string id, Dictionary<string, string> attributeParameters)
    {
        if (!_monitoringController.IsEnabled)
        {
            _logger.LogDebug($"Monitoring is disabled. Returning Null MethodCallInfo for {callerType.Name}.{methodInfo.Name}");
            return Null;
        }

        if (!_pool.TryTake(out var item))
        {
            item = new MethodCallInfo();
        }

        item.Reset(_monitoringController, classMonitor, callerType, methodInfo, genericArguments, id, attributeParameters);
        return item;
    }

    public MethodCallInfo GetNull()
    {
        return Null;
    }

    public void Return(MethodCallInfo item)
    {
        if (item.IsNull)
        {
            return;
        }

        item.ReadyToReturn = true;

        if (item.UsageCounter == 0)
        {
            item.Clear();
            _pool.Add(item);
        }
    }

    public IAsyncDisposable UseAndReturn(MethodCallInfo item)
    {
        if (item.IsNull)
        {
            return new AsyncDisposable(async () => { });
        }

        item.UsageCounter++;

        return new AsyncDisposable(async () =>
        {
            item.UsageCounter--;
            if (item.UsageCounter == 0 && item.ReadyToReturn)
            {
                Return(item);
            }
        });
    }
}
