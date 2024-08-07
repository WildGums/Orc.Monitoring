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
    private readonly HierarchicalRuleManager _ruleManager = new();
    private readonly TypeAndMethodTracker _typeAndMethodTracker = new();
    private readonly List<Assembly> _trackedAssemblies = new();
    private readonly List<Type> _reporters = new();
    private readonly List<IMethodFilter> _filters = new();

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

    // These methods now return all reporters and filters, not class-specific ones
    public IEnumerable<Type> GetReportersForMethod(MethodInfo method)
    {
        return _reporters;
    }

    public IEnumerable<Type> GetFiltersForMethod(MethodInfo method)
    {
        return _filters.Select(f => f.GetType());
    }
}
