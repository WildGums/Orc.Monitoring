namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Reporters;


/// <summary>
/// Provides a pool of <see cref="MethodCallInfo"/> instances to optimize performance by reusing objects.
/// </summary>
public class MethodCallInfoPool
{
    private static readonly MethodCallInfo NullMethodCallInfo = new() { IsNull = true };

    private readonly IMonitoringController _monitoringController;
    private readonly ILogger<MethodCallInfoPool> _logger;
    private readonly ConcurrentBag<MethodCallInfo> _pool = new();

    public MethodCallInfoPool(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _monitoringController = monitoringController;
        _logger = loggerFactory.CreateLogger<MethodCallInfoPool>();
    }

    public static MethodCallInfoPool Instance { get; } = new MethodCallInfoPool(MonitoringController.Instance, MonitoringLoggerFactory.Instance);

    /// <summary>
    /// Rents a <see cref="MethodCallInfo"/> from the pool.
    /// </summary>
    /// <param name="classMonitor">The class monitor.</param>
    /// <param name="callerType">The type of the caller.</param>
    /// <param name="methodInfo">The method information.</param>
    /// <param name="genericArguments">The generic arguments.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="attributeParameters">The attribute parameters.</param>
    /// <param name="isExternalCall">Indicates whether this is an external method call.</param>
    /// <param name="externalTypeName">The name of the external type for external calls.</param>
    /// <returns>A <see cref="MethodCallInfo"/> instance.</returns>
    public MethodCallInfo Rent(
        IClassMonitor? classMonitor,
        Type callerType,
        MethodInfo methodInfo,
        IReadOnlyCollection<Type> genericArguments,
        string id,
        Dictionary<string, string> attributeParameters,
        bool isExternalCall = false,
        string? externalTypeName = null)
    {
        if (!_monitoringController.IsEnabled)
        {
            _logger.LogDebug($"Monitoring is disabled. Returning Null MethodCallInfo for {callerType.Name}.{methodInfo.Name}");
            return NullMethodCallInfo;
        }

        if (!_pool.TryTake(out var item))
        {
            item = new MethodCallInfo();
        }

        item.Reset(_monitoringController, classMonitor, callerType, methodInfo, genericArguments, id, attributeParameters, isExternalCall, externalTypeName);
        return item;
    }

    /// <summary>
    /// Gets a null <see cref="MethodCallInfo"/> instance.
    /// </summary>
    /// <returns>A null <see cref="MethodCallInfo"/> instance.</returns>
    public MethodCallInfo GetNull()
    {
        return NullMethodCallInfo;
    }

    /// <summary>
    /// Returns a <see cref="MethodCallInfo"/> instance to the pool.
    /// </summary>
    /// <param name="item">The <see cref="MethodCallInfo"/> to return.</param>
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

    /// <summary>
    /// Marks a <see cref="MethodCallInfo"/> as in use and returns an <see cref="IAsyncDisposable"/> that will return it to the pool.
    /// </summary>
    /// <param name="item">The <see cref="MethodCallInfo"/> to use.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that returns the item to the pool when disposed.</returns>
    public IAsyncDisposable UseAndReturn(MethodCallInfo item)
    {
        if (item.IsNull)
        {
            return AsyncDisposable.Empty;
        }

        Interlocked.Increment(ref item.UsageCounter);

        return new AsyncDisposable(async () =>
        {
            if (Interlocked.Decrement(ref item.UsageCounter) == 0 && item.ReadyToReturn)
            {
                Return(item);
            }
        });
    }
}
