namespace Orc.Monitoring.Core.Abstractions;

using System;
using Models;

public class ComponentStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of the component whose state has changed.
    /// </summary>
    public Type? ComponentType { get; }

    /// <summary>
    /// The new enabled state of the component.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// The monitoring version at the time of the change.
    /// </summary>
    public MonitoringVersion Version { get; }

    public ComponentStateChangedEventArgs(Type? componentType, bool isEnabled, MonitoringVersion version)
    {
        ComponentType = componentType;
        IsEnabled = isEnabled;
        Version = version;
    }
}
