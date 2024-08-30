namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;

public class MethodCallInfoPool
{
    private readonly ILogger<MethodCallInfoPool> _logger;
    private readonly ConcurrentBag<MethodCallInfo> _pool = [];

    public MethodCallInfoPool()
    : this(MonitoringController.CreateLogger<MethodCallInfoPool>())
    {
        
    }

    public MethodCallInfoPool(ILogger<MethodCallInfoPool> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public MethodCallInfo Rent(IClassMonitor? classMonitor, Type callerType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, string id, Dictionary<string, string> attributeParameters)
    {
        if (!MonitoringController.IsEnabled)
        {
            _logger.LogDebug($"Monitoring is disabled. Returning Null MethodCallInfo for {callerType.Name}.{methodInfo.Name}");
            return MethodCallInfo.CreateNull();
        }

        if (_pool.TryTake(out var item))
        {
            item.Reset(classMonitor, callerType, methodInfo, genericArguments, id, attributeParameters);
            return item;
        }

        return MethodCallInfo.Create(this, classMonitor, callerType, methodInfo, genericArguments, id, attributeParameters);
    }

    public void Return(MethodCallInfo item)
    {
        if (item.IsNull)
        {
            return;
        }

        item.Clear();
        _pool.Add(item);
    }
}
