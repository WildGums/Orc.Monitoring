// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;


public static class PerformanceMonitor
{
    private static readonly object _configLock = new object();
    private static CallStack? _callStack;
    private static MonitoringConfiguration? _configuration;
    private static readonly ILogger _logger = CreateLogger(typeof(PerformanceMonitor));

    public static void Configure(Action<ConfigurationBuilder> configAction)
    {
        _logger.LogInformation("PerformanceMonitor.Configure called");
        var builder = new ConfigurationBuilder();

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
                _callStack = new CallStack(_configuration);
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
    private static void EnableDefaultOutputTypes(ConfigurationBuilder builder)
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

    public static IClassMonitor ForCurrentClass()
    {
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public static IClassMonitor ForClass<T>()
    {
        _logger.LogDebug($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");
        if (!MonitoringController.IsEnabled)
        {
            _logger.LogWarning("Monitoring is disabled. Returning NullClassMonitor.");
            return new NullClassMonitor();
        }
        var monitor = CreateClassMonitor(typeof(T));
        _logger.LogDebug($"Created monitor of type: {monitor.GetType().Name}");
        return monitor;
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        if (_configuration is null || _callStack is null)
        {
            _logger.LogWarning("MonitoringConfiguration or CallStack is null. Returning NullClassMonitor");
            return new NullClassMonitor();
        }

        _logger.LogDebug($"CreateClassMonitor called for {callingType.Name}");

        _logger.LogDebug($"Creating ClassMonitor for {callingType.Name}");
        return new ClassMonitor(callingType, _callStack, _configuration);
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    public static ILogger<T> CreateLogger<T>() => MonitoringController.CreateLogger<T>();
    public static ILogger CreateLogger(Type type) => MonitoringController.CreateLogger(type);

    // Method to reset the configuration and CallStack if needed
    public static void Reset()
    {
        lock (_configLock)
        {
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
            var isConfigured = _configuration is not null && _callStack is not null;
            _logger.LogDebug($"IsConfigured called, returning: {isConfigured}. Configuration: {_configuration is not null}, CallStack: {_callStack is not null}");
            return isConfigured;
        }
    }

    // Method to get the current configuration (if needed)
    public static MonitoringConfiguration? GetCurrentConfiguration() => _configuration;
}
