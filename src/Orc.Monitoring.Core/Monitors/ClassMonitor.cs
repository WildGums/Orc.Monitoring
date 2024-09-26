#pragma warning disable IDISP005
namespace Orc.Monitoring.Core.Monitors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Abstractions;
using CallStacks;
using Configuration;
using Controllers;
using MethodCallContexts;
using MethodLifecycle;
using Microsoft.Extensions.Logging;
using Models;
using Monitoring.Utilities.Logging;
using Monitoring.Utilities.Metadata;
using Pooling;
using Utilities;

/// <summary>
/// Monitors method calls within a class and reports them based on the monitoring configuration.
/// </summary>
public class ClassMonitor : IClassMonitor
{
    private readonly IMonitoringController _monitoringController;
    private readonly Type _classType;
    private readonly CallStack _callStack;
    private readonly MonitoringConfiguration _monitoringConfig;
    private readonly IMethodCallContextFactory _methodCallContextFactory;
    private readonly MethodCallInfoPool _methodCallInfoPool;
    private readonly ILogger<ClassMonitor> _logger;

    private readonly HashSet<string> _trackedMethodNames;

    public ClassMonitor(
        IMonitoringController monitoringController,
        Type classType,
        CallStack callStack,
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
        ArgumentNullException.ThrowIfNull(methodCallInfoPool);

        _monitoringController = monitoringController;
        _classType = classType;
        _callStack = callStack;
        _monitoringConfig = monitoringConfig;
        _methodCallContextFactory = methodCallContextFactory;
        _methodCallInfoPool = methodCallInfoPool;
        _logger = loggerFactory.CreateLogger<ClassMonitor>();
        _logger.LogInformation($"ClassMonitor created for {classType.Name}");

        _trackedMethodNames = GetAllMethodNames();
    }

