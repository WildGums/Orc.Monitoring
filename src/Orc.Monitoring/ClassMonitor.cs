﻿#pragma warning disable IDISP015
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
        _logger = MonitoringManager.CreateLogger<ClassMonitor>();
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
        _logger.LogDebug($"StartMethodInternal called for {callerMethod}. Async: {async}, Monitoring enabled: {MonitoringManager.IsEnabled}, Method tracked: {_trackedMethodNames.Contains(callerMethod)}");

        if (!IsMonitoringEnabled(callerMethod))
        {
            return GetDummyContext(async);
        }

        using var operation = MonitoringManager.BeginOperation();

        var methodCallInfo = PushMethodCallInfo(config, callerMethod);
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

        var applicableFilters = _monitoringConfig.GetFiltersForMethod(methodInfo);

        if (ShouldReturnDummyContext(methodCallInfo))
        {
            return GetDummyContext(async);
        }

        var disposables = StartReporters(config, methodCallInfo);

        if (!ShouldTrackMethod(methodCallInfo, applicableFilters))
        {
            _logger.LogDebug("Method filtered out, returning Dummy context");
            return GetDummyContext(async);
        }

        _logger.LogDebug($"Returning {(async ? "async" : "sync")} context");
        return CreateMethodCallContext(async, methodCallInfo, disposables);
    }

    private bool IsMonitoringEnabled(string callerMethod)
    {
        return MonitoringManager.IsEnabled && _trackedMethodNames.Contains(callerMethod);
    }

    private object GetDummyContext(bool async)
    {
        _logger.LogDebug("Returning Dummy context");
        return async ? AsyncMethodCallContext.Dummy : MethodCallContext.Dummy;
    }

    private MethodCallInfo PushMethodCallInfo(MethodConfiguration config, string callerMethod)
    {
        var methodCallInfo = _callStack.Push(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes
        });

        _logger.LogDebug($"MethodCallInfo pushed. IsNull: {methodCallInfo.IsNull}, Version: {methodCallInfo.MonitoringVersion}");
        return methodCallInfo;
    }

    private bool ShouldReturnDummyContext(MethodCallInfo methodCallInfo)
    {
        return methodCallInfo.IsNull || !MonitoringManager.ShouldTrack(methodCallInfo.MonitoringVersion);
    }

    private List<IAsyncDisposable> StartReporters(MethodConfiguration config, MethodCallInfo methodCallInfo)
    {
        var disposables = new List<IAsyncDisposable>();
        foreach (var reporter in config.Reporters)
        {
            if (MonitoringManager.IsReporterEnabled(reporter.GetType()))
            {
                _logger.LogDebug($"Starting reporter: {reporter.GetType().Name}");
                reporter.RootMethod = methodCallInfo.MethodInfo;
                var reporterDisposable = reporter.StartReporting(_callStack);
                disposables.Add(reporterDisposable);
            }
        }
        return disposables;
    }

    private object CreateMethodCallContext(bool async, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables)
    {
        return async
            ? new AsyncMethodCallContext(this, methodCallInfo, disposables)
            : new MethodCallContext(this, methodCallInfo, disposables);
    }

    private bool ShouldTrackMethod(MethodCallInfo methodCallInfo, IEnumerable<Type> applicableFilters)
    {
        return applicableFilters.Any(filterType =>
        {
            if (MonitoringManager.IsFilterEnabled(filterType))
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
        if (!MonitoringManager.IsEnabled)
        {
            _logger.LogDebug("Monitoring is disabled, not logging status");
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
