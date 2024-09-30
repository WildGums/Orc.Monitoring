// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring.Core.PerformanceMonitoring;

using System;
using Abstractions;
using CallStacks;
using Configuration;
using Microsoft.Extensions.Logging;
using Monitoring.Utilities.Logging;

public class PerformanceMonitor(
    IMonitoringController monitoringController,
    IMonitoringLoggerFactory loggerFactory,
    ICallStackFactory callStackFactory,
    IClassMonitorFactory classMonitorFactory)
    : IPerformanceMonitor
{
    private readonly object _configLock = new();
    private CallStack? _callStack;
    private readonly ILogger _logger = loggerFactory.CreateLogger<PerformanceMonitor>();

    public void Start()
    {
        lock (_configLock)
        {
            _logger.LogDebug("PerformanceMonitor.Start called");
            if (_callStack is not null)
            {
                _logger.LogWarning("PerformanceMonitor already started");
                return;
            }

            _callStack = callStackFactory.CreateCallStack();
            monitoringController.Enable();
            _logger.LogInformation("PerformanceMonitor started");
        }
    }

    public IClassMonitor ForClass<T>()
    {
        _logger.LogDebug($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");

        var monitor = CreateClassMonitor(typeof(T));
        _logger.LogDebug($"Created monitor of type: {monitor.GetType().Name}");
        return monitor;
    }

    public IClassMonitor ForCurrentClass()
    {
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    private IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);
        if (_callStack is null)
        {
            _logger.LogWarning("MonitoringConfiguration or CallStack is null.");
            return classMonitorFactory.CreateNullClassMonitor();
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return classMonitorFactory.CreateClassMonitor(callingType, _callStack);
    }

    private Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    public void Reset()
    {
        lock (_configLock)
        {
            _logger.LogDebug("Resetting PerformanceMonitor");
            _callStack = null;
            monitoringController.Disable();
            _logger.LogInformation("PerformanceMonitor reset");
        }
    }

    public bool IsActivated
    {
        get
        {
            var isActivated = _callStack is not null;
            _logger.LogDebug($"IsActivated called, returning: {isActivated}. CallStack: {_callStack is not null}");
            return isActivated;
        }
    }
}
