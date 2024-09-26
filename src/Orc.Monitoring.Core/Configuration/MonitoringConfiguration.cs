namespace Orc.Monitoring.Core.Configuration;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Orc.Monitoring.Core.Abstractions;

/// <summary>
/// Represents the configuration settings for the monitoring system.
/// </summary>
public class MonitoringConfiguration
{
    private readonly ConcurrentDictionary<Type, Type> _registeredComponentTypes = new();

    /// <summary>
    /// Gets or sets a value indicating whether monitoring is globally enabled.
    /// </summary>
    public bool IsGloballyEnabled { get; set; } = true;

    /// <summary>
    /// Gets the dictionary that holds the enabled/disabled state of components.
    /// The key is the component type, and the value is a boolean indicating if it's enabled.
    /// </summary>
    public ConcurrentDictionary<Type, bool> ComponentStates { get; } = new ConcurrentDictionary<Type, bool>();

    /// <summary>
    /// Gets the registry that holds relationships between components.
    /// For example, it can hold which filters are applied to which reporters.
    /// </summary>
    public MonitoringComponentRegistry ComponentRegistry { get; } = new MonitoringComponentRegistry();

    /// <summary>
    /// Gets the dictionary that holds instances of components.
    /// </summary>
    public ConcurrentDictionary<Type, IMonitoringComponent> ComponentInstances { get; } = new ConcurrentDictionary<Type, IMonitoringComponent>();

    public void AddComponentInstance<T>(T instance) where T : IMonitoringComponent
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        ComponentInstances[typeof(T)] = instance;
    }

    public T GetComponentInstance<T>() where T : IMonitoringComponent
    {
        if (ComponentInstances.TryGetValue(typeof(T), out var instance))
        {
            return (T)instance;
        }
        throw new KeyNotFoundException($"Component instance of type {typeof(T).FullName} not found.");
    }

    public IMonitoringComponent GetComponentInstance(Type type)
    {
        if (ComponentInstances.TryGetValue(type, out var instance))
        {
            return instance;
        }
        throw new KeyNotFoundException($"Component instance of type {type.FullName} not found.");
    }

    public IEnumerable<IMonitoringComponent> GetComponentInstances()
    {
        return ComponentInstances.Values;
    }

    /// <summary>
    /// Sets the enabled state of a component.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    /// <param name="isEnabled">True to enable; false to disable.</param>
    public void SetComponentState(Type componentType, bool isEnabled)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        ComponentStates[componentType] = isEnabled;
    }

    /// <summary>
    /// Gets the enabled state of a component.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    /// <returns>True if the component is enabled; otherwise, false.</returns>
    public bool GetComponentState(Type componentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        return ComponentStates.TryGetValue(componentType, out var isEnabled) && isEnabled;
    }

    /// <summary>
    /// Applies a relationship between two components.
    /// For example, applies a filter to a reporter.
    /// </summary>
    /// <param name="componentType">The primary component type (e.g., reporter).</param>
    /// <param name="relatedComponentType">The related component type (e.g., filter).</param>
    public void AddComponentRelationship(Type componentType, Type relatedComponentType)
    {
        ComponentRegistry.AddRelationship(componentType, relatedComponentType);
    }

    /// <summary>
    /// Removes a relationship between two components.
    /// </summary>
    /// <param name="componentType">The primary component type.</param>
    /// <param name="relatedComponentType">The related component type.</param>
    public void RemoveComponentRelationship(Type componentType, Type relatedComponentType)
    {
        ComponentRegistry.RemoveRelationship(componentType, relatedComponentType);
    }

    /// <summary>
    /// Checks if a relationship exists between two components.
    /// </summary>
    /// <param name="componentType">The primary component type.</param>
    /// <param name="relatedComponentType">The related component type.</param>
    /// <returns>True if the relationship exists; otherwise, false.</returns>
    public bool HasComponentRelationship(Type componentType, Type relatedComponentType)
    {
        return ComponentRegistry.HasRelationship(componentType, relatedComponentType);
    }

    /// <summary>
    /// Applies configurations from another MonitoringConfiguration instance.
    /// This can be used to update the current configuration with new settings.
    /// </summary>
    /// <param name="other">The other configuration to apply.</param>
    public void Apply(MonitoringConfiguration other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        IsGloballyEnabled = other.IsGloballyEnabled;

        foreach (var kvp in other.ComponentStates)
        {
            ComponentStates[kvp.Key] = kvp.Value;
        }

        ComponentRegistry.Apply(other.ComponentRegistry);
    }

    /// <summary>
    /// Registers a component type.
    /// </summary>
    /// <param name="componentType">The component type to register.</param>
    public void RegisterComponentType(Type componentType)
    {
        _registeredComponentTypes[componentType] = componentType;
    }

    /// <summary>
    /// Gets registered component types filtered by the specified type.
    /// </summary>
    /// <typeparam name="T">The type to filter by (e.g., IMethodCallReporter).</typeparam>
    /// <returns>An enumerable of registered component types of type <typeparamref name="T"/>.</returns>
    public IEnumerable<Type> GetRegisteredComponentTypes<T>() where T : IMonitoringComponent
    {
        return _registeredComponentTypes.Keys.Where(t => typeof(T).IsAssignableFrom(t));
    }
}
