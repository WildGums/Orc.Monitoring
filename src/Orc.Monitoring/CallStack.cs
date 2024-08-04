#pragma warning disable CTL0011
#pragma warning disable CTL0011
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using MethodLifeCycleItems;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Filters;

public class CallStack : IObservable<ICallStackItem>
{
    private readonly ILogger<CallStack> _logger = MonitoringManager.CreateLogger<CallStack>();

    private readonly MethodCallInfoPool _methodCallInfoPool = new();
    private readonly ConcurrentDictionary<int, Stack<MethodCallInfo>> _threadCallStacks = new();
    private readonly ConcurrentStack<MethodCallInfo> _globalCallStack = new();
    private readonly object _idLock = new();
    private readonly List<IObserver<ICallStackItem>> _observers = new();
    private readonly MonitoringConfiguration _monitoringConfig;

    private int _idCounter = 0;

    public CallStack(MonitoringConfiguration monitoringConfig)
    {
        ArgumentNullException.ThrowIfNull(monitoringConfig);

        _monitoringConfig = monitoringConfig;
    }

    public MethodCallInfo Push(IClassMonitor classMonitor, Type callerType, MethodCallContextConfig config)
    {
        _logger.LogDebug($"CallStack.Push called for {callerType.Name}.{config.CallerMethodName}");

        if (!MonitoringManager.IsEnabled)
        {
            return MethodCallInfo.CreateNull();
        }

        var threadId = Environment.CurrentManagedThreadId;
        var threadStack = _threadCallStacks.GetOrAdd(threadId, _ => new Stack<MethodCallInfo>());

        var methodInfo = FindMatchingMethod(config);

        if (methodInfo is null)
        {
            var classTypeName = config.ClassType?.Name ?? string.Empty;
            throw new InvalidOperationException($"Method {config.CallerMethodName} not found in {classTypeName} with the specified parameter types");
        }

        var attributeParameters = methodInfo.GetCustomAttributes<MethodCallParameterAttribute>()
            .ToDictionary(attr => attr.Name, attr => attr.Value);

        lock (threadStack)
        {
            var level = threadStack.Count + 1;
            var id = GenerateId();

            MethodCallInfo? parentMethodCallInfo = null;
            if (threadStack.Count > 0)
            {
                parentMethodCallInfo = threadStack.Peek();
            }
            else if (_globalCallStack.TryPeek(out var globalParent))
            {
                parentMethodCallInfo = globalParent;
            }

            var methodCallInfo = _methodCallInfoPool.Rent(classMonitor, callerType, methodInfo, config.GenericArguments, level, id, parentMethodCallInfo, attributeParameters);

            if (!methodCallInfo.IsNull)
            {
                threadStack.Push(methodCallInfo);
                _globalCallStack.Push(methodCallInfo);
            }

            return methodCallInfo;
        }
    }

