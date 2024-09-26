namespace Orc.Monitoring.Core.Abstractions;

using System;
using Configuration;
using Models;
using Versioning;
using static Controllers.MonitoringController;

public interface IMonitoringController
{
    /// <summary>
    /// Indicates whether the monitoring is globally enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Event that is triggered when the monitoring version changes.
    /// </summary>
    event EventHandler<VersionChangedEventArgs>? VersionChanged;

    /// <summary>
    /// Gets the current monitoring version.
    /// </summary>
    /// <returns>The current <see cref="MonitoringVersion"/>.</returns>
    MonitoringVersion GetCurrentVersion();

    /// <summary>
    /// Enables monitoring globally.
    /// </summary>
    void Enable();

    /// <summary>
    /// Disables monitoring globally.
    /// </summary>
    void Disable();

    /// <summary>
    /// Begins a monitoring operation and provides the operation version.
    /// </summary>
    /// <param name="operationVersion">The operation's monitoring version.</param>
    /// <returns>An <see cref="IDisposable"/> that ends the operation when disposed.</returns>
    IDisposable BeginOperation(out MonitoringVersion operationVersion);

    /// <summary>
    /// Registers a monitoring context.
    /// </summary>
    /// <param name="context">The <see cref="VersionedMonitoringContext"/> to register.</param>
    void RegisterContext(VersionedMonitoringContext context);

    /// <summary>
    /// Gets or sets the monitoring configuration.
    /// </summary>
    MonitoringConfiguration Configuration { get; set; }

    /// <summary>
    /// Adds a callback that is invoked when a component's state changes.
    /// </summary>
    /// <param name="callback">The callback to add.</param>
    void AddStateChangedCallback(Action<ComponentStateChangedEventArgs> callback);

    /// <summary>
    /// Sets the state (enabled/disabled) of a component.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    /// <param name="enabled">True to enable; false to disable.</param>
    void SetComponentState(Type componentType, bool enabled);

    /// <summary>
    /// Gets the state (enabled/disabled) of a component.
    /// </summary>
    /// <param name="componentType">The type of the component.</param>
    /// <returns>True if enabled; otherwise, false.</returns>
    bool GetComponentState(Type componentType);
}

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
