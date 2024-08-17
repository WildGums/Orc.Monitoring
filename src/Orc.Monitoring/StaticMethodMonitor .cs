#pragma warning disable IDISP015
#pragma warning disable IDISP005
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Orc.Monitoring.MethodLifeCycleItems;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Filters;

public class StaticMethodMonitor : IClassMonitor
{
    private readonly ILogger<StaticMethodMonitor> _logger;
    private readonly CallStack _callStack;
    private readonly HashSet<string> _trackedMethodNames;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly Type _classType;

    public StaticMethodMonitor(Type classType, CallStack callStack, HashSet<string> trackedMethodNames, MonitoringConfiguration monitoringConfig)
    {
        _classType = classType;
        _callStack = callStack;
        _trackedMethodNames = trackedMethodNames;
        _monitoringConfig = monitoringConfig;
        _logger = MonitoringController.CreateLogger<StaticMethodMonitor>();
        _logger.LogInformation($"StaticMethodMonitor created for {classType.Name}. Tracked static methods: {string.Join(", ", trackedMethodNames)}");
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        return StartMethodInternal<AsyncMethodCallContext>(config, callerMethod, async: true);
    }

    public MethodCallContext StartMethod(MethodConfiguration config, string callerMethod = "")
    {
        return StartMethodInternal<MethodCallContext>(config, callerMethod, async: false);
    }

    private T StartMethodInternal<T>(MethodConfiguration config, string callerMethod, bool async)
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        _logger.LogDebug($"StartMethodInternal called for static method {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringController.IsEnabled}, Method tracked: {_trackedMethodNames.Contains(callerMethod)}");

        if (!IsMonitoringEnabled(callerMethod, currentVersion))
        {
            return GetDummyContext<T>(async);
        }

        using var operation = MonitoringController.BeginOperation(out var operationVersion);

        var methodCallInfo = CreateMethodCallInfo(config, callerMethod);
        if (methodCallInfo.IsNull)
        {
            _logger.LogDebug("MethodCallInfo is null, returning Dummy context");
            return GetDummyContext<T>(async);
        }

        var methodInfo = methodCallInfo.MethodInfo;
        if (methodInfo is null || !methodInfo.IsStatic)
        {
            _logger.LogWarning($"Method {callerMethod} is not static or MethodInfo is null. Returning Dummy context");
            return GetDummyContext<T>(async);
        }

        // Set RootMethod on reporters before starting them
        foreach (var reporter in config.Reporters)
        {
            reporter.RootMethod = methodInfo;
        }

        // Start the reporters
        var disposables = StartReporters(config, operationVersion);

        // Push the method call to the stack
        PushMethodCallInfoToStack(methodCallInfo);

        var applicableFilters = _monitoringConfig.GetFiltersForMethod(methodInfo);

        if (ShouldReturnDummyContext(methodCallInfo, operationVersion))
        {
            return GetDummyContext<T>(async);
        }

        if (!ShouldTrackMethod(methodCallInfo, applicableFilters, operationVersion))
        {
            _logger.LogDebug("Static method filtered out, returning Dummy context");
            return GetDummyContext<T>(async);
        }

