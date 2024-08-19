namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Reporters;
using Filters;
using Configuration;
using System.Threading.Tasks;

public class MonitoringConfiguration
{
    private readonly HierarchicalRuleManager _ruleManager = new();
    private readonly TypeAndMethodTracker _typeAndMethodTracker = new();
    private readonly List<Assembly> _trackedAssemblies = [];
    private readonly List<Type> _reporters = [];
    private readonly List<IMethodFilter> _filters = [];

    public void AddHierarchicalRule(HierarchicalMonitoringRule rule)
    {
        _ruleManager.AddRule(rule);
    }

    public void RemoveHierarchicalRule(HierarchicalMonitoringRule rule)
    {
        _ruleManager.RemoveRule(rule);
    }

    public bool ShouldMonitor(Type type)
    {
        return _ruleManager.ShouldMonitor(type);
    }

    public bool ShouldMonitor(string @namespace)
    {
        return _ruleManager.ShouldMonitor(@namespace);
    }

    public void TrackAssembly(Assembly assembly)
    {
        _typeAndMethodTracker.TrackAssembly(assembly);
        _trackedAssemblies.Add(assembly);
    }

    public void AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);
        _typeAndMethodTracker.AddFilter(filter);
    }

    public void AddReporter<T>() where T : IMethodCallReporter
    {
        _reporters.Add(typeof(T));
    }

    public void AddReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }
        _reporters.Add(reporterType);
    }

    public IEnumerable<Type> GetReportersForMethod(MethodInfo method)
    {
        return _reporters;
    }

    public IEnumerable<Type> GetFiltersForMethod(MethodInfo method)
    {
        return _filters.Select(f => f.GetType());
    }
}
