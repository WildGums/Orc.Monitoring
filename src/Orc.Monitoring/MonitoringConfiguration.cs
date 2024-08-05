namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Configuration;

public class MonitoringConfiguration
{
    private readonly Dictionary<Type, List<Type>> _reportersByClass = new();
    private readonly Dictionary<Type, List<Type>> _filtersByClass = new();
    private readonly HierarchicalRuleManager _ruleManager = new();
    private readonly TypeAndMethodTracker _typeAndMethodTracker = new();
    private readonly List<Assembly> _trackedAssemblies = new();
    private readonly List<Type> _reporters = new();

    public void AddReporterForClass<TClass, TReporter>() where TReporter : IMethodCallReporter
    {
        AddComponentForClass<TClass, TReporter>(_reportersByClass);
    }

    public void AddFilterForClass<TClass, TFilter>() where TFilter : IMethodFilter
    {
        AddComponentForClass<TClass, TFilter>(_filtersByClass);
    }

    private void AddComponentForClass<TClass, TComponent>(Dictionary<Type, List<Type>> dictionary)
    {
        var classType = typeof(TClass);
        var componentType = typeof(TComponent);

        if (!dictionary.TryGetValue(classType, out var components))
        {
            components = new List<Type>();
            dictionary[classType] = components;
        }

        components.Add(componentType);
    }

    public IEnumerable<Type> GetReportersForMethod(MethodInfo method)
    {
        return GetComponentsForMethod(method, _reportersByClass);
    }

    public IEnumerable<Type> GetFiltersForMethod(MethodInfo method)
    {
        return GetComponentsForMethod(method, _filtersByClass);
    }

    private IEnumerable<Type> GetComponentsForMethod(MethodInfo method, Dictionary<Type, List<Type>> dictionary)
    {
        var declaringType = method.DeclaringType;
        if (declaringType is not null && dictionary.TryGetValue(declaringType, out var components))
        {
            return components;
        }
        return Array.Empty<Type>();
    }

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
        return _ruleManager.ShouldMonitor(method);
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
        _typeAndMethodTracker.AddFilter(filter);
    }

    public IReadOnlyDictionary<Type, HashSet<MethodInfo>> GetTargetMethods()
    {
        return _typeAndMethodTracker.TargetMethods;
    }

    public IReadOnlyList<IMethodFilter> GetFilters()
    {
        return _typeAndMethodTracker.Filters;
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
}
