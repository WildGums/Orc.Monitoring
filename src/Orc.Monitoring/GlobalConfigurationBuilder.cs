namespace Orc.Monitoring;

using System;
using System.Reflection;
using Orc.Monitoring.Configuration;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;

public class GlobalConfigurationBuilder
{
    private readonly MonitoringConfiguration _config = new MonitoringConfiguration();

    public GlobalConfigurationBuilder SetGlobalState(bool enabled)
    {
        if (enabled)
        {
            MonitoringController.Enable();
        }
        else
        {
            MonitoringController.Disable();
        }
        return this;
    }

    public GlobalConfigurationBuilder SetNamespaceState(string @namespace, bool enabled, int priority = 0)
    {
        var rule = new HierarchicalMonitoringRule(HierarchicalMonitoringRule.RuleTarget.Namespace, @namespace, enabled, priority);
        _config.AddHierarchicalRule(rule);
        return this;
    }

    public GlobalConfigurationBuilder SetClassState(Type classType, bool enabled, int priority = 0)
    {
        var rule = new HierarchicalMonitoringRule(HierarchicalMonitoringRule.RuleTarget.Class, classType.FullName ?? string.Empty, enabled, priority);
        _config.AddHierarchicalRule(rule);
        return this;
    }

    public GlobalConfigurationBuilder SetMethodState(MethodInfo method, bool enabled, int priority = 0)
    {
        var typeFullName = method.DeclaringType?.FullName ?? string.Empty;
        var fullName = $"{typeFullName}.{method.Name}";
        var rule = new HierarchicalMonitoringRule(HierarchicalMonitoringRule.RuleTarget.Method, fullName, enabled, priority);
        _config.AddHierarchicalRule(rule);
        return this;
    }

    public GlobalConfigurationBuilder SetDefaultState(bool enabled)
    {
        var rule = new HierarchicalMonitoringRule(HierarchicalMonitoringRule.RuleTarget.Namespace, string.Empty, enabled, int.MinValue);
        _config.AddHierarchicalRule(rule);
        return this;
    }

    public GlobalConfigurationBuilder AddFilter<T>(bool initialState = true) where T : IMethodFilter
    {
        _config.AddFilter(Activator.CreateInstance<T>());
        if (initialState)
        {
            MonitoringController.EnableFilter(typeof(T));
        }
        return this;
    }

    public GlobalConfigurationBuilder AddReporter<T>(bool initialState = true) where T : IMethodCallReporter
    {
        if (initialState)
        {
            MonitoringController.EnableReporter(typeof(T));
        }
        return this;
    }

    public GlobalConfigurationBuilder TrackAssembly(Assembly assembly)
    {
        _config.TrackAssembly(assembly);
        return this;
    }

    public GlobalConfigurationBuilder AddReporterForClass<TClass, TReporter>() where TReporter : IMethodCallReporter
    {
        _config.AddReporterForClass<TClass, TReporter>();
        return this;
    }

    public GlobalConfigurationBuilder AddFilterForClass<TClass, TFilter>() where TFilter : IMethodFilter
    {
        _config.AddFilterForClass<TClass, TFilter>();
        return this;
    }

    public MonitoringConfiguration Build()
    {
        return _config;
    }
}
