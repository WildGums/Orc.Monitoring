#pragma warning disable IDISP015
#pragma warning disable IDISP005
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.MethodLifeCycleItems;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;
using System.Linq;


internal class ClassMonitor : IClassMonitor
{
    private readonly Type _classType;
    private readonly CallStack _callStack;
    private readonly HashSet<string> _trackedMethodNames;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly ILogger<ClassMonitor> _logger;

    public ClassMonitor(Type classType, CallStack callStack, HashSet<string> trackedMethodNames, MonitoringConfiguration monitoringConfig)
    {
        _classType = classType;
        _callStack = callStack;
        _trackedMethodNames = trackedMethodNames;
        _monitoringConfig = monitoringConfig;
        _logger = MonitoringController.CreateLogger<ClassMonitor>();
        _logger.LogInformation($"ClassMonitor created for {classType.Name}. Tracked methods: {string.Join(", ", trackedMethodNames)}");
    }

    public AsyncMethodCallContext StartAsyncMethod(MethodConfiguration config, string callerMethod = "")
    {
        _logger.LogDebug($"StartAsyncMethod called for {callerMethod}");
        return (AsyncMethodCallContext)StartMethodInternal(config, callerMethod, async: true);
    }

    public MethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        _logger.LogDebug($"StartMethod called for {callerMethod}");
        return (MethodCallContext)StartMethodInternal(config, callerMethod, async: false);
    }

    private object StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        var currentVersion = MonitoringController.GetCurrentVersion();
        _logger.LogDebug($"StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringController.IsEnabled}, Method tracked: {_trackedMethodNames.Contains(callerMethod)}");

        if (!IsMonitoringEnabled(callerMethod, currentVersion))
        {
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

        // Set RootMethod on reporters before starting them
        foreach (var reporter in config.Reporters)
        {
            reporter.RootMethod = methodInfo;
        }

        // Now start the reporters
        var disposables = StartReporters(config, operationVersion);

        // Push the method call to the stack after starting reporters
        PushMethodCallInfoToStack(methodCallInfo);

        var applicableFilters = _monitoringConfig.GetFiltersForMethod(methodInfo);

        if (ShouldReturnDummyContext(methodCallInfo, operationVersion))
        {
            return GetDummyContext(async);
        }

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
        return _callStack.CreateMethodCallInfo(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes
        });
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
            if (MonitoringController.IsReporterEnabled(reporter.GetType()) && MonitoringController.ShouldTrack(operationVersion, reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter: {reporter.GetType().Name}");
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

    private object GetDummyContext(bool async)
    {
        _logger.LogDebug("Returning Dummy context");
        return async ? AsyncMethodCallContext.Dummy : MethodCallContext.Dummy;
    }

    private bool ShouldReturnDummyContext(MethodCallInfo methodCallInfo, MonitoringVersion operationVersion)
    {
        return methodCallInfo.IsNull || !MonitoringController.ShouldTrack(operationVersion);
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

        if (status is MethodCallEnd { MethodCallInfo.IsNull: false } endStatus)
        {
            _logger.LogDebug($"Popping MethodCallInfo for {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        _logger.LogDebug($"Logging status to CallStack: {status}");
        _callStack.LogStatus(status);
    }
}
