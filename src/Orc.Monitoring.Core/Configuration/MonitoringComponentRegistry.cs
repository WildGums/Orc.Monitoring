namespace Orc.Monitoring.Core.Configuration;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Manages relationships between components.
/// </summary>
public class MonitoringComponentRegistry
{
    // A dictionary where the key is a component type (e.g., reporter)
    // and the value is a set of related component types (e.g., filters applied to the reporter).
    private readonly ConcurrentDictionary<Type, HashSet<Type>> _componentRelationships = new ConcurrentDictionary<Type, HashSet<Type>>();

    /// <summary>
    /// Adds a relationship between two components.
    /// </summary>
    /// <param name="componentType">The primary component type.</param>
    /// <param name="relatedComponentType">The related component type.</param>
    public void AddRelationship(Type componentType, Type relatedComponentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        if (relatedComponentType is null)
        {
            throw new ArgumentNullException(nameof(relatedComponentType));
        }

        var relatedComponents = _componentRelationships.GetOrAdd(componentType, _ => new HashSet<Type>());
        lock (relatedComponents)
        {
            relatedComponents.Add(relatedComponentType);
        }
    }

    /// <summary>
    /// Removes a relationship between two components.
    /// </summary>
    /// <param name="componentType">The primary component type.</param>
    /// <param name="relatedComponentType">The related component type.</param>
    public void RemoveRelationship(Type componentType, Type relatedComponentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        if (relatedComponentType is null)
        {
            throw new ArgumentNullException(nameof(relatedComponentType));
        }

        if (_componentRelationships.TryGetValue(componentType, out var relatedComponents))
        {
            lock (relatedComponents)
            {
                relatedComponents.Remove(relatedComponentType);
            }
        }
    }

    /// <summary>
    /// Checks if a relationship exists between two components.
    /// </summary>
    /// <param name="componentType">The primary component type.</param>
    /// <param name="relatedComponentType">The related component type.</param>
    /// <returns>True if the relationship exists; otherwise, false.</returns>
    public bool HasRelationship(Type componentType, Type relatedComponentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        if (relatedComponentType is null)
        {
            throw new ArgumentNullException(nameof(relatedComponentType));
        }

        if (_componentRelationships.TryGetValue(componentType, out var relatedComponents))
        {
            lock (relatedComponents)
            {
                return relatedComponents.Contains(relatedComponentType);
            }
        }

        return false;
    }

    /// <summary>
    /// Applies relationships from another registry to this registry.
    /// </summary>
    /// <param name="other">The other registry to apply relationships from.</param>
    public void Apply(MonitoringComponentRegistry other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        foreach (var kvp in other._componentRelationships)
        {
            var relatedComponents = _componentRelationships.GetOrAdd(kvp.Key, _ => new HashSet<Type>());
            lock (relatedComponents)
            {
                foreach (var relatedComponentType in kvp.Value)
                {
                    relatedComponents.Add(relatedComponentType);
                }
            }
        }
    }
}
