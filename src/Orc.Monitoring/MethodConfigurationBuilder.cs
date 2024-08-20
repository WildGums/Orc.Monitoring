namespace Orc.Monitoring;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Filters;
using Reporters;

public class MethodConfigurationBuilder
{
    private readonly ILogger<MethodConfigurationBuilder> _logger;
    private readonly MethodConfiguration _config = new();

    public MethodConfigurationBuilder()
    {
        _logger = MonitoringController.CreateLogger<MethodConfigurationBuilder>();
    }

    public MethodConfigurationBuilder AddReporter(IMethodCallReporter reporter)
    {
        Console.WriteLine($"Adding reporter: {reporter.GetType().Name}");
        _config.Reporters.Add(reporter);
        return this;
    }

    public MethodConfigurationBuilder AddReporter<TReporter>(Action<TReporter>? configAction = null) where TReporter : IMethodCallReporter, new()
    {
        var reporter = new TReporter();
        configAction?.Invoke(reporter);
        _config.Reporters.Add(reporter);

        // Apply filters based on global configuration
        var globalConfig = MonitoringController.Configuration;
        if (globalConfig.ReporterFilterMappings.TryGetValue(typeof(TReporter), out var filters))
        {
            foreach (var filter in filters)
            {
                if (MonitoringController.IsFilterEnabledForReporterType(typeof(TReporter), filter.GetType()))
                {
                    _logger.LogDebug($"Adding filter {filter.GetType().Name} to reporter {typeof(TReporter).Name}");
                    reporter.AddFilter(filter);
                }
            }
        }

        return this;
    }

    public MethodConfigurationBuilder WithArguments(params object[] parameters)
    {
        _config.ParameterTypes.AddRange(parameters.Select(p => p.GetType()));
        return this;
    }

    public MethodConfigurationBuilder WithArguments<T>(params object[] parameters)
    {
        _config.GenericArguments.Add(typeof(T));
        _config.ParameterTypes.AddRange(parameters.Select(p => p.GetType()));
        return this;
    }

    public MethodConfigurationBuilder WithArguments<T1, T2>(params object[] parameters)
    {
        _config.GenericArguments.Add(typeof(T1));
        _config.GenericArguments.Add(typeof(T2));
        _config.ParameterTypes.AddRange(parameters.Select(p => p.GetType()));
        return this;
    }

    public MethodConfiguration Build() => _config;
}