    public void Pop(MethodCallInfo methodCallInfo)
    {
        _logger.LogDebug($"CallStack.Pop called for {methodCallInfo}");

        if (methodCallInfo.IsNull)
        {
            return;
        }

        var threadId = methodCallInfo.ThreadId;

        if (_threadCallStacks.TryGetValue(threadId, out var threadStack))
        {
            lock (threadStack)
            {
                if (threadStack.Count > 0)
                {
                    var poppedContext = threadStack.Pop();

                    if (poppedContext != methodCallInfo)
                    {
                        _logger.LogWarning($"Thread CallStack mismatch: popped context is not the same as the method call info.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Thread CallStack mismatch: no context found for thread {threadId}.");
                }
            }
        }
        else
        {
            _logger.LogWarning($"Thread CallStack mismatch: no stack found for thread {threadId}.");
        }

        if (_globalCallStack.TryPop(out var globalPoppedContext))
        {
            if (globalPoppedContext != methodCallInfo)
            {
                _logger.LogWarning("Global CallStack mismatch: popped context is not the same as the method call info.");
            }
        }
        else
        {
            _logger.LogWarning("Global CallStack mismatch: failed to pop method call info.");
        }
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"LogStatus called with {status.GetType().Name}");
        if (!MonitoringManager.ShouldTrack(status.MethodCallInfo.MonitoringVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (ShouldLogStatus(status))  // Add this check
        {
            _logger.LogDebug($"Notifying observers: {status}");
            NotifyObservers(status);

            if (status is MethodCallEnd && IsEmpty())
            {
                _logger.LogDebug("Call stack is empty, notifying with CallStackItem.Empty");
                NotifyObservers(CallStackItem.Empty);
            }
        }
        else
        {
            _logger.LogDebug($"Skipping status logging: {status}");
        }
    }

    private bool ShouldLogStatus(IMethodLifeCycleItem status)
    {
        var methodInfo = status.MethodCallInfo.MethodInfo;
        if (methodInfo is null)
        {
            return false;
        }

        var applicableReporters = _monitoringConfig.GetReportersForMethod(methodInfo);
        var applicableFilters = _monitoringConfig.GetFiltersForMethod(methodInfo);

        // Check if any enabled reporter is interested in this status
        bool anyEnabledReporterInterested = applicableReporters.Any(reporter =>
            MonitoringManager.IsReporterEnabled(reporter));

        // Check if any enabled filter allows this status
        bool anyEnabledFilterAllows = applicableFilters.Any(filter =>
        {
            if (MonitoringManager.IsFilterEnabled(filter))
            {
                var filterInstance = (IMethodFilter)Activator.CreateInstance(filter)!;
                return filterInstance.ShouldInclude(status.MethodCallInfo);
            }
            return false;
        });

        return anyEnabledReporterInterested && anyEnabledFilterAllows;
    }

    private MethodInfo? FindMatchingMethod(MethodCallContextConfig config)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var classType = config.ClassType;
        var methodName = config.CallerMethodName;

        if (classType is null)
        {
            return null;
        }


        // First, try to find the method in the current class
        var currentClassMethods = classType.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName)
            .ToList();

        var matchedMethod = FindBestMatch(currentClassMethods, config);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If not found in the current class, search through the hierarchy
        var allMethods = new List<MethodInfo>();
        var currentType = classType.BaseType;

        while (currentType is not null)
        {
            allMethods.AddRange(currentType.GetMethods(bindingFlags).Where(m => m.Name == methodName));
            currentType = currentType.BaseType;
        }

        matchedMethod = FindBestMatch(allMethods, config);
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If we still can't determine, throw an exception
        var classTypeName = config.ClassType?.Name ?? string.Empty;
        throw new InvalidOperationException($"No matching method named {methodName} found in {classTypeName} or its base classes that matches the provided configuration.");
    }

    private MethodInfo? FindBestMatch(List<MethodInfo> methods, MethodCallContextConfig config)
    {
        if (!methods.Any())
        {
            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }

        // If we have multiple methods, try to match based on parameter types
        if (config.ParameterTypes.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => ParametersMatch(m.GetParameters(), config.ParameterTypes));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        // If we still can't determine, and it's a generic method, try to match based on generic arguments
        if (config.GenericArguments.Any())
        {
            var matchedMethod = methods.FirstOrDefault(m => GenericArgumentsMatch(m, config.GenericArguments));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        var classTypeName = config.ClassType?.Name ?? string.Empty;

        throw new AmbiguousImplementationException(
            $"Multiple methods found in {classTypeName} with the name {methods[0].Name} that match the provided configuration.");
    }

    private bool ParametersMatch(ParameterInfo[] methodParams, IReadOnlyCollection<Type> configParams)
    {
        if (methodParams.Length != configParams.Count)
        {
            return false;
        }

        for (var i = 0; i < methodParams.Length; i++)
        {
            if (!methodParams[i].ParameterType.IsAssignableFrom(configParams.ElementAt(i)))
            {
                return false;
            }
        }

        return true;
    }

    private bool GenericArgumentsMatch(MethodInfo method, IReadOnlyCollection<Type> configGenericArgs)
    {
        if (!method.IsGenericMethod)
        {
            return false;
        }

        var genericArgs = method.GetGenericArguments();
        return genericArgs.Length == configGenericArgs.Count;
    }

    private bool IsEmpty() => _threadCallStacks.Values.All(t => t.Count == 0);

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
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }

        return new Unsubscriber<ICallStackItem>(_observers, observer);
    }

    private void NotifyObservers(ICallStackItem value)
    {
        Console.WriteLine($"Notifying observers: {value}");
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(value);
        }
    }

    private sealed class Unsubscriber<T> : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
        {
            _observers = observers;
            _observer = observer;
        }

        public void Dispose()
        {
            if (_observer is not null && _observers.Contains(_observer))
            {
                _observers.Remove(_observer);
            }
        }
    }
}
