namespace Orc.Monitoring.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Filters;

/// <summary>
/// Provides a way to configure the performance monitor.
/// </summary>
public class MonitorConfiguration
{
    private readonly HashSet<Type> _trackedTypes = new();
    private readonly Dictionary<Type, HashSet<MethodInfo>> _targetMethods = new();
    private readonly List<IMethodFilter> _filters = new();

    public IReadOnlyDictionary<Type, HashSet<MethodInfo>> TargetMethods => _targetMethods;
    public IReadOnlyList<IMethodFilter> Filters => _filters;

    public MonitorConfiguration TrackNamespace(Type typeInNamespace)
    {
        var namespaceName = typeInNamespace.Namespace;
        var assemblyTypes = typeInNamespace.Assembly.GetTypes();
        foreach (var type in assemblyTypes)
        {
            if (!string.Equals(type.Namespace, namespaceName, StringComparison.Ordinal))
            {
                continue;
            }

            TrackType(type);
        }

        return this;
    }

    /// <summary>
    /// Tracks all types in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to track.</param>
    /// <returns>The current <see cref="MonitorConfiguration"/> instance.</returns>
    public MonitorConfiguration TrackAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            TrackType(type);
        }

        return this;
    }

    public MonitorConfiguration TrackType(Type type)
    {
        if (!_trackedTypes.Add(type))
        {
            return this;
        }

        UpdateTrackingMethods(type);
        return this;
    }

    public MonitorConfiguration AddFilter(IMethodFilter filter)
    {
        _filters.Add(filter);

        foreach (var type in _trackedTypes)
        {
            UpdateTrackingMethods(type);
        }

        return this;
    }

    private void UpdateTrackingMethods(Type type)
    {
        if (!_targetMethods.TryGetValue(type, out var methods))
        {
            methods = [];
            _targetMethods.Add(type, methods);
        }

        foreach (var method in GetAllMonitoringMethods(type).Where(ShouldIncludeMethod))
        {
            methods.Add(method);
        }
    }

    private bool ShouldIncludeMethod(MethodInfo methodInfo)
    {
        return _filters.Any(filter => filter.ShouldInclude(methodInfo));
    }

    public static MethodInfo[] GetAllMonitoringMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.NonPublic);
}

