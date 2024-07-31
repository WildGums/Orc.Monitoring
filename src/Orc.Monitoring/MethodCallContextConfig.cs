namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using Orc.Monitoring.Reporters;

public class MethodCallContextConfig
{
    public string? CallerMethodName { get; set; }
    public IReadOnlyCollection<IMethodCallReporter> Reporters { get; set; } = Array.Empty<IMethodCallReporter>();
    public IReadOnlyCollection<Type> GenericArguments { get; set; } = Array.Empty<Type>();
    public IReadOnlyCollection<Type> ParameterTypes { get; set; } = Array.Empty<Type>();
    public Type? ClassType { get; set; }
}
