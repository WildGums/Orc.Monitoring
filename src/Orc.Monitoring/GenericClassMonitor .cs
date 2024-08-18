#pragma warning disable IDISP015
#pragma warning disable IDISP005
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;

public class GenericClassMonitor : IClassMonitor
{
    private readonly ILogger<GenericClassMonitor> _logger;
    private readonly Type _genericTypeDefinition;
    private readonly CallStack _callStack;
    private readonly HashSet<string> _trackedMethodNames;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly Dictionary<Type[], ClassMonitor> _instantiatedMonitors;

    public GenericClassMonitor(Type genericTypeDefinition, CallStack callStack, HashSet<string> trackedMethodNames, MonitoringConfiguration monitoringConfig)
    {
        if (!genericTypeDefinition.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Type must be a generic type definition", nameof(genericTypeDefinition));
        }

        _genericTypeDefinition = genericTypeDefinition;
        _callStack = callStack;
        _trackedMethodNames = trackedMethodNames;
        _monitoringConfig = monitoringConfig;
        _instantiatedMonitors = new Dictionary<Type[], ClassMonitor>(new TypeArrayEqualityComparer());
        _logger = MonitoringController.CreateLogger<GenericClassMonitor>();

        _logger.LogInformation($"GenericClassMonitor created for {genericTypeDefinition.Name}. Tracked methods: {string.Join(", ", trackedMethodNames)}");
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        return (AsyncMethodCallContext)StartMethodInternal(config, callerMethod, async: true);
    }

    public MethodCallContext StartMethod(MethodConfiguration config, string callerMethod = "")
    {
        return (MethodCallContext)StartMethodInternal(config, callerMethod, async: false);
    }

    private object StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        _logger.LogDebug($"StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringController.IsEnabled}, Method tracked: {_trackedMethodNames.Contains(callerMethod)}");

        if (!MonitoringController.IsEnabled || !_trackedMethodNames.Contains(callerMethod))
        {
            return GetDummyContext(async);
        }

        var typeArguments = config.GenericArguments.ToArray();
        var instantiatedType = _genericTypeDefinition.MakeGenericType(typeArguments);

        ClassMonitor? monitor;
        lock (_instantiatedMonitors)
        {
            if (!_instantiatedMonitors.TryGetValue(typeArguments, out monitor))
            {
                monitor = new ClassMonitor(instantiatedType, _callStack, _trackedMethodNames, _monitoringConfig);
                _instantiatedMonitors[typeArguments] = monitor;
                _logger.LogInformation($"Created new ClassMonitor for {instantiatedType.Name}");
            }
        }

        return async
            ? monitor.StartAsyncMethod(config, callerMethod)
            : monitor.StartMethod(config, callerMethod);
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"LogStatus called with {status.GetType().Name}");
        var currentVersion = MonitoringController.GetCurrentVersion();
        if (!MonitoringController.ShouldTrack(currentVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        var genericArguments = status.MethodCallInfo.MethodInfo?.DeclaringType?.GetGenericArguments();
        if (genericArguments is null || genericArguments.Length == 0)
        {
            _logger.LogWarning("Unable to determine generic arguments for method call");
            return;
        }

        lock (_instantiatedMonitors)
        {
            if (_instantiatedMonitors.TryGetValue(genericArguments, out var monitor))
            {
                monitor.LogStatus(status);
            }
            else
            {
                _logger.LogWarning($"No monitor found for generic arguments: {string.Join(", ", genericArguments.Select(t => t.Name))}");
            }
        }
    }

    private static object GetDummyContext(bool async)
    {
        return async ? AsyncMethodCallContext.Dummy : MethodCallContext.Dummy;
    }

    public IEnumerable<Type> GetTrackedGenericInstantiations()
    {
        lock (_instantiatedMonitors)
        {
            return _instantiatedMonitors.Keys.Select(args => _genericTypeDefinition.MakeGenericType(args)).ToList();
        }
    }

    public int GetInstantiationCount()
    {
        lock (_instantiatedMonitors)
        {
            return _instantiatedMonitors.Count;
        }
    }

    private class TypeArrayEqualityComparer : IEqualityComparer<Type[]>
    {
        public bool Equals(Type[]? x, Type[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.SequenceEqual(y);
        }

        public int GetHashCode(Type[]? obj)
        {
            return obj?.Aggregate(17, (current, type) => current * 31 + (type?.GetHashCode() ?? 0)) ?? 0;
        }
    }
}
