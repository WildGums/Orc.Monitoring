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

public class ClassMonitor : IClassMonitor
{
    private readonly IMonitoringController _monitoringController;
    private readonly Type _classType;
    private readonly CallStack _callStack;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly IMethodCallContextFactory _methodCallContextFactory;
    private readonly MethodCallInfoPool _methodCallInfoPool;
    private readonly ILogger<ClassMonitor> _logger;

    private HashSet<string>? _trackedMethodNames;

    public ClassMonitor(IMonitoringController monitoringController, 
        Type classType,
        CallStack? callStack, 
        MonitoringConfiguration monitoringConfig,
        IMonitoringLoggerFactory loggerFactory,
        IMethodCallContextFactory methodCallContextFactory,
        MethodCallInfoPool methodCallInfoPool)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(classType);
        ArgumentNullException.ThrowIfNull(callStack);
        ArgumentNullException.ThrowIfNull(monitoringConfig);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(methodCallContextFactory);

        _monitoringController = monitoringController;
        _classType = classType;
        _callStack = callStack;
        _monitoringConfig = monitoringConfig;
        _methodCallContextFactory = methodCallContextFactory;
        _methodCallInfoPool = methodCallInfoPool;
        _logger = loggerFactory.CreateLogger<ClassMonitor>();
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
        var currentVersion = _monitoringController.GetCurrentVersion();
        _logger.LogDebug($"StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {_monitoringController.IsEnabled}, Version: {currentVersion}");

        if (!IsMonitoringEnabled(callerMethod, currentVersion))
        {
            _logger.LogDebug($"Monitoring not enabled for {callerMethod}, returning Dummy context");
            return GetDummyContext(async);
        }

        using var operation = _monitoringController.BeginOperation(out var operationVersion);

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

        // Handle static, generic, and extension methods
        HandleSpecialMethodTypes(methodInfo, methodCallInfo);

        var disposables = new List<IAsyncDisposable>();
        var enabledReporterIds = new List<string>();

        foreach (var reporter in config.Reporters)
        {
            reporter.Initialize(_monitoringConfig, methodCallInfo);

            // Only set root method if it hasn't been set already
            if (reporter.RootMethod is null)
            {
                reporter.SetRootMethod(methodInfo);
                _logger.LogDebug($"Set root method for reporter: {reporter.GetType().Name}");
            }

            methodCallInfo.AssociatedReporter = reporter;

            if (_monitoringController.IsReporterEnabled(reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter: {reporter.GetType().Name} (Id: {reporter.Id})");
                var reporterDisposable = reporter.StartReporting(_callStack);
                disposables.Add(reporterDisposable);
                enabledReporterIds.Add(reporter.Id);
                _logger.LogDebug($"Reporter started: {reporter.GetType().Name} (Id: {reporter.Id})");
            }
            else
            {
                _logger.LogWarning($"Reporter not enabled: {reporter.GetType().Name} (Id: {reporter.Id})");
            }
        }

        _callStack.Push(methodCallInfo);

        if (!ShouldTrackMethod(methodCallInfo, operationVersion, enabledReporterIds))
        {
            _logger.LogDebug($"Method filtered out, returning Dummy context. MethodInfo: {methodInfo.Name}, Filters: {string.Join(", ", _monitoringConfig.Filters.Select(f => f.GetType().Name))}");
            return GetDummyContext(async);
        }

        _logger.LogDebug($"Returning {(async ? "async" : "sync")} context");
        return CreateMethodCallContext(async, methodCallInfo, disposables, enabledReporterIds);
    }

    private void HandleSpecialMethodTypes(MethodInfo methodInfo, MethodCallInfo methodCallInfo)
    {
        if (methodInfo.IsStatic)
        {
            _logger.LogDebug($"Handling static method: {methodInfo.Name}");
            methodCallInfo.IsStatic = true;
        }

        if (methodInfo.IsGenericMethod)
        {
            _logger.LogDebug($"Handling generic method: {methodInfo.Name}");
            methodCallInfo.SetGenericArguments(methodInfo.GetGenericArguments());
        }

        if (methodInfo.IsDefined(typeof(ExtensionAttribute), false))
        {
            _logger.LogDebug($"Handling extension method: {methodInfo.Name}");
            methodCallInfo.IsExtensionMethod = true;
            methodCallInfo.ExtendedType = methodInfo.GetParameters()[0].ParameterType;
        }
    }

    private MethodCallInfo CreateMethodCallInfo(MethodConfiguration config, string callerMethod)
    {
        var methodInfo = FindMethod(callerMethod, config);
        if (methodInfo is null)
        {
            _logger.LogWarning($"Method not found: {callerMethod}");
            return _methodCallInfoPool.GetNull();
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

    private bool IsMonitoringEnabled(string callerMethod, MonitoringVersion currentVersion)
    {
        EnsureMethodsLoaded();

        if (_trackedMethodNames is null)
        {
            _logger.LogWarning($"_trackedMethodNames is null for {callerMethod}");
            return false;
        }

        var isEnabled = _monitoringController.ShouldTrack(currentVersion) && _trackedMethodNames.Contains(callerMethod);
        _logger.LogDebug($"IsMonitoringEnabled: {isEnabled} for {callerMethod}. ShouldTrack: {_monitoringController.ShouldTrack(currentVersion)}, Contains: {_trackedMethodNames.Contains(callerMethod)}");
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
        return async
            ? _methodCallContextFactory.GetDummyAsyncMethodCallContext()
            : _methodCallContextFactory.GetDummyMethodCallContext();
    }

    private object CreateMethodCallContext(bool async, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, IEnumerable<string> reporterIds)
    {
        return async
            ? _methodCallContextFactory.CreateAsyncMethodCallContext(this, methodCallInfo, disposables, reporterIds)
            : _methodCallContextFactory.CreateMethodCallContext(this, methodCallInfo, disposables, reporterIds);
    }

    private bool ShouldTrackMethod(MethodCallInfo methodCallInfo, MonitoringVersion operationVersion, IEnumerable<string> reporterIds)
    {
        _logger.LogDebug($"ShouldTrackMethod called for {methodCallInfo.MethodName}");
        var shouldTrack = _monitoringController.ShouldTrack(operationVersion, reporterIds: reporterIds);

        if (shouldTrack)
        {
            _logger.LogDebug($"ShouldTrack returned true for reporters: {string.Join(", ", reporterIds)}");
            return true;
        }

        _logger.LogDebug($"Method should not be tracked: {methodCallInfo.MethodName}. Checked reporters: {string.Join(", ", reporterIds)}");
        return false;
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

        if (status is MethodCallEnd endStatus && !endStatus.MethodCallInfo.IsNull)
        {
            _logger.LogDebug($"Popping MethodCallInfo for {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        _logger.LogDebug($"Logging status to CallStack: {status}");
        _callStack.LogStatus(status);
    }
}
