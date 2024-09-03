// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;


public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly IMonitoringController _monitoringController;
    private readonly IMonitoringLoggerFactory _loggerFactory;
    private readonly ICallStackFactory _callStackFactory;
    private readonly IClassMonitorFactory _classMonitorFactory;
    private readonly Func<ConfigurationBuilder> _configurationBuilderFactory;
    private readonly object _configLock = new object();
    private CallStack? _callStack;
    private MonitoringConfiguration? _configuration;
    private readonly ILogger _logger;

    public PerformanceMonitor(
        IMonitoringController monitoringController,
        IMonitoringLoggerFactory loggerFactory,
        ICallStackFactory callStackFactory,
        IClassMonitorFactory classMonitorFactory,
        Func<ConfigurationBuilder> configurationBuilderFactory)
    {
        _monitoringController = monitoringController;
        _loggerFactory = loggerFactory;
        _callStackFactory = callStackFactory;
        _classMonitorFactory = classMonitorFactory;
        _configurationBuilderFactory = configurationBuilderFactory;

        _logger = _loggerFactory.CreateLogger<PerformanceMonitor>();
    }

    public void Configure(Action<ConfigurationBuilder> configAction)
    {
        _logger.LogInformation("PerformanceMonitor.Configure called");
        var builder = _configurationBuilderFactory();

        lock (_configLock)
        {
            try
            {
                _logger.LogDebug("Applying custom configuration");
                configAction(builder);

                _logger.LogDebug("Building configuration");
                _configuration = builder.Build();

                _logger.LogDebug("Setting MonitoringController Configuration");
                _monitoringController.Configuration = _configuration;

                _logger.LogDebug("Applying global state");
                if (_configuration.IsGloballyEnabled)
                {
                    _monitoringController.Enable();
                }
                else
                {
                    _monitoringController.Disable();
                }

                _logger.LogDebug("Creating CallStack instance");

                _callStack = _callStackFactory.CreateCallStack(_configuration);

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
            return _classMonitorFactory.CreateNullClassMonitor();
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return _classMonitorFactory.CreateClassMonitor(callingType, _callStack, _configuration);
    }

    private Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    public void LogCurrentConfiguration()
    {
        lock (_configLock)
        {
            if (_configuration is null)
            {
                _logger.LogWarning("Configuration is null");
                return;
            }

            _logger.LogInformation("Current configuration:");
            _logger.LogInformation($"IsGloballyEnabled: {_configuration.IsGloballyEnabled}");
            _logger.LogInformation("Tracked Assemblies:");
            foreach (var assembly in _configuration.TrackedAssemblies)
            {
                _logger.LogInformation(assembly.FullName);
            }

            _logger.LogInformation("OutputTypeStates:");
            foreach (var (key, value) in _configuration.OutputTypeStates)
            {
                _logger.LogInformation($"{key.Name}: {value}");
            }

            _logger.LogInformation("Filters:");
            foreach (var filter in _configuration.Filters)
            {
                _logger.LogInformation(filter.GetType().Name);
            }

            _logger.LogInformation("ReporterTypes:");
            foreach (var reporterType in _configuration.ReporterTypes)
            {
                _logger.LogInformation(reporterType.Name);
            }
        }
    }

    public void Reset()
    {
        lock (_configLock)
        {
            _logger.LogDebug("Resetting PerformanceMonitor");
            _configuration = null;
            _callStack = null;
            _monitoringController.Disable();
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
