namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;

public class MonitoringConfiguration
{
    private readonly Dictionary<Type, IMethodFilter> _reporterFilters = new();
    public List<Assembly> TrackedAssemblies { get; } = new List<Assembly>();
    public IReadOnlyCollection<IMethodFilter> Filters => _reporterFilters.Values;
    public List<Type> ReporterTypes { get; } = new List<Type>();
    public Dictionary<Type, bool> OutputTypeStates { get; } = new Dictionary<Type, bool>();
    public bool IsGloballyEnabled { get; set; } = true;

    public Dictionary<Type, HashSet<IMethodFilter>> ReporterFilterMappings { get; } = new();

    internal void AddFilter(IMethodFilter filter)
    {
        _reporterFilters[filter.GetType()] = filter;
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
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }

        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type {filterType.Name} does not implement IMethodFilter", nameof(filterType));
        }

        if (!ReporterFilterMappings.TryGetValue(reporterType, out var filters))
        {
            filters = new();
            ReporterFilterMappings[reporterType] = filters;
        }

        var filter = _reporterFilters.GetValueOrDefault(filterType);
        if (filter is null && Activator.CreateInstance(filterType) is IMethodFilter newFilter)
        {
            filter = newFilter;
        }

        if (filter is not null)
        {
            filters.Add(filter);
        }
        else
        {
            throw new InvalidOperationException($"Failed to create instance of {filterType.Name}");
        }

        ReporterFilterMappings[reporterType] = filters;
    }
}
