namespace Orc.Monitoring;

using System;
using System.Reflection;


public class HierarchicalMonitoringRule
{
    public enum RuleTarget
    {
        Namespace,
        Class,
        Method
    }

    public RuleTarget Target { get; }
    public string TargetName { get; }
    public bool IsEnabled { get; }
    public int Priority { get; }

    public HierarchicalMonitoringRule(RuleTarget target, string targetName, bool isEnabled, int priority)
    {
        Target = target;
        TargetName = targetName;
        IsEnabled = isEnabled;
        Priority = priority;
    }

    public bool Applies(MethodInfo method)
    {
        return Target switch
        {
            RuleTarget.Method => method.DeclaringType?.FullName + "." + method.Name == TargetName,
            RuleTarget.Class => method.DeclaringType?.FullName == TargetName,
            RuleTarget.Namespace => method.DeclaringType?.Namespace?.StartsWith(TargetName) ?? false,
            _ => false
        };
    }

    public bool Applies(Type type)
    {
        return Target switch
        {
            RuleTarget.Class => type.FullName == TargetName,
            RuleTarget.Namespace => type.Namespace?.StartsWith(TargetName) ?? false,
            _ => false
        };
    }

    public bool Applies(string @namespace)
    {
        return Target == RuleTarget.Namespace && @namespace.StartsWith(TargetName);
    }
}
