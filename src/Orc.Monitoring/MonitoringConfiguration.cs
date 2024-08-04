namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Filters;

public class MonitoringConfiguration
{
    private readonly Dictionary<Type, List<Type>> _reportersByClass = new();
    private readonly Dictionary<Type, List<Type>> _filtersByClass = new();

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
}
