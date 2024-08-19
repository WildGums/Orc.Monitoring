// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using Reporters.ReportOutputs;

public static class PerformanceMonitor
{
    private static readonly object _configLock = new object();

    public static void Configure(Action<GlobalConfigurationBuilder> configAction)
    {
        Console.WriteLine("PerformanceMonitor.Configure called");
        var builder = new GlobalConfigurationBuilder();

        lock (_configLock)
        {
            try
            {
                // Enable default output types first
                EnableDefaultOutputTypes();

                // Apply custom configuration
                configAction(builder);

                var config = builder.Build();
                MonitoringController.Configuration = config;

                // Enable monitoring by default when configured
                MonitoringController.Enable();
                Console.WriteLine("Monitoring enabled after configuration");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during PerformanceMonitor configuration: {ex.Message}");
                // Consider logging the exception or rethrowing if necessary
            }
        }
    }

    private static void EnableDefaultOutputTypes()
    {
        var outputTypes = new[]
        {
            typeof(RanttOutput),
            typeof(TxtReportOutput),
        };

        foreach (var outputType in outputTypes)
        {
            // Only enable if not already configured
            if (!MonitoringController.IsOutputTypeEnabled(outputType))
            {
                try
                {
                    MonitoringController.EnableOutputType(outputType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error enabling output type {outputType.Name}: {ex.Message}");
                    // Consider logging the exception or rethrowing if necessary
                }
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
        Console.WriteLine($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");
        if (!MonitoringController.IsEnabled)
        {
            Console.WriteLine("Monitoring is disabled. Returning NullClassMonitor.");
            return new NullClassMonitor();
        }
        var monitor = CreateClassMonitor(typeof(T));
        Console.WriteLine($"Created monitor of type: {monitor.GetType().Name}");
        return monitor;
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        if (MonitoringController.Configuration is null)
        {
            Console.WriteLine("MonitoringConfiguration is null. Returning NullClassMonitor");
            return new NullClassMonitor();
        }

        Console.WriteLine($"CreateClassMonitor called for {callingType.Name}");

        var callStack = new CallStack(MonitoringController.Configuration);

        Console.WriteLine($"Creating ClassMonitor for {callingType.Name}");
        return new ClassMonitor(callingType, callStack, MonitoringController.Configuration);
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }
}
