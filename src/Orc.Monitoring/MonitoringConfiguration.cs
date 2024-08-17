namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Configuration;
using System.Threading.Tasks;

public class MonitoringConfiguration
{
    private readonly HierarchicalRuleManager _ruleManager = new();
    private readonly TypeAndMethodTracker _typeAndMethodTracker = new();
    private readonly List<Assembly> _trackedAssemblies = [];
    private readonly List<Type> _reporters = [];
    private readonly List<IMethodFilter> _filters = [];

    // New properties for handling special cases
    public bool TrackStaticMethods { get; set; } = true;
    public bool TrackGenericMethods { get; set; } = true;
    public bool TrackExtensionMethods { get; set; } = true;
    public bool IncludeGenericTypeParameters { get; set; } = true;
    public bool TrackAsyncMethods { get; set; } = true;

    public void AddHierarchicalRule(HierarchicalMonitoringRule rule)
    {
        _ruleManager.AddRule(rule);
    }

    public void RemoveHierarchicalRule(HierarchicalMonitoringRule rule)
    {
        _ruleManager.RemoveRule(rule);
    }

    public bool ShouldMonitor(MethodInfo method)
    {
        if (!_ruleManager.ShouldMonitor(method))
        {
            return false;
        }

        // Check special cases
        if (method.IsStatic && !TrackStaticMethods)
        {
            return false;
        }

        if (method.IsGenericMethod && !TrackGenericMethods)
        {
            return false;
        }

        if (method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false) && !TrackExtensionMethods)
        {
            return false;
        }

        if (IsAsyncMethod(method) && !TrackAsyncMethods)
        {
            return false;
        }

        return true;
    }

    public bool ShouldMonitor(Type type)
    {
        return _ruleManager.ShouldMonitor(type);
    }

    public bool ShouldMonitor(string @namespace)
    {
        return _ruleManager.ShouldMonitor(@namespace);
    }

    public void TrackNamespace(Type typeInNamespace)
    {
        _typeAndMethodTracker.TrackNamespace(typeInNamespace);
    }

    public void TrackAssembly(Assembly assembly)
    {
        _typeAndMethodTracker.TrackAssembly(assembly);
        _trackedAssemblies.Add(assembly);
    }

    public void TrackType(Type type)
    {
        _typeAndMethodTracker.TrackType(type);
    }

    public void AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);
        _typeAndMethodTracker.AddFilter(filter);
    }

    public IReadOnlyDictionary<Type, HashSet<MethodInfo>> GetTargetMethods()
    {
        return _typeAndMethodTracker.TargetMethods;
    }

    public IReadOnlyList<IMethodFilter> GetFilters()
    {
        return _filters;
    }

    public IReadOnlyList<Assembly> GetTrackedAssemblies()
    {
        return _trackedAssemblies;
    }

    public void AddReporter<T>() where T : IMethodCallReporter
    {
        _reporters.Add(typeof(T));
    }

    public IReadOnlyList<Type> GetReporters()
    {
        return _reporters;
    }

    public IEnumerable<Type> GetReportersForMethod(MethodInfo method)
    {
        return _reporters;
    }

    public IEnumerable<Type> GetFiltersForMethod(MethodInfo method)
    {
        return _filters.Select(f => f.GetType());
    }

    // New method to get method name with generic type parameters if configured
    public string GetMethodDisplayName(MethodInfo method)
    {
        string methodName = method.Name;

        if (method.IsGenericMethod && IncludeGenericTypeParameters)
        {
            var genericArgs = method.GetGenericArguments();
            string genericParams = string.Join(", ", genericArgs.Select(t => t.Name));
            methodName += $"<{genericParams}>";
        }

        return methodName;
    }

    // Helper method to check if a method is async
    private bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(Task) ||
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }
}
