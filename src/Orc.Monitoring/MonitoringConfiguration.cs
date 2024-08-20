namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;

public class MonitoringConfiguration
{
    // List of assemblies to be tracked
    public List<Assembly> TrackedAssemblies { get; } = new List<Assembly>();

    // List of filters
    public List<IMethodFilter> Filters { get; } = new List<IMethodFilter>();

    // List of reporter types
    public List<Type> ReporterTypes { get; } = new List<Type>();

    // Dictionary to store output type states
    public Dictionary<Type, bool> OutputTypeStates { get; } = new Dictionary<Type, bool>();

    // Global monitoring state
    public bool IsGloballyEnabled { get; set; } = true;

    // Internal methods to add items to the configuration
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
}
