namespace Orc.Monitoring;

using System;
using System.Linq;
using System.Reflection;
using Orc.Monitoring.Filters;
using Orc.Monitoring.Reporters;
using Orc.Monitoring.Reporters.ReportOutputs;


public class ConfigurationBuilder
{
    private readonly MonitoringConfiguration _config = new();

    public ConfigurationBuilder SetGlobalState(bool enabled)
    {
        _config.IsGloballyEnabled = enabled;
        if (enabled)
        {
            MonitoringController.Enable();
        }
        else
        {
            MonitoringController.Disable();
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
            MonitoringController.EnableFilter(filter.GetType());
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

    public ConfigurationBuilder AddReporter<T>(bool initialState = true) where T : IMethodCallReporter, new()
    {
        _config.AddReporter<T>();
        if (initialState)
        {
            MonitoringController.EnableReporter(typeof(T));
        }
        return this;
    }

    public ConfigurationBuilder AddReporter(Type reporterType, bool initialState = true)
    {
        if (!typeof(IMethodCallReporter).IsAssignableFrom(reporterType))
        {
            throw new ArgumentException($"Type {reporterType.Name} does not implement IMethodCallReporter", nameof(reporterType));
        }

        _config.AddReporter(reporterType);
        if (initialState)
        {
            MonitoringController.EnableReporter(reporterType);
        }
        return this;
    }

    public ConfigurationBuilder AddFilterToReporter<TReporter, TFilter>(bool initialState = true)
        where TReporter : IMethodCallReporter
        where TFilter : IMethodFilter, new()
    {
        _config.AddFilterMappingForReporter(typeof(TReporter), typeof(TFilter));
        if (initialState)
        {
            MonitoringController.EnableFilterForReporterType(typeof(TReporter), typeof(TFilter));
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
            MonitoringController.EnableOutputType<T>();
        }
        else
        {
            MonitoringController.DisableOutputType<T>();
        }
        return this;
    }

    public ConfigurationBuilder SetOutputTypeState(Type outputType, bool enabled)
    {
        if (!typeof(IReportOutput).IsAssignableFrom(outputType))
        {
            throw new ArgumentException($"Type {outputType.Name} does not implement IReportOutput", nameof(outputType));
        }

        if (enabled)
        {
            MonitoringController.EnableOutputType(outputType);
        }
        else
        {
            MonitoringController.DisableOutputType(outputType);
        }
        return this;
    }

    public MonitoringConfiguration Build()
    {
        // Enable default output types if not explicitly set
        if (!_config.OutputTypeStates.ContainsKey(typeof(RanttOutput)))
        {
            SetOutputTypeState<RanttOutput>(true);
        }
        if (!_config.OutputTypeStates.ContainsKey(typeof(TxtReportOutput)))
        {
            SetOutputTypeState<TxtReportOutput>(true);
        }

        // Ensure all reporters in ReporterFilterMappings are added to ReporterTypes
        foreach (var reporterType in _config.ReporterFilterMappings.Keys)
        {
            if (!_config.ReporterTypes.Contains(reporterType))
            {
                _config.AddReporter(reporterType);
            }
        }

        return _config;
    }
}
