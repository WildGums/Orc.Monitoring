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
        var builder = new GlobalConfigurationBuilder();
        configAction(builder);
        _globalConfig = builder.Build();
        ApplyGlobalConfiguration(_globalConfig);
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
        var callingType = GetCallingType();
        return CreateClassMonitor(callingType);
    }

    public static IClassMonitor ForClass<T>()
    {
        return CreateClassMonitor(typeof(T));
    }

    private static IClassMonitor CreateClassMonitor(Type? callingType)
    {
        ArgumentNullException.ThrowIfNull(callingType);

        if (_callStack is null)
        {
            return new ClassMonitor(callingType, null, new HashSet<string>());
        }

        if (!TargetMethods.TryGetValue(callingType, out var methods))
        {
            methods = new HashSet<MethodInfo>();
        }

        return new ClassMonitor(callingType, _callStack, methods.Select(m => m.Name).ToHashSet());
    }

    private static Type? GetCallingType()
    {
        var frame = new System.Diagnostics.StackFrame(2, false);
        return frame.GetMethod()?.DeclaringType;
    }
}
