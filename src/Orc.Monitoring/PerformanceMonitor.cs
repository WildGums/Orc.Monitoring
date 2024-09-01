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
    private readonly Func<MonitoringConfiguration, CallStack> _callStackFactory;
    private readonly Func<Type, CallStack, MonitoringConfiguration, ClassMonitor> _classMonitorFactory;
    private readonly Func<ConfigurationBuilder> _configurationBuilderFactory;
    private readonly object _configLock = new object();
    private CallStack? _callStack;
    private MonitoringConfiguration? _configuration;
    private readonly ILogger _logger;

    public PerformanceMonitor(IMonitoringController monitoringController, IMonitoringLoggerFactory loggerFactory,
        Func<MonitoringConfiguration, CallStack> callStackFactory, Func<Type, CallStack, MonitoringConfiguration, ClassMonitor> classMonitorFactory, Func<ConfigurationBuilder> configurationBuilderFactory)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(callStackFactory);
        ArgumentNullException.ThrowIfNull(classMonitorFactory);
        ArgumentNullException.ThrowIfNull(configurationBuilderFactory);

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

                _callStack = _callStackFactory(_configuration);

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

    public void LogCurrentConfiguration()
    {
        var config = GetCurrentConfiguration();
        if (config is not null)
        {
            _logger.LogInformation($"Current configuration: Reporters: {string.Join(", ", config.ReporterTypes.Select(r => r.Name))}, " +
                                  $"Filters: {string.Join(", ", config.Filters.Select(f => f.GetType().Name))}, " +
                                  $"TrackedAssemblies: {string.Join(", ", config.TrackedAssemblies.Select(a => a.GetName().Name))}");
        }
        else
        {
            _logger.LogWarning("Current configuration is null");
        }
    }

    private void EnableDefaultOutputTypes(ConfigurationBuilder builder)
    {
        var outputTypes = new[]
        {
            typeof(RanttOutput),
            typeof(TxtReportOutput),
        };

        foreach (var outputType in outputTypes)
        {
            try
            {
                builder.SetOutputTypeState(outputType, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enabling output type {outputType.Name}");
            }
        }
    }

    public IClassMonitor ForCurrentClass()
    {
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public IClassMonitor ForClass<T>()
    {
        _logger.LogDebug($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");

        var monitor = CreateClassMonitor(typeof(T));
        _logger.LogDebug($"Created monitor of type: {monitor.GetType().Name}");
        return monitor;
    }

    private IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        if (_configuration is null || _callStack is null)
        {
            _logger.LogWarning("MonitoringConfiguration or CallStack is null.");
            throw new InvalidOperationException("MonitoringConfiguration or CallStack is null.");
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return _classMonitorFactory(callingType, _callStack, _configuration);
    }

    private Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    // Method to reset the configuration and CallStack if needed
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

    // Method to check if PerformanceMonitor is configured
    public bool IsConfigured
    {
        get
        {
            var isConfigured = _configuration is not null && _callStack is not null;
            _logger.LogDebug($"IsConfigured called, returning: {isConfigured}. Configuration: {_configuration is not null}, CallStack: {_callStack is not null}");
            return isConfigured;
        }
    }

    // Method to get the current configuration (if needed)
    public MonitoringConfiguration? GetCurrentConfiguration() => _configuration;
}
