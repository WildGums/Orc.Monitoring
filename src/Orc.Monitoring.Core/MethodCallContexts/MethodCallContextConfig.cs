namespace Orc.Monitoring.Core.MethodCallContexts;

using System;
using System.Collections.Generic;

public class MethodCallContextConfig
{
    public string? CallerMethodName { get; set; }
    public IReadOnlyCollection<Type> GenericArguments { get; set; } = Array.Empty<Type>();
    public IReadOnlyCollection<Type> ParameterTypes { get; set; } = Array.Empty<Type>();
    public Type? ClassType { get; set; }
    public Dictionary<string, string>? StaticParameters { get; set; }
}
