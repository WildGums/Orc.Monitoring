namespace Orc.Monitoring.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


public class HierarchicalRuleManager
{
    private readonly List<HierarchicalMonitoringRule> _hierarchicalRules = [];

    public void AddRule(HierarchicalMonitoringRule rule)
    {
        _hierarchicalRules.Add(rule);
        _hierarchicalRules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public void RemoveRule(HierarchicalMonitoringRule rule)
    {
        _hierarchicalRules.Remove(rule);
    }

    public bool ShouldMonitor(MethodInfo method)
    {
        var applicableRule = _hierarchicalRules.FirstOrDefault(r => r.Applies(method));
        return applicableRule?.IsEnabled ?? true;
    }

    public bool ShouldMonitor(Type type)
    {
        var applicableRule = _hierarchicalRules.FirstOrDefault(r => r.Applies(type));
        return applicableRule?.IsEnabled ?? true;
    }

    public bool ShouldMonitor(string @namespace)
    {
        var applicableRule = _hierarchicalRules.FirstOrDefault(r => r.Applies(@namespace));
        return applicableRule?.IsEnabled ?? true;
    }

    public IEnumerable<HierarchicalMonitoringRule> GetRules()
    {
        return _hierarchicalRules;
    }
}
