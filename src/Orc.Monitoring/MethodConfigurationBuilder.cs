namespace Orc.Monitoring;

using System;
using System.Linq;
using Orc.Monitoring.Reporters;

public class MethodConfigurationBuilder
{
    private readonly MethodConfiguration _config = new MethodConfiguration();

    public MethodConfigurationBuilder AddReporter<TReporter>(Action<TReporter>? configAction = null) where TReporter : IMethodCallReporter, new()
    {
        var reporter = new TReporter();
        configAction?.Invoke(reporter);
        _config.Reporters.Add(reporter);
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
