namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using Reporters;

public class MethodConfiguration
{
    public List<IMethodCallReporter> Reporters { get; set; } = [];
    public Dictionary<string, string> Parameters { get; set; } = new();
    public List<Type> GenericArguments { get; set; } = [];
    public List<Type> ParameterTypes { get; set; } = [];
}
