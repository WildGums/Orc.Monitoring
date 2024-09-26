namespace Orc.Monitoring.Core.Configuration;

using System;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Core.Abstractions;

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
    /// Adds a component of type <typeparamref name="T"/> to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddComponent<T>(bool initialState = true) where T : IMonitoringComponent
    {
        var componentType = typeof(T);
        _config.SetComponentState(componentType, initialState);
        _monitoringController.SetComponentState(componentType, initialState);

        // Register the component type
        _config.RegisterComponentType(componentType);

        return this;
    }

    /// <summary>
    /// Adds a component of the specified type to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddComponent(Type componentType, bool initialState = true)
    {
        if (!typeof(IMonitoringComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"Type {componentType.FullName} does not implement IMonitoringComponent.", nameof(componentType));
        }

        _config.SetComponentState(componentType, initialState);
        _monitoringController.SetComponentState(componentType, initialState);

        // Register the component type
        _config.RegisterComponentType(componentType);

        return this;
    }

    /// <summary>
    /// Adds a filter of type <typeparamref name="T"/> to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddFilter<T>(bool initialState = true) where T : IMethodFilter
    {
        return AddComponent<T>(initialState);
    }

    /// <summary>
    /// Adds a filter of the specified type to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddFilter(Type filterType, bool initialState = true)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type {filterType.FullName} does not implement IMethodFilter.", nameof(filterType));
        }

        return AddComponent(filterType, initialState);
    }

    /// <summary>
    /// Adds a reporter of type <typeparamref name="T"/> to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddReporter<T>(bool initialState = true) where T : IMethodCallReporter
    {
        return AddComponent<T>(initialState);
    }

    /// <summary>
    /// Adds a reporter of the specified type to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddReporter(Type reporterType, bool initialState = true)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.FullName} does not implement IMethodCallReporter.", nameof(reporterType));
        }

        return AddComponent(reporterType, initialState);
    }

    /// <summary>
    /// Adds an output of type <typeparamref name="T"/> to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddOutput<T>(bool initialState = true) where T : IReportOutput
    {
        return AddComponent<T>(initialState);
    }

    /// <summary>
    /// Adds an output of the specified type to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddOutput(Type outputType, bool initialState = true)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException($"Type {outputType.FullName} does not implement IReportOutput.", nameof(outputType));
        }

        return AddComponent(outputType, initialState);
    }

    /// <summary>
    /// Adds a filter instance to the configuration and optionally sets its initial state.
    /// </summary>
    public ConfigurationBuilder AddFilterInstance(IMethodFilter filterInstance, bool initialState = true)
    {
        if (filterInstance is null)
        {
            throw new ArgumentNullException(nameof(filterInstance));
        }

        var filterType = filterInstance.GetType();

        _config.AddComponentInstance(filterInstance);
        _config.SetComponentState(filterType, initialState);
        _monitoringController.SetComponentState(filterType, initialState);

        return this;
    }

    public ConfigurationBuilder AddReporterType<T>() where T : IMethodCallReporter
    {
        return AddComponent<T>();
    }

    public ConfigurationBuilder AddReporterType(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (!typeof(IMethodCallReporter).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.FullName} does not implement IMethodCallReporter.", nameof(type));
        }

        return AddComponent(type);
    }

    public ConfigurationBuilder TrackAssembly(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var componentTypes = assembly.GetTypes()
            .Where(t => typeof(IMonitoringComponent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var type in componentTypes)
        {
            _config.RegisterComponentType(type);
        }

        return this;
    }

    /// <summary>
    /// Builds the <see cref="MonitoringConfiguration"/> instance.
    /// </summary>
    public MonitoringConfiguration Build()
    {
        foreach (var type in _config.GetRegisteredComponentTypes<IMonitoringComponent>())
        {
            var defaultAttribute = type.GetCustomAttribute<DefaultComponentAttribute>();
            if (defaultAttribute is null || !defaultAttribute.IsEnabled)
            {
                continue;
            }

            _config.SetComponentState(type, true);
            _monitoringController.SetComponentState(type, true);
        }

        return _config;
    }
}
