namespace Orc.Monitoring.Core.Configuration;

using System;
using Abstractions;

public class ConfigurationBuilder
{
    private readonly IMonitoringController _monitoringController;
    private readonly MonitoringConfiguration _config = new();

    public ConfigurationBuilder(IMonitoringController monitoringController)
    {
        ArgumentNullException.ThrowIfNull(monitoringController, nameof(monitoringController));
        _monitoringController = monitoringController;
    }

    /// <summary>
    /// Sets the global monitoring state.
    /// </summary>
    public ConfigurationBuilder SetGlobalState(bool enabled)
    {
        _config.IsGloballyEnabled = enabled;
        if (enabled)
        {
            _monitoringController.Enable();
        }
        else
        {
            _monitoringController.Disable();
        }
        return this;
    }

    /// <summary>
    /// Builds the <see cref="MonitoringConfiguration"/> instance.
    /// </summary>
    public MonitoringConfiguration Build()
    {
        //foreach (var type in _config.GetRegisteredComponentTypes<IMonitoringComponent>())
        //{
        //    var defaultAttribute = type.GetCustomAttribute<DefaultComponentAttribute>();
        //    if (defaultAttribute is null || !defaultAttribute.IsEnabled)
        //    {
        //        continue;
        //    }

        //    _config.SetComponentState(type, true);
        //    _monitoringController.SetComponentState(type, true);
        //}

        return _config;
    }
}
