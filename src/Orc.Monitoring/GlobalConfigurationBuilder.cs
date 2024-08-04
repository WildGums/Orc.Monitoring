namespace Orc.Monitoring;

using System.Reflection;
using Filters;
using Reporters;

public class GlobalConfigurationBuilder
{
    private readonly GlobalConfiguration _config = new();
    private readonly MonitoringConfiguration _monitoringConfig = new();

    public GlobalConfigurationBuilder AddFilter<T>(bool initialState = true) where T : IMethodFilter
    {
        _config.Filters[typeof(T)] = initialState;
        return this;
    }

    public GlobalConfigurationBuilder AddReporter<T>(bool initialState = true) where T : IMethodCallReporter
    {
        _config.Reporters[typeof(T)] = initialState;
        return this;
    }

    public GlobalConfigurationBuilder TrackAssembly(Assembly assembly)
    {
        _config.TrackedAssemblies.Add(assembly);
        return this;
    }

    public GlobalConfigurationBuilder AddReporterForClass<TClass, TReporter>() where TReporter : IMethodCallReporter
    {
        _monitoringConfig.AddReporterForClass<TClass, TReporter>();
        return this;
    }

    public GlobalConfigurationBuilder AddFilterForClass<TClass, TFilter>() where TFilter : IMethodFilter
    {
        _monitoringConfig.AddFilterForClass<TClass, TFilter>();
        return this;
    }

    public GlobalConfiguration Build()
    {
        _config.MonitoringConfiguration = _monitoringConfig;
        return _config;
    }
}
