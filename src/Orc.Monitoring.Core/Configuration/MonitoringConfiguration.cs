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
    /// <summary>
    /// Gets or sets a value indicating whether monitoring is globally enabled.
    /// </summary>
    public bool IsGloballyEnabled { get; set; } = true;

    /// <summary>
    /// Gets the registry that holds relationships between components.
    /// For example, it can hold which filters are applied to which reporters.
    /// </summary>
    public MonitoringComponentRegistry ComponentRegistry { get; } = new MonitoringComponentRegistry();


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
}
