#pragma warning disable IDISP015
#pragma warning disable IDISP005
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MethodLifeCycleItems;
using Filters;
using System.Linq;
using System.Reflection;


internal class ClassMonitor : IClassMonitor
{
    private readonly Type _classType;
    private readonly CallStack _callStack;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly ILogger<ClassMonitor> _logger;
    
    private HashSet<string>? _trackedMethodNames;

    public ClassMonitor(Type classType, CallStack callStack, MonitoringConfiguration monitoringConfig)
    {
        _classType = classType;
        _callStack = callStack;
        _monitoringConfig = monitoringConfig;
        _logger = MonitoringController.CreateLogger<ClassMonitor>();
        _logger.LogInformation($"ClassMonitor created for {classType.Name}");
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        return (AsyncMethodCallContext)StartMethodInternal(config, callerMethod, async: true);
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        return (MethodCallContext)StartMethodInternal(config, callerMethod, async: false);
    }

    private object StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        _logger.LogDebug($"StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringController.IsEnabled}");

        if (!IsMonitoringEnabled(callerMethod, currentVersion))
        {
            _logger.LogDebug($"Monitoring not enabled for {callerMethod}, returning Dummy context");
            return GetDummyContext(async);
        }

        using var operation = MonitoringController.BeginOperation(out var operationVersion);

        // Create MethodCallInfo without pushing it to the stack yet
        var methodCallInfo = CreateMethodCallInfo(config, callerMethod);
        if (methodCallInfo.IsNull)
        {
            _logger.LogDebug("MethodCallInfo is null, returning Dummy context");
            return GetDummyContext(async);
        }

        var methodInfo = methodCallInfo.MethodInfo;
        if (methodInfo is null)
        {
            _logger.LogDebug("MethodInfo is null, returning Dummy context");
            return GetDummyContext(async);
        }

        // Handle static methods
        if (methodInfo.IsStatic)
        {
            _logger.LogDebug($"Handling static method: {methodInfo.Name}");
            methodCallInfo.IsStatic = true;
        }

        // Handle generic methods
        if (methodInfo.IsGenericMethod)
        {
            _logger.LogDebug($"Handling generic method: {methodInfo.Name}");
            methodCallInfo.SetGenericArguments(methodInfo.GetGenericArguments());
        }

        // Handle extension methods
        if (methodInfo.IsDefined(typeof(ExtensionAttribute), false))
        {
            _logger.LogDebug($"Handling extension method: {methodInfo.Name}");
            methodCallInfo.IsExtensionMethod = true;
            methodCallInfo.ExtendedType = methodInfo.GetParameters()[0].ParameterType;
        }


        var disposables = new List<IAsyncDisposable>();

        // Set RootMethod on reporters before starting them
        foreach (var reporter in config.Reporters)
        {
            reporter.RootMethod = methodInfo;
            _logger.LogDebug($"Set RootMethod for reporter: {reporter.GetType().Name}");
        }

        // Start all reporters in the config
        foreach (var reporter in config.Reporters)
        {
            if (MonitoringController.IsReporterEnabled(reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter: {reporter.GetType().Name}");
                var reporterDisposable = reporter.StartReporting(_callStack);
                disposables.Add(reporterDisposable);
                _logger.LogDebug($"Reporter started: {reporter.GetType().Name}");
            }
            else
            {
                _logger.LogWarning($"Reporter not enabled: {reporter.GetType().Name}");
            }
        }

        // Push the method call to the stack after starting reporters
        PushMethodCallInfoToStack(methodCallInfo);

        var applicableFilters = _monitoringConfig.GetFiltersForMethod(methodInfo);

        if (!ShouldTrackMethod(methodCallInfo, applicableFilters, operationVersion))
        {
            _logger.LogDebug("Method filtered out, returning Dummy context");
            return GetDummyContext(async);
        }

        _logger.LogDebug($"Returning {(async ? "async" : "sync")} context");
        return CreateMethodCallContext(async, methodCallInfo, disposables);
    }

    private MethodCallInfo CreateMethodCallInfo(MethodConfiguration config, string callerMethod)
    {
        var methodInfo = FindMethod(callerMethod, config);
        if (methodInfo is null)
        {
            _logger.LogWarning($"Method not found: {callerMethod}");
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

    private MethodInfo? FindMethod(string methodName, MethodConfiguration config)
    {
        var methods = _classType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .ToList();

        if (methods.Count == 0)
        {
            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }

        // If we have multiple methods with the same name, try to match based on parameter types
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
        _logger.LogWarning($"Multiple methods found with name {methodName}, unable to determine exact match. Using first method.");
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
        _logger.LogDebug($"MethodCallInfo pushed. IsNull: {methodCallInfo.IsNull}, Version: {methodCallInfo.Version}");
    }

    private List<IAsyncDisposable> StartReporters(MethodConfiguration config, MonitoringVersion operationVersion)
    {
        var disposables = new List<IAsyncDisposable>();
        foreach (var reporter in config.Reporters)
        {
            _logger.LogDebug($"Checking if reporter is enabled: {reporter.GetType().Name}");
            if (MonitoringController.IsReporterEnabled(reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter: {reporter.GetType().Name}");
                var reporterDisposable = reporter.StartReporting(_callStack);
                disposables.Add(reporterDisposable);
                _logger.LogDebug($"Reporter started: {reporter.GetType().Name}");
            }
            else
            {
                _logger.LogWarning($"Reporter not enabled: {reporter.GetType().Name}");
            }
        }
        return disposables;
    }

    private bool IsMonitoringEnabled(string callerMethod, MonitoringVersion currentVersion)
    {
        EnsureMethodsLoaded();

        if(_trackedMethodNames is null)
        {
            return false;
        }

        var isEnabled = MonitoringController.ShouldTrack(currentVersion) && _trackedMethodNames.Contains(callerMethod);
        _logger.LogDebug($"IsMonitoringEnabled: {isEnabled} for {callerMethod}");
        return isEnabled;
    }

    private void EnsureMethodsLoaded()
    {
        if (_trackedMethodNames is not null)
        {
            return;
        }

        _trackedMethodNames = GetAllMethodNames();
    }

    private HashSet<string>? GetAllMethodNames()
    {
        return _classType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet();
    }

    private object GetDummyContext(bool async)
    {
        _logger.LogDebug("Returning Dummy context");
        return async ? AsyncMethodCallContext.Dummy : MethodCallContext.Dummy;
    }

    private object CreateMethodCallContext(bool async, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
    {
        return async
            ? new AsyncMethodCallContext(this, methodCallInfo, disposables)
            : new MethodCallContext(this, methodCallInfo, disposables);
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
        _logger.LogDebug($"LogStatus called with {status.GetType().Name}");
        var currentVersion = MonitoringController.GetCurrentVersion();
        if (!MonitoringController.ShouldTrack(currentVersion))
        {
            _logger.LogDebug("Monitoring is disabled or version mismatch, not logging status");
            return;
        }

        ArgumentNullException.ThrowIfNull(status);

        if (status is MethodCallEnd endStatus && !endStatus.MethodCallInfo.IsNull)
        {
            _logger.LogDebug($"Popping MethodCallInfo for {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        _logger.LogDebug($"Logging status to CallStack: {status}");
        _callStack.LogStatus(status);
    }
}
