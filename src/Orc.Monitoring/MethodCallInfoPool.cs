namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

public class MethodCallInfoPool
{
    private readonly ConcurrentBag<MethodCallInfo> _pool = new ConcurrentBag<MethodCallInfo>();

    public MethodCallInfo Rent(IClassMonitor classMonitor, Type classType, MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments, int level, string id, MethodCallInfo? parent, Dictionary<string, string> attributeParameters)
    {
        if (!MonitoringManager.IsEnabled)
        {
            return MethodCallInfo.CreateNull();
        }

        if (_pool.TryTake(out var item))
        {
            item.Reset(classMonitor, classType, methodInfo, genericArguments, level, id, parent, attributeParameters);
            return item;
        }

        return MethodCallInfo.Create(this, classMonitor, classType, methodInfo, genericArguments, level, id, parent, attributeParameters);
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
