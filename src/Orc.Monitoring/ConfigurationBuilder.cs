namespace Orc.Monitoring;

using System;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;

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

    public ConfigurationBuilder SetOutputTypeState<T>(bool enabled) where T : IReportOutput
    {
        _config.SetOutputTypeState(typeof(T), enabled);
        if (enabled)
        {
            _monitoringController.EnableOutputType<T>();
        }
        else
        {
            _monitoringController.DisableOutputType<T>();
        }
        return this;
    }

    public ConfigurationBuilder SetOutputTypeState(Type outputType, bool enabled)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException($"Type {outputType.Name} does not implement IReportOutput", nameof(outputType));
        }

        _config.SetOutputTypeState(outputType, enabled);
        if (enabled)
        {
            _monitoringController.EnableOutputType(outputType);
        }
        else
        {
            _monitoringController.DisableOutputType(outputType);
        }
        return this;
    }

    public MonitoringConfiguration Build()
    {
        if (!_config.OutputTypeStates.ContainsKey(typeof(RanttOutput)))
        {
            SetOutputTypeState<RanttOutput>(true);
        }
        if (!_config.OutputTypeStates.ContainsKey(typeof(TxtReportOutput)))
        {
            SetOutputTypeState<TxtReportOutput>(true);
        }

        return _config;
    }
}
