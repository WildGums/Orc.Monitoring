﻿namespace Orc.Monitoring;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Orc.Monitoring.Filters;
using Reporters;

public class MethodConfigurationBuilder
{
    private readonly ILogger<MethodConfigurationBuilder> _logger = MonitoringController.CreateLogger<MethodConfigurationBuilder>();
    private readonly MethodConfiguration _config = new();

    public MethodConfigurationBuilder()
    {
        
    }

    public MethodConfigurationBuilder AddReporter(IMethodCallReporter reporter)
    {
        _logger.LogDebug($"Added reporter: {reporter.GetType().Name} with ID: {reporter.Id}");
        _config.Reporters.Add(reporter);
        return this;
    }

    public MethodConfigurationBuilder AddReporter<TReporter>(Action<TReporter>? configAction = null) where TReporter : IMethodCallReporter, new()
    {
        var reporter = new TReporter();
        if (string.IsNullOrEmpty(reporter.Id))
        {
            reporter.Id = Guid.NewGuid().ToString(); // Assign a unique Id only if one doesn't exist
        }
        configAction?.Invoke(reporter);

        return AddReporter(reporter);
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
