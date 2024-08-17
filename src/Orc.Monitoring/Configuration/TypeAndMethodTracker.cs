namespace Orc.Monitoring.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Filters;

public class TypeAndMethodTracker
{
    private readonly HashSet<Type> _trackedTypes = [];
    private readonly Dictionary<Type, HashSet<MethodInfo>> _targetMethods = new();
    private readonly List<IMethodFilter> _filters = [];

    public IReadOnlyDictionary<Type, HashSet<MethodInfo>> TargetMethods => _targetMethods;
    public IReadOnlyList<IMethodFilter> Filters => _filters;

    public TypeAndMethodTracker TrackNamespace(Type typeInNamespace)
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

    public TypeAndMethodTracker TrackAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            TrackType(type);
        }

        return this;
    }

    public TypeAndMethodTracker TrackType(Type type)
    {
        if (!_trackedTypes.Add(type))
        {
            return this;
        }

        UpdateTrackingMethods(type);
        return this;
    }

    public TypeAndMethodTracker AddFilter(IMethodFilter filter)
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
            methods = new HashSet<MethodInfo>();
            _targetMethods.Add(type, methods);
        }

        foreach (var method in GetAllMonitoringMethods(type).Where(ShouldIncludeMethod))
        {
            methods.Add(method);
        }
    }

    public static MethodInfo[] GetAllMonitoringMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                               BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
    }

    private bool ShouldIncludeMethod(MethodInfo methodInfo)
    {
        return _filters.Any(filter => filter.ShouldInclude(methodInfo));
    }

    public bool IsMethodTracked(MethodInfo method)
    {
        if (method.DeclaringType is null)
        {
            return false;
        }
        return _targetMethods.TryGetValue(method.DeclaringType, out var methods) && methods.Contains(method);
    }

    public IEnumerable<MethodInfo> GetTrackedMethodsForType(Type type)
    {
        return _targetMethods.TryGetValue(type, out var methods) ? methods : Enumerable.Empty<MethodInfo>();
    }

    public IEnumerable<MethodInfo> GetAllTrackedMethods()
    {
        return _targetMethods.Values.SelectMany(methods => methods);
    }

    public IEnumerable<MethodInfo> GetTrackedStaticMethods()
    {
        return GetAllTrackedMethods().Where(m => m.IsStatic);
    }

    public IEnumerable<MethodInfo> GetTrackedGenericMethods()
    {
        return GetAllTrackedMethods().Where(m => m.IsGenericMethod);
    }

    public IEnumerable<MethodInfo> GetTrackedExtensionMethods()
    {
        return GetAllTrackedMethods().Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false));
    }

    public IEnumerable<MethodInfo> GetTrackedAsyncMethods()
    {
        return GetAllTrackedMethods().Where(m =>
            m.ReturnType == typeof(System.Threading.Tasks.Task) ||
            (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>)));
    }

    public void RemoveType(Type type)
    {
        _trackedTypes.Remove(type);
        _targetMethods.Remove(type);
    }

    public void RemoveMethod(MethodInfo method)
    {
        if (method.DeclaringType is not null && _targetMethods.TryGetValue(method.DeclaringType, out var methods))
        {
            methods.Remove(method);
        }
    }

    public void ClearAllTracking()
    {
        _trackedTypes.Clear();
        _targetMethods.Clear();
    }
}
