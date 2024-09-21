namespace Orc.Monitoring.Core.Configuration;

using System;
using System.Linq;
using System.Reflection;
using Abstractions;
using Attributes;

public class ConfigurationBuilder
{
    private readonly IMonitoringController _monitoringController;
    private readonly MonitoringConfiguration _config = new();

    public ConfigurationBuilder(IMonitoringController monitoringController)
    {
        ArgumentNullException.ThrowIfNull(monitoringController);

        _monitoringController = monitoringController;
    }

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

    public ConfigurationBuilder AddFilter<T>(bool initialState = true) where T : IMethodFilter, new()
    {
        var filter = new T();
        return AddFilter(filter, initialState);
    }

    public ConfigurationBuilder AddFilter(IMethodFilter filter, bool initialState = true)
    {
        _config.AddFilter(filter);
        if (initialState)
        {
            _monitoringController.EnableFilter(filter.GetType());
        }
        return this;
    }

    public ConfigurationBuilder AddFilter(Type filterType, bool initialState = true)
    {
        if (!typeof(IMethodFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type {filterType.Name} does not implement IMethodFilter", nameof(filterType));
        }

        var methodFilter = Activator.CreateInstance(filterType) as IMethodFilter;
        if (methodFilter is null)
        {
            throw new InvalidOperationException($"Failed to create instance of {filterType.Name}");
        }

        return AddFilter(methodFilter, initialState);
    }

    public ConfigurationBuilder AddReporterType<T>(bool initialState = true) where T : IMethodCallReporter
    {
        _config.AddReporter<T>();
        if (initialState)
        {
            _monitoringController.EnableReporter(typeof(T));
        }
        return this;
    }

    public ConfigurationBuilder AddReporterType(Type reporterType, bool initialState = true)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }

        _config.AddReporter(reporterType);
        if (initialState)
        {
            _monitoringController.EnableReporter(reporterType);
        }
        return this;
    }

    public ConfigurationBuilder TrackAssembly(Assembly assembly)
    {
        _config.TrackAssembly(assembly);
        return this;
    }

    public ConfigurationBuilder SetOutputTypeState(Type type, bool enabled)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.Name} does not implement IReportOutput", nameof(type));
        }

        _config.SetOutputTypeState(type, enabled);
        if (enabled)
        {
            _monitoringController.EnableOutputType(type);
        }
        else
        {
            _monitoringController.DisableOutputType(type);
        }

        return this;
    }

    public MonitoringConfiguration Build()
    {
        // Get all loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToList();

        var outputTypes = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => typeof(IReportOutput).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        foreach (var type in outputTypes)
        {
            var defaultAttribute = type.GetCustomAttribute<DefaultOutputAttribute>();
            if(defaultAttribute is null || !defaultAttribute.IsEnabled)
            {
                continue;
            }

            _config.OutputTypeStates.TryAdd(type, true);
        }

        return _config;
    }
}
