#pragma warning disable CTL0011
namespace Orc.Monitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Orc.Monitoring.MethodLifeCycleItems;
using Microsoft.Extensions.Logging;
using System.Runtime;
using Orc.Monitoring.Reporters;

public class CallStack : IObservable<ICallStackItem>
{
    private const int MaxCallStackDepth = 1000;
    private readonly ILogger<CallStack> _logger;
    private readonly MethodCallInfoPool _methodCallInfoPool;
    private readonly ConcurrentDictionary<int, Stack<MethodCallInfo>> _threadCallStacks = new();
    private readonly ConcurrentStack<MethodCallInfo> _globalCallStack = new();
    private readonly object _idLock = new();
    private readonly List<IObserver<ICallStackItem>> _observers = [];
    private readonly IMonitoringController _monitoringController;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly ConcurrentDictionary<int, MethodCallInfo> _threadRootMethods = new();
    private readonly object _globalLock = new();
    private readonly ConcurrentDictionary<IMethodCallReporter, MethodCallInfo> _reporterRootMethods = new();

    private MethodCallInfo? _rootParent;
    private int _idCounter;
    private int _currentDepth = 0;

    public CallStack(IMonitoringController monitoringController,  MonitoringConfiguration? monitoringConfig, MethodCallInfoPool methodCallInfoPool, IMonitoringLoggerFactory loggerFactory)
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

    public MethodCallInfo CreateMethodCallInfo(IClassMonitor? classMonitor, Type callerType, MethodCallContextConfig config, MethodInfo? methodInfo = null)
    {
        methodInfo ??= FindMatchingMethod(config);
        if (methodInfo is null)
        {
            var classTypeName = config.ClassType?.Name ?? string.Empty;
            throw new InvalidOperationException($"Method {config.CallerMethodName} not found in {classTypeName} with the specified parameter types");
        }

        var attributeParameters = methodInfo.GetCustomAttributes(typeof(MethodCallParameterAttribute), false)
            .OfType<MethodCallParameterAttribute>()
            .ToDictionary(attr => attr.Name, attr => attr.Value);

        var threadId = Environment.CurrentManagedThreadId;
        var id = GenerateId();

        var result = _methodCallInfoPool.Rent(classMonitor, callerType, methodInfo, config.GenericArguments, id, attributeParameters);

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

        if (_currentDepth >= MaxCallStackDepth)
        {
            throw new StackOverflowException("Maximum call stack depth exceeded");
        }

        _currentDepth++;

        var threadId = methodCallInfo.ThreadId;
        var threadStack = _threadCallStacks.GetOrAdd(threadId, _ => new Stack<MethodCallInfo>());


        if (methodCallInfo.AssociatedReporter is not null)
        {
            if (methodCallInfo.IsRootForAssociatedReporter)
            {
                _reporterRootMethods[methodCallInfo.AssociatedReporter] = methodCallInfo;
                methodCallInfo.Parent = _methodCallInfoPool.GetNull();
                methodCallInfo.Level = 1;
            }
            else
            {
                var rootForReporter = _reporterRootMethods[methodCallInfo.AssociatedReporter];
                methodCallInfo.Parent = rootForReporter;
                methodCallInfo.Level = rootForReporter.Level + 1;
            }
        }

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

            _logger.LogInformation($"Pushed: {methodCallInfo}");

            var currentVersion = _monitoringController.GetCurrentVersion();
            if (_monitoringController.ShouldTrack(currentVersion))
            {
                NotifyObservers(new MethodCallStart(methodCallInfo), currentVersion);
            }
        }
    }

    public void Pop(MethodCallInfo methodCallInfo)
    {
        ArgumentNullException.ThrowIfNull(methodCallInfo);

        _currentDepth--;

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
                        _logger.LogWarning($"Thread CallStack mismatch: popped context is not the same as the method call info.");
                    }

                    if (threadStack.Count == 0)
                    {
                        _threadCallStacks.TryRemove(threadId, out _);
                        _threadRootMethods.TryRemove(threadId, out _);
                    }

                    _logger.LogInformation($"Popped: {methodCallInfo}");

                    var currentVersion = _monitoringController.GetCurrentVersion();
                    if (_monitoringController.ShouldTrack(currentVersion))
                    {
                        NotifyObservers(new MethodCallEnd(methodCallInfo), currentVersion);
                    }
                }
                else
                {
                    _logger.LogWarning($"Thread CallStack mismatch: no context found for thread {threadId}.");
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

            if (methodCallInfo == _rootParent)
            {
                _rootParent = null;
            }
        }
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"LogStatus called with {status.GetType().Name}");

        var currentVersion = _monitoringController.GetCurrentVersion();
        if (!_monitoringController.ShouldTrack(currentVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (ShouldLogStatus(status, currentVersion))
        {
            _logger.LogDebug($"Notifying observers: {status}");
            NotifyObservers(status, currentVersion);

            if (status is MethodCallEnd && IsEmpty())
            {
                _logger.LogDebug("Call stack is empty, notifying with CallStackItem.Empty");
                NotifyObservers(CallStackItem.Empty, currentVersion);
            }
        }
        else
        {
            _logger.LogDebug($"Skipping status logging: {status}");
        }
    }

    private bool ShouldLogStatus(IMethodLifeCycleItem status, MonitoringVersion currentVersion)
    {
        var methodInfo = status.MethodCallInfo.MethodInfo;
        if (methodInfo is null)
        {
            return false;
        }

        var applicableReporters = _monitoringConfig.ReporterTypes;
        var applicableFilters = _monitoringConfig.Filters;

        // Check if any enabled reporter is interested in this status
        var anyEnabledReporterInterested = applicableReporters.Any(_monitoringController.IsReporterEnabled);

        // Check if any enabled filter allows this status
        var anyEnabledFilterAllows = applicableFilters.Any(filter =>
        {
            if (_monitoringController.IsFilterEnabled(filter.GetType()))
            {
                return filter.ShouldInclude(status.MethodCallInfo);
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

    private void NotifyObservers(ICallStackItem value, MonitoringVersion version)
    {
        foreach (var observer in _observers.ToArray())
        {
            if (_monitoringController.ShouldTrack(version))
            {
                observer.OnNext(value);
            }
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
