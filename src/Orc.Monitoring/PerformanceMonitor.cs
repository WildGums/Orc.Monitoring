namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Filters;

public static class PerformanceMonitor
{
    private static readonly Dictionary<Type, HashSet<MethodInfo>> TargetMethods = new();

    private static GlobalConfiguration? _globalConfig;
    private static CallStack? _callStack;

    public static void Configure(Action<GlobalConfigurationBuilder> configAction)
    {
        Console.WriteLine("PerformanceMonitor.Configure called");
        var builder = new GlobalConfigurationBuilder();
        configAction(builder);
        _globalConfig = builder.Build();
        ApplyGlobalConfiguration(_globalConfig);

        // Enable monitoring by default when configured
        MonitoringManager.Enable();
        Console.WriteLine("Monitoring enabled after configuration");
    }

    private static void ApplyGlobalConfiguration(GlobalConfiguration config)
    {
        _callStack = new();

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
    }

    private static bool ShouldIncludeMethod(MethodInfo method, List<IMethodFilter> filters)
    {
        return filters.Any(filter => filter.ShouldInclude(method));
    }

    public static IClassMonitor ForCurrentClass()
    {
        if (!MonitoringManager.IsEnabled)
        {
            return new NullClassMonitor();
        }

        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public static IClassMonitor ForClass<T>()
    {
        Console.WriteLine($"PerformanceMonitor.ForClass<{typeof(T).Name}> called");
        if (!MonitoringManager.IsEnabled)
        {
            Console.WriteLine($"Monitoring is disabled. Returning NullClassMonitor for {typeof(T).Name}");
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
        Console.WriteLine($"Added tracked method: {type.Name}.{method.Name}");
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

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
        return new ClassMonitor(callingType, _callStack, trackedMethodNames);
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }
}
