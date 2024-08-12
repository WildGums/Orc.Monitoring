namespace Orc.Monitoring;

using System;
using System.Collections.Generic;
using System.Reflection;

public class GlobalConfiguration
{
    public Dictionary<Type, bool> Filters { get; set; } = new();
    public Dictionary<Type, bool> Reporters { get; set; } = new();
    public List<Assembly> TrackedAssemblies { get; set; } = [];
    public MonitoringConfiguration MonitoringConfiguration { get; set; } = new();
}
