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


public static class PerformanceMonitor
{
    private static readonly object _configLock = new object();
    private static CallStack? _callStack;
    private static MonitoringConfiguration? _configuration;
    private static ILogger? _logger;

    internal static IMonitoringLoggerFactory MonitoringLoggerFactory { get; set; } = Orc.Monitoring.MonitoringLoggerFactory.Instance;
    internal static Func<MethodCallInfoPool> MethodCallInfoPoolFactory { get; set; } = () => new MethodCallInfoPool(MonitoringLoggerFactory);
    internal static Func<CallStack> CallStackFactory { get; set; } = () => new CallStack(_configuration, MethodCallInfoPoolFactory(), MonitoringLoggerFactory);
    internal static Func<ClassMonitor> ClassMonitorFactory { get; set; } = () => new ClassMonitor(typeof(PerformanceMonitor), _callStack, _configuration, MonitoringLoggerFactory);
    internal static Func<NullClassMonitor> NullClassMonitorFactory { get; set; } = () => new NullClassMonitor(MonitoringLoggerFactory);
    internal static Func<ConfigurationBuilder> ConfigurationBuilderFactory { get; set; } = () => new ConfigurationBuilder();

    public static void Configure(Action<ConfigurationBuilder> configAction)
    {
        _logger = MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

        _logger.LogInformation("PerformanceMonitor.Configure called");
        var builder = ConfigurationBuilderFactory();

        lock (_configLock)
        {
            try
            {
                _logger.LogDebug("Applying custom configuration");
                configAction(builder);

                _logger.LogDebug("Building configuration");
                _configuration = builder.Build();

                _logger.LogDebug("Setting MonitoringController Configuration");
                MonitoringController.Configuration = _configuration;

                _logger.LogDebug("Applying global state");
                if (_configuration.IsGloballyEnabled)
                {
                    MonitoringController.Enable();
                }
                else
                {
                    MonitoringController.Disable();
                }

                _logger.LogDebug("Creating CallStack instance");

                _callStack = CallStackFactory();

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

    public static void LogCurrentConfiguration()
    {
        var logger = MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

        var config = GetCurrentConfiguration();
        if (config is not null)
        {
            logger.LogInformation($"Current configuration: Reporters: {string.Join(", ", config.ReporterTypes.Select(r => r.Name))}, " +
                                  $"Filters: {string.Join(", ", config.Filters.Select(f => f.GetType().Name))}, " +
                                  $"TrackedAssemblies: {string.Join(", ", config.TrackedAssemblies.Select(a => a.GetName().Name))}");
        }
        else
        {
            logger.LogWarning("Current configuration is null");
        }
    }

    private static void EnableDefaultOutputTypes(ConfigurationBuilder builder)
    {
        _logger ??= MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

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

    public static IClassMonitor ForCurrentClass()
    {
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public static IClassMonitor ForClass<T>()
    {
        _logger ??= MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

        _logger.LogDebug($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");
        if (!MonitoringController.IsEnabled)
        {
            _logger.LogWarning("Monitoring is disabled. Returning NullClassMonitor.");
            return NullClassMonitorFactory();
        }
        var monitor = CreateClassMonitor(typeof(T));
        _logger.LogDebug($"Created monitor of type: {monitor.GetType().Name}");
        return monitor;
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        _logger ??= MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

        if (_configuration is null || _callStack is null)
        {
            _logger.LogWarning("MonitoringConfiguration or CallStack is null. Returning NullClassMonitor");
            return NullClassMonitorFactory();
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return ClassMonitorFactory();
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    // Method to reset the configuration and CallStack if needed
    public static void Reset()
    {
        lock (_configLock)
        {
            _logger ??= MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

            _logger.LogDebug("Resetting PerformanceMonitor");
            _configuration = null;
            _callStack = null;
            MonitoringController.Disable();
            _logger.LogInformation("PerformanceMonitor reset");
        }
    }

    // Method to check if PerformanceMonitor is configured
    public static bool IsConfigured
    {
        get
        {
            _logger ??= MonitoringLoggerFactory.CreateLogger(typeof(PerformanceMonitor));

            var isConfigured = _configuration is not null && _callStack is not null;
            _logger.LogDebug($"IsConfigured called, returning: {isConfigured}. Configuration: {_configuration is not null}, CallStack: {_callStack is not null}");
            return isConfigured;
        }
    }

    // Method to get the current configuration (if needed)
    public static MonitoringConfiguration? GetCurrentConfiguration() => _configuration;
}