    /// <summary>
    /// Starts monitoring an asynchronous method.
    /// </summary>
    /// <param name="config">The method configuration.</param>
    /// <param name="callerMethod">The name of the caller method.</param>
    /// <returns>An <see cref="IMethodCallContext"/> representing the method call.</returns>
    public IMethodCallContext StartAsyncMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        return StartMethodInternal(config, callerMethod, async: true);
    }

    /// <summary>
    /// Starts monitoring a synchronous method.
    /// </summary>
    /// <param name="config">The method configuration.</param>
    /// <param name="callerMethod">The name of the caller method.</param>
    /// <returns>An <see cref="IMethodCallContext"/> representing the method call.</returns>
    public IMethodCallContext StartMethod(MethodConfiguration config, [CallerMemberName] string callerMethod = "")
    {
        return StartMethodInternal(config, callerMethod, async: false);
    }

    /// <summary>
    /// Starts monitoring an external method call.
    /// </summary>
    /// <param name="config">The method configuration.</param>
    /// <param name="externalMethodName">The name of the external method.</param>
    /// <param name="externalType">The external type.</param>
    /// <param name="async">Whether the external method is asynchronous.</param>
    /// <returns>An <see cref="IMethodCallContext"/> representing the external method call.</returns>
    public IMethodCallContext StartExternalMethod(MethodConfiguration config, Type externalType, string externalMethodName, bool async = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(externalType);
        ArgumentException.ThrowIfNullOrEmpty(externalMethodName);

        try
        {
            var currentVersion = _monitoringController.GetCurrentVersion();
            _logger.LogDebug($"StartExternalMethod called for {externalMethodName} in {externalType.Name}. Async: {async}, Monitoring enabled: {_monitoringController.IsEnabled}, Version: {currentVersion}");

            if (!_monitoringController.IsEnabled)
            {
                _logger.LogDebug($"Monitoring not enabled for external method {externalMethodName}, returning Dummy context");
                return GetDummyContext(async);
            }

            using var operation = _monitoringController.BeginOperation(out var operationVersion);

            var methodCallInfo = CreateMethodCallInfo(config, externalMethodName, externalType);
            if (methodCallInfo is null || methodCallInfo.IsNull)
            {
                _logger.LogDebug("MethodCallInfo is null, returning Dummy context");
                return GetDummyContext(async);
            }

            var disposables = new List<IAsyncDisposable>();

            var reporterTypes = config.Reporters.Select(x => x.GetType()).ToArray();
            foreach (var reporter in config.Reporters)
            {
                reporter.Initialize(_monitoringConfig, methodCallInfo);

                methodCallInfo.AddAssociatedReporter(reporter);

                if (_monitoringController.IsComponentEnabled(reporter.GetType()))
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

            _callStack.Push(methodCallInfo);

            if (!ShouldTrackMethod(methodCallInfo, operationVersion, reporterTypes))
            {
                _logger.LogDebug($"External method filtered out, returning Dummy context. Method: {externalMethodName}, Filters: {string.Join(", ", _monitoringConfig.GetComponentInstances().OfType<IMethodFilter>().Select(f => f.GetType().Name))}");
                return GetDummyContext(async);
            }

            _logger.LogDebug($"Returning {(async ? "async" : "sync")} context for external method");
            return CreateMethodCallContext(async, methodCallInfo, disposables, reporterTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception in StartExternalMethod for {externalMethodName} in {externalType.Name}");
            return GetDummyContext(async);
        }
    }

    private MethodCallInfo CreateMethodCallInfo(MethodConfiguration config, string methodName, Type? externalType = null)
    {
        var isExternalCall = externalType is not null;
        MethodInfo? methodInfo = null;
        if (!isExternalCall)
        {
            methodInfo = ReflectionHelper.FindMatchingMethod(externalType ?? _classType, methodName, config.GenericArguments, config.ParameterTypes);
            if (methodInfo is null)
            {
                _logger.LogWarning($"Method not found: {methodName}");
                return _methodCallInfoPool.GetNull();
            }
        }

        return _callStack.CreateMethodCallInfo(
            this,
            externalType ?? _classType,
            new MethodCallContextConfig
            {
                ClassType = isExternalCall ? externalType : _classType,
                CallerMethodName = methodName,
                GenericArguments = config.GenericArguments,
                ParameterTypes = config.ParameterTypes,
                StaticParameters = config.StaticParameters
            },
            methodInfo,
            externalType is not null);
    }

    private IMethodCallContext StartMethodInternal(MethodConfiguration config, string callerMethod, bool async)
    {
        try
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
            if (methodCallInfo is null || methodCallInfo.IsNull)
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

            foreach (var reporter in config.Reporters)
            {
                reporter.Initialize(_monitoringConfig, methodCallInfo);

                methodCallInfo.AddAssociatedReporter(reporter);

                if (_monitoringController.IsComponentEnabled(reporter.GetType()))
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

            _callStack.Push(methodCallInfo);
            var reporterTypes = config.Reporters.Select(x => x.GetType()).ToArray();
            if (!ShouldTrackMethod(methodCallInfo, operationVersion, reporterTypes))
            {
                _logger.LogDebug($"Method filtered out, returning Dummy context. MethodInfo: {methodInfo.Name}, Filters: {string.Join(", ", _monitoringConfig.GetComponentInstances().OfType<IMethodFilter>().Select(f => f.GetType().Name))}");
                return GetDummyContext(async);
            }

            PopulateAttributeParameters(methodCallInfo);

            _logger.LogDebug($"Returning {(async ? "async" : "sync")} context");
            return CreateMethodCallContext(async, methodCallInfo, disposables, reporterTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception in StartMethodInternal for {callerMethod}");
            return GetDummyContext(async);
        }
    }
    private void PopulateAttributeParameters(MethodCallInfo methodCallInfo)
    {
        var attributes = methodCallInfo.MethodInfo?.GetCustomAttributes<MethodCallParameterAttribute>(false);
        if (attributes is not null)
        {
            foreach (var attr in attributes)
            {
                if (methodCallInfo.AttributeParameters?.Add(attr.Name) ?? false)
                {
                    var parameters = methodCallInfo.Parameters;
                    if (parameters is null)
                    {
                        parameters = new Dictionary<string, string>();
                        methodCallInfo.Parameters = parameters;
                    }

                    if (!parameters.ContainsKey(attr.Name))
                    {
                        methodCallInfo.AddParameter(attr.Name, attr.Value);
                    }
                    else
                    {
                        _logger.LogWarning($"Parameter '{attr.Name}' already exists in method parameters. Skipping attribute parameter.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Attribute parameter '{attr.Name}' is already added. Skipping duplicate.");
                }
            }
        }
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

    private MethodCallInfo? CreateMethodCallInfo(MethodConfiguration config, string callerMethod)
    {
        var methodInfo = ReflectionHelper.FindMatchingMethod(_classType, callerMethod, config.GenericArguments, config.ParameterTypes);
        if (methodInfo is null)
        {
            _logger.LogWarning($"Method not found: {callerMethod}");
            return null;
        }

        return _callStack.CreateMethodCallInfo(this, _classType, new MethodCallContextConfig
        {
            ClassType = _classType,
            CallerMethodName = callerMethod,
            GenericArguments = config.GenericArguments,
            ParameterTypes = config.ParameterTypes,
            StaticParameters = config.StaticParameters
        });
    }

    private bool IsMonitoringEnabled(string callerMethod, MonitoringVersion currentVersion)
    {
        if (!_monitoringController.ShouldTrack(currentVersion))
        {
            return false;
        }

        var isEnabled = _trackedMethodNames.Contains(callerMethod);
        _logger.LogDebug($"IsMonitoringEnabled: {isEnabled} for {callerMethod}");
        return isEnabled;
    }

    private HashSet<string> GetAllMethodNames()
    {
        return _classType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet();
    }

    private IMethodCallContext GetDummyContext(bool async)
    {
        _logger.LogDebug("Returning Dummy context");
        return async
            ? _methodCallContextFactory.GetDummyAsyncMethodCallContext()
            : _methodCallContextFactory.GetDummyMethodCallContext();
    }

    private IMethodCallContext CreateMethodCallContext(bool async, MethodCallInfo methodCallInfo, List<IAsyncDisposable> disposables, params Type[] reporterTypes)
    {
        return async
            ? _methodCallContextFactory.CreateAsyncMethodCallContext(this, methodCallInfo, disposables, reporterTypes)
            : _methodCallContextFactory.CreateMethodCallContext(this, methodCallInfo, disposables, reporterTypes);
    }

    private bool ShouldTrackMethod(MethodCallInfo methodCallInfo, MonitoringVersion operationVersion, params Type[] reporterTypes)
    {
        _logger.LogDebug($"ShouldTrackMethod called for {methodCallInfo.MethodName}");
        var shouldTrack = _monitoringController.ShouldTrack(operationVersion, reporterTypes);

        if (shouldTrack)
        {
            _logger.LogDebug($"ShouldTrack returned true for reporters: {string.Join(", ", reporterTypes.Select(x => x.Name))}");
            return true;
        }

        _logger.LogDebug($"Method should not be tracked: {methodCallInfo.MethodName}. Checked reporters: {string.Join(", ", reporterTypes.Select(x => x.Name))}");
        return false;
    }

    /// <summary>
    /// Logs the status of a method lifecycle event.
    /// </summary>
    /// <param name="status">The lifecycle event to log.</param>
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

        if (status is MethodCallEnd { MethodCallInfo.IsNull: false } endStatus)
        {
            _logger.LogDebug($"Popping MethodCallInfo for {endStatus.MethodCallInfo}");
            _callStack.Pop(endStatus.MethodCallInfo);
        }

        _logger.LogDebug($"Logging status to CallStack: {status}");
        _callStack.LogStatus(status);
    }
}
