#pragma warning disable CTL0011
namespace Orc.Monitoring.Core.CallStacks;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Abstractions;
using Configuration;
using Controllers;
using MethodCallContexts;
using MethodLifecycle;
using Microsoft.Extensions.Logging;
using Models;
using Monitoring.Utilities.Logging;
using Monitoring.Utilities.Metadata;
using Pooling;

public class CallStack : IObservable<ICallStackItem>
{
    internal const int MaxCallStackDepth = 1000;
    private readonly ILogger<CallStack> _logger;
    private readonly MethodCallInfoPool _methodCallInfoPool;
    private readonly ConcurrentDictionary<int, Stack<MethodCallInfo>> _threadCallStacks = new();
    private readonly Stack<MethodCallInfo> _globalCallStack = new();
    private readonly object _idLock = new();
    private readonly ConcurrentDictionary<IObserver<ICallStackItem>, object?> _observers = new();
    private readonly IMonitoringController _monitoringController;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly ConcurrentDictionary<int, MethodCallInfo> _threadRootMethods = new();
    private readonly object _globalLock = new();

    private MethodCallInfo? _rootParent;
    private int _idCounter;
    private readonly ThreadLocal<int> _currentDepth = new(() => 0);

    public CallStack(IMonitoringController monitoringController, MonitoringConfiguration monitoringConfig, MethodCallInfoPool methodCallInfoPool, IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(monitoringConfig);
        ArgumentNullException.ThrowIfNull(methodCallInfoPool);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _monitoringController = monitoringController;
        _monitoringConfig = monitoringConfig;
        _methodCallInfoPool = methodCallInfoPool;
        _logger = loggerFactory.CreateLogger<CallStack>();
    }

    public MethodCallInfo CreateMethodCallInfo(IClassMonitor? classMonitor, Type callerType, MethodCallContextConfig config, MethodInfo? methodInfo = null, bool isExternalCall = false)
    {
        methodInfo ??= ReflectionHelper.FindMatchingMethod(config.ClassType, config.CallerMethodName, config.GenericArguments, config.ParameterTypes);
        if (methodInfo is null)
        {
            var classTypeName = config.ClassType?.Name ?? string.Empty;
            throw new InvalidOperationException($"Method {config.CallerMethodName} not found in {classTypeName} with the specified parameter types");
        }

        var attributeParameters = methodInfo.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .ToDictionary(attr => attr.Name, attr => attr.Value);

        foreach (var configStaticParameter in config.StaticParameters ?? [])
        {
            attributeParameters[configStaticParameter.Key] = configStaticParameter.Value;
        }

        var id = GenerateId();

        var result = _methodCallInfoPool.Rent(classMonitor, callerType, methodInfo, config.GenericArguments, id, attributeParameters, isExternalCall);

        if (result.IsNull)
        {
            _logger.LogWarning($"Created Null MethodCallInfo for {callerType.Name}.{methodInfo.Name}. Monitoring enabled: {_monitoringController.IsEnabled}");
        }

        result.IsStatic = methodInfo.IsStatic;
        result.IsExtensionMethod = methodInfo.IsDefined(typeof(ExtensionAttribute), false);
        if (result.IsExtensionMethod)
        {
            result.ExtendedType = methodInfo.GetParameters()[0].ParameterType;
        }

        return result;
    }

    public void Push(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        _currentDepth.Value++;

        if (_currentDepth.Value >= MaxCallStackDepth)
        {
            throw new InvalidOperationException("Maximum call stack depth exceeded");
        }

        var threadId = methodCallInfo.ThreadId;
        var threadStack = _threadCallStacks.GetOrAdd(threadId, _ => new Stack<MethodCallInfo>());

        lock (_globalLock)
        {
            if (_rootParent is null)
            {
                _rootParent = methodCallInfo;
                methodCallInfo.Parent = _methodCallInfoPool.GetNull();
                methodCallInfo.Level = 1;
                methodCallInfo.ParentThreadId = -1;
            }
            else
            {
                MethodCallInfo parent;
                if (threadStack.Count == 0 || threadId != _rootParent.ThreadId)
                {
                    parent = _rootParent;
                }
                else
                {
                    parent = threadStack.Peek();
                }

                methodCallInfo.Parent = parent;
                methodCallInfo.Level = parent.Level + 1;
                methodCallInfo.ParentThreadId = parent.ThreadId;
            }

            threadStack.Push(methodCallInfo);
            _globalCallStack.Push(methodCallInfo);

            if (threadStack.Count == 1)
            {
                _threadRootMethods[threadId] = methodCallInfo;
            }

            _logger.LogDebug("Pushed: {MethodCallInfo}", methodCallInfo);

            //var currentVersion = _monitoringController.GetCurrentVersion();
            //if (_monitoringController.ShouldTrack(currentVersion))
            //{
            //    NotifyObservers(new MethodCallStart(methodCallInfo), currentVersion);
            //}
        }
    }

