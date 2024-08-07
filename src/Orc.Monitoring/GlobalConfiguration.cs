namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;
using Filters;

public class GlobalConfiguration
{
    public Dictionary<Type, bool> Filters { get; set; } = new();
    public Dictionary<Type, bool> Reporters { get; set; } = new();
    public List<Assembly> TrackedAssemblies { get; set; } = new();
    public MonitoringConfiguration MonitoringConfiguration { get; set; } = new();
}
