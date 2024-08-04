// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Filters;
using Reporters;

public static class PerformanceMonitor
{
    private static readonly Dictionary<Type, HashSet<MethodInfo>> TargetMethods = new();

    private static GlobalConfiguration? _globalConfig;
    private static CallStack? _callStack;
    private static MonitoringConfiguration? _monitoringConfig;

    public static void Configure(Action<GlobalConfigurationBuilder> configAction)
    {
        Console.WriteLine("PerformanceMonitor.Configure called");
        var builder = new GlobalConfigurationBuilder();
        configAction(builder);
        _globalConfig = builder.Build();
        _monitoringConfig = new MonitoringConfiguration();
        ApplyGlobalConfiguration(_globalConfig);

        // Enable monitoring by default when configured
        MonitoringManager.Enable();
        Console.WriteLine("Monitoring enabled after configuration");
    }

    private static void ApplyGlobalConfiguration(GlobalConfiguration config)
    {
        if (_monitoringConfig is null)
        {
            Console.WriteLine("MonitoringConfiguration is null. Returning");
            return;
        }

        _callStack = new CallStack(_monitoringConfig);

        foreach (var assembly in config.TrackedAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!TargetMethods.TryGetValue(type, out var methods))
                {
                    methods = new HashSet<MethodInfo>();
                    TargetMethods[type] = methods;
                }

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (ShouldIncludeMethod(method, config.Filters))
                    {
                        methods.Add(method);
                    }
                }
            }
        }

        // Configure reporters and filters
        foreach (var reporter in config.Reporters)
        {
            if (reporter.Value)
            {
                MonitoringManager.EnableReporter(reporter.Key);
            }
            else
            {
                MonitoringManager.DisableReporter(reporter.Key);
            }
        }

        foreach (var filter in config.Filters)
        {
            if (filter.Value)
            {
                MonitoringManager.EnableFilter(filter.Key);
            }
            else
            {
                MonitoringManager.DisableFilter(filter.Key);
            }
        }
    }

    private static bool ShouldIncludeMethod(MethodInfo method, Dictionary<Type, bool> filters)
    {
        return filters.Where(f => f.Value).Any(filter => ((IMethodFilter)Activator.CreateInstance(filter.Key)!).ShouldInclude(method));
    }

    public static IClassMonitor ForCurrentClass()
    {
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public static IClassMonitor ForClass<T>()
    {
        Console.WriteLine($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");
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
        Console.WriteLine($"Added tracked method: {type.Name}.{method.Name}");
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        if (_monitoringConfig is null)
        {
            Console.WriteLine("MonitoringConfiguration is null. Returning NullClassMonitor");
            return new NullClassMonitor();
        }

        Console.WriteLine($"CreateClassMonitor called for {callingType.Name}");

        if (_callStack is null)
        {
            Console.WriteLine("CallStack is null. Returning NullClassMonitor");
            return new NullClassMonitor();
        }

        if (!TargetMethods.TryGetValue(callingType, out var methods))
        {
            Console.WriteLine($"No tracked methods found for {callingType.Name}. Creating empty set.");
            methods = new HashSet<MethodInfo>();
        }

        var trackedMethodNames = methods.Select(m => m.Name).ToHashSet();
        Console.WriteLine($"Creating ClassMonitor for {callingType.Name} with tracked methods: {string.Join(", ", trackedMethodNames)}");
        return new ClassMonitor(callingType, _callStack, trackedMethodNames, _monitoringConfig);
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }

    public static void AddReporterForClass<TClass, TReporter>() where TReporter : IMethodCallReporter
    {
        _monitoringConfig?.AddReporterForClass<TClass, TReporter>();
    }

    public static void AddFilterForClass<TClass, TFilter>() where TFilter : IMethodFilter
    {
        _monitoringConfig?.AddFilterForClass<TClass, TFilter>();
    }
}
