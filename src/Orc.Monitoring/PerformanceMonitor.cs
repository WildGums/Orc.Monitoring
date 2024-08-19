// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Reporters.ReportOutputs;

public static class PerformanceMonitor
{
    private static readonly Dictionary<Type, HashSet<MethodInfo>> TargetMethods = new();
    private static readonly object _configLock = new object();

    private static CallStack? _callStack;

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

    private static void ApplyGlobalConfiguration(MonitoringConfiguration config)
    {
        _callStack = new CallStack(config);

        foreach (var assembly in config.GetTrackedAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!TargetMethods.TryGetValue(type, out var methods))
                {
                    methods = new HashSet<MethodInfo>();
                    TargetMethods[type] = methods;
                }

                DiscoverMethods(type, methods, config);
            }
        }

        // Configure reporters and filters
        foreach (var reporter in config.GetReporters())
        {
            MonitoringController.EnableReporter(reporter);
        }

        foreach (var filter in config.GetFilters())
        {
            MonitoringController.EnableFilter(filter.GetType());
        }
    }

    private static void DiscoverMethods(Type type, HashSet<MethodInfo> methods, MonitoringConfiguration config)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var allMethods = type.GetMethods(bindingFlags);

        foreach (var method in allMethods)
        {
            if (ShouldIncludeMethod(method, config))
            {
                methods.Add(method);
                Console.WriteLine($"Discovered method: {type.Name}.{method.Name} (Static: {method.IsStatic}, Generic: {method.IsGenericMethod}, Extension: {IsExtensionMethod(method)})");
            }
        }
    }

    private static bool ShouldIncludeMethod(MethodInfo method, MonitoringConfiguration config)
    {
        // Include the method if any filter allows it
        return config.GetFilters().Any(filter => filter.ShouldInclude(method));
    }

    private static bool IsExtensionMethod(MethodInfo method)
    {
        return method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
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

    public static void AddTrackedMethod(Type type, MethodInfo method)
    {
        if (!TargetMethods.TryGetValue(type, out var methods))
        {
            methods = new HashSet<MethodInfo>();
            TargetMethods[type] = methods;
        }
        methods.Add(method);
        Console.WriteLine($"Added tracked method: {type.Name}.{method.Name} (Static: {method.IsStatic}, Generic: {method.IsGenericMethod}, Extension: {IsExtensionMethod(method)})");
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

        if (!TargetMethods.TryGetValue(callingType, out var methods))
        {
            Console.WriteLine($"No tracked methods found for {callingType.Name}. Creating empty set.");
            methods = new HashSet<MethodInfo>();
        }

        var trackedMethodNames = methods.Select(m => GetUniqueMethodName(m)).ToHashSet();
        Console.WriteLine($"Creating ClassMonitor for {callingType.Name} with tracked methods: {string.Join(", ", trackedMethodNames)}");
        return new ClassMonitor(callingType, callStack, trackedMethodNames, MonitoringController.Configuration);
    }

    private static string GetUniqueMethodName(MethodInfo method)
    {
        var name = method.Name;
        if (method.IsGenericMethod)
        {
            name += $"<{string.Join(",", method.GetGenericArguments().Select(t => t.Name))}>";
        }
        if (IsExtensionMethod(method))
        {
            name = $"Extension_{name}";
        }
        if (method.IsStatic)
        {
            name = $"Static_{name}";
        }
        return name;
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    public static MonitoringVersion GetCurrentVersion()
    {
        return MonitoringController.GetCurrentVersion();
    }
}
