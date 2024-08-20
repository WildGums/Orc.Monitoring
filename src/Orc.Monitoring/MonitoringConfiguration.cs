namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;

public class MonitoringConfiguration
{
    public List<Assembly> TrackedAssemblies { get; } = new List<Assembly>();
    public List<IMethodFilter> Filters { get; } = new List<IMethodFilter>();
    public List<Type> ReporterTypes { get; } = new List<Type>();
    public Dictionary<Type, bool> OutputTypeStates { get; } = new Dictionary<Type, bool>();
    public bool IsGloballyEnabled { get; set; } = true;

    public Dictionary<Type, HashSet<IMethodFilter>> ReporterFilterMappings { get; } = new();

    internal void AddFilter(IMethodFilter filter)
    {
        Filters.Add(filter);
    }

    internal void AddReporter<T>() where T : IMethodCallReporter
    {
        ReporterTypes.Add(typeof(T));
    }

    internal void AddReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }
        ReporterTypes.Add(reporterType);
    }

    internal void TrackAssembly(Assembly assembly)
    {
        if (!TrackedAssemblies.Contains(assembly))
        {
            TrackedAssemblies.Add(assembly);
        }
    }

    internal void SetOutputTypeState(Type outputType, bool enabled)
    {
        OutputTypeStates[outputType] = enabled;
    }

    internal void AddFilterMappingForReporter(Type reporterType, Type filterType)
    {
        if (!ReporterFilterMappings.TryGetValue(reporterType, out var filters))
        {
            filters = new HashSet<IMethodFilter>();
            ReporterFilterMappings[reporterType] = filters;
        }

        var filter = Activator.CreateInstance(filterType) as IMethodFilter;
        if (filter is null)
        {
            throw new InvalidOperationException($"Failed to create instance of {filterType.Name}");
        }

        filters.Add(filter);
        Filters.Add(filter);
    }
}
