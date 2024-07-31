namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using Orc.Monitoring.Reporters;

public class MethodConfiguration
{
    public List<IMethodCallReporter> Reporters { get; set; } = new List<IMethodCallReporter>();
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    public List<Type> GenericArguments { get; set; } = new List<Type>();
    public List<Type> ParameterTypes { get; set; } = new List<Type>();
}
