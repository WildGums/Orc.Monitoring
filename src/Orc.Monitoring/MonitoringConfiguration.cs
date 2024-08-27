namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;

public class MonitoringConfiguration
{
    private readonly Dictionary<Type, IMethodFilter> _filters = new();
    private readonly HashSet<Type> _reporterTypes = new();

    public IReadOnlyCollection<IMethodFilter> Filters => _filters.Values;
    public IReadOnlyDictionary<Type, IMethodFilter> FilterDictionary => _filters.AsReadOnly();
    public IReadOnlyCollection<Type> ReporterTypes => _reporterTypes;

    public List<Assembly> TrackedAssemblies { get; } = new List<Assembly>();
    public Dictionary<Type, bool> OutputTypeStates { get; } = new Dictionary<Type, bool>();
    public bool IsGloballyEnabled { get; set; } = true;
    public EnhancedDataPostProcessor.OrphanedNodeStrategy OrphanedNodeStrategy { get; set; } = EnhancedDataPostProcessor.OrphanedNodeStrategy.AttachToNearestAncestor;

    internal void AddFilter(IMethodFilter filter)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        _filters[filter.GetType()] = filter;
    }

    internal void AddReporter<T>() where T : IMethodCallReporter
    {
        AddReporter(typeof(T));
    }

    internal void AddReporter(Type reporterType)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }
        _reporterTypes.Add(reporterType);
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

    internal void SetOrphanedNodeStrategy(EnhancedDataPostProcessor.OrphanedNodeStrategy strategy)
    {
        OrphanedNodeStrategy = strategy;
    }
}
