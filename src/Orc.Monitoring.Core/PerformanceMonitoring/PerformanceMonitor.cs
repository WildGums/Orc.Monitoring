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
    IClassMonitorFactory classMonitorFactory,
    Func<ConfigurationBuilder> configurationBuilderFactory)
    : IPerformanceMonitor
{
    private readonly object _configLock = new();
    private CallStack? _callStack;
    private MonitoringConfiguration? _configuration;
    private readonly ILogger _logger = loggerFactory.CreateLogger<PerformanceMonitor>();

    public void Configure(Action<ConfigurationBuilder> configAction)
    {
        _logger.LogInformation("PerformanceMonitor.Configure called");
        var builder = configurationBuilderFactory();

        lock (_configLock)
        {
            try
            {
                _logger.LogDebug("Applying custom configuration");
                configAction(builder);

                _logger.LogDebug("Building configuration");
                _configuration = builder.Build();

                _logger.LogDebug("Setting MonitoringController Configuration");
                monitoringController.Configuration = _configuration;

                _logger.LogDebug("Applying global state");
                if (_configuration.IsGloballyEnabled)
                {
                    monitoringController.Enable();
                }
                else
                {
                    monitoringController.Disable();
                }

                _logger.LogDebug("Creating CallStack instance");

                _callStack = callStackFactory.CreateCallStack(_configuration);

                _logger.LogInformation($"CallStack instance created: {_callStack is not null}");

                if (_callStack is null)
                {
                    _logger.LogError("Failed to create CallStack instance");
                }

                _logger.LogInformation("Monitoring configured");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during PerformanceMonitor configuration");
            }
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
        if (_configuration is null || _callStack is null)
        {
            _logger.LogWarning("MonitoringConfiguration or CallStack is null.");
            return classMonitorFactory.CreateNullClassMonitor();
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return classMonitorFactory.CreateClassMonitor(callingType, _callStack, _configuration);
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
            _configuration = null;
            _callStack = null;
            monitoringController.Disable();
            _logger.LogInformation("PerformanceMonitor reset");
        }
    }

    public bool IsConfigured
    {
        get
        {
            var isConfigured = _configuration is not null && _callStack is not null;
            _logger.LogDebug($"IsConfigured called, returning: {isConfigured}. Configuration: {_configuration is not null}, CallStack: {_callStack is not null}");
            return isConfigured;
        }
    }

    public MonitoringConfiguration? GetCurrentConfiguration() => _configuration;
}
