namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Logging;

public class GenericMethodTracker
{
    private readonly ILogger<GenericMethodTracker> _logger;
    private readonly Dictionary<MethodInfo, HashSet<Type[]>> _genericMethodInstantiations;
    private readonly Dictionary<string, int> _instantiationCounts;

    public GenericMethodTracker()
    {
        _logger = MonitoringController.CreateLogger<GenericMethodTracker>();
        _genericMethodInstantiations = new Dictionary<MethodInfo, HashSet<Type[]>>();
        _instantiationCounts = new Dictionary<string, int>();
    }

    public void TrackGenericMethodInstantiation(MethodInfo genericMethodDefinition, Type[] typeArguments)
    {
        if (!genericMethodDefinition.IsGenericMethodDefinition)
        {
            _logger.LogWarning($"Attempted to track non-generic method: {genericMethodDefinition.Name}");
            return;
        }

        lock (_genericMethodInstantiations)
        {
            if (!_genericMethodInstantiations.TryGetValue(genericMethodDefinition, out var instantiations))
            {
                instantiations = new HashSet<Type[]>(new TypeArrayEqualityComparer());
                _genericMethodInstantiations[genericMethodDefinition] = instantiations;
            }

            if (instantiations.Add(typeArguments))
            {
                var key = GetInstantiationKey(genericMethodDefinition, typeArguments);
                _instantiationCounts[key] = _instantiationCounts.TryGetValue(key, out var count) ? count + 1 : 1;

                _logger.LogInformation($"Tracked new instantiation of {genericMethodDefinition.Name}<{string.Join(", ", typeArguments.Select(t => t.Name))}>");
            }
        }
    }

    public IEnumerable<Type[]> GetInstantiations(MethodInfo genericMethodDefinition)
    {
        lock (_genericMethodInstantiations)
        {
            return _genericMethodInstantiations.TryGetValue(genericMethodDefinition, out var instantiations)
                ? instantiations.ToList()
                : Enumerable.Empty<Type[]>();
        }
    }

    public int GetInstantiationCount(MethodInfo genericMethodDefinition, Type[] typeArguments)
    {
        var key = GetInstantiationKey(genericMethodDefinition, typeArguments);
        lock (_instantiationCounts)
        {
            return _instantiationCounts.TryGetValue(key, out var count) ? count : 0;
        }
    }

    public IEnumerable<KeyValuePair<string, int>> GetAllInstantiationCounts()
    {
        lock (_instantiationCounts)
        {
            return _instantiationCounts.ToList();
        }
    }

    public void Clear()
    {
        lock (_genericMethodInstantiations)
        {
            _genericMethodInstantiations.Clear();
        }

        lock (_instantiationCounts)
        {
            _instantiationCounts.Clear();
        }

        _logger.LogInformation("Cleared all tracked generic method instantiations");
    }

    public bool HasInstantiations(MethodInfo genericMethodDefinition)
    {
        lock (_genericMethodInstantiations)
        {
            return _genericMethodInstantiations.ContainsKey(genericMethodDefinition);
        }
    }

    public IEnumerable<MethodInfo> GetTrackedGenericMethods()
    {
        lock (_genericMethodInstantiations)
        {
            return _genericMethodInstantiations.Keys.ToList();
        }
    }

    private string GetInstantiationKey(MethodInfo genericMethodDefinition, Type[] typeArguments)
    {
        return $"{genericMethodDefinition.DeclaringType?.FullName}.{genericMethodDefinition.Name}<{string.Join(", ", typeArguments.Select(t => t.FullName))}>";
    }

    private class TypeArrayEqualityComparer : IEqualityComparer<Type[]>
    {
        public bool Equals(Type[]? x, Type[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.SequenceEqual(y);
        }

        public int GetHashCode(Type[] obj)
        {
            return obj.Aggregate(17, (current, type) => current * 31 + (type?.GetHashCode() ?? 0));
        }
    }
}