    public void Pop(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        _currentDepth.Value--;

        var threadId = methodCallInfo.ThreadId;
        lock (_globalLock)
        {
            if (_threadCallStacks.TryGetValue(threadId, out var threadStack))
            {
                if (threadStack.Count > 0)
                {
                    var poppedContext = threadStack.Pop();

                    if (poppedContext != methodCallInfo)
                    {
                        _logger.LogWarning("Thread CallStack mismatch: popped context is not the same as the method call info.");
                    }

                    if (threadStack.Count == 0)
                    {
                        _threadCallStacks.TryRemove(threadId, out _);
                        _threadRootMethods.TryRemove(threadId, out _);
                    }

                    _logger.LogDebug("Popped: {MethodCallInfo}", methodCallInfo);

                    //var currentVersion = _monitoringController.GetCurrentVersion();
                    //if (_monitoringController.ShouldTrack(currentVersion))
                    //{
                    //    NotifyObservers(new MethodCallEnd(methodCallInfo), currentVersion);
                    //}
                }
                else
                {
                    _logger.LogWarning("Thread CallStack mismatch: no context found for thread {ThreadId}.", threadId);
                }
            }
            else
            {
                _logger.LogWarning("Thread CallStack mismatch: no stack found for thread {ThreadId}.", threadId);
            }

            if (_globalCallStack.Count > 0 && _globalCallStack.Peek() == methodCallInfo)
            {
                _globalCallStack.Pop();
            }
            else
            {
                _logger.LogWarning("Global CallStack mismatch: popped context is not the same as the method call info.");
            }

            if (methodCallInfo == _rootParent)
            {
                _rootParent = null;
            }
        }
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        ArgumentNullException.ThrowIfNull(status);

        _logger.LogDebug("LogStatus called with {StatusTypeName}", status.GetType().Name);

        var currentVersion = _monitoringController.GetCurrentVersion();
        if (!_monitoringController.ShouldTrack(currentVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status");
            return;
        }

        if (ShouldLogStatus(status))
        {
            _logger.LogDebug("Notifying observers: {Status}", status);
            NotifyObservers(status, currentVersion);

            if (status is MethodCallEnd && IsEmpty())
            {
                _logger.LogDebug("Call stack is empty, notifying with CallStackItem.Empty");
                NotifyObservers(CallStackItem.Empty, currentVersion);
            }
        }
        else
        {
            _logger.LogDebug("Skipping status logging: {Status}", status);
        }
    }

    private bool ShouldLogStatus(IMethodLifeCycleItem status)
    {
        var methodCallInfo = status.MethodCallInfo;
        if(methodCallInfo is null)
        {
            return false;
        }

        var methodInfo = methodCallInfo.MethodInfo;
        if (methodInfo is null)
        {
            return false;
        }

        var applicableReporters = _monitoringConfig.ReporterTypes;
        var applicableFilters = _monitoringConfig.Filters;

        // Check if any enabled reporter is interested in this status
        var anyEnabledReporterInterested = !applicableReporters.Any() || applicableReporters.Any(_monitoringController.IsReporterEnabled);

        // Check if any enabled filter allows this status
        var anyEnabledFilterAllows = !applicableFilters.Any() || applicableFilters.Any(filter =>
        {
            if (_monitoringController.IsFilterEnabled(filter.GetType()))
            {
                return filter.ShouldInclude(methodCallInfo);
            }
            return false;
        });

        return anyEnabledReporterInterested && anyEnabledFilterAllows;
    }

    private bool IsEmpty()
    {
        foreach (var stack in _threadCallStacks.Values)
        {
            if (stack.Count > 0)
            {
                return false;
            }
        }
        return true;
    }

    private string GenerateId()
    {
        lock (_idLock)
        {
            var number = _idCounter++;
            var sb = new StringBuilder();
            while (number >= 0)
            {
                sb.Insert(0, (char)('A' + number % 26));
                number = number / 26 - 1;
            }
            return sb.ToString();
        }
    }

    public IDisposable Subscribe(IObserver<ICallStackItem> observer)
    {
        _observers.TryAdd(observer, null);
        return new Unsubscriber<ICallStackItem>(_observers, observer);
    }

    private void NotifyObservers(ICallStackItem value, MonitoringVersion version)
    {
        if (!_monitoringController.ShouldTrack(version))
        {
            return;
        }

        foreach (var observer in _observers.Keys)
        {
            observer.OnNext(value);
        }
    }

    private sealed class Unsubscriber<T> : IDisposable
    {
        private readonly ConcurrentDictionary<IObserver<T>, object?> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(ConcurrentDictionary<IObserver<T>, object?> observers, IObserver<T> observer)
        {
            _observers = observers;
            _observer = observer;
        }

        public void Dispose()
        {
            _observers.TryRemove(_observer, out _);
        }
    }
}