        _logger.LogDebug($"Returning {(async ? "async" : "sync")} context for static method");
        return CreateMethodCallContext<T>(async, methodCallInfo, disposables);
    }

    private MethodCallInfo CreateMethodCallInfo(MethodConfiguration config, string callerMethod)
    {
        var methodInfo = FindStaticMethod(callerMethod, config);
        if (methodInfo is null)
        {
            _logger.LogWarning($"Static method not found: {callerMethod}");
            return MethodCallInfo.CreateNull();
        }

        return _callStack.CreateMethodCallInfo(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes
        });
    }

    private MethodInfo? FindStaticMethod(string methodName, MethodConfiguration config)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var methods = _classType.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName && m.IsStatic)
            .ToList();

        if (methods.Count == 0)
        {
            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }

        // If we have multiple static methods with the same name, try to match based on parameter types
        var matchedMethod = methods.FirstOrDefault(m => ParametersMatch(m.GetParameters(), config.ParameterTypes));
        if (matchedMethod is not null)
        {
            return matchedMethod;
        }

        // If we still can't determine, and it's a generic method, try to match based on generic arguments
        if (config.GenericArguments.Any())
        {
            matchedMethod = methods.FirstOrDefault(m => GenericArgumentsMatch(m, config.GenericArguments));
            if (matchedMethod is not null)
            {
                return matchedMethod;
            }
        }

        // If we still can't determine, log a warning and return the first method
        _logger.LogWarning($"Multiple static methods found with name {methodName}, unable to determine exact match. Using first method.");
        return methods[0];
    }

    private bool ParametersMatch(ParameterInfo[] methodParams, List<Type> configParams)
    {
        if (methodParams.Length != configParams.Count)
        {
            return false;
        }

        for (int i = 0; i < methodParams.Length; i++)
        {
            if (!methodParams[i].ParameterType.IsAssignableFrom(configParams[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool GenericArgumentsMatch(MethodInfo method, List<Type> configGenericArgs)
    {
        if (!method.IsGenericMethod)
        {
            return false;
        }

        var genericArgs = method.GetGenericArguments();
        return genericArgs.Length == configGenericArgs.Count;
    }

    private void PushMethodCallInfoToStack(MethodCallInfo methodCallInfo)
    {
        _callStack.Push(methodCallInfo);
        _logger.LogDebug($"Static MethodCallInfo pushed. IsNull: {methodCallInfo.IsNull}, Version: {methodCallInfo.Version}");
    }

    private List<IAsyncDisposable> StartReporters(MethodConfiguration config, MonitoringVersion operationVersion)
    {
        var disposables = new List<IAsyncDisposable>();
        foreach (var reporter in config.Reporters)
        {
            if (MonitoringController.IsReporterEnabled(reporter.GetType()) && MonitoringController.ShouldTrack(operationVersion, reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter for static method: {reporter.GetType().Name}");
                var reporterDisposable = reporter.StartReporting(_callStack);
                disposables.Add(reporterDisposable);
            }
        }
        return disposables;
    }

    private bool IsMonitoringEnabled(string callerMethod, MonitoringVersion currentVersion)
    {
        return MonitoringController.ShouldTrack(currentVersion) && _trackedMethodNames.Contains(callerMethod);
    }

    private T GetDummyContext<T>(bool async)
    {
        _logger.LogDebug("Returning Dummy context for static method");
        return async
            ? (T)(object)AsyncMethodCallContext.Dummy
            : (T)(object)MethodCallContext.Dummy;
    }

    private bool ShouldReturnDummyContext(MethodCallInfo methodCallInfo, MonitoringVersion operationVersion)
    {
        return methodCallInfo.IsNull || !MonitoringController.ShouldTrack(operationVersion);
    }

    private T CreateMethodCallContext<T>(bool async, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
    {
        return async
            ? (T)(object)new AsyncMethodCallContext(this, methodCallInfo, disposables)
            : (T)(object)new MethodCallContext(this, methodCallInfo, disposables);
    }

    private bool ShouldTrackMethod(MethodCallInfo methodCallInfo, IEnumerable<Type> applicableFilters, MonitoringVersion operationVersion)
    {
        return applicableFilters.Any(filterType =>
        {
            if (MonitoringController.IsFilterEnabled(filterType) && MonitoringController.ShouldTrack(operationVersion, filterType: filterType))
            {
                var filter = (IMethodFilter)Activator.CreateInstance(filterType)!;
                return filter.ShouldInclude(methodCallInfo);
            }
            return false;
        });
    }

    public void LogStatus(IMethodLifeCycleItem status)
    {
        _logger.LogDebug($"LogStatus called for static method with {status.GetType().Name}");
        var currentVersion = MonitoringController.GetCurrentVersion();
        if (!MonitoringController.ShouldTrack(currentVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status for static method");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (status is MethodCallEnd endStatus && !endStatus.MethodCallInfo.IsNull)
        {
            _logger.LogDebug($"Popping MethodCallInfo for static method {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        _logger.LogDebug($"Logging status to CallStack for static method: {status}");
        _callStack.LogStatus(status);
    }
}
